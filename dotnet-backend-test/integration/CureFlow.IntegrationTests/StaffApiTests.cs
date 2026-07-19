using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/staff</c> profiles and schedules (INT-730…INT-734).
/// Skips when no test database is available.
/// </summary>
public class StaffApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly CustomWebApplicationFactory _factory;

    public StaffApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Int730_StaffEndpoints_RequireAuth()
    {
        await using var host = CreateAuthHost();
        var client = host.CreateClient();

        (await client.GetAsync("/api/staff")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/staff/doctors")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/staff/nurses")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/staff/booking-options")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Int730_731_732_ListGetCreatePatchDelete_StaffProfile()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "staff-crud");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var doctorEmail = $"doctor-{suffix}@e2e.cureflow.test";

        var createUser = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest($"Dr {suffix}", doctorEmail, "DoctorUser123!", "doctor", Specialty: "General"),
            JsonOptions);
        createUser.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await createUser.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var createStaff = await client.PostAsJsonAsync(
            "/api/staff",
            new CreateStaffProfileRequest(
                userId,
                Department: "OPD",
                Specialization: "General Medicine",
                Qualification: "MBBS",
                ConsultationFee: 500,
                EmploymentType: StaffEmploymentType.Permanent,
                Shift: "morning",
                WardAssignment: null,
                IsAvailable: true),
            JsonOptions);
        createStaff.StatusCode.Should().Be(HttpStatusCode.OK);
        var staffId = (await createStaff.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        staffId.Should().NotBe(Guid.Empty);

        var list = await client.GetAsync("/api/staff");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        listBody.EnumerateArray().Any(s => s.GetProperty("id").GetGuid() == staffId).Should().BeTrue();

        var doctors = await client.GetAsync("/api/staff/doctors");
        doctors.StatusCode.Should().Be(HttpStatusCode.OK);
        var doctorsBody = await doctors.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        doctorsBody.EnumerateArray().Any(s => s.GetProperty("id").GetGuid() == staffId).Should().BeTrue();

        var get = await client.GetAsync($"/api/staff/{staffId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        getBody.GetProperty("id").GetGuid().Should().Be(staffId);

        var patch = await client.PatchAsJsonAsync(
            $"/api/staff/{staffId}",
            new UpdateStaffProfileRequest(
                Department: "Cardiology",
                Specialization: "Cardiology",
                Qualification: null,
                ConsultationFee: 750,
                EmploymentType: null,
                Shift: "evening",
                WardAssignment: "C1",
                IsAvailable: true),
            JsonOptions);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfter = await client.GetAsync($"/api/staff/{staffId}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBody = await getAfter.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        afterBody.GetProperty("department").GetString().Should().Be("Cardiology");

        var delete = await client.DeleteAsync($"/api/staff/{staffId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDeleted = await client.GetAsync($"/api/staff/{staffId}");
        getDeleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Int733_734_SchedulesAndBookingOptions()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "staff-sched");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var createUser = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest($"Dr Sched {suffix}", $"sched-{suffix}@e2e.cureflow.test", "DoctorUser123!", "doctor"),
            JsonOptions);
        createUser.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await createUser.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var createStaff = await client.PostAsJsonAsync(
            "/api/staff",
            new CreateStaffProfileRequest(
                userId, "OPD", "General", "MBBS", 400, StaffEmploymentType.Permanent,
                "morning", null, true),
            JsonOptions);
        createStaff.StatusCode.Should().Be(HttpStatusCode.OK);
        var staffId = (await createStaff.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var addSchedule = await client.PostAsJsonAsync(
            "/api/staff/schedules",
            new CreateDoctorScheduleRequest(staffId, DayOfWeek: 1, SpecificDate: null, "09:00", "13:00", true, "AM clinic"),
            JsonOptions);
        addSchedule.StatusCode.Should().Be(HttpStatusCode.OK);
        var scheduleId = (await addSchedule.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        scheduleId.Should().NotBe(Guid.Empty);

        var listSchedules = await client.GetAsync($"/api/staff/{staffId}/schedules");
        listSchedules.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedules = await listSchedules.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        schedules.EnumerateArray().Any(s => s.GetProperty("id").GetGuid() == scheduleId).Should().BeTrue();

        var updateSchedule = await client.PatchAsJsonAsync(
            $"/api/staff/schedules/{scheduleId}",
            new UpdateDoctorScheduleRequest(1, null, TimeSpan.FromHours(10), TimeSpan.FromHours(14), true, "updated"),
            JsonOptions);
        updateSchedule.StatusCode.Should().Be(HttpStatusCode.OK);

        var booking = await client.GetAsync("/api/staff/booking-options");
        booking.StatusCode.Should().Be(HttpStatusCode.OK);
        var bookingBody = await booking.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        bookingBody.TryGetProperty("doctors", out var doctors).Should().BeTrue();
        doctors.EnumerateArray().Any(d => d.GetProperty("staff_profile_id").GetGuid() == staffId
            || d.GetProperty("user_id").GetGuid() == userId).Should().BeTrue();

        var deleteSchedule = await client.DeleteAsync($"/api/staff/schedules/{scheduleId}");
        deleteSchedule.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Int730_ListNurses_ReturnsOk()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "staff-nurses");

        var response = await client.GetAsync("/api/staff/nurses");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task RegisterApproveOnboardAndLoginAsync(
        WebApplicationFactory<Program> host,
        HttpClient client,
        string label)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{label}-{suffix}@e2e.cureflow.test";
        const string password = "TestHospital123!";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register-tenant",
            new RegisterTenantRequest($"Staff Hospital {label} {suffix}", "Staff Admin", email, password, "+919900000030"),
            JsonOptions);
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenant = await register.Content.ReadFromJsonAsync<TenantDto>(JsonOptions);
        tenant.Should().NotBeNull();

        await ActivateTenantAsync(tenant!.Id);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), JsonOptions);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var complete = await client.PostAsync("/api/onboarding/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task ActivateTenantAsync(Guid tenantId)
    {
        var cs = IntegrationDb.ResolveConnectionString()
            ?? throw new InvalidOperationException("Test DB connection required.");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = @status,
                "OnboardingComplete" = true,
                "ApprovedAt" = @approvedAt,
                "UpdatedAt" = @approvedAt
            WHERE "Id" = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("status", (int)TenantLifecycleStatus.Active);
        cmd.Parameters.AddWithValue("approvedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", tenantId);
        var updated = await cmd.ExecuteNonQueryAsync();
        updated.Should().Be(1);
    }

    private WebApplicationFactory<Program> CreateAuthHost() =>
        CreateConfiguredHost(IntegrationDb.ResolveConnectionString());

    private WebApplicationFactory<Program> CreateHostOrSkip()
    {
        var connectionString = IntegrationDb.ResolveConnectionString();
        Skip.If(connectionString is null, IntegrationTestHelpers.NoDatabaseReason);
        return CreateConfiguredHost(connectionString);
    }

    private WebApplicationFactory<Program> CreateConfiguredHost(string? connectionString)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
            builder.UseSetting("Jwt:Secret", CustomWebApplicationFactory.TestJwtSecret);
            builder.UseSetting("Jwt:Issuer", TestJwtHelper.Issuer);
            builder.UseSetting("Jwt:Audience", TestJwtHelper.Audience);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                    ["Jwt:Secret"] = CustomWebApplicationFactory.TestJwtSecret,
                    ["Jwt:Issuer"] = TestJwtHelper.Issuer,
                    ["Jwt:Audience"] = TestJwtHelper.Audience,
                    ["Database:AutoMigrate"] = "false",
                    ["Database:Seed"] = "false",
                    ["Database:SeedPlatformUser"] = "false",
                };
                if (connectionString is not null)
                    settings["ConnectionStrings:Default"] = connectionString;
                config.AddInMemoryCollection(settings);
            });
        });
    }
}
