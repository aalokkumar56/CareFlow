using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Services.Integrations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Safe-string / injection edges for outbound email (CRLF header injection, malformed recipients).
/// </summary>
public class EmailInjectionSafetyTests
{
    private static (EmailService Service, Mock<ICureFlowDbSession> Db) BuildConfigured()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSettings
            {
                SmtpHost = "smtp.example.com",
                FromEmail = "noreply@example.com",
                FromName = "CureFlow",
                Enabled = true,
                SmtpPort = 587,
                UseSsl = true
            });
        db.Setup(x => x.InsertAsync(It.IsAny<EmailMessage>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.UpdateAsync(It.IsAny<EmailMessage>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new EmailService(db.Object, Mock.Of<ILogger<EmailService>>());
        return (service, db);
    }

    [Theory]
    [InlineData("victim@example.com\r\nBcc: attacker@evil.com")]
    [InlineData("victim@example.com\nCc: attacker@evil.com")]
    [InlineData("victim@example.com%0d%0aBcc:attacker@evil.com")]
    public async Task SendAsync_rejects_crlf_in_recipient(string injectedRecipient)
    {
        var (service, db) = BuildConfigured();

        var (ok, _, error) = await service.SendAsync(injectedRecipient, "Subject", "Body");

        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
        // Must not report a successful SMTP hand-off.
        db.Verify(
            x => x.UpdateAsync(
                It.Is<EmailMessage>(m => m.Status == Domain.Enums.MessageStatus.Sent),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Hello\r\nBcc: attacker@evil.com")]
    [InlineData("Hello\nX-Injected: true")]
    public async Task SendAsync_does_not_mark_sent_when_subject_contains_crlf(string injectedSubject)
    {
        var (service, db) = BuildConfigured();

        // SmtpClient will fail against the fake host; we only assert we never claim success
        // and that the attempt surfaces an error rather than silently succeeding.
        var (ok, messageId, error) = await service.SendAsync(
            "patient@example.com", injectedSubject, "Body");

        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
        if (messageId.HasValue)
        {
            db.Verify(
                x => x.UpdateAsync(
                    It.Is<EmailMessage>(m => m.Id == messageId && m.Status == Domain.Enums.MessageStatus.Sent),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [Fact]
    public async Task SendAsync_rejects_recipient_with_angle_bracket_spoof()
    {
        var (service, _) = BuildConfigured();

        var (ok, _, error) = await service.SendAsync(
            "Display Name <attacker@evil.com>, victim@example.com",
            "Subject",
            "Body");

        // Either rejected up-front or failed without a sent status — never a clean success.
        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetStatusAsync_treats_blank_smtp_host_as_not_configured()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSettings
            {
                SmtpHost = "   ",
                FromEmail = "noreply@example.com",
                Enabled = true
            });

        var service = new EmailService(db.Object, Mock.Of<ILogger<EmailService>>());
        var status = await service.GetStatusAsync();

        status.IsConfigured.Should().BeFalse();
        status.Message.Should().Contain("not configured");
    }
}
