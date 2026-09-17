using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Application.Notifications;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CureFlow.Infrastructure.Services;

public class CampaignService : ICampaignService
{
    private sealed record AudienceContact(Guid? PatientId, Guid? LeadId, string Name, string Phone, string? Department = null);
    private readonly ICureFlowDbSession _db;
    private readonly IWhatsappMessagingService _messaging;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CampaignService> _logger;
    private readonly INotificationPublisher _notifications;

    public CampaignService(
        ICureFlowDbSession db,
        IWhatsappMessagingService messaging,
        ITenantContext tenant,
        ILogger<CampaignService> logger,
        INotificationPublisher notifications)
    {
        _db = db;
        _messaging = messaging;
        _tenant = tenant;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<Guid> CreateAsync(string name, string? description, string messageBody, object audience, DateTime? scheduledAt = null, CancellationToken ct = default)
    {
        var audienceJson = JsonSerializer.Serialize(audience);
        var patients = await ResolveAudienceAsync(audienceJson, ct);
        var c = new Campaign
        {
            Name = name,
            Description = description,
            MessageBody = messageBody,
            AudienceJson = audienceJson,
            TotalRecipients = patients.Count,
            CreatedByUserId = _tenant.UserId,
        };

        if (scheduledAt.HasValue)
        {
            c.ScheduledAt = DateTimeHelper.EnsureUtc(scheduledAt.Value);
            c.Status = CampaignStatus.Scheduled;
        }

        await _db.InsertAsync(c, ct: ct);
        return c.Id;
    }

    public async Task<IReadOnlyList<object>> ListAsync(CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Campaign>(ignoreTenant: false);
        var rows = await _db.QueryAsync<Campaign>(
            $"""SELECT * FROM "Campaigns" WHERE {where} ORDER BY "CreatedAt" DESC""",
            ct: ct);
        return rows.Cast<object>().ToList();
    }

    public async Task<object> GetAsync(Guid id, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Campaign>(ignoreTenant: false);
        var c = await _db.QueryFirstOrDefaultAsync<Campaign>(
            $"""SELECT * FROM "Campaigns" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Campaign");

        var recipientWhere = SqlFragments.WhereActive<CampaignRecipient>(ignoreTenant: false);
        var recipients = await _db.QueryAsync<CampaignRecipient>(
            $"""SELECT * FROM "CampaignRecipients" WHERE "CampaignId" = @id AND {recipientWhere}""",
            new { id },
            ct: ct);

        object? audience = null;
        if (!string.IsNullOrWhiteSpace(c.AudienceJson))
        {
            try { audience = JsonSerializer.Deserialize<object>(c.AudienceJson); }
            catch { /* ignore malformed audience JSON */ }
        }

        return new { campaign = c, audience, recipients };
    }

    public async Task<(int count, IReadOnlyList<object> sample)> PreviewAudienceAsync(object audience, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(audience);
        var patients = await ResolveAudienceAsync(json, ct);
        return (patients.Count, patients.Take(10).Cast<object>().ToList());
    }

    public async Task ScheduleAsync(Guid id, DateTime scheduledAtUtc, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Campaign>(ignoreTenant: false);
        var c = await _db.QueryFirstOrDefaultAsync<Campaign>(
            $"""SELECT * FROM "Campaigns" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Campaign");
        if (c.Status is CampaignStatus.Sending or CampaignStatus.Sent)
            throw new ValidationException("Cannot schedule a campaign that is already sent");

        c.ScheduledAt = DateTimeHelper.EnsureUtc(scheduledAtUtc);
        c.Status = CampaignStatus.Scheduled;
        await _db.UpdateAsync(c, ct: ct);
    }

    public async Task UpdateAsync(Guid id, UpdateCampaignRequest update, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Campaign>(ignoreTenant: false);
        var c = await _db.QueryFirstOrDefaultAsync<Campaign>(
            $"""SELECT * FROM "Campaigns" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Campaign");
        if (c.Status is CampaignStatus.Sending or CampaignStatus.Sent)
            throw new ValidationException("Cannot edit a campaign that is already sent");

        if (!string.IsNullOrWhiteSpace(update.Name)) c.Name = update.Name;
        if (update.Description != null) c.Description = update.Description;
        if (!string.IsNullOrWhiteSpace(update.MessageBody)) c.MessageBody = update.MessageBody;

        if (update.Audience != null)
        {
            var audienceJson = JsonSerializer.Serialize(update.Audience);
            var contacts = await ResolveAudienceAsync(audienceJson, ct);
            c.AudienceJson = audienceJson;
            c.TotalRecipients = contacts.Count;
        }
        if (update.ClearSchedule)
        {
            c.ScheduledAt = null;
            if (c.Status == CampaignStatus.Scheduled) c.Status = CampaignStatus.Draft;
        }
        else if (update.ScheduledAt.HasValue)
        {
            c.ScheduledAt = DateTimeHelper.EnsureUtc(update.ScheduledAt.Value);
            c.Status = CampaignStatus.Scheduled;
        }

        if (!string.IsNullOrWhiteSpace(update.Status))
        {
            if (!EnumParseHelper.TryParseSnakeCase<CampaignStatus>(update.Status, out var newStatus))
                throw new ValidationException("Invalid campaign status");

            if (c.Status is CampaignStatus.Sending or CampaignStatus.Sent)
                throw new ValidationException("Cannot change status of a sent campaign");

            switch (newStatus)
            {
                case CampaignStatus.Draft:
                    c.Status = CampaignStatus.Draft;
                    c.ScheduledAt = null;
                    break;
                case CampaignStatus.Scheduled:
                    c.ScheduledAt ??= DateTime.UtcNow.Date.AddDays(1).AddHours(9);
                    c.Status = CampaignStatus.Scheduled;
                    break;
                case CampaignStatus.Cancelled:
                    c.Status = CampaignStatus.Cancelled;
                    break;
                default:
                    throw new ValidationException("Status cannot be set manually");
            }
        }

        await _db.UpdateAsync(c, ct: ct);
    }

    public async Task<IReadOnlyList<SuggestedCampaignDraftDto>> GetSuggestedDraftsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(365);
        var where = SqlFragments.WhereActive<MarketingCalendarEvent>(ignoreTenant: false);
        var events = await _db.QueryAsync<MarketingCalendarEvent>(
            $"""
            SELECT * FROM "MarketingCalendarEvents"
            WHERE {where} AND "EventDate" >= @today AND "EventDate" <= @horizon
            ORDER BY "EventDate" ASC
            """,
            new { today, horizon },
            ct: ct);

        return events.Select(e => new SuggestedCampaignDraftDto(
            e.Id,
            e.Name,
            e.EventDate,
            e.Category,
            e.Region,
            e.Source,
            e.SuggestedMessage ?? $"Warm wishes from Cure & Care Hospital on {e.Name}!",
            $"{e.Name} — {e.EventDate:dd MMM yyyy}")).ToList();
    }

    public async Task<(int sent, int failed, int total)> SendAsync(Guid campaignId, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Campaign>(ignoreTenant: false);
        var c = await _db.QueryFirstOrDefaultAsync<Campaign>(
            $"""SELECT * FROM "Campaigns" WHERE "Id" = @campaignId AND {where}""",
            new { campaignId },
            ct: ct) ?? throw new NotFoundException("Campaign");
        if (c.Status is CampaignStatus.Sending or CampaignStatus.Sent)
            throw new ValidationException("Campaign already in progress");

        var patients = await ResolveAudienceAsync(c.AudienceJson, ct);
        // Only one sender may claim a scheduled campaign, including overlapping scheduler runs.
        var claimed = await _db.ExecuteAsync($"""
            UPDATE "Campaigns" SET "Status" = @sending, "UpdatedAt" = @now
            WHERE "Id" = @campaignId AND {where} AND "Status" IN (@draft, @scheduled)
            """, new { campaignId, sending = (int)CampaignStatus.Sending, draft = (int)CampaignStatus.Draft,
                scheduled = (int)CampaignStatus.Scheduled, now = DateTime.UtcNow }, ct: ct);
        if (claimed == 0) throw new ValidationException("Campaign is already being sent or has been cancelled.");
        c.Status = CampaignStatus.Sending;
        var placeholderValues = await BuildPlaceholderValuesAsync(ct);
        int sent = 0, failed = 0, attempted = 0;
        foreach (var contact in patients)
        {
            var patient = contact;
            if (contact.LeadId.HasValue)
            {
                // Recheck immediately before dispatch, including during a long campaign.
                var lead = await _db.GetByIdAsync<Lead>(contact.LeadId.Value, ct: ct);
                if (lead == null || !lead.IsActive || lead.ConvertedAt.HasValue || lead.Phone != contact.Phone)
                    continue;
            }
            var batchResults = await _messaging.SendCampaignBatchAsync(
                [new CampaignBatchSendItem(contact.Phone, RenderMessage(c.MessageBody, contact, placeholderValues), contact.PatientId)], ct);
            var result = batchResults.Single();
            attempted++;
            try
            {
                var body = patient != null ? RenderMessage(c.MessageBody, patient, placeholderValues) : "";
                var phone = WhatsappPhoneHelper.Normalize(result.Phone);

                var conv = await WhatsappConversationHelper.FindByPhoneAsync(_db, phone, ct);
                if (conv == null && patient != null)
                {
                    conv = new Conversation
                    {
                        WaPhone = phone,
                        Name = patient.Name,
                        PatientId = patient.PatientId,
                    };
                    await _db.InsertAsync(conv, ct: ct);
                }

                if (conv != null)
                {
                    var msg = new Message
                    {
                        ConversationId = conv.Id,
                        PatientId = patient?.PatientId,
                        Direction = MessageDirection.Outbound,
                        Body = body,
                        Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed,
                        WaMessageId = result.MessageId,
                        SenderUserId = _tenant.UserId,
                    };
                    await _db.InsertAsync(msg, ct: ct);
                    conv.LastMessageAt = msg.CreatedAt;
                    conv.LastMessagePreview = body.Length > 120 ? body[..120] : body;
                    await _db.UpdateAsync(conv, ct: ct);
                }

                var recipient = new CampaignRecipient
                {
                    CampaignId = campaignId,
                    PatientId = patient?.PatientId,
                    LeadId = patient?.LeadId,
                    PatientName = patient?.Name ?? "",
                    PatientPhone = phone,
                    Status = result.Success ? RecipientStatus.Sent : RecipientStatus.Failed,
                    WaMessageId = result.MessageId,
                    ErrorMessage = result.Error,
                    SentAt = DateTime.UtcNow,
                };
                await _db.InsertAsync(recipient, ct: ct);

                if (result.Success) sent++;
                else
                {
                    failed++;
                    _logger.LogWarning(
                        "Campaign {CampaignId} failed for patient {PatientId} ({Phone}): {Error}",
                        campaignId, result.PatientId, phone, result.Error);
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Campaign {CampaignId} exception for phone {Phone}.", campaignId, result.Phone);
                await _db.InsertAsync(new CampaignRecipient
                {
                    CampaignId = campaignId,
                    PatientId = patient?.PatientId,
                    LeadId = patient?.LeadId,
                    PatientName = patient?.Name ?? "",
                    PatientPhone = WhatsappPhoneHelper.Normalize(result.Phone),
                    Status = RecipientStatus.Failed,
                    ErrorMessage = ex.Message,
                    SentAt = DateTime.UtcNow,
                }, ct: ct);
            }
        }

        c.Status = CampaignStatus.Sent;
        c.SentAt = DateTime.UtcNow;
        c.SentCount = sent;
        c.FailedCount = failed;
        c.TotalRecipients = attempted;
        await _db.UpdateAsync(c, ct: ct);

        var severity = failed > 0 && sent == 0 ? NotificationSeverity.Warning : NotificationSeverity.Info;
        var type = failed > 0 && sent == 0 ? NotificationTypeCodes.CampaignFailed : NotificationTypeCodes.CampaignCompleted;
        await _notifications.PublishAsync(new NotificationPublishRequest(
            type,
            failed > 0 && sent == 0 ? "Campaign failed" : "Campaign completed",
            $"{c.Name}: {sent} sent, {failed} failed",
            severity,
            EntityType: "campaign",
            EntityId: c.Id,
            ActionUrl: $"/campaigns/{c.Id}",
            DedupeKey: $"campaign.complete:{c.Id}:{c.SentAt:O}",
            CreatorUserId: c.CreatedBy), ct);

        return (sent, failed, attempted);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Campaign>(ignoreTenant: false);
        var campaign = await _db.QueryFirstOrDefaultAsync<Campaign>(
            $"""SELECT * FROM "Campaigns" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Campaign");
        campaign.IsDeleted = true;
        await _db.UpdateAsync(campaign, ct: ct);
    }

    private async Task<Dictionary<string, string>> BuildPlaceholderValuesAsync(CancellationToken ct)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hospitalWhere = SqlFragments.WhereActive<HospitalProfile>(ignoreTenant: false);
        var hospital = await _db.QueryFirstOrDefaultAsync<HospitalProfile>(
            $"""SELECT * FROM "HospitalProfiles" WHERE {hospitalWhere} LIMIT 1""",
            ct: ct);
        if (hospital != null)
            values["hospital"] = hospital.Name ?? "Cure & Care Hospital";

        var placeholderWhere = SqlFragments.WhereActive<TemplatePlaceholder>(ignoreTenant: false);
        var custom = await _db.QueryAsync<TemplatePlaceholder>(
            $"""SELECT * FROM "TemplatePlaceholders" WHERE {placeholderWhere} AND NOT "IsSystem" AND "StaticValue" IS NOT NULL""",
            ct: ct);
        foreach (var p in custom)
            values[p.Key] = p.StaticValue ?? "";

        return values;
    }

    private static string RenderMessage(string template, AudienceContact p, IReadOnlyDictionary<string, string>? extra = null)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p.Name ?? "",
            ["department"] = p.Department ?? "",
            ["phone"] = p.Phone ?? "",
        };
        if (extra != null)
        {
            foreach (var (k, v) in extra)
                values[k] = v;
        }
        return MessageTemplateHelper.Render(template, values);
    }

    private static List<AudienceContact> DeduplicateAudienceByPhone(IEnumerable<Patient> patients)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<AudienceContact>();
        foreach (var patient in patients)
        {
            var phone = WhatsappPhoneHelper.Normalize(patient.Phone);
            if (string.IsNullOrWhiteSpace(phone) || !seen.Add(phone))
                continue;

            result.Add(new AudienceContact(patient.Id, null, patient.Name, phone, patient.Department));
        }

        return result;
    }

    private async Task<List<AudienceContact>> ResolveAudienceAsync(string audienceJson, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(audienceJson);
        var doc = document.RootElement;
        if (doc.TryGetProperty("source", out var source) && source.GetString() == "leads")
            return await ResolveLeadsAsync(doc, ct);
        var where = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var filters = new List<string> { @"NULLIF(BTRIM(""Phone""), '') IS NOT NULL" };
        var param = new Dictionary<string, object?>();

        if (doc.TryGetProperty("patient_ids", out var patientIds) && patientIds.ValueKind == JsonValueKind.Array)
        {
            var list = patientIds.EnumerateArray()
                .Select(t => Guid.TryParse(t.GetString(), out var id) ? (Guid?)id : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToArray();
            if (list.Length > 0)
            {
                filters.Add(@"""Id"" = ANY(@patientIds)");
                param["patientIds"] = list;

                var selectedSql = $"""
                    SELECT * FROM "Patients"
                    WHERE {where} AND {string.Join(" AND ", filters)}
                    ORDER BY "CreatedAt" DESC
                    """;
                var selectedRows = await _db.QueryAsync<Patient>(selectedSql, param, ct: ct);
                return DeduplicateAudienceByPhone(selectedRows);
            }
        }

        if (doc.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            var list = tags.EnumerateArray().Select(t => t.GetString()).Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
            if (list.Count > 0)
            {
                filters.Add("""
                    EXISTS (
                      SELECT 1 FROM jsonb_array_elements_text("Tags") elem
                      WHERE elem = ANY(@tags)
                    )
                    """);
                param["tags"] = list.ToArray();
            }
        }
        if (doc.TryGetProperty("departments", out var dep) && dep.ValueKind == JsonValueKind.Array)
        {
            var list = dep.EnumerateArray().Select(t => t.GetString()).Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
            if (list.Count > 0)
            {
                filters.Add(@"""Department"" = ANY(@departments)");
                param["departments"] = list.ToArray();
            }
        }
        if (doc.TryGetProperty("statuses", out var st) && st.ValueKind == JsonValueKind.Array)
        {
            var list = st.EnumerateArray()
                .Select(t => EnumParseHelper.TryParseSnakeCase<LeadStatus>(t.GetString(), out var e) ? (int?)e : null)
                .Where(e => e.HasValue).Select(e => e!.Value).ToArray();
            if (list.Length > 0)
            {
                filters.Add(@"""Status"" = ANY(@statuses)");
                param["statuses"] = list;
            }
        }
        if (doc.TryGetProperty("genders", out var genders) && genders.ValueKind == JsonValueKind.Array)
        {
            var list = genders.EnumerateArray()
                .Select(t => EnumParseHelper.TryParseSnakeCase<Gender>(t.GetString(), out var e) ? (int?)e : null)
                .Where(e => e.HasValue).Select(e => e!.Value).ToArray();
            if (list.Length > 0)
            {
                filters.Add(@"""Gender"" = ANY(@genders)");
                param["genders"] = list;
            }
        }
        if (doc.TryGetProperty("inactive_days", out var inDays) && inDays.ValueKind == JsonValueKind.Number)
        {
            var days = inDays.GetInt32();
            var threshold = DateTime.UtcNow.AddDays(-days);
            filters.Add(@"(""LastContactAt"" IS NULL OR ""LastContactAt"" < @threshold)");
            param["threshold"] = threshold;
        }

        var sql = $"""
            SELECT * FROM "Patients"
            WHERE {where} AND {string.Join(" AND ", filters)}
            ORDER BY "CreatedAt" DESC
            """;
        var rows = await _db.QueryAsync<Patient>(sql, param, ct: ct);
        return DeduplicateAudienceByPhone(rows);
    }

    private async Task<List<AudienceContact>> ResolveLeadsAsync(JsonElement audience, CancellationToken ct)
    {
        var filters = new List<string> { "\"ConvertedAt\" IS NULL", "\"IsActive\" = true" };
        var parameters = new Dictionary<string, object?>();
        if (audience.TryGetProperty("lead_ids", out var ids) && ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() > 0)
        {
            var selected = ids.EnumerateArray().Select(id => Guid.TryParse(id.GetString(), out var value) ? value : throw new ValidationException("Invalid lead selection.")).Distinct().ToArray();
            filters.Add("\"Id\" = ANY(@ids)");
            parameters["ids"] = selected;
        }
        else if (audience.TryGetProperty("genders", out var genders) && genders.ValueKind == JsonValueKind.Array && genders.GetArrayLength() > 0)
        {
            parameters["genders"] = genders.EnumerateArray().Select(g => EnumParseHelper.TryParseSnakeCase<Gender>(g.GetString(), out var value) ? (int)value : throw new ValidationException("Invalid gender.")).ToArray();
            filters.Add("\"Gender\" = ANY(@genders)");
        }
        var where = SqlFragments.WhereActive<Lead>(ignoreTenant: false);
        var leads = await _db.QueryAsync<Lead>($"""
            SELECT * FROM "Leads" WHERE {where} AND {string.Join(" AND ", filters)} ORDER BY "CreatedAt" DESC, "Id"
            """, parameters, ct: ct);
        return leads.Select(l => new AudienceContact(null, l.Id, l.Name ?? "there", WhatsappPhoneHelper.Normalize(l.Phone)))
            .Where(l => l.Phone.Length is >= 10 and <= 15).DistinctBy(l => l.Phone).ToList();
    }
}
