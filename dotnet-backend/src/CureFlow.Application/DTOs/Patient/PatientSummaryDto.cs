using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record PatientSummaryDto(
    Guid Id, string Name, string Phone, string? Email, int? Age, Gender Gender,
    string? Department, LeadStatus Status, List<string> Tags,
    string InquirySource, DateTime CreatedAt, DateTime? LastContactAt);
