using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record RegisterTenantRequest(string HospitalName, string AdminName, string AdminEmail, string AdminPassword, string? Phone);
