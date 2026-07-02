using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record AssignPermissionsRequest(IReadOnlyList<string> Permissions);
