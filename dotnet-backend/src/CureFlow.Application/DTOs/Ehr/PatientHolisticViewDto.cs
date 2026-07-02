using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record PatientHolisticViewDto(
    PatientDetailDto Patient,
    LifestyleProfileDto? Lifestyle,
    IReadOnlyList<AllergyDto> Allergies,
    IReadOnlyList<PrescriptionDto> RecentPrescriptions,
    IReadOnlyList<object> VitalSigns,
    IReadOnlyList<object> LabReports,
    IReadOnlyList<object> ClinicalNotes,
    IReadOnlyList<object> MedicalHistory,
    IReadOnlyList<object> FamilyHistory,
    IReadOnlyList<object> Appointments);
