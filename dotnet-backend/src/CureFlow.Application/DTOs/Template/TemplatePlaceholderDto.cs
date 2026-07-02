namespace CureFlow.Application.DTOs;

public record TemplatePlaceholderDto(
    Guid Id,
    string Key,
    string Label,
    string? Description,
    string? Example,
    bool IsSystem,
    string? StaticValue);

public record CreateTemplatePlaceholderRequest(
    string Key,
    string Label,
    string? Description,
    string? Example,
    string? StaticValue);
