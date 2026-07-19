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
/// HTTP coverage for <c>/api/clinical</c> (vitals, notes, medical/family history).
/// Skips when no PostgreSQL connection is available.
/// </summary>
public class ClinicalApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string HospitalPassword = "TestHospital123!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public ClinicalApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Clinical_Endpoints_RequireAuth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var patientId = Guid.NewGuid();

        (await client.GetAsync($"/api/clinical/vitals/patient/{patientId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/clinical/notes/patient/{patientId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/clinical/vitals", JsonContent(new { })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Vitals_Notes_And_History_RoundTrip_ForTenantPatient()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await ProvisionActiveHospitalAsync(client, $"Clinical Hospital {suffix}", $"clinical-{suffix}@e2e.cureflow.test");
        var patientId = await CreatePatientAsync(client, $"Clinical Patient {suffix}", DigitsPhone(suffix, "9188"));

        var vitals = await client.PostAsync(
            "/api/clinical/vitals",
            JsonContent(new
            {
                PatientId = patientId,
                HeightCm = 170,
                WeightKg = 70,
                SystolicBp = 120,
                DiastolicBp = 80,
                HeartRate = 72,
                Temperature = 98.6m,
                RespiratoryRate = 16,
                OxygenSaturation = 98,
                Notes = "integration vitals",
            }));
        vitals.StatusCode.Should().Be(HttpStatusCode.OK);
        (await vitals.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);

        var listVitals = await client.GetAsync($"/api/clinical/vitals/patient/{patientId}");
        listVitals.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listVitals.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetArrayLength().Should().BeGreaterThan(0);

        var note = await client.PostAsync(
            "/api/clinical/notes",
            JsonContent(new
            {
                PatientId = patientId,
                NoteType = "progress",
                Subjective = "S: headache",
                Objective = "O: NAD",
                Assessment = "A: migraine",
                Plan = "P: rest + fluids",
            }));
        note.StatusCode.Should().Be(HttpStatusCode.OK);
        var noteId = (await note.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
        noteId.Should().NotBe(Guid.Empty);

        var patch = await client.PatchAsync(
            $"/api/clinical/notes/{noteId}",
            JsonContent(new { Subjective = "S: improved", Plan = "P: follow up" }));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync($"/api/clinical/notes/patient/{patientId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var medical = await client.PostAsync(
            "/api/clinical/medical-history",
            JsonContent(new
            {
                PatientId = patientId,
                Category = "chronic",
                Title = "Hypertension",
                OnsetDate = DateTime.UtcNow.AddYears(-2),
                IsOngoing = true,
                Description = "controlled",
            }));
        medical.StatusCode.Should().Be(HttpStatusCode.OK);

        var family = await client.PostAsync(
            "/api/clinical/family-history",
            JsonContent(new
            {
                PatientId = patientId,
                Relation = "father",
                Condition = "Diabetes",
                AgeOfOnset = 55,
                Notes = "type 2",
            }));
        family.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync($"/api/clinical/medical-history/patient/{patientId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/clinical/family-history/patient/{patientId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task AddVitals_WithInvalidPayload_ReturnsBadRequest()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await ProvisionActiveHospitalAsync(client, $"Clinical BadVitals {suffix}", $"clinical-bad-{suffix}@e2e.cureflow.test");
        var patientId = await CreatePatientAsync(client, $"Bad Vitals Patient {suffix}", DigitsPhone(suffix, "9187"));

        var response = await client.PostAsync(
            "/api/clinical/vitals",
            JsonContent(new
            {
                PatientId = patientId,
                HeightCm = -1,
                WeightKg = -1,
                SystolicBp = 900,
                DiastolicBp = -5,
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> CreatePatientAsync(HttpClient client, string name, string phone)
    {
        var create = await client.PostAsync(
            "/api/patients",
            JsonContent(new
            {
                Name = name,
                Phone = phone,
                Age = 40,
                Gender = "male",
                Department = "General",
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
        id.Should().NotBe(Guid.Empty);
        return id;
    }

    private static async Task ProvisionActiveHospitalAsync(HttpClient client, string hospitalName, string adminEmail)
    {
        var register = await client.PostAsync(
            "/api/auth/register-tenant",
            JsonContent(new
            {
                HospitalName = hospitalName,
                AdminName = "Clinical Admin",
                AdminEmail = adminEmail,
                AdminPassword = HospitalPassword,
                Phone = "+919900000011",
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
