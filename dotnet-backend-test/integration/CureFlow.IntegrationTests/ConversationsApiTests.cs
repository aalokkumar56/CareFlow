using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP contract tests for <c>/api/conversations</c> with mocked <see cref="IConversationService"/>.
/// </summary>
public class ConversationsApiTests : IClassFixture<ConversationsApiTests.Factory>
{
    private readonly Factory _factory;
    private HttpClient? _client;

    public ConversationsApiTests(Factory factory)
    {
        _factory = factory;
        _factory.Conversations.Reset();
        SetupDefaultConversationMock();
    }

    private HttpClient Client =>
        _client ??= _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    [Fact]
    public async Task Get_conversations_without_auth_returns_unauthorized()
    {
        var response = await Client.GetAsync("/api/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_conversations_with_view_permission_returns_paged_list()
    {
        var conversationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _factory.Conversations
            .Setup(s => s.ListAsync(null, 1, Pagination.DefaultPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<object>
            {
                Items = new object[] { new { id = conversationId, wa_phone = "+919876543210" } },
                Total = 1,
                Page = 1,
                PageSize = Pagination.DefaultPageSize,
            });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/conversations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(
            CureFlowPermissions.ConversationView));

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("total").GetInt32().Should().Be(1);
        json.GetProperty("items").GetArrayLength().Should().Be(1);
        json.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(conversationId.ToString());

        _factory.Conversations.Verify(
            s => s.ListAsync(null, 1, Pagination.DefaultPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Get_conversation_by_id_returns_ok()
    {
        var conversationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        _factory.Conversations
            .Setup(s => s.GetAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { id = conversationId, status = "open" });

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/conversations/{conversationId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(
            CureFlowPermissions.ConversationView));

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetString().Should().Be(conversationId.ToString());
        json.GetProperty("status").GetString().Should().Be("open");
    }

    [Fact]
    public async Task Post_messages_with_empty_body_returns_bad_request()
    {
        var conversationId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            conversation_id = conversationId,
            body = "   ",
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/conversations/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(
            CureFlowPermissions.ConversationManage));

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("body is required");

        _factory.Conversations.Verify(
            s => s.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_messages_with_valid_body_returns_message_id()
    {
        var conversationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var messageId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        _factory.Conversations
            .Setup(s => s.SendMessageAsync(conversationId, "Hello patient", It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageId);

        var payload = JsonSerializer.Serialize(new
        {
            conversation_id = conversationId,
            body = "Hello patient",
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/conversations/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(
            CureFlowPermissions.ConversationManage));

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().Be(messageId);

        _factory.Conversations.Verify(
            s => s.SendMessageAsync(conversationId, "Hello patient", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static string IssueToken(params string[] permissions) =>
        TestJwtHelper.CreateToken(
            userId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            tenantId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            permissions: permissions,
            email: "integration@cureflow.test",
            role: "Admin",
            asPlatformUser: true);

    private void SetupDefaultConversationMock()
    {
        _factory.Conversations
            .Setup(s => s.ListAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<object>());

        _factory.Conversations
            .Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { id = Guid.Empty });

        _factory.Conversations
            .Setup(s => s.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
    }

    public sealed class Factory : CustomWebApplicationFactory
    {
        public Mock<IConversationService> Conversations { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Mocked conversation contract tests do not need RBAC / live DB at startup.
                    ["Database:SeedRbac"] = "false",
                });
            });
            builder.ConfigureTestServices(services => ReplaceService(services, Conversations.Object));
        }
    }
}
