using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.External;

/// <summary>
/// Shared inbound webhook processor for Meta Cloud, WhatsBiz, and WPBox relay payloads.
/// </summary>
public static class WhatsappWebhookProcessor
{
    public static async Task ProcessAsync(
        string body,
        string? signatureHeader,
        CancellationToken ct,
        IServiceProvider? services = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (string.IsNullOrWhiteSpace(body))
            return;

        using var scope = services.CreateScope();
        var tenantContext = (CurrentTenant)scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<ICureFlowDbSession>();
        var mediaStore = scope.ServiceProvider.GetRequiredService<IWhatsappMediaStore>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("WhatsappWebhookProcessor");

        var tenantId = await ResolveWebhookTenantIdAsync(db, ct);
        if (tenantId == Guid.Empty)
        {
            logger.LogWarning("Webhook received but no active tenant found; skipping.");
            return;
        }

        tenantContext.TenantId = tenantId;
        tenantContext.IsAuthenticated = true;
        tenantContext.UserEmail = "whatsapp@webhook";

        var settings = await scope.ServiceProvider.GetRequiredService<IWhatsAppSettingsService>().GetAsync(ct);
        if (!TryVerifyWebhookSignature(body, signatureHeader, settings.AppSecret, logger))
            return;

        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var ctx = new InboundContext(db, mediaStore, tenantId, publisher);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var processed = await TryProcessWhatsBizEventAsync(ctx, root, ct);
        if (!processed)
            processed = await TryProcessMetaPayloadAsync(ctx, root, ct);
        if (!processed)
            processed = await TryProcessMessagesArrayAsync(ctx, root, ct);
        if (!processed && root.TryGetProperty("data", out var data))
            processed = await TryProcessMessagesArrayAsync(ctx, data, ct);
        if (!processed)
            processed = await TryProcessFlatPayloadAsync(ctx, root, ct);
        if (!processed && root.TryGetProperty("payload", out var payloadEl))
            processed = await TryProcessFlatPayloadAsync(ctx, payloadEl, ct);

        if (!processed)
            logger.LogWarning("Webhook payload did not match a known format: {Preview}", Truncate(body));
        else
            logger.LogInformation("Webhook inbound event processed.");
    }

    /// <summary>
    /// Returns true when signature is valid, or when verification is skipped (no secret / no header).
    /// </summary>
    public static bool TryVerifyWebhookSignature(
        string body,
        string? signatureHeader,
        string? appSecret,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(appSecret))
            return true;

        if (string.IsNullOrEmpty(signatureHeader))
        {
            logger?.LogWarning(
                "WhatsApp AppSecret is configured but X-Hub-Signature-256 header is missing — processing webhook anyway. " +
                "Set the same App Secret in WhatsBiz Webhook Relay for strict verification.");
            return true;
        }

        if (!VerifyMetaSignature(body, signatureHeader, appSecret))
        {
            logger?.LogWarning("Invalid WhatsApp webhook signature — rejecting payload.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// WhatsBiz Webhook Relay format: { "event": "message_received", "from": "+91...", "text": { "body": "..." }, ... }
    /// Docs: https://whatsbizapi.com/docs/webhooks
    /// </summary>
    private static async Task<bool> TryProcessWhatsBizEventAsync(InboundContext ctx, JsonElement root, CancellationToken ct)
    {
        var eventName = ReadString(root, "event");
        if (string.IsNullOrWhiteSpace(eventName))
            return false;

        if (string.Equals(eventName, "message_received", StringComparison.OrdinalIgnoreCase))
        {
            var phone = ReadPhone(root);
            if (string.IsNullOrWhiteSpace(phone) && root.TryGetProperty("contact", out var contact))
                phone = ReadString(contact, "wa_id", "phone", "from");

            var text = ReadTextBody(root);
            var media = ReadMediaReference(root);
            var waMessageId = ReadMessageId(root);

            if (string.IsNullOrWhiteSpace(phone))
                return false;

            if (string.IsNullOrWhiteSpace(text) && media == null)
            {
                var typeLabel = ReadString(root, "type") ?? "message";
                text = $"[{typeLabel}]";
            }

            await SaveInboundMessageAsync(ctx, phone, text ?? string.Empty, media, waMessageId, ct);
            return true;
        }

        if (string.Equals(eventName, "message_status", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessSingleStatusAsync(ctx.Db, root, ct);
            return true;
        }

        return false;
    }

    private static async Task ProcessSingleStatusAsync(ICureFlowDbSession db, JsonElement statusItem, CancellationToken ct)
    {
        var waMessageId = ReadMessageId(statusItem);
        if (string.IsNullOrWhiteSpace(waMessageId)) return;
        if (!statusItem.TryGetProperty("status", out var statusEl)) return;

        var msgWhere = SqlFragments.WhereActive<Message>(ignoreTenant: false);
        var message = await db.QueryFirstOrDefaultAsync<Message>(
            $"""SELECT * FROM "Messages" WHERE "WaMessageId" = @waMessageId AND {msgWhere} LIMIT 1""",
            new { waMessageId },
            ct: ct);
        if (message == null) return;

        var status = (statusEl.GetString() ?? "").ToLowerInvariant();
        message.Status = status switch
        {
            "sent" => MessageStatus.Sent,
            "delivered" => MessageStatus.Delivered,
            "read" => MessageStatus.Read,
            "failed" => MessageStatus.Failed,
            _ => message.Status
        };
        await db.UpdateAsync(message, ct: ct);
    }

    public static bool VerifyMetaSignature(string body, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256=")) return false;
        var sigHex = signatureHeader["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sigHex.ToLowerInvariant()));
    }

    private static async Task<Guid> ResolveWebhookTenantIdAsync(ICureFlowDbSession db, CancellationToken ct)
    {
        var tenantId = await db.QueryFirstOrDefaultAsync<Guid?>(
            """
            SELECT "Id" FROM "Tenants"
            WHERE "IsActive" = true AND "IsDeleted" = false
            ORDER BY "CreatedAt" ASC
            LIMIT 1
            """,
            ignoreTenant: true,
            ct: ct);
        return tenantId ?? Guid.Empty;
    }

    private static async Task<bool> TryProcessMetaPayloadAsync(InboundContext ctx, JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return false;

        var processed = false;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) continue;
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)) continue;

                if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                {
                    await ProcessIncomingMessagesAsync(ctx, messages, ct);
                    processed = true;
                }

                if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                {
                    await ProcessStatusesAsync(ctx.Db, statuses, ct);
                    processed = true;
                }
            }
        }

        return processed;
    }

    private static async Task<bool> TryProcessMessagesArrayAsync(InboundContext ctx, JsonElement root, CancellationToken ct)
    {
        if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
        {
            await ProcessIncomingMessagesAsync(ctx, messages, ct);
            return true;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            await ProcessIncomingMessagesAsync(ctx, root, ct);
            return true;
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            var processed = false;
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("messages", out var nestedMessages) && nestedMessages.ValueKind == JsonValueKind.Array)
                {
                    await ProcessIncomingMessagesAsync(ctx, nestedMessages, ct);
                    processed = true;
                }
                if (item.TryGetProperty("statuses", out var nestedStatuses) && nestedStatuses.ValueKind == JsonValueKind.Array)
                {
                    await ProcessStatusesAsync(ctx.Db, nestedStatuses, ct);
                    processed = true;
                }
            }
            return processed;
        }

        return false;
    }

    private static async Task<bool> TryProcessFlatPayloadAsync(InboundContext ctx, JsonElement root, CancellationToken ct)
    {
        if (root.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
        {
            await ProcessStatusesAsync(ctx.Db, statuses, ct);
            return true;
        }

        var phone = ReadPhone(root);
        var text = ReadTextBody(root);
        var media = ReadMediaReference(root);
        if (string.IsNullOrWhiteSpace(phone) || (string.IsNullOrWhiteSpace(text) && media == null))
        {
            if (root.TryGetProperty("last_message_of_user", out var lastUserEl))
            {
                phone = ReadPhone(root) ?? ReadString(root, "phone_number", "phoneNumber", "phone");
                text = lastUserEl.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(phone) || (string.IsNullOrWhiteSpace(text) && media == null))
            return false;

        if (IsFromBusiness(root))
            return false;

        await SaveInboundMessageAsync(ctx, phone, text ?? string.Empty, media, ReadMessageId(root), ct);
        return true;
    }

    private static async Task ProcessIncomingMessagesAsync(InboundContext ctx, JsonElement messages, CancellationToken ct)
    {
        foreach (var message in messages.EnumerateArray())
        {
            if (IsFromBusiness(message))
                continue;

            var phone = ReadPhone(message);
            if (string.IsNullOrWhiteSpace(phone)) continue;

            var waMessageId = ReadMessageId(message);
            if (!string.IsNullOrWhiteSpace(waMessageId))
            {
                var msgWhere = SqlFragments.WhereActive<Message>(ignoreTenant: false);
                var exists = await ctx.Db.QueryFirstOrDefaultAsync<Guid?>(
                    $"""SELECT "Id" FROM "Messages" WHERE "WaMessageId" = @waMessageId AND {msgWhere} LIMIT 1""",
                    new { waMessageId },
                    ct: ct);
                if (exists.HasValue) continue;
            }

            var media = ReadMediaReference(message);
            var textBody = ReadTextBody(message);
            if (string.IsNullOrWhiteSpace(textBody))
            {
                if (media?.Caption is { Length: > 0 })
                    textBody = media.Caption;
                else if (message.TryGetProperty("type", out var typeEl))
                    textBody = $"[{typeEl.GetString() ?? "message"}]";
            }

            await SaveInboundMessageAsync(ctx, phone, textBody, media, waMessageId, ct);
        }
    }

    private static async Task SaveInboundMessageAsync(
        InboundContext ctx,
        string rawPhone,
        string textBody,
        InboundMediaReference? media,
        string? waMessageId,
        CancellationToken ct)
    {
        var db = ctx.Db;
        var phone = WhatsappPhoneHelper.Normalize(rawPhone);
        if (string.IsNullOrWhiteSpace(phone)) return;

        textBody ??= string.Empty;

        var conversation = await WhatsappConversationHelper.FindByPhoneAsync(db, phone, ct);
        if (conversation == null)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var patient = await db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Phone" = @phone AND {patientWhere} ORDER BY "CreatedAt" ASC LIMIT 1""",
                new { phone },
                ct: ct);

            conversation = new Conversation
            {
                WaPhone = phone,
                Name = patient?.Name ?? phone,
                PatientId = patient?.Id,
                Category = ConversationCategory.General,
                Priority = Priority.Medium
            };
            await db.InsertAsync(conversation, ct: ct);
        }
        else
        {
            await LinkPatientIfNeededAsync(db, conversation, phone, ct);
        }

        var messageId = Guid.NewGuid();
        var inbound = new Message
        {
            Id = messageId,
            ConversationId = conversation.Id,
            PatientId = conversation.PatientId,
            WaMessageId = waMessageId,
            Direction = MessageDirection.Inbound,
            Type = "text",
            Body = textBody,
            Status = MessageStatus.Received
        };

        if (media != null)
        {
            inbound.Type = media.Type;
            inbound.Caption = string.IsNullOrWhiteSpace(media.Caption) ? null : media.Caption;

            var stored = await ctx.MediaStore.IngestInboundAsync(ctx.TenantId, messageId, media, ct);
            if (stored != null)
            {
                inbound.Type = WhatsappMediaPolicy.ToTypeString(stored.Kind);
                inbound.MediaUrl = $"/api/conversations/messages/{messageId}/media";
                inbound.MimeType = stored.ContentType;
                inbound.FileName = stored.FileName;
                inbound.MediaSize = stored.Size;
                inbound.MediaStoredPath = stored.RelativePath;
            }
            else if (!string.IsNullOrWhiteSpace(media.Url) &&
                     (media.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                      || media.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                // Could not download/host locally — fall back to the original remote URL.
                inbound.MediaUrl = media.Url;
                inbound.MimeType = media.MimeType;
                inbound.FileName = media.FileName;
            }
        }

        await db.InsertAsync(inbound, ct: ct);

        var preview = !string.IsNullOrWhiteSpace(textBody)
            ? textBody
            : $"[{inbound.Type}]";

        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.LastMessagePreview = preview.Length > 120 ? preview[..120] : preview;
        conversation.UnreadCount += 1;
        conversation.AwaitingReplySince = DateTime.UtcNow;
        await db.UpdateAsync(conversation, ct: ct);

        if (conversation.PatientId.HasValue)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var patient = await db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Id" = @id AND {patientWhere}""",
                new { id = conversation.PatientId.Value },
                ct: ct);
            if (patient != null)
            {
                patient.LastContactAt = DateTime.UtcNow;
                await db.UpdateAsync(patient, ct: ct);
            }
        }

        if (ctx.Publisher != null)
        {
            var contactName = conversation.Name ?? phone;
            await ctx.Publisher.PublishAsync(new NotificationPublishRequest(
                NotificationTypeCodes.WhatsappInboundMessage,
                "New WhatsApp message",
                $"New message from {contactName}",
                Domain.Enums.NotificationSeverity.Info,
                EntityType: "conversation",
                EntityId: conversation.Id,
                ActionUrl: $"/inbox?c={conversation.Id}",
                DedupeKey: $"whatsapp.inbound:{conversation.Id}:{messageId}",
                AssignedStaffId: conversation.AssignedStaffId), ct);
        }
    }

    private static async Task LinkPatientIfNeededAsync(ICureFlowDbSession db, Conversation conversation, string phone, CancellationToken ct)
    {
        if (conversation.PatientId.HasValue && conversation.Name != phone && conversation.Name != conversation.WaPhone)
            return;

        var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var patient = await db.QueryFirstOrDefaultAsync<Patient>(
            $"""SELECT * FROM "Patients" WHERE "Phone" = @phone AND {patientWhere} ORDER BY "CreatedAt" ASC LIMIT 1""",
            new { phone },
            ct: ct);

        if (patient == null) return;

        conversation.PatientId ??= patient.Id;
        if (string.IsNullOrWhiteSpace(conversation.Name) || conversation.Name == phone || conversation.Name == conversation.WaPhone)
            conversation.Name = patient.Name;
        await db.UpdateAsync(conversation, ct: ct);
    }

    private static async Task ProcessStatusesAsync(ICureFlowDbSession db, JsonElement statuses, CancellationToken ct)
    {
        foreach (var statusItem in statuses.EnumerateArray())
        {
            var waMessageId = ReadMessageId(statusItem);
            if (string.IsNullOrWhiteSpace(waMessageId)) continue;

            var msgWhere = SqlFragments.WhereActive<Message>(ignoreTenant: false);
            var message = await db.QueryFirstOrDefaultAsync<Message>(
                $"""SELECT * FROM "Messages" WHERE "WaMessageId" = @waMessageId AND {msgWhere} LIMIT 1""",
                new { waMessageId },
                ct: ct);
            if (message == null) continue;

            if (!statusItem.TryGetProperty("status", out var statusEl)) continue;
            var status = (statusEl.GetString() ?? "").ToLowerInvariant();
            message.Status = status switch
            {
                "sent" => MessageStatus.Sent,
                "delivered" => MessageStatus.Delivered,
                "read" => MessageStatus.Read,
                "failed" => MessageStatus.Failed,
                _ => message.Status
            };
            await db.UpdateAsync(message, ct: ct);
        }
    }

    private static bool IsFromBusiness(JsonElement element)
    {
        if (element.TryGetProperty("from_me", out var fromMe)
            && (fromMe.ValueKind == JsonValueKind.True
                || (fromMe.ValueKind == JsonValueKind.Number && fromMe.GetInt32() == 1)
                || (fromMe.ValueKind == JsonValueKind.String && fromMe.GetString() is "1" or "true")))
            return true;
        if (element.TryGetProperty("fromMe", out var fromMeCamel)
            && (fromMeCamel.ValueKind == JsonValueKind.True
                || (fromMeCamel.ValueKind == JsonValueKind.Number && fromMeCamel.GetInt32() == 1)
                || (fromMeCamel.ValueKind == JsonValueKind.String && fromMeCamel.GetString() is "1" or "true")))
            return true;
        if (element.TryGetProperty("type", out var typeEl)
            && string.Equals(typeEl.GetString(), "bot", StringComparison.OrdinalIgnoreCase))
            return true;
        if (element.TryGetProperty("direction", out var direction))
        {
            var dir = direction.GetString()?.ToLowerInvariant();
            if (dir is "outbound" or "outgoing" or "sent")
                return true;
        }
        return false;
    }

    private static string? ReadPhone(JsonElement element)
    {
        var phone = ReadString(element, "from", "phone", "phone_number", "phoneNumber", "sender", "wa_phone", "waPhone", "recipient", "wa_id");
        if (!string.IsNullOrWhiteSpace(phone))
            return phone;

        if (element.TryGetProperty("chat_id", out var chatIdEl))
        {
            var chatId = chatIdEl.GetString() ?? string.Empty;
            var atIndex = chatId.IndexOf('@');
            if (atIndex > 0)
                return chatId[..atIndex];
        }

        return null;
    }

    private static string ReadTextBody(JsonElement element)
    {
        if (element.TryGetProperty("text", out var textEl))
        {
            if (textEl.ValueKind == JsonValueKind.Object && textEl.TryGetProperty("body", out var bodyEl))
                return bodyEl.GetString() ?? string.Empty;
            if (textEl.ValueKind == JsonValueKind.String)
                return textEl.GetString() ?? string.Empty;
        }

        if (element.TryGetProperty("interactive", out var interactive))
        {
            if (interactive.TryGetProperty("button_reply", out var buttonReply)
                && buttonReply.TryGetProperty("title", out var buttonTitle))
                return buttonTitle.GetString() ?? string.Empty;
            if (interactive.TryGetProperty("list_reply", out var listReply)
                && listReply.TryGetProperty("title", out var listTitle))
                return listTitle.GetString() ?? string.Empty;
        }

        return ReadString(element, "message", "body", "content", "last_message_of_user", "lastMessageOfUser") ?? string.Empty;
    }

    private static string? ReadMessageId(JsonElement element)
        => ReadString(element, "id", "message_id", "messageId", "wamid", "wa_message_id", "waMessageId");

    /// <summary>
    /// Extracts a media reference from a webhook message element. Handles both Meta-style typed
    /// nodes (e.g. <c>image: { id, mime_type, caption }</c>) and flat <c>media_url</c>/<c>link</c> payloads.
    /// </summary>
    private static InboundMediaReference? ReadMediaReference(JsonElement message)
    {
        var rawType = ReadString(message, "type");
        var kind = WhatsappMediaPolicy.ClassifyByType(rawType);

        // The typed node key (image/video/audio/document/sticker).
        string? typedKey = rawType?.Trim().ToLowerInvariant();
        if (kind == null && typedKey is not ("image" or "video" or "audio" or "document" or "sticker" or "voice"))
            typedKey = null;

        string? mediaId = null;
        string? mimeType = null;
        string? fileName = null;
        string? caption = null;
        string? url = ReadString(message, "media_url", "mediaUrl", "url", "link");

        if (typedKey != null && message.TryGetProperty(typedKey, out var typedNode) && typedNode.ValueKind == JsonValueKind.Object)
        {
            mediaId = ReadString(typedNode, "id", "media_id", "mediaId");
            mimeType = ReadString(typedNode, "mime_type", "mimeType");
            fileName = ReadString(typedNode, "filename", "file_name", "fileName");
            caption = ReadString(typedNode, "caption");
            url ??= ReadString(typedNode, "link", "url", "media_url", "mediaUrl");
        }

        // A "media" wrapper (used by the normalized relay payload).
        if (message.TryGetProperty("media", out var mediaNode) && mediaNode.ValueKind == JsonValueKind.Object)
        {
            mediaId ??= ReadString(mediaNode, "id", "media_id", "mediaId");
            mimeType ??= ReadString(mediaNode, "mime_type", "mimeType");
            fileName ??= ReadString(mediaNode, "filename", "file_name", "fileName");
            url ??= ReadString(mediaNode, "hosted_url", "url", "link", "media_url", "mediaUrl");
            kind ??= WhatsappMediaPolicy.ClassifyByType(ReadString(mediaNode, "type"));
        }

        if (string.IsNullOrWhiteSpace(mediaId) && string.IsNullOrWhiteSpace(url))
            return null;

        var typeString = kind != null
            ? WhatsappMediaPolicy.ToTypeString(kind.Value)
            : (typedKey ?? "document");

        return new InboundMediaReference(typeString, mediaId, url, mimeType, fileName, caption);
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        return null;
    }

    private static string Truncate(string value, int max = 300)
        => value.Length <= max ? value : value[..max] + "...";

    private sealed record InboundContext(
        ICureFlowDbSession Db,
        IWhatsappMediaStore MediaStore,
        Guid TenantId,
        INotificationPublisher? Publisher);
}
