using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IDashboardService
{
    Task<object> GetOverviewAsync(CancellationToken ct = default);
    Task<object> GetMissedRevenueAsync(CancellationToken ct = default);
    Task<object> GetClinicalOverviewAsync(DateTime? date, string? scope, Guid? doctorUserId, CancellationToken ct = default);
}

// External integrations
