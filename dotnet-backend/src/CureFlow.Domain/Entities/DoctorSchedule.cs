using CureFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CureFlow.Domain.Entities;

/// <summary>Doctor weekly schedule / availability slots.</summary>
public class DoctorSchedule : TenantEntity
{
    public Guid StaffProfileId { get; set; }
    public StaffProfile? StaffProfile { get; set; }

    /// <summary>0=Sunday … 6=Saturday, or null for one-off date override.</summary>
    public int? DayOfWeek { get; set; }
    public DateTime? SpecificDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? Notes { get; set; }
}
