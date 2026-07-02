using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

/// <summary>Extended profile for internal hospital staff (doctors, nurses, staff).</summary>
public class StaffProfile : TenantEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string? Department { get; set; }
    public string? Specialization { get; set; }
    public string? Qualification { get; set; }
    public decimal? ConsultationFee { get; set; }
    public StaffEmploymentType EmploymentType { get; set; } = StaffEmploymentType.Permanent;
    public string? Shift { get; set; }              // "Morning", "Evening", "Night"
    public string? WardAssignment { get; set; }
    public bool IsAvailable { get; set; } = true;
}


