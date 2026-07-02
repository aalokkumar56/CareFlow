using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IAiConversationService
{
    Task<object> DraftReplyAsync(Guid conversationId, string? instruction, CancellationToken ct = default);
    Task<object> SummarizeAsync(Guid conversationId, CancellationToken ct = default);
}
