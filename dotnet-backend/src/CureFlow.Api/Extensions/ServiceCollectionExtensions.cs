using System.Text;
using System.Text.Json.Serialization;
using CureFlow.Application.Validation;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using System.Threading.RateLimiting;
using CureFlow.Api.Authorization;
using CureFlow.Api.Middleware;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.DependencyInjection;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Persistence.Seeders;
using Npgsql;
using CureFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Microsoft.OpenApi.Models;
using CureFlow.Infrastructure.Services.Business;
using CureFlow.Infrastructure.Services.CRM;
using CureFlow.Infrastructure.Services.Ehr;
using CureFlow.Infrastructure.Services.Notifications;
using CureFlow.Infrastructure.Services.Query;

namespace CureFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCureFlowApi(this IServiceCollection services, IConfiguration config, IHostEnvironment? environment = null)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<CureFlow.Api.Filters.RequestValidationFilter>();
            options.Filters.Add<CureFlow.Api.Filters.AuthenticationAttemptFilter>();
            options.Filters.Add<CureFlow.Api.Filters.SafeErrorResultFilter>();
            options.Filters.Add<CureFlow.Api.Filters.ContentDeliverySafetyFilter>();
            options.Filters.Add<CureFlow.Api.Filters.ClinicalReadAuditFilter>();
            options.MaxModelValidationErrors = ApiProblems.MaximumErrors;
            options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, _) => "Invalid field value.");
            options.ModelBindingMessageProvider.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "Invalid value.");
            options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(_ => "Invalid value.");
        })
            .AddJsonOptions(opt =>
            {
                opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
                opt.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
                opt.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                opt.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
                opt.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
                opt.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeJsonConverter());
            });

        services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context => context.HttpContext.Items["request_body_limit_exceeded"] is true
                ? ApiProblems.Result(ApiProblems.Create(context.HttpContext, 413, "Request exceeds the permitted size. Send a smaller request."))
                : ApiProblems.Result(ApiProblems.Create(
                context.HttpContext, 400, "Correct the indicated fields and try again.",
                context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
                    .Take(ApiProblems.MaximumErrors)
                    .ToDictionary(entry => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(entry.Key),
                        _ => new[] { "Invalid or missing field value." })));
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "CureFlow API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT bearer token. Example: 'Bearer eyJ...'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                  Array.Empty<string>() }
            });
        });

        var isDevelopment = environment?.IsDevelopment()
            ?? string.Equals(config["ASPNETCORE_ENVIRONMENT"], Environments.Development, StringComparison.OrdinalIgnoreCase);
        var corsOrigins = (config["Cors:Origins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var origin in corsOrigins)
            if (!TryReadOrigin(origin, out _))
                throw new InvalidOperationException("Cors:Origins must contain exact HTTP(S) scheme/host/port origins without paths, credentials or wildcards.");

        services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            p.SetIsOriginAllowed(origin => IsAllowedCorsOrigin(origin, corsOrigins, isDevelopment))
             .WithHeaders("Authorization", "Content-Type", "Accept", "X-Tenant-Slug", "X-Request-ID",
                 "Idempotency-Key", "If-Match", "If-None-Match", "X-Correlation-ID",
                 "If-Modified-Since", "If-Unmodified-Since", "Range", "If-Range")
             .WithMethods("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
             .WithExposedHeaders("ETag", "Retry-After", "Content-Disposition", "X-Correlation-ID",
                 "Location", "Content-Range", "Accept-Ranges", "Last-Modified");
            // Bearer-token BFF traffic needs no browser credential grant.
            if (config.GetValue<bool>("Cors:AllowCredentials")) p.AllowCredentials();
        }));
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        var envName = environment?.EnvironmentName ?? config["ASPNETCORE_ENVIRONMENT"] ?? Environments.Production;
        // Production cannot opt into development quotas through configuration.
        var relaxedRateLimits = envName is "Development" or "Testing";

        services.AddRateLimiter(options => CureFlow.Api.Configuration.RequestBudgetPolicy.Configure(options, config, relaxedRateLimits));
        services.AddRequestTimeouts(options => options.DefaultPolicy = new()
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(config.GetValue<int?>("RequestLimits:TimeoutSeconds") ?? 180, 10, 600)),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = context => ApiProblems.WriteAsync(context, ApiProblems.Create(context, 504,
                "Request timed out. Verify the current state before retrying a change."))
        });
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.ValueCountLimit = 128;
            options.ValueLengthLimit = 64 * 1024;
            options.MultipartHeadersCountLimit = 16;
            options.MultipartHeadersLengthLimit = 16 * 1024;
        });

        services.AddMemoryCache();
        services.AddSingleton<CureFlow.Api.Filters.AuthenticationAttemptBudget>();

        services.AddValidatorsFromAssemblyContaining<CreatePatientValidator>();

        return services;
    }

    public static IServiceCollection AddCureFlowInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        DapperSetup.Configure();

        var connectionString = CureFlowNpgsqlDataSource.Normalize(
            config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is required."));

        services.AddDbContext<ApplicationDbContext>(opt =>
            opt.UseNpgsql(connectionString));
        services.AddSingleton(_ => CureFlowNpgsqlDataSource.Create(connectionString));
        services.AddScoped<ICureFlowDbSession, CureFlowDbSession>();
        services.AddKeyedSingleton<NpgsqlDataSource>("Identity", (_, _) => AuthoritySource(config, "Identity"));
        services.AddKeyedSingleton<NpgsqlDataSource>("Catalogue", (_, _) => AuthoritySource(config, "Catalogue"));
        services.AddKeyedSingleton<NpgsqlDataSource>("PlatformIdentity", (_, _) => AuthoritySource(config, "PlatformIdentity"));
        services.AddKeyedSingleton<NpgsqlDataSource>("ControlPlane", (_, _) => AuthoritySource(config, "ControlPlane"));
        services.AddKeyedSingleton<NpgsqlDataSource>("Provisioning", (_, _) => AuthoritySource(config, "Provisioning"));
        services.AddScoped<IIdentityDbSession>(sp => new IdentityDbSession(sp.GetRequiredKeyedService<NpgsqlDataSource>("Identity"), sp.GetRequiredService<ITenantContext>()));
        services.AddScoped<ICatalogueDbSession>(sp => new CatalogueDbSession(sp.GetRequiredKeyedService<NpgsqlDataSource>("Catalogue"), sp.GetRequiredService<ITenantContext>()));
        services.AddScoped<IPlatformIdentityDbSession>(sp => new PlatformIdentityDbSession(sp.GetRequiredKeyedService<NpgsqlDataSource>("PlatformIdentity"), sp.GetRequiredService<ITenantContext>()));
        services.AddScoped<IPlatformDbSession>(sp => new PlatformDbSession(sp.GetRequiredKeyedService<NpgsqlDataSource>("ControlPlane"), sp.GetRequiredService<ITenantContext>()));
        services.AddScoped<IProvisioningDbSession>(sp => new ProvisioningDbSession(sp.GetRequiredKeyedService<NpgsqlDataSource>("Provisioning"), sp.GetRequiredService<ITenantContext>()));

        services.AddScoped<ITenantContext>(_ => new CurrentTenant());
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserPermissionService, UserPermissionService>();

        services.AddWhatsappIntegration(config);
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.SectionName));
        services.AddHttpClient<IHospitalWebsiteScraper, HospitalWebsiteScraper>();
        services.AddScoped<IAiService, ClaudeAiClient>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IIdempotentCommandExecutor, CureFlow.Infrastructure.Commands.IdempotentCommandExecutor>();
        services.Configure<OutboxOptions>(config.GetSection("Outbox"));
        services.AddSingleton<IOutboxWriter, CureFlow.Infrastructure.Outbox.OutboxWriter>();
        services.AddScoped<CureFlow.Infrastructure.Outbox.OutboxDispatcher>();
        services.AddScoped<IFailedJobService, CureFlow.Infrastructure.Outbox.FailedJobService>();
        services.AddScoped<IExternalProcessingPolicy, CureFlow.Infrastructure.External.ExternalProcessingPolicy>();
        services.AddScoped<IFacilityOperatingSettingsService, CureFlow.Infrastructure.Services.Organization.FacilityOperatingSettingsService>();
        services.AddScoped<IPatientIdentityReviewService, PatientIdentityReviewService>();
        services.AddScoped<IPatientMergeService, PatientMergeService>();
        services.AddScoped<IPatientDemographicHistoryService, PatientDemographicHistoryService>();
        services.AddScoped<IPatientIdentityVerificationService, PatientIdentityVerificationService>();
        services.AddScoped<IAppointmentReservationService, AppointmentReservationService>();
        services.AddScoped<IQueueArrivalService, QueueArrivalService>();
        services.AddScoped<IQueueDispatchService, QueueDispatchService>();
        services.AddScoped<IAppointmentBookingService, AppointmentBookingService>();
        services.AddScoped<IAppointmentWaitlistService, AppointmentWaitlistService>();
        services.AddScoped<INamedFollowUpService, NamedFollowUpService>();
        services.AddScoped<IAppointmentNoShowService, AppointmentNoShowService>();
        services.AddScoped<IPatientAppointmentQueryService, PatientAppointmentQueryService>();
        services.AddScoped<IClinicianWorklistService, ClinicianWorklistService>();
        services.AddScoped<IAppointmentBookingChangeService, AppointmentBookingChangeService>();
        services.AddScoped<ILeadConversionService, LeadConversionService>();
        services.AddScoped<PatientExportService>();
        services.AddScoped<IPatientExportService>(sp=>sp.GetRequiredService<PatientExportService>());
        services.AddScoped<ITransactionalOutboxConsumer>(sp=>sp.GetRequiredService<PatientExportService>());
        services.AddScoped<IPatientImportService, CureFlow.Infrastructure.Imports.PatientImportService>();
        services.AddScoped<IPatientAlertService, PatientAlertService>();
        services.AddScoped<IPatientContactService, PatientContactService>();
        services.AddScoped<IPatientIdentifierService, PatientIdentifierService>();
        services.AddScoped<IFacilityResourceService, CureFlow.Infrastructure.Organization.FacilityResourceService>();
        services.AddScoped<IOrganizationHierarchyService, CureFlow.Infrastructure.Organization.OrganizationHierarchyService>();
        services.Configure<OrganizationOnboardingOptions>(config.GetSection("OrganizationOnboarding"));
        services.AddSingleton<IDocumentScanner>(_ => CureFlow.Infrastructure.Storage.ClamAvDocumentScanner.Create(config));
        services.AddScoped<ITransactionalOutboxConsumer, CureFlow.Infrastructure.Storage.DocumentScanConsumer>();
        services.AddHostedService<CureFlow.Infrastructure.Outbox.OutboxHostedWorker>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, CureFlow.Api.Filters.ClinicalAuthorizationAuditHandler>();
        services.AddScoped<IClinicalAccessAuditService, CureFlow.Infrastructure.Auditing.ClinicalAccessAuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPlatformAuthService, PlatformAuthService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<ILifestyleService, LifestyleService>();
        services.AddScoped<IAllergyService, AllergyService>();
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<IClinicalRecordService, ClinicalRecordService>();
        services.AddScoped<IPatientDocumentService, PatientDocumentService>();
        services.AddSingleton<IPrivateDocumentStorage, CureFlow.Infrastructure.Storage.FileSystemPrivateDocumentStorage>();
        services.AddScoped<IScheduleExceptionService, ScheduleExceptionService>();
        services.AddScoped<IScheduleRuleService, ScheduleRuleService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IVisitService, VisitService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IReferralService, ReferralService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<IHospitalProfileService, HospitalProfileService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddScoped<IAiConversationService, AiConversationService>();
        services.AddHostedService<SchedulerHostedService>();
        services.AddHostedService<CampaignSchedulerHostedService>();
        services.AddHostedService<WhatsappConversationNormalizationHostedService>();
        services.Configure<Application.Options.MarketingCalendarOptions>(
            config.GetSection(Application.Options.MarketingCalendarOptions.SectionName));
        services.AddSingleton<GoogleCalendarSyncStub>();

        return services;
    }

    public static IServiceCollection AddCureFlowAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwtSecret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret required");
        var environment = config["ASPNETCORE_ENVIRONMENT"] ?? Environments.Production;
        var isProduction = string.Equals(environment, Environments.Production, StringComparison.OrdinalIgnoreCase);
        if (isProduction
            && (jwtSecret.Contains("REPLACE-WITH", StringComparison.OrdinalIgnoreCase)
                || jwtSecret.Length < 32))
        {
            throw new InvalidOperationException(
                "Jwt:Secret must be a real secret in Production (min 32 chars). Set Jwt__Secret in the environment.");
        }

        var tenantAudience = config["Jwt:TenantAudience"] ?? (config["Jwt:Audience"] ?? "cureflow-api") + ".tenant";
        var platformAudience = config["Jwt:PlatformAudience"] ?? (config["Jwt:Audience"] ?? "cureflow-api") + ".platform";
        if (string.IsNullOrWhiteSpace(tenantAudience) || string.IsNullOrWhiteSpace(platformAudience)
            || string.Equals(tenantAudience, platformAudience, StringComparison.Ordinal))
            throw new InvalidOperationException("Hospital and platform token audiences must be distinct.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"] ?? "cureflow",
                    ValidAudiences = new[] { tenantAudience, platformAudience },
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.Zero,
                };
                opt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (!CureFlow.Api.Authorization.ActorTokenBoundary.IsValid(context.Principal, tenantAudience, platformAudience))
                            context.Fail("Invalid actor/session binding.");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddScoped<RefreshSessionService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var permission in CureFlowPermissions.All)
            {
                options.AddPolicy(CureFlowPermissions.PolicyName(permission), policy =>
                    policy.AddRequirements(new PermissionRequirement(permission)));
            }

            options.AddPolicy("PlatformUser", policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim("platform_user", "true"));
        });

        return services;
    }

    public static WebApplication UseCureFlowRequestPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        var whatsappOptions = app.Configuration.GetSection(WhatsappOptions.SectionName).Get<WhatsappOptions>() ?? new WhatsappOptions();
        var mediaStoragePath = string.IsNullOrWhiteSpace(whatsappOptions.RelayMediaStoragePath)
            ? "webhook-media"
            : whatsappOptions.RelayMediaStoragePath.Trim('/');
        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, mediaStoragePath));

        var mediaOptions = app.Configuration.GetSection(WhatsappMediaOptions.SectionName).Get<WhatsappMediaOptions>() ?? new WhatsappMediaOptions();
        var whatsappMediaPath = string.IsNullOrWhiteSpace(mediaOptions.StoragePath) ? "whatsapp-media" : mediaOptions.StoragePath.Trim('/');
        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, whatsappMediaPath));

        app.UseResponseCompression();
        app.UseSerilogRequestLogging();
        app.UseCors();
        app.UseMiddleware<TenantApiActivityMiddleware>();
        app.UseRequestTimeouts();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<DependencyHealthMiddleware>();
        app.UseStatusCodePages(async context =>
        {
            var response = context.HttpContext.Response;
            await ApiProblems.WriteAsync(context.HttpContext, ApiProblems.Create(context.HttpContext,
                response.StatusCode, response.StatusCode switch
                {
                    401 => "Sign in to continue.",
                    403 => "You do not have permission to perform this action.",
                    404 => "The requested resource was not found.",
                    429 => "Too many requests. Try again later.",
                    _ => "The request could not be completed."
                }));
        });
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<RequestBodyBudgetMiddleware>();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseMiddleware<TenantMiddleware>();
        app.UseAuthorization();
        app.UseMiddleware<TenantLifecycleMiddleware>();
        app.UseMiddleware<CureFlow.Api.Areas.Platform.EntitlementMiddleware>();
        app.MapControllers();

        return app;
    }

    private static bool TryReadOrigin(string? origin, out Uri? uri)
    {
        uri = null;
        return !string.IsNullOrWhiteSpace(origin)
            && Uri.TryCreate(origin, UriKind.Absolute, out uri)
            && uri.Scheme is "http" or "https"
            && string.IsNullOrEmpty(uri.UserInfo)
            && !origin.Contains('*')
            && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedCorsOrigin(string? origin, string[] configuredOrigins, bool isDevelopment)
    {
        if (!TryReadOrigin(origin, out var uri)) return false;
        if (uri!.IsLoopback) return isDevelopment;
        return configuredOrigins.Contains(origin!, StringComparer.OrdinalIgnoreCase);
    }
    private static NpgsqlDataSource AuthoritySource(IConfiguration config, string name) =>
        CureFlowNpgsqlDataSource.Create(CureFlowNpgsqlDataSource.Normalize(config.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is required for this authority.")));
}










