namespace CureFlow.Application.DTOs;

public record PatientDocumentDto(
    Guid Id,
    Guid PatientId,
    Guid? VisitId,
    Guid? AppointmentId,
    string DocumentType,
    string Title,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string FileUrl,
    string? UploadedByName,
    DateTime CapturedAt,
    DateTime CreatedAt, string Status = "unresolved");

public record PatientDocumentUploadResultDto(Guid Id, string FileUrl, string Title, string Status = "quarantined", string StatusUrl = "");
public record PatientDocumentStatusDto(Guid Id, string Status, DateTime? ScannedAt);

public record VisitChartCardDto(
    Guid? VisitId,
    DateTime VisitDate,
    string VisitDateKey,
    string? DoctorName,
    string? Department,
    string? ChiefComplaint,
    string? Diagnosis,
    string? DoctorNotes,
    string Status,
    IReadOnlyList<VitalSignsDto> Vitals,
    IReadOnlyList<ClinicalNoteDto> Notes,
    IReadOnlyList<PrescriptionDto> Prescriptions,
    IReadOnlyList<PatientDocumentDto> PaperNotes);

