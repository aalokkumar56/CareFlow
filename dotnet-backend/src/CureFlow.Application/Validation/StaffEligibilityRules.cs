using CureFlow.Application.Common;
using CureFlow.Domain.Enums;
namespace CureFlow.Application.Validation;

public static class StaffEligibilityRules
{
    public static void Profile(decimal? fee, StaffEmploymentType? employment, string? department, string? specialization,
        string? qualification, string? shift, string? ward)
    {
        if(fee is <0||employment.HasValue&&!Enum.IsDefined(employment.Value))
            throw new ValidationException("Select a valid employment type and nonnegative consultation fee.");
        foreach(var (value,max) in new[]{(department,120),(specialization,200),(qualification,1000),(shift,64),(ward,120)})
            if(value is not null&&(value.Length>max||value.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')))
                throw new ValidationException("Staff profile fields exceed their permitted format or length.");
    }
}
