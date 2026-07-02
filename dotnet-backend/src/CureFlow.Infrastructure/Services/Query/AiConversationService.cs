using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Query;

public class AiConversationService : IAiConversationService
{
    private readonly ICureFlowDbSession _db;
    private readonly IAiService _ai;

    public AiConversationService(ICureFlowDbSession db, IAiService ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<object> DraftReplyAsync(Guid conversationId, string? instruction, CancellationToken ct = default)
    {
        var (history, patientName, department) = await LoadConversationContextAsync(conversationId, ct);
        var draft = await _ai.DraftReplyAsync(history, patientName, department, instruction, ct);
        return new { draft };
    }

    public async Task<object> SummarizeAsync(Guid conversationId, CancellationToken ct = default)
    {
        var (history, patientName, _) = await LoadConversationContextAsync(conversationId, ct);
        var (summary, urgency, category, suggestedFollowUp) = await _ai.SummarizeAsync(history, patientName, ct);
        return new
        {
            summary,
            urgency,
            category,
            suggested_follow_up = suggestedFollowUp
        };
    }

    private async Task<(IEnumerable<(string direction, string body)> history, string? patientName, string? department)> LoadConversationContextAsync(
        Guid conversationId, CancellationToken ct)
    {
        var convWhere = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var conv = await _db.QueryFirstOrDefaultAsync<Conversation>(
            $"""SELECT * FROM "Conversations" WHERE "Id" = @conversationId AND {convWhere}""",
            new { conversationId },
            ct: ct) ?? throw new NotFoundException("Conversation");

        var msgWhere = SqlFragments.WhereActive<Message>(ignoreTenant: false);
        var msgs = await _db.QueryAsync<Message>(
            $"""SELECT "Direction", "Body" FROM "Messages" WHERE "ConversationId" = @conversationId AND {msgWhere} ORDER BY "CreatedAt" ASC""",
            new { conversationId },
            ct: ct);

        Patient? patient = null;
        if (conv.PatientId.HasValue)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            patient = await _db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
                new { patientId = conv.PatientId.Value },
                ct: ct);
        }

        var history = msgs.Select(m => (
            m.Direction == MessageDirection.Inbound ? "inbound" : "outbound",
            m.Body));

        return (history, patient?.Name ?? conv.Name, patient?.Department);
    }
}
