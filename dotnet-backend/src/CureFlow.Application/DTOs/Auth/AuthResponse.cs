using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record AuthResponse(string AccessToken, UserDto User, TenantDto Tenant);
