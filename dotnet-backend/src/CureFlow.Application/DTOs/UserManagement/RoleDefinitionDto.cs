using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record RoleDefinitionDto(

    string Name, string? LegacyRole, string Label, string Description,

    IReadOnlyList<string> Permissions, bool IsSystem);
