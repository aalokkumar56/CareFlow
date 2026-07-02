using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IDashboardService
{
    Task<object> GetOverviewAsync(CancellationToken ct = default);
    Task<object> GetMissedRevenueAsync(CancellationToken ct = default);
}

// External integrations
