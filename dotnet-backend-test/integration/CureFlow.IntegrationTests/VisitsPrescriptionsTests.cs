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
/// HTTP coverage for visits and prescriptions.
/// Skips when no PostgreSQL connection is available.
/// </summary>
public class VisitsPrescriptionsTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string HospitalPassword = "TestHospital123!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public VisitsPrescriptionsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Visits_And_Prescriptions_RequireAuth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        (await client.GetAsync($"/api/patients/{id}/visits"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/visits/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/prescriptions/patient/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/prescriptions", JsonContent(new { })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task CreateVisit_Update_Complete_And_Prescription_RoundTrip()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await ProvisionActiveHospitalAsync(client, $"Visits Hospital {suffix}", $"visits-{suffix}@e2e.cureflow.test");
        var patientId = await CreatePatientAsync(client, $"Visit Patient {suffix}", DigitsPhone(suffix, "9186"));

        var createVisit = await client.PostAsync(
            $"/api/patients/{patientId}/visits",
            JsonContent(new
            {
                PatientId = patientId,
                Department = "General",
                VisitType = "outpatient",
                Symptoms = "fever",
                Diagnosis = "viral fever",
                DoctorNotes = "observe",
                FollowUpAdvice = "fluids",
                FollowUpDate = DateTime.UtcNow.AddDays(7),
            }));
        createVisit.StatusCode.Should().Be(HttpStatusCode.OK);
        var visitId = (await createVisit.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
        visitId.Should().NotBe(Guid.Empty);

        (await client.GetAsync($"/api/visits/{visitId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var listVisits = await client.GetAsync($"/api/patients/{patientId}/visits");
        listVisits.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listVisits.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetArrayLength().Should().BeGreaterThan(0);

        (await client.GetAsync($"/api/patients/{patientId}/timeline"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var patch = await client.PatchAsync(
            $"/api/visits/{visitId}",
            JsonContent(new
            {
                Symptoms = "fever + cough",
                Diagnosis = "URI",
                DoctorNotes = "supportive care",
                FollowUpAdvice = "rest",
            }));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var rx = await client.PostAsync(
            "/api/prescriptions",
            JsonContent(new
            {
                PatientId = patientId,
                VisitId = visitId,
                Diagnosis = "URI",
                ChiefComplaint = "cough",
                ClinicalNotes = "mild",
                FollowUpAdvice = "rest",
                NextVisitDate = DateTime.UtcNow.AddDays(5),
                Items = new[]
                {
                    new
                    {
                        DrugName = "Paracetamol",
                        GenericName = "Acetaminophen",
                        Strength = "500mg",
                        Form = "tablet",
                        Route = "oral",
                        Dosage = "1 tab",
                        Frequency = "TID",
                        Duration = "3 days",
                        Timing = "after food",
                        Quantity = 9,
                        ReasonForPrescribing = "fever / pain",
                        PossibleSideEffects = "rare rash",
                        PatientInstructions = "do not exceed dose",
                        IsContinuation = false,
                        IsAcute = true,
                    },
                },
            }));
        rx.StatusCode.Should().Be(HttpStatusCode.OK);
        var rxId = (await rx.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
        rxId.Should().NotBe(Guid.Empty);

        (await client.GetAsync($"/api/prescriptions/{rxId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/prescriptions/patient/{patientId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var print = await client.GetAsync($"/api/prescriptions/{rxId}/print");
        print.StatusCode.Should().Be(HttpStatusCode.OK);
        print.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await print.Content.ReadAsStringAsync()).Should().Contain("Paracetamol");

        var complete = await client.PostAsync($"/api/visits/{visitId}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid> CreatePatientAsync(HttpClient client, string name, string phone)
    {
        var create = await client.PostAsync(
            "/api/patients",
            JsonContent(new
            {
                Name = name,
                Phone = phone,
                Age = 32,
                Gender = "female",
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
                AdminName = "Visits Admin",
                AdminEmail = adminEmail,
                AdminPassword = HospitalPassword,
                Phone = "+919900000022",
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
