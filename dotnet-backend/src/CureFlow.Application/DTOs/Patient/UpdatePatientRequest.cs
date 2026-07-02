using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UpdatePatientRequest(
    string? Name, string? Phone, string? Email, bool? EmailNotificationsEnabled,
    int? Age, Gender? Gender, BloodGroup? BloodGroup,
    DateTime? DateOfBirth,
    string? Department, LeadStatus? Status, Guid? AssignedStaffId,
    List<string>? Tags, string? Notes, DateTime? FollowUpDate,
    string? Occupation, string? MaritalStatus,
    string? GovIdType, string? GovIdNumber,
    string? AddressLine1, string? AddressLine2, string? City, string? State, string? Pincode,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation);
