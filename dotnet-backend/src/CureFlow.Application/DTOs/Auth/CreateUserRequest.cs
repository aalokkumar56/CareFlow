namespace CureFlow.Application.DTOs;

public record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    string Role,
    string? Specialty = null,
    string? Phone = null,
    string? Qualifications = null);
