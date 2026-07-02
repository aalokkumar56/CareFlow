using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/settings/email")]
public class EmailSettingsController : ControllerBase
{
    private readonly ICureFlowDbSession _db;
    private readonly IEmailService _email;

    public EmailSettingsController(ICureFlowDbSession db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public record EmailSettingsRequest(
        string? SmtpHost,
        int? SmtpPort,
        string? SmtpUsername,
        string? SmtpPassword,
        bool? UseSsl,
        string? FromEmail,
        string? FromName,
        bool Enabled,
        bool SendWithWhatsApp);

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await _email.GetStatusAsync(ct);
        return Ok(new { enabled = status.Enabled, is_configured = status.IsConfigured, message = status.Message });
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Settings.View")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<EmailSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<EmailSettings>(
            $"""SELECT * FROM "EmailSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (s == null)
        {
            return Ok(new { enabled = false, smtp_port = 587, use_ssl = true, send_with_whatsapp = true });
        }

        return Ok(new
        {
            smtp_host = s.SmtpHost,
            smtp_port = s.SmtpPort,
            smtp_username = s.SmtpUsername,
            smtp_password = MaskSecret(s.SmtpPasswordEncrypted),
            use_ssl = s.UseSsl,
            from_email = s.FromEmail,
            from_name = s.FromName,
            enabled = s.Enabled,
            send_with_whatsapp = s.SendWithWhatsApp,
            has_smtp_password = !string.IsNullOrWhiteSpace(s.SmtpPasswordEncrypted),
        });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> Save([FromBody] EmailSettingsRequest req, CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<EmailSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<EmailSettings>(
            $"""SELECT * FROM "EmailSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        var isNew = s == null;
        s ??= new EmailSettings();
        if (req.SmtpHost != null) s.SmtpHost = req.SmtpHost;
        if (req.SmtpPort.HasValue) s.SmtpPort = req.SmtpPort.Value;
        if (req.SmtpUsername != null) s.SmtpUsername = req.SmtpUsername;
        if (!string.IsNullOrWhiteSpace(req.SmtpPassword)) s.SmtpPasswordEncrypted = req.SmtpPassword;
        if (req.UseSsl.HasValue) s.UseSsl = req.UseSsl.Value;
        if (req.FromEmail != null) s.FromEmail = req.FromEmail;
        if (req.FromName != null) s.FromName = req.FromName;
        s.Enabled = req.Enabled;
        s.SendWithWhatsApp = req.SendWithWhatsApp;
        if (isNew)
            await _db.InsertAsync(s, ct: ct);
        else
            await _db.UpdateAsync(s, ct: ct);
        return Ok(new { ok = true });
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return "********";
    }
}
