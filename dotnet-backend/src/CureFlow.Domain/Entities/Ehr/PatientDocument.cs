using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Scanned/photographed clinical document (e.g. hardcopy doctor note) linked to a patient visit.</summary>
public class PatientDocument : TenantEntity
{
    public Guid PatientId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? AppointmentId { get; set; }
    public string DocumentType { get; set; } = "paper_note";
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public Guid? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
