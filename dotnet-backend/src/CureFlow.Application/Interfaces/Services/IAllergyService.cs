using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IAllergyService
{
    Task<Guid> AddAsync(CreateAllergyRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<AllergyDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
