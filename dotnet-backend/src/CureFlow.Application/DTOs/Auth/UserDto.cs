using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UserDto(
    Guid Id, string Name, string Email, UserRole Role, bool IsActive,
    string? Specialty, string? Phone, IReadOnlyList<string>? Permissions = null);
