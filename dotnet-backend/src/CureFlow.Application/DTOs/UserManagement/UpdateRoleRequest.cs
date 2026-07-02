namespace CureFlow.Application.DTOs;

public record UpdateRoleRequest(string? Description, IReadOnlyList<string> Permissions);
