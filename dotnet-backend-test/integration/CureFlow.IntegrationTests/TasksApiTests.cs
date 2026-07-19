using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/tasks</c> (create, list, patch, delete) + cross-tenant isolation.
/// Skips when no test database / e2e seed login is available.
/// </summary>
public class TasksApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly CustomWebApplicationFactory _factory;

    public TasksApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Tasks_Endpoints_RequireAuth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        (await client.GetAsync("/api/tasks"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync(
                "/api/tasks",
                new { title = "x", type = "follow_up", priority = "medium" },
                JsonOpts))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PatchAsJsonAsync($"/api/tasks/{id}", new { status = "done" }, JsonOpts))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync($"/api/tasks/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Tasks_WithoutDashboardView_Return403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithPermissions(CureFlowPermissions.PatientView);

        (await client.GetAsync("/api/tasks"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync(
                "/api/tasks",
                new { title = "blocked", type = "callback", priority = "low" },
                JsonOpts))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Create_List_Patch_And_Delete_Task()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var token = await LoginE2eAdminAsync(MultiHospitalE2eSeeder.Hospitals[0].AdminEmail);
        token.Should().NotBeNullOrWhiteSpace("auth token required when database is available");

        using var client = CreateAuthedClient(token);
        var title = $"integration-task-{Guid.NewGuid():N}"[..40];

        var create = await client.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title,
                type = "follow_up",
                patient_id = (Guid?)null,
                notes = "integration notes",
                due_at = DateTime.UtcNow.AddDays(1),
                priority = "high",
                assigned_to = (Guid?)null,
            },
            JsonOpts);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var taskId = createBody.GetProperty("id").GetGuid();
        taskId.Should().NotBe(Guid.Empty);

        var list = await client.GetAsync("/api/tasks");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        listBody.ValueKind.Should().Be(JsonValueKind.Array);
        var listedIds = listBody.EnumerateArray()
            .Select(t =>
            {
                if (!t.TryGetProperty("id", out var idProp))
                    return Guid.Empty;
                return idProp.GetGuid();
            }).ToList();
        listedIds.Should().Contain(taskId);

        var patch = await client.PatchAsJsonAsync(
            $"/api/tasks/{taskId}",
            new { status = "done" },
            JsonOpts);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/tasks/{taskId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var listAfter = await client.GetAsync("/api/tasks");
        listAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBody = await listAfter.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var afterIds = afterBody.EnumerateArray()
            .Select(t =>
            {
                if (!t.TryGetProperty("id", out var idProp))
                    return Guid.Empty;
                return idProp.GetGuid();
            }).ToList();
        afterIds.Should().NotContain(taskId);
    }

    [SkippableFact]
    public async Task Patch_OtherTenantTask_ReturnsNotFound()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var tokenA = await LoginE2eAdminAsync(MultiHospitalE2eSeeder.Hospitals[0].AdminEmail);
        var tokenB = await LoginE2eAdminAsync(MultiHospitalE2eSeeder.Hospitals[1].AdminEmail);
        tokenA.Should().NotBeNullOrWhiteSpace("e2e hospital A admin login required");
        tokenB.Should().NotBeNullOrWhiteSpace("e2e hospital B admin login required");

        using var clientA = CreateAuthedClient(tokenA);
        var create = await clientA.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = $"cross-tenant-{Guid.NewGuid():N}"[..36],
                type = "callback",
                priority = "medium",
            },
            JsonOpts);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var taskId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        using var clientB = CreateAuthedClient(tokenB);
        var patch = await clientB.PatchAsJsonAsync(
            $"/api/tasks/{taskId}",
            new { status = "done" },
            JsonOpts);

        patch.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        var client = _factory.CreateClient();
        var token = TestJwtHelper.CreateToken(
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            permissions: permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string?> LoginE2eAdminAsync(string email)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = MultiHospitalE2eSeeder.DefaultPassword },
            JsonOpts);

        if (response.StatusCode != HttpStatusCode.OK)
            return null;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("access_token").GetString();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private HttpClient CreateAuthedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
