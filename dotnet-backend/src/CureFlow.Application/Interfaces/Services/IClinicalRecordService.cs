using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IClinicalRecordService
{
    Task<Guid> AddVitalsAsync(CreateVitalSignsRequest req, CancellationToken ct = default);
    Task<Guid> AddNoteAsync(CreateClinicalNoteRequest req, CancellationToken ct = default);
    Task UpdateNoteAsync(Guid id, UpdateClinicalNoteRequest req, CancellationToken ct = default);
    Task<Guid> AddMedicalHistoryAsync(CreateMedicalHistoryRequest req, CancellationToken ct = default);
    Task<Guid> AddFamilyHistoryAsync(CreateFamilyHistoryRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<VitalSignsDto>> ListVitalsAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<ClinicalNoteDto>> ListNotesAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<MedicalHistoryDto>> ListMedicalHistoryAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<FamilyHistoryDto>> ListFamilyHistoryAsync(Guid patientId, CancellationToken ct = default);
}
