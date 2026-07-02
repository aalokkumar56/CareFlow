using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class HospitalProfile : TenantEntity
{
    public string Name { get; set; } = "Hospital";
    public string? Tagline { get; set; }
    public string? About { get; set; }
    public string? Address { get; set; }
    public List<string> Phones { get; set; } = new();
    public List<string> Emails { get; set; } = new();
    public string? Website { get; set; }
    public string? WorkingHours { get; set; }
    public bool Emergency24x7 { get; set; } = true;
    /// <summary>JSON list of departments.</summary>
    public string DepartmentsJson { get; set; } = "[]";
    /// <summary>JSON list of services.</summary>
    public string ServicesJson { get; set; } = "[]";
    /// <summary>JSON list of doctors.</summary>
    public string DoctorsJson { get; set; } = "[]";
    /// <summary>JSON list of packages.</summary>
    public string PackagesJson { get; set; } = "[]";
    /// <summary>JSON list of FAQs.</summary>
    public string FaqsJson { get; set; } = "[]";
}
