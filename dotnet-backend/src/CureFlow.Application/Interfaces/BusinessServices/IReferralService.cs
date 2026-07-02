using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IReferralService
{
    Task<Guid> CreateDoctorAsync(string name, string? clinic, string? specialty, string? phone, Domain.Enums.DoctorCategory category, int reconnectDays, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListDoctorsAsync(string? q, string? category = null, CancellationToken ct = default);
    Task<Guid> CreateReferralAsync(Guid doctorId, Guid patientId, decimal revenue, string? notes, CancellationToken ct = default);
    Task<object> GetAnalyticsAsync(CancellationToken ct = default);
}
