using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IConversationService
{
    Task<PagedResult<object>> ListAsync(string? q, int page, int pageSize, CancellationToken ct = default);
    Task<object> GetAsync(Guid id, CancellationToken ct = default);
    Task<object> GetOrCreateByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<Guid> SendMessageAsync(Guid conversationId, string body, CancellationToken ct = default);
    Task<Guid> SendMediaAsync(Guid conversationId, byte[] content, string fileName, string contentType, string? caption, CancellationToken ct = default);
    Task<MessageMediaFile?> GetMessageMediaAsync(Guid messageId, CancellationToken ct = default);
    Task SendToPatientAsync(Guid patientId, string body, CancellationToken ct = default);
    Task AssignAsync(Guid conversationId, Guid staffId, CancellationToken ct = default);
    Task<Guid> AddNoteAsync(Guid conversationId, string body, CancellationToken ct = default);
}

/// <summary>Resolved media file for download, scoped to the current tenant.</summary>
public sealed record MessageMediaFile(string AbsolutePath, string ContentType, string? FileName);
