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
    DateTime CreatedAt);

public record PatientDocumentUploadResultDto(Guid Id, string FileUrl, string Title);

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
