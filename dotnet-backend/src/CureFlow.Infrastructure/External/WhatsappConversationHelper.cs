using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.External;

public static class WhatsappConversationHelper
{
    public static async Task<Conversation?> FindByPhoneAsync(
        ICureFlowDbSession db,
        string rawPhone,
        CancellationToken ct = default)
    {
        var phone = WhatsappPhoneHelper.Normalize(rawPhone);
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var match = await db.QueryFirstOrDefaultAsync<Conversation>(
            """
            SELECT * FROM "Conversations"
            WHERE "WaPhone" = @phone AND "IsDeleted" = false AND "TenantId" = @TenantId
            """,
            new { phone },
            ct: ct);
        if (match != null)
            return match;

        if (phone.Length < 10)
            return null;

        var suffix = phone[^10..];
        var legacyMatches = await db.QueryAsync<Conversation>(
            """
            SELECT * FROM "Conversations"
            WHERE "WaPhone" LIKE @suffixPattern AND "IsDeleted" = false AND "TenantId" = @TenantId
            """,
            new { suffixPattern = $"%{suffix}" },
            ct: ct);

        match = legacyMatches.FirstOrDefault(c => WhatsappPhoneHelper.Normalize(c.WaPhone) == phone);
        if (match != null && match.WaPhone != phone)
        {
            match.WaPhone = phone;
            await db.UpdateAsync(match, ct: ct);
        }

        return match;
    }

    public static async Task NormalizeAndMergeAsync(ICureFlowDbSession db, Guid defaultTenantId, CancellationToken ct = default)
    {
        var conversations = await db.QueryAsync<Conversation>(
            """SELECT * FROM "Conversations" WHERE "IsDeleted" = false""",
            ignoreTenant: true,
            ct: ct);

        foreach (var conversation in conversations)
        {
            if (conversation.TenantId == Guid.Empty)
                conversation.TenantId = defaultTenantId;

            var normalized = WhatsappPhoneHelper.Normalize(conversation.WaPhone);
            if (!string.IsNullOrWhiteSpace(normalized))
                conversation.WaPhone = normalized;
        }

        var groups = conversations
            .Where(c => !string.IsNullOrWhiteSpace(c.WaPhone))
            .GroupBy(c => (c.TenantId, c.WaPhone))
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(c => c.LastMessageAt).ToList();
            var primary = ordered[0];

            foreach (var duplicate in ordered.Skip(1))
            {
                var messages = await db.QueryAsync<Message>(
                    """
                    SELECT * FROM "Messages"
                    WHERE "ConversationId" = @conversationId AND "IsDeleted" = false
                    """,
                    new { conversationId = duplicate.Id },
                    ignoreTenant: true,
                    ct: ct);

                foreach (var message in messages)
                {
                    message.ConversationId = primary.Id;
                    if (message.TenantId == Guid.Empty)
                        message.TenantId = primary.TenantId;
                    await db.UpdateAsync(message, ignoreTenant: true, ct: ct);
                }

                primary.UnreadCount += duplicate.UnreadCount;
                if (duplicate.LastMessageAt > primary.LastMessageAt)
                {
                    primary.LastMessageAt = duplicate.LastMessageAt;
                    primary.LastMessagePreview = duplicate.LastMessagePreview;
                }

                primary.PatientId ??= duplicate.PatientId;
                if (string.IsNullOrWhiteSpace(primary.Name) || primary.Name == primary.WaPhone)
                    primary.Name = duplicate.Name;

                duplicate.IsDeleted = true;
                await db.UpdateAsync(duplicate, ignoreTenant: true, ct: ct);
            }

            await db.UpdateAsync(primary, ignoreTenant: true, ct: ct);
        }

        var orphanMessages = await db.QueryAsync<Message>(
            """SELECT * FROM "Messages" WHERE "TenantId" = @empty AND "IsDeleted" = false""",
            new { empty = Guid.Empty },
            ignoreTenant: true,
            ct: ct);

        foreach (var message in orphanMessages)
        {
            message.TenantId = defaultTenantId;
            await db.UpdateAsync(message, ignoreTenant: true, ct: ct);
        }

        foreach (var conversation in conversations.Where(c => c.TenantId != Guid.Empty || !string.IsNullOrWhiteSpace(c.WaPhone)))
            await db.UpdateAsync(conversation, ignoreTenant: true, ct: ct);
    }
}
