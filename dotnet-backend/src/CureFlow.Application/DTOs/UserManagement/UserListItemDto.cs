using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UserListItemDto(
    Guid Id, string Name, string Email, UserRole Role, bool IsActive,
    string? Specialty, string? Phone, DateTime? LastLoginAt, DateTime CreatedAt);
