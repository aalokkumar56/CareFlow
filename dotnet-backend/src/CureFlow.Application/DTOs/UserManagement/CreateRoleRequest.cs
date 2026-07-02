namespace CureFlow.Application.DTOs;

public record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);
