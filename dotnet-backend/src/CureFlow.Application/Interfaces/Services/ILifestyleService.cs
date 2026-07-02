using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface ILifestyleService
{
    Task<LifestyleProfileDto?> GetAsync(Guid patientId, CancellationToken ct = default);
    Task UpsertAsync(Guid patientId, LifestyleProfileDto dto, CancellationToken ct = default);
}
