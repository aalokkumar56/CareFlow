namespace CureFlow.Application.Interfaces;

public interface IEmailService
{
    Task<IntegrationStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<(bool ok, Guid? messageId, string? error)> SendToPatientAsync(
        Guid patientId,
        string subject,
        string body,
        CancellationToken ct = default);
    Task<(bool ok, Guid? messageId, string? error)> SendAsync(
        string toEmail,
        string? subject,
        string body,
        Guid? patientId = null,
        CancellationToken ct = default);
}

public interface IEmailInboxService
{
    Task<IReadOnlyList<EmailThreadDto>> ListThreadsAsync(string? q, int limit = 100, CancellationToken ct = default);
    Task<EmailThreadDetailDto> GetThreadAsync(Guid patientId, CancellationToken ct = default);
}

public record EmailThreadDto(
    Guid PatientId,
    string PatientName,
    string ToEmail,
    string? LastSubject,
    string? LastPreview,
    DateTime? LastAt,
    int MessageCount);

public record EmailThreadDetailDto(
    Guid PatientId,
    string PatientName,
    string ToEmail,
    IReadOnlyList<EmailMessageDto> Messages);

public record EmailMessageDto(
    Guid Id,
    string ToEmail,
    string? Subject,
    string Body,
    string Direction,
    string Status,
    DateTime CreatedAt,
    DateTime? SentAt,
    string? ErrorMessage);
