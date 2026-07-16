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
    public static IServiceCollection AddCureFlowApi(this IServiceCollection services, IConfiguration config)
    {
        services.AddControllers()
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

        services.AddCors(o => o.AddDefaultPolicy(p =>
            p.WithOrigins(config["Cors:Origins"]?.Split(',') ?? new[] { "*" })
             .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        var envName = config["ASPNETCORE_ENVIRONMENT"] ?? Environments.Production;
        var relaxedRateLimits = envName is "Development" or "Testing"
            || config.GetValue<bool>("RateLimiting:Relaxed");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = relaxedRateLimits ? 1000 : 10,
                        QueueLimit = 0,
                    }));
            options.AddPolicy("register-tenant", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(10),
                        PermitLimit = relaxedRateLimits ? 100 : 3,
                        QueueLimit = 0,
                    }));
        });

        services.AddMemoryCache();

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

        services.AddScoped<ITenantContext>(_ => new CurrentTenant());
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserPermissionService, UserPermissionService>();

        services.AddWhatsappIntegration(config);
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.SectionName));
        services.AddHttpClient<IHospitalWebsiteScraper, HospitalWebsiteScraper>();
        services.AddScoped<IAiService, ClaudeAiClient>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPlatformAuthService, PlatformAuthService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<ILifestyleService, LifestyleService>();
        services.AddScoped<IAllergyService, AllergyService>();
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<IClinicalRecordService, ClinicalRecordService>();
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

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.FromMinutes(2),
                };
            });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
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

        var storageOptions = app.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        var labPath = string.IsNullOrWhiteSpace(storageOptions.LabReportsPath) ? "lab-reports" : storageOptions.LabReportsPath.Trim('/');
        var labRoot = Path.Combine(app.Environment.ContentRootPath, labPath);
        Directory.CreateDirectory(labRoot);

        var mediaOptions = app.Configuration.GetSection(WhatsappMediaOptions.SectionName).Get<WhatsappMediaOptions>() ?? new WhatsappMediaOptions();
        var whatsappMediaPath = string.IsNullOrWhiteSpace(mediaOptions.StoragePath) ? "whatsapp-media" : mediaOptions.StoragePath.Trim('/');
        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, whatsappMediaPath));

        app.UseResponseCompression();
        app.UseSerilogRequestLogging();
        app.UseCors();
        app.UseRateLimiter();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<TenantMiddleware>();
        app.UseMiddleware<TenantLifecycleMiddleware>();
        app.MapControllers();

        return app;
    }
}
