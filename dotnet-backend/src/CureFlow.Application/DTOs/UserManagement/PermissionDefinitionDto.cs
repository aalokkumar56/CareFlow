using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record PermissionDefinitionDto(string Key, string Label, string? Category);
