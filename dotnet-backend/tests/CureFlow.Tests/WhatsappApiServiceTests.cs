using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CureFlow.Tests;

public class WhatsappApiServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static (WhatsappApiService Service, Mock<IWhatsappProvider> Provider, Mock<ICureFlowDbSession> Db) Build(
        bool providerConfigured = true)
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);

        db.Setup(x => x.QueryFirstOrDefaultAsync<Conversation>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        db.Setup(x => x.QueryAsync<Conversation>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Conversation>());

        db.Setup(x => x.InsertAsync(It.IsAny<Conversation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.InsertAsync(It.IsAny<Message>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.UpdateAsync(It.IsAny<Conversation>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var provider = new Mock<IWhatsappProvider>();
        provider.SetupGet(x => x.ProviderName).Returns("TestProvider");
        provider.Setup(x => x.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerConfigured);
        provider.Setup(x => x.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSendResult(true, "wamid.test", null));

        var tenant = new CurrentTenant { TenantId = TenantId, IsAuthenticated = true };

        var service = new WhatsappApiService(
            db.Object,
            provider.Object,
            tenant,
            Mock.Of<ILogger<WhatsappApiService>>());

        return (service, provider, db);
    }

    [Theory]
    [InlineData("", "hello")]
    [InlineData("9876543210", "")]
    [InlineData("   ", "message")]
    public async Task SendMessageAsync_rejects_missing_phone_or_message(string phone, string message)
    {
        var (service, provider, _) = Build();

        var result = await service.SendMessageAsync(new SendMessageRequest { Phone = phone, Message = message });

        result.Success.Should().BeFalse();
        provider.Verify(x => x.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_uses_demo_mode_when_provider_not_configured()
    {
        var (service, provider, db) = Build(providerConfigured: false);

        var result = await service.SendMessageAsync(new SendMessageRequest
        {
            Phone = "9876543210",
            Message = "Hello from demo"
        });

        result.Success.Should().BeTrue();
        result.MessageId.Should().StartWith("demo-");
        provider.Verify(x => x.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(x => x.InsertAsync(It.IsAny<Message>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_delegates_to_provider_when_configured()
    {
        var (service, provider, db) = Build(providerConfigured: true);

        var result = await service.SendMessageAsync(new SendMessageRequest
        {
            Phone = "+91 98765 43210",
            Message = "Configured send"
        });

        result.Success.Should().BeTrue();
        result.MessageId.Should().Be("wamid.test");
        provider.Verify(x => x.SendTextAsync("+91 98765 43210", "Configured send", It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(x => x.InsertAsync(It.IsAny<Message>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMediaAsync_rejects_missing_media_url()
    {
        var (service, provider, _) = Build();

        var result = await service.SendMediaAsync(new SendMediaRequest
        {
            Phone = "9876543210",
            MediaType = "image",
            MediaUrl = ""
        });

        result.Success.Should().BeFalse();
        provider.Verify(x => x.SendMediaAsync(It.IsAny<SendMediaRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendTemplateMessageAsync_rejects_missing_template_name()
    {
        var (service, provider, _) = Build();

        var result = await service.SendTemplateMessageAsync(new SendTemplateMessageRequest
        {
            Phone = "9876543210",
            TemplateName = ""
        });

        result.Success.Should().BeFalse();
        provider.Verify(x => x.SendTemplateMessageAsync(It.IsAny<SendTemplateMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendCampaignAsync_returns_demo_trigger_when_not_configured()
    {
        var (service, _, _) = Build(providerConfigured: false);

        var result = await service.SendCampaignAsync(new SendCampaignRequest { CampaignId = 42 });

        result.Success.Should().BeTrue();
        result.Status.Should().Be("triggered");
        result.CampaignId.Should().Be("42");
    }
}
