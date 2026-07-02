using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IPrescriptionService
{
    Task<Guid> CreateAsync(CreatePrescriptionRequest req, CancellationToken ct = default);
    Task<PrescriptionDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PrescriptionDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
}
