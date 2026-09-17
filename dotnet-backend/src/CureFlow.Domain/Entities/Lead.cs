using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class Lead : TenantEntity
{
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int? Age { get; set; }
    public Gender Gender { get; set; } = Gender.Unknown;
    public Guid? ConvertedPatientId { get; set; }
    public DateTime? ConvertedAt { get; set; }
}
