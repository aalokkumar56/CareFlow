using CureFlow.Application.Common;
using System.Net;
using System.Net.Mail;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services.Integrations;

public class EmailService : IEmailService, IEmailInboxService
{
    private readonly ICureFlowDbSession _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ICureFlowDbSession db, ILogger<EmailService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IntegrationStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<EmailSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<EmailSettings>(
            $"""SELECT * FROM "EmailSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (s == null || string.IsNullOrWhiteSpace(s.SmtpHost) || string.IsNullOrWhiteSpace(s.FromEmail))
        {
            return new IntegrationStatusDto(
                s?.Enabled ?? false,
                false,
                "Email is not configured. Add SMTP settings under Settings → Integrations.");
        }

        if (!s.Enabled)
        {
            return new IntegrationStatusDto(
                false,
                true,
                "Email is disabled. Enable it under Settings → Integrations.");
        }

        return new IntegrationStatusDto(true, true, "Email is active.");
    }

    public async Task<(bool ok, Guid? messageId, string? error)> SendToPatientAsync(
        Guid patientId, string subject, string body, CancellationToken ct = default)
    {
        var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var patient = await _db.QueryFirstOrDefaultAsync<Patient>(
            $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
            new { patientId },
            ct: ct);
        if (patient == null)
            return (false, null, "Patient not found");
        if (!patient.EmailNotificationsEnabled)
            return (false, null, "Email notifications are disabled for this patient.");
        if (string.IsNullOrWhiteSpace(patient.Email))
            return (false, null, "Patient has no email address.");

        return await SendAsync(patient.Email.Trim(), subject, body, patientId, ct);
    }

    public async Task<(bool ok, Guid? messageId, string? error)> SendAsync(
        string toEmail, string? subject, string body, Guid? patientId = null, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.Enabled || !status.IsConfigured)
            return (false, null, status.Message);

        if (string.IsNullOrWhiteSpace(toEmail))
            return (false, null, "Recipient email is required.");

        var where = SqlFragments.WhereActive<EmailSettings>(ignoreTenant: false);
        var settings = await _db.QueryFirstOrDefaultAsync<EmailSettings>(
            $"""SELECT * FROM "EmailSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (settings == null)
            return (false, null, "Email is not configured.");

        var msg = new EmailMessage
        {
            PatientId = patientId,
            ToEmail = toEmail.Trim(),
            Subject = subject,
            Body = body,
            Direction = MessageDirection.Outbound,
            Status = MessageStatus.Pending,
        };
        await _db.InsertAsync(msg, ct: ct);

        try
        {
            using var client = BuildSmtpClient(settings);
            using var mail = new MailMessage
            {
                From = new MailAddress(settings.FromEmail!, settings.FromName ?? settings.FromEmail),
                Subject = subject ?? "(no subject)",
                Body = body,
                IsBodyHtml = false,
            };
            mail.To.Add(msg.ToEmail);
            await client.SendMailAsync(mail, ct);

            msg.Status = MessageStatus.Sent;
            msg.SentAt = DateTime.UtcNow;
            await _db.UpdateAsync(msg, ct: ct);
            return (true, msg.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email send failed to {Email}", toEmail);
            msg.Status = MessageStatus.Failed;
            msg.ErrorMessage = ex.Message;
            await _db.UpdateAsync(msg, ct: ct);
            return (false, msg.Id, ex.Message);
        }
    }

    public async Task<IReadOnlyList<EmailThreadDto>> ListThreadsAsync(string? q, int limit = 100, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<EmailMessage>(ignoreTenant: false);
        var filterSql = "";
        if (!string.IsNullOrWhiteSpace(q))
        {
            filterSql = """
                 AND (LOWER("ToEmail") LIKE @term
                      OR LOWER(COALESCE("Subject", '')) LIKE @term
                      OR LOWER("Body") LIKE @term)
                """;
        }

        var sql = $"""
            SELECT "PatientId", "ToEmail", "Subject", "Body", "CreatedAt"
            FROM "EmailMessages"
            WHERE {where} AND "PatientId" IS NOT NULL{filterSql}
            ORDER BY "CreatedAt" DESC
            LIMIT @limit
            """;

        var rows = !string.IsNullOrWhiteSpace(q)
            ? await _db.QueryAsync<EmailThreadRow>(sql, new { term = $"%{q.Trim().ToLower()}%", limit = Math.Clamp(limit, 1, 500) }, ct: ct)
            : await _db.QueryAsync<EmailThreadRow>(sql, new { limit = Math.Clamp(limit, 1, 500) }, ct: ct);

        var patientIds = rows.Where(r => r.PatientId.HasValue).Select(r => r.PatientId!.Value).Distinct().ToArray();
        var names = new Dictionary<Guid, string>();
        if (patientIds.Length > 0)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var patients = await _db.QueryAsync<Patient>(
                $"""SELECT "Id", "Name" FROM "Patients" WHERE {patientWhere} AND "Id" = ANY(@patientIds)""",
                new { patientIds },
                ct: ct);
            names = patients.ToDictionary(p => p.Id, p => p.Name);
        }

        return rows
            .GroupBy(r => r.PatientId!.Value)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.CreatedAt).First();
                return new EmailThreadDto(
                    g.Key,
                    names.GetValueOrDefault(g.Key, latest.ToEmail),
                    latest.ToEmail,
                    latest.Subject,
                    latest.Body.Length > 120 ? latest.Body[..120] : latest.Body,
                    latest.CreatedAt,
                    g.Count());
            })
            .OrderByDescending(t => t.LastAt)
            .ToList();
    }

    public async Task<EmailThreadDetailDto> GetThreadAsync(Guid patientId, CancellationToken ct = default)
    {
        var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var patient = await _db.QueryFirstOrDefaultAsync<Patient>(
            $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
            new { patientId },
            ct: ct) ?? throw new NotFoundException("Patient");

        var msgWhere = SqlFragments.WhereActive<EmailMessage>(ignoreTenant: false);
        var messages = await _db.QueryAsync<EmailMessage>(
            $"""SELECT * FROM "EmailMessages" WHERE "PatientId" = @patientId AND {msgWhere} ORDER BY "CreatedAt" ASC""",
            new { patientId },
            ct: ct);

        return new EmailThreadDetailDto(
            patientId,
            patient.Name,
            patient.Email ?? messages.FirstOrDefault()?.ToEmail ?? "",
            messages.Select(Map).ToList());
    }

    private static EmailMessageDto Map(EmailMessage m) => new(
        m.Id,
        m.ToEmail,
        m.Subject,
        m.Body,
        m.Direction.ToString().ToLowerInvariant(),
        m.Status.ToString().ToLowerInvariant(),
        m.CreatedAt,
        m.SentAt,
        m.ErrorMessage);

    private static SmtpClient BuildSmtpClient(EmailSettings settings)
    {
        var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl,
        };
        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
            client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPasswordEncrypted ?? "");
        return client;
    }

    private sealed class EmailThreadRow
    {
        public Guid? PatientId { get; set; }
        public string ToEmail { get; set; } = "";
        public string? Subject { get; set; }
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
