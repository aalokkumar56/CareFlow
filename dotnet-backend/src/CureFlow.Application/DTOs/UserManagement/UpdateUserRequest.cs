namespace CureFlow.Application.DTOs;

public record UpdateUserRequest(
    string? Name,
    string? Email,
    string? Role,
    bool? IsActive,
    string? Specialty = null,
    string? Phone = null,
    string? Qualifications = null,
    string? Password = null);
