using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Tenant-scoped appointment CRUD via <see cref="CustomWebApplicationFactory"/>.
/// Skips when no PostgreSQL connection is available.
/// </summary>
public class AppointmentsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string HospitalPassword = "TestHospital123!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public AppointmentsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Appointment_Crud_IsScopedToAuthenticatedTenant()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"appts-api-{suffix}@e2e.cureflow.test";

        await ProvisionActiveHospitalAsync(client, $"Appointments API Hospital {suffix}", email);
        var doctorUserId = await CreateBookableDoctorAsync(client, suffix);
        var patientId = await CreatePatientAsync(client, $"Appt Patient {suffix}", DigitsPhone(suffix, "9196"));

        var scheduledAt = DateTime.UtcNow.AddDays(2).AddHours(3);
        var create = await client.PostAsync(
            "/api/appointments",
            JsonContent(new
            {
                PatientId = patientId,
                DoctorUserId = doctorUserId,
                DoctorName = $"Dr Integration {suffix}",
                Department = "General Medicine",
                ScheduledAt = scheduledAt,
                Notes = "integration-create",
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var appointmentId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
        appointmentId.Should().NotBe(Guid.Empty);

        var get = await client.GetAsync($"/api/appointments/{appointmentId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await get.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        detail.GetProperty("id").GetGuid().Should().Be(appointmentId);
        detail.GetProperty("patient_id").GetGuid().Should().Be(patientId);
        detail.GetProperty("doctor_user_id").GetGuid().Should().Be(doctorUserId);
        detail.GetProperty("department").GetString().Should().Be("General Medicine");
        detail.GetProperty("notes").GetString().Should().Be("integration-create");
        detail.GetProperty("status").GetString().Should().Be("scheduled");

        var newTime = scheduledAt.AddHours(2);
        var patch = await client.PatchAsync(
            $"/api/appointments/{appointmentId}",
            JsonContent(new
            {
                ScheduledAt = newTime,
                Notes = "integration-reschedule",
                ChiefComplaint = "follow-up",
                DurationMinutes = 45,
            }));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterUpdate = await client.GetAsync($"/api/appointments/{appointmentId}");
        getAfterUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await getAfterUpdate.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        updated.GetProperty("notes").GetString().Should().Be("integration-reschedule");
        updated.GetProperty("chief_complaint").GetString().Should().Be("follow-up");
        updated.GetProperty("duration_minutes").GetInt32().Should().Be(45);

        var statusPatch = await client.PatchAsync(
            $"/api/appointments/{appointmentId}/status",
            JsonContent(new { Status = "confirmed" }));
        statusPatch.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterStatus = await client.GetAsync($"/api/appointments/{appointmentId}");
        getAfterStatus.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await getAfterStatus.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        confirmed.GetProperty("status").GetString().Should().Be("confirmed");

        var list = await client.GetAsync("/api/appointments?status=confirmed");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        listBody.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .Should().Contain(appointmentId);

        var bookingOptions = await client.GetAsync("/api/appointments/booking-options");
        bookingOptions.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Appointment_Get_ReturnsNotFound_ForOtherTenant()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var clientA = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var emailA = $"appts-a-{suffix}@e2e.cureflow.test";
        var emailB = $"appts-b-{suffix}@e2e.cureflow.test";

        await ProvisionActiveHospitalAsync(clientA, $"Appts A {suffix}", emailA);
        var doctorUserId = await CreateBookableDoctorAsync(clientA, $"a{suffix}");
        var patientId = await CreatePatientAsync(clientA, $"Appt A Patient {suffix}", DigitsPhone(suffix, "9195"));

        var create = await clientA.PostAsync(
            "/api/appointments",
            JsonContent(new
            {
                PatientId = patientId,
                DoctorUserId = doctorUserId,
                Department = "Cardiology",
                ScheduledAt = DateTime.UtcNow.AddDays(3),
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var appointmentId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        using var clientB = _factory.CreateClient();
        await ProvisionActiveHospitalAsync(clientB, $"Appts B {suffix}", emailB);

        var foreignGet = await clientB.GetAsync($"/api/appointments/{appointmentId}");
        foreignGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownGet = await clientA.GetAsync($"/api/appointments/{appointmentId}");
        ownGet.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Appointments_List_RequiresAuthentication()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/appointments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<Guid> CreatePatientAsync(HttpClient client, string name, string phone)
    {
        var create = await client.PostAsync(
            "/api/patients",
            JsonContent(new { Name = name, Phone = phone }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateBookableDoctorAsync(HttpClient client, string suffix)
    {
        var doctorEmail = $"doctor-{suffix}@e2e.cureflow.test";
        var register = await client.PostAsync(
            "/api/auth/register",
            JsonContent(new
            {
                Name = $"Dr Integration {suffix}",
                Email = doctorEmail,
                Password = HospitalPassword,
                Role = "doctor",
                Specialty = "General Medicine",
                Phone = "+919900000088",
            }));
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var doctorUserId = (await register.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        var staff = await client.PostAsync(
            "/api/staff",
            JsonContent(new
            {
                UserId = doctorUserId,
                Department = "General Medicine",
                Specialization = "General Medicine",
                ConsultationFee = 500m,
                IsAvailable = true,
            }));
        staff.StatusCode.Should().Be(HttpStatusCode.OK);
        return doctorUserId;
    }

    private static async Task ProvisionActiveHospitalAsync(HttpClient client, string hospitalName, string adminEmail)
    {
        var register = await client.PostAsync(
            "/api/auth/register-tenant",
            JsonContent(new
            {
                HospitalName = hospitalName,
                AdminName = "Appointments Admin",
                AdminEmail = adminEmail,
                AdminPassword = HospitalPassword,
                Phone = "+919900000099",
            }));
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenantId = (await register.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        await ActivateTenantInDbAsync(tenantId);

        var login = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new { Email = adminEmail, Password = HospitalPassword }));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessToken = (await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task ActivateTenantInDbAsync(Guid tenantId)
    {
        var cs = IntegrationDb.ResolveConnectionString();
        cs.Should().NotBeNullOrWhiteSpace();

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = 1,
                "OnboardingComplete" = true,
                "ApprovedAt" = NOW(),
                "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "IsDeleted" = false
            """,
            conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        var updated = await cmd.ExecuteNonQueryAsync();
        updated.Should().Be(1);
    }

    private static string DigitsPhone(string suffix, string prefix) =>
        prefix + $"{Math.Abs(suffix.GetHashCode(StringComparison.Ordinal)):D6}"[..6];

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
}
