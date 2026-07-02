using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record PatientDetailDto(
    Guid Id, string Name, string Phone, string? Email, bool EmailNotificationsEnabled,
    int? Age, Gender Gender, BloodGroup BloodGroup,
    DateTime? DateOfBirth,
    string? AddressLine1, string? AddressLine2, string? City, string? State, string? Pincode,
    string? Department, LeadStatus Status, List<string> Tags,
    string? Notes, string? Occupation, string? MaritalStatus,
    string? GovIdType, string? GovIdNumber,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation,
    string InquirySource, string? ReferralDoctor,
    DateTime? LastContactAt, DateTime? FollowUpDate,
    string? AiSummary, int? AiLeadScore,
    DateTime CreatedAt, DateTime UpdatedAt);

// ===== Lifestyle =====
