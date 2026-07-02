using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.Services.CRM;

public class ConversationService : IConversationService
{
    private readonly ICureFlowDbSession _db;
    private readonly IWhatsappService _wa;
    private readonly IWhatsappProvider _provider;
    private readonly IWhatsappMediaStore _mediaStore;
    private readonly WhatsappMediaOptions _mediaOptions;
    private readonly ITenantContext _tenant;

    public ConversationService(
        ICureFlowDbSession db,
        IWhatsappService wa,
        IWhatsappProvider provider,
        IWhatsappMediaStore mediaStore,
        IOptions<WhatsappMediaOptions> mediaOptions,
        ITenantContext tenant)
    {
        _db = db;
        _wa = wa;
        _provider = provider;
        _mediaStore = mediaStore;
        _mediaOptions = mediaOptions.Value;
        _tenant = tenant;
    }

    public async Task<PagedResult<object>> ListAsync(string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize, skip) = Pagination.Normalize(page, pageSize);
        var where = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var filterSql = "";
        if (!string.IsNullOrWhiteSpace(q))
        {
            filterSql = """
                 AND (LOWER(COALESCE("Name", '')) LIKE @filter
                      OR LOWER("WaPhone") LIKE @filter
                      OR LOWER(COALESCE("LastMessagePreview", '')) LIKE @filter)
                """;
        }

        var countSql = $"""SELECT COUNT(*)::int FROM "Conversations" WHERE {where}{filterSql}""";
        var listSql = $"""
            SELECT * FROM "Conversations"
            WHERE {where}{filterSql}
            ORDER BY "LastMessageAt" DESC
            OFFSET @skip LIMIT @take
            """;

        int total;
        IReadOnlyList<Conversation> convs;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var filter = $"%{q.Trim().ToLower()}%";
            var paging = new { filter, skip, take = normalizedPageSize };
            total = await _db.QuerySingleAsync<int>(countSql, paging, ct: ct);
            convs = await _db.QueryAsync<Conversation>(listSql, paging, ct: ct);
        }
        else
        {
            var paging = new { skip, take = normalizedPageSize };
            total = await _db.QuerySingleAsync<int>(countSql, paging, ct: ct);
            convs = await _db.QueryAsync<Conversation>(listSql, paging, ct: ct);
        }

        var phones = convs.Select(c => c.WaPhone).Distinct().ToArray();
        var patientNames = new Dictionary<string, string>();
        if (phones.Length > 0)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var patients = await _db.QueryAsync<Patient>(
                $"""SELECT "Phone", "Name", "CreatedAt" FROM "Patients" WHERE {patientWhere} AND "Phone" = ANY(@phones)""",
                new { phones },
                ct: ct);
            patientNames = patients
                .GroupBy(p => p.Phone)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.CreatedAt).First().Name);
        }

        var items = convs.Select(c =>
        {
            var displayName = ResolveDisplayName(c, patientNames);
            return (object)new
            {
                c.Id,
                c.WaPhone,
                c.Name,
                display_name = displayName,
                c.LastMessagePreview,
                c.LastMessageAt,
                c.UnreadCount,
                c.Priority,
                c.Category,
                c.Escalated,
                c.PatientId,
                c.AssignedStaffId,
                c.AwaitingReplySince,
                c.CreatedAt,
                c.UpdatedAt
            };
        }).ToList();

        return new PagedResult<object>
        {
            Items = items,
            Total = total,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
        };
    }

    private static string ResolveDisplayName(Conversation c, IReadOnlyDictionary<string, string> patientNames)
    {
        if (!string.IsNullOrWhiteSpace(c.Name) && c.Name != c.WaPhone && !c.Name.All(char.IsDigit))
            return c.Name;
        if (patientNames.TryGetValue(c.WaPhone, out var patientName))
            return patientName;
        return c.Name ?? c.WaPhone;
    }

    public async Task SendToPatientAsync(Guid patientId, string body, CancellationToken ct = default)
    {
        var conv = await EnsureConversationForPatientAsync(patientId, ct);
        await SendMessageAsync(conv.Id, body, ct);
    }

    private async Task<Conversation> EnsureConversationForPatientAsync(Guid patientId, CancellationToken ct)
    {
        var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var patient = await _db.QueryFirstOrDefaultAsync<Patient>(
            $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
            new { patientId },
            ct: ct) ?? throw new NotFoundException("Patient");

        var phone = WhatsappPhoneHelper.Normalize(patient.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            throw new ValidationException("Patient phone number is required for WhatsApp chat");

        var conv = await WhatsappConversationHelper.FindByPhoneAsync(_db, phone, ct);
        if (conv == null)
        {
            conv = new Conversation
            {
                WaPhone = phone,
                Name = patient.Name,
                PatientId = patient.Id,
                Category = ConversationCategory.General,
                Priority = Priority.Medium
            };
            await _db.InsertAsync(conv, ct: ct);
        }
        else
        {
            conv.PatientId ??= patient.Id;
            if (string.IsNullOrWhiteSpace(conv.Name) || conv.Name == conv.WaPhone)
                conv.Name = patient.Name;
            await _db.UpdateAsync(conv, ct: ct);
        }

        return conv;
    }

    public async Task<object> GetOrCreateByPatientIdAsync(Guid patientId, CancellationToken ct = default)
    {
        var conv = await EnsureConversationForPatientAsync(patientId, ct);
        return await GetAsync(conv.Id, ct);
    }

    public async Task<object> GetAsync(Guid id, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var c = await _db.QueryFirstOrDefaultAsync<Conversation>(
            $"""SELECT * FROM "Conversations" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Conversation");

        var msgWhere = SqlFragments.WhereActive<Message>(ignoreTenant: false);
        var msgs = await _db.QueryAsync<Message>(
            $"""SELECT * FROM "Messages" WHERE "ConversationId" = @id AND {msgWhere} ORDER BY "CreatedAt" ASC""",
            new { id },
            ct: ct);
        var noteWhere = SqlFragments.WhereActive<InternalNote>(ignoreTenant: false);
        var notes = await _db.QueryAsync<InternalNote>(
            $"""SELECT * FROM "InternalNotes" WHERE "ConversationId" = @id AND {noteWhere} ORDER BY "CreatedAt" ASC""",
            new { id },
            ct: ct);

        Patient? patient = null;
        if (c.PatientId.HasValue)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            patient = await _db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
                new { patientId = c.PatientId.Value },
                ct: ct);
        }

        c.UnreadCount = 0;
        await _db.UpdateAsync(c, ct: ct);
        return new { conversation = c, messages = msgs, notes, patient };
    }

    public async Task<Guid> SendMessageAsync(Guid conversationId, string body, CancellationToken ct = default)
    {
        var convWhere = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var conv = await _db.QueryFirstOrDefaultAsync<Conversation>(
            $"""SELECT * FROM "Conversations" WHERE "Id" = @conversationId AND {convWhere}""",
            new { conversationId },
            ct: ct) ?? throw new NotFoundException("Conversation");

        var apiPhone = WhatsappPhoneHelper.ForApi(conv.WaPhone);
        if (string.IsNullOrWhiteSpace(apiPhone))
            throw new ValidationException("Conversation phone number is invalid for WhatsApp");

        var msg = new Message
        {
            ConversationId = conversationId,
            PatientId = conv.PatientId,
            Direction = MessageDirection.Outbound,
            Body = body,
            Status = MessageStatus.Pending,
            SenderUserId = _tenant.UserId,
        };
        await _db.InsertAsync(msg, ct: ct);

        conv.LastMessageAt = msg.CreatedAt;
        conv.LastMessagePreview = body.Length > 120 ? body[..120] : body;
        conv.AwaitingReplySince = null;
        conv.Escalated = false;
        await _db.UpdateAsync(conv, ct: ct);

        bool ok;
        string? msgId;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            (ok, msgId, _) = await _wa.SendTextAsync(apiPhone, body, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            ok = false;
            msgId = null;
        }

        msg.Status = ok ? MessageStatus.Sent : MessageStatus.Failed;
        msg.WaMessageId = msgId;

        if (conv.PatientId.HasValue)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var p = await _db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Id" = @id AND {patientWhere}""",
                new { id = conv.PatientId.Value },
                ct: ct);
            if (p != null)
            {
                p.LastContactAt = msg.CreatedAt;
                await _db.UpdateAsync(p, ct: ct);
            }
        }

        await _db.UpdateAsync(msg, ct: ct);
        return msg.Id;
    }

    public async Task<Guid> SendMediaAsync(
        Guid conversationId,
        byte[] content,
        string fileName,
        string contentType,
        string? caption,
        CancellationToken ct = default)
    {
        var validation = WhatsappMediaPolicy.Validate(contentType, content?.LongLength ?? 0, _mediaOptions);
        if (!validation.IsValid)
            throw new ValidationException(validation.Error ?? "Invalid media file.");

        var convWhere = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var conv = await _db.QueryFirstOrDefaultAsync<Conversation>(
            $"""SELECT * FROM "Conversations" WHERE "Id" = @conversationId AND {convWhere}""",
            new { conversationId },
            ct: ct) ?? throw new NotFoundException("Conversation");

        var apiPhone = WhatsappPhoneHelper.ForApi(conv.WaPhone);
        if (string.IsNullOrWhiteSpace(apiPhone))
            throw new ValidationException("Conversation phone number is invalid for WhatsApp");

        var mediaKind = validation.Kind;
        var mediaType = WhatsappMediaPolicy.ToTypeString(mediaKind);
        var trimmedCaption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();

        var messageId = Guid.NewGuid();
        var stored = await _mediaStore.SaveOutboundAsync(_tenant.TenantId, messageId, content!, fileName, contentType, ct);

        var preview = trimmedCaption ?? $"[{mediaType}] {stored.FileName}";

        var msg = new Message
        {
            Id = messageId,
            ConversationId = conversationId,
            PatientId = conv.PatientId,
            Direction = MessageDirection.Outbound,
            Type = mediaType,
            Body = trimmedCaption ?? string.Empty,
            Caption = trimmedCaption,
            MediaUrl = $"/api/conversations/messages/{messageId}/media",
            MimeType = stored.ContentType,
            FileName = stored.FileName,
            MediaSize = stored.Size,
            MediaStoredPath = stored.RelativePath,
            Status = MessageStatus.Pending,
            SenderUserId = _tenant.UserId,
        };
        await _db.InsertAsync(msg, ct: ct);

        conv.LastMessageAt = msg.CreatedAt;
        conv.LastMessagePreview = preview.Length > 120 ? preview[..120] : preview;
        conv.AwaitingReplySince = null;
        conv.Escalated = false;
        await _db.UpdateAsync(conv, ct: ct);

        bool ok;
        string? msgId;
        string? error;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var result = await _provider.SendMediaFileAsync(
                apiPhone, mediaType, content!, stored.FileName, stored.ContentType, trimmedCaption, timeout.Token);
            ok = result.Success;
            msgId = result.MessageId;
            error = result.Error;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            ok = false;
            msgId = null;
            error = "Sending the attachment timed out.";
        }

        msg.Status = ok ? MessageStatus.Sent : MessageStatus.Failed;
        msg.WaMessageId = msgId;
        await _db.UpdateAsync(msg, ct: ct);

        if (conv.PatientId.HasValue)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var p = await _db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Id" = @id AND {patientWhere}""",
                new { id = conv.PatientId.Value },
                ct: ct);
            if (p != null)
            {
                p.LastContactAt = msg.CreatedAt;
                await _db.UpdateAsync(p, ct: ct);
            }
        }

        if (!ok)
            throw new DomainException(error ?? "The attachment was saved but could not be delivered via WhatsApp.", 400);

        return msg.Id;
    }

    public async Task<MessageMediaFile?> GetMessageMediaAsync(Guid messageId, CancellationToken ct = default)
    {
        var msg = await _db.GetByIdAsync<Message>(messageId, ct: ct);
        if (msg == null)
            return null;

        var absolutePath = _mediaStore.ResolveExistingPath(msg.MediaStoredPath);
        if (absolutePath == null)
            return null;

        var contentType = string.IsNullOrWhiteSpace(msg.MimeType) ? "application/octet-stream" : msg.MimeType!;
        return new MessageMediaFile(absolutePath, contentType, msg.FileName);
    }

    public async Task AssignAsync(Guid conversationId, Guid staffId, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var c = await _db.QueryFirstOrDefaultAsync<Conversation>(
            $"""SELECT * FROM "Conversations" WHERE "Id" = @conversationId AND {where}""",
            new { conversationId },
            ct: ct) ?? throw new NotFoundException("Conversation");
        c.AssignedStaffId = staffId;
        await _db.UpdateAsync(c, ct: ct);
    }

    public async Task<Guid> AddNoteAsync(Guid conversationId, string body, CancellationToken ct = default)
    {
        var userWhere = SqlFragments.WhereActive<User>(ignoreTenant: false);
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            $"""SELECT * FROM "Users" WHERE "Id" = @userId AND {userWhere}""",
            new { userId = _tenant.UserId },
            ct: ct);
        var n = new InternalNote
        {
            ConversationId = conversationId,
            AuthorId = _tenant.UserId ?? Guid.Empty,
            AuthorName = user?.Name ?? _tenant.UserEmail ?? "User",
            Body = body,
        };
        await _db.InsertAsync(n, ct: ct);
        return n.Id;
    }
}
