using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class ReferringDoctor : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Clinic { get; set; }
    public string? Specialty { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DoctorCategory Category { get; set; } = DoctorCategory.Specialist;
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public int ReconnectEveryDays { get; set; } = 30;
    public DateTime? LastContactAt { get; set; }
}
