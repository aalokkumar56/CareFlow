using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

/// <summary>Patient / Lead record. Source of truth for everything about a patient.</summary>
public class Patient : TenantEntity
{
    // Identity
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>When true and email is set, transactional emails may be sent to this patient.</summary>
    public bool EmailNotificationsEnabled { get; set; }
    public int? Age { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public Gender Gender { get; set; } = Gender.Unknown;
    public BloodGroup BloodGroup { get; set; } = BloodGroup.Unknown;

    // Address
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }

    // Emergency contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }

    // CRM
    public string? Department { get; set; }
    public string InquirySource { get; set; } = "manual";
    public string? ReferralDoctor { get; set; }
    public Guid? ReferringDoctorId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.NewInquiry;
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }

    public DateTime? LastContactAt { get; set; }
    public DateTime? FollowUpDate { get; set; }

    // AI
    public string? AiSummary { get; set; }
    public int? AiLeadScore { get; set; }

    // Identity proof
    public string? GovIdType { get; set; }
    public string? GovIdNumber { get; set; }

    public string? Occupation { get; set; }
    public string? MaritalStatus { get; set; }
}
