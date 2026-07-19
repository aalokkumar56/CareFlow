using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Services.Integrations;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

public class WhatsAppSettingsServiceTests
{
    private static WhatsAppSettingsService Service(Mock<ICureFlowDbSession> db) => new(db.Object);

    [Fact]
    public async Task GetStatusAsync_reports_not_configured_when_no_settings_row()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<WhatsAppSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppSettings?)null);

        var status = await Service(db).GetStatusAsync();

        status.IsConfigured.Should().BeFalse();
        status.Message.Should().Contain("not configured");
    }

    [Fact]
    public async Task GetStatusAsync_MetaCloud_requires_phone_id_and_access_token()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<WhatsAppSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppSettings
            {
                Provider = "MetaCloud",
                PhoneNumberId = "123",
                AccessTokenEncrypted = null,
                Enabled = true
            });

        var status = await Service(db).GetStatusAsync();

        status.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_WhatsBiz_requires_api_token()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<WhatsAppSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppSettings
            {
                Provider = "WhatsBiz",
                ApiTokenEncrypted = null,
                Enabled = true
            });

        var status = await Service(db).GetStatusAsync();

        status.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_reports_active_when_meta_cloud_fully_configured()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<WhatsAppSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppSettings
            {
                Provider = "MetaCloud",
                PhoneNumberId = "phone-id",
                AccessTokenEncrypted = "secret",
                Enabled = true
            });

        var status = await Service(db).GetStatusAsync();

        status.Enabled.Should().BeTrue();
        status.IsConfigured.Should().BeTrue();
        status.Message.Should().Contain("active");
    }

    [Fact]
    public async Task GetStatusAsync_reports_disabled_when_configured_but_inactive()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<WhatsAppSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppSettings
            {
                Provider = "MetaCloud",
                PhoneNumberId = "phone-id",
                AccessTokenEncrypted = "secret",
                Enabled = false
            });

        var status = await Service(db).GetStatusAsync();

        status.Enabled.Should().BeFalse();
        status.IsConfigured.Should().BeTrue();
        status.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task GetAsync_returns_whatsbiz_defaults_when_row_missing()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<WhatsAppSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppSettings?)null);

        var runtime = await Service(db).GetAsync();

        runtime.Provider.Should().Be("MetaCloud");
        runtime.IsConfigured.Should().BeFalse();
        runtime.WhatsBizBaseUrl.Should().Contain("whatsbizapi.com");
    }
}
