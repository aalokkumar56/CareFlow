using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record AuthResponse(string AccessToken, UserDto User, TenantDto Tenant)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int ValidatedSecurityRevision { get; init; }
}

