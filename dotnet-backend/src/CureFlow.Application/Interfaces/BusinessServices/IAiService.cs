using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IAiService
{
    Task<string> DraftReplyAsync(IEnumerable<(string direction, string body)> history, string? patientName, string? department, string? instruction, CancellationToken ct = default);
    Task<(string summary, string urgency, string category, string? suggestedFollowUp)> SummarizeAsync(IEnumerable<(string direction, string body)> history, string? patientName, CancellationToken ct = default);
    Task<(string urgency, string category)> ClassifyMessageAsync(string text, CancellationToken ct = default);
    Task<object> ExtractHospitalProfileFromHtmlAsync(string aggregatedText, CancellationToken ct = default);
}
