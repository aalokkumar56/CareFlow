using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public sealed record PatientSearchItem(Guid Id, string Name, string Phone, int? Age, Gender Gender, string? Department);
public sealed record PatientSearchPage(IReadOnlyList<PatientSearchItem> Items, Guid? NextAfterId);
