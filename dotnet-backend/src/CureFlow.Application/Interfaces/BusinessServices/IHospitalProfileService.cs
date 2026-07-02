using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IHospitalProfileService
{
    Task<object> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(object profile, CancellationToken ct = default);
    Task<object> ImportFromUrlAsync(string url, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListDepartmentsAsync(CancellationToken ct = default);
}
