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
/// Tenant-scoped patient CRUD via <see cref="CustomWebApplicationFactory"/>.
/// Skips when no PostgreSQL connection is available.
/// </summary>
public class PatientsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string HospitalPassword = "TestHospital123!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public PatientsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Patient_Crud_IsScopedToAuthenticatedTenant()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"patients-api-{suffix}@e2e.cureflow.test";

        await ProvisionActiveHospitalAsync(client, $"Patients API Hospital {suffix}", email);
        var phone = $"+9198{suffix.Where(char.IsDigit).Take(8).DefaultIfEmpty('0').Aggregate("", (a, c) => a + c).PadRight(8, '0')[..8]}";

        var create = await client.PostAsync(
            "/api/patients",
            JsonContent(new
            {
                Name = $"Integration Patient {suffix}",
                Phone = phone,
                Department = "General Medicine",
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var patientId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
        patientId.Should().NotBe(Guid.Empty);

        var get = await client.GetAsync($"/api/patients/{patientId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await get.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        detail.GetProperty("id").GetGuid().Should().Be(patientId);
        detail.GetProperty("name").GetString().Should().Be($"Integration Patient {suffix}");

        var patch = await client.PatchAsync(
            $"/api/patients/{patientId}",
            JsonContent(new
            {
                Name = $"Updated Patient {suffix}",
                Notes = "integration-update",
            }));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterUpdate = await client.GetAsync($"/api/patients/{patientId}");
        getAfterUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await getAfterUpdate.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        updated.GetProperty("name").GetString().Should().Be($"Updated Patient {suffix}");
        updated.GetProperty("notes").GetString().Should().Be("integration-update");

        var list = await client.GetAsync("/api/patients");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        listBody.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .Should().Contain(patientId);

        var delete = await client.DeleteAsync($"/api/patients/{patientId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDeleted = await client.GetAsync($"/api/patients/{patientId}");
        getDeleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Patient_Get_ReturnsNotFound_ForOtherTenant()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var clientA = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var emailA = $"patients-a-{suffix}@e2e.cureflow.test";
        var emailB = $"patients-b-{suffix}@e2e.cureflow.test";

        await ProvisionActiveHospitalAsync(clientA, $"Patients A {suffix}", emailA);

        var create = await clientA.PostAsync(
            "/api/patients",
            JsonContent(new
            {
                Name = $"Tenant A Patient {suffix}",
                Phone = $"+9197{new string(suffix.Where(char.IsDigit).Take(8).ToArray()).PadRight(8, '0')[..8]}",
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var patientId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        using var clientB = _factory.CreateClient();
        await ProvisionActiveHospitalAsync(clientB, $"Patients B {suffix}", emailB);

        var foreignGet = await clientB.GetAsync($"/api/patients/{patientId}");
        foreignGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownGet = await clientA.GetAsync($"/api/patients/{patientId}");
        ownGet.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Patients_List_RequiresAuthentication()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/patients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task ProvisionActiveHospitalAsync(HttpClient client, string hospitalName, string adminEmail)
    {
        var register = await client.PostAsync(
            "/api/auth/register-tenant",
            JsonContent(new
            {
                HospitalName = hospitalName,
                AdminName = "Patients Admin",
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

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
}
