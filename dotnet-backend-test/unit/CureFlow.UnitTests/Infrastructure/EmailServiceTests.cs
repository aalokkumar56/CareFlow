using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Services.Integrations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

public class EmailServiceTests
{
    private static readonly Guid PatientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (EmailService Service, Mock<ICureFlowDbSession> Db) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        var service = new EmailService(db.Object, Mock.Of<ILogger<EmailService>>());
        return (service, db);
    }

    [Fact]
    public async Task GetStatusAsync_reports_not_configured_when_settings_missing()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailSettings?)null);

        var status = await service.GetStatusAsync();

        status.Enabled.Should().BeFalse();
        status.IsConfigured.Should().BeFalse();
        status.Message.Should().Contain("not configured");
    }

    [Fact]
    public async Task GetStatusAsync_reports_disabled_when_configured_but_inactive()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSettings
            {
                SmtpHost = "smtp.example.com",
                FromEmail = "noreply@example.com",
                Enabled = false
            });

        var status = await service.GetStatusAsync();

        status.Enabled.Should().BeFalse();
        status.IsConfigured.Should().BeTrue();
        status.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task GetStatusAsync_reports_active_when_enabled_and_configured()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSettings
            {
                SmtpHost = "smtp.example.com",
                FromEmail = "noreply@example.com",
                Enabled = true
            });

        var status = await service.GetStatusAsync();

        status.Enabled.Should().BeTrue();
        status.IsConfigured.Should().BeTrue();
        status.Message.Should().Contain("active");
    }

    [Fact]
    public async Task SendAsync_rejects_when_email_not_configured()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailSettings?)null);

        var (ok, messageId, error) = await service.SendAsync("patient@example.com", "Subject", "Body");

        ok.Should().BeFalse();
        messageId.Should().BeNull();
        error.Should().Contain("not configured");
        db.Verify(x => x.InsertAsync(It.IsAny<EmailMessage>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_rejects_empty_recipient()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSettings
            {
                SmtpHost = "smtp.example.com",
                FromEmail = "noreply@example.com",
                Enabled = true
            });

        var (ok, _, error) = await service.SendAsync("  ", "Subject", "Body");

        ok.Should().BeFalse();
        error.Should().Contain("Recipient email is required");
    }

    [Fact]
    public async Task SendToPatientAsync_rejects_when_patient_not_found()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var (ok, _, error) = await service.SendToPatientAsync(PatientId, "Hi", "Body");

        ok.Should().BeFalse();
        error.Should().Be("Patient not found");
    }

    [Fact]
    public async Task SendToPatientAsync_rejects_when_patient_opted_out()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient
            {
                Id = PatientId,
                Email = "patient@example.com",
                EmailNotificationsEnabled = false
            });

        var (ok, _, error) = await service.SendToPatientAsync(PatientId, "Hi", "Body");

        ok.Should().BeFalse();
        error.Should().Contain("disabled for this patient");
    }

    [Fact]
    public async Task SendToPatientAsync_rejects_when_patient_has_no_email()
    {
        var (service, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient
            {
                Id = PatientId,
                Email = null,
                EmailNotificationsEnabled = true
            });

        var (ok, _, error) = await service.SendToPatientAsync(PatientId, "Hi", "Body");

        ok.Should().BeFalse();
        error.Should().Contain("no email address");
    }
}
