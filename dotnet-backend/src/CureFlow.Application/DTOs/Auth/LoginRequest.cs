using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record LoginRequest(string Email, string Password, string? TenantSlug = null);
