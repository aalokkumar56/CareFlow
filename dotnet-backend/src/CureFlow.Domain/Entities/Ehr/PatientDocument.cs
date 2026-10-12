using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Scanned/photographed clinical document (e.g. hardcopy doctor note) linked to a patient visit.</summary>
public class PatientDocument : TenantEntity
{
    public string ScanStatus { get; set; } = "unresolved";
    public Guid? ScanEvidenceId { get; set; }
    public DateTime? ScannedAt { get; set; }
    public string? StorageKey { get; set; }
    public Guid? StorageVersion { get; set; }
    public string? Sha256 { get; set; }
    public long? StoredLength { get; set; }
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

