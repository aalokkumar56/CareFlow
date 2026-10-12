using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Validation;

public static class PatientSearchRules
{
    public static void Validate(string? query, string? status, string? department, string? tag, string? source)
    {
        foreach (var (value, max) in new[] { (query, 200), (status, 40), (department, 120), (tag, 100), (source, 100) })
            if (value is not null && (value.Length > max || value.Any(char.IsControl)))
                throw new ValidationException("Patient search filters exceed their permitted format or length.");
        if (!string.IsNullOrWhiteSpace(status) &&
            (!EnumParseHelper.TryParseSnakeCase<LeadStatus>(status, out var parsed) || !Enum.IsDefined(parsed)))
            throw new ValidationException("Select a supported patient status.");
    }

    public static string? Pattern(string? query) => string.IsNullOrWhiteSpace(query) ? null :
        "%" + query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
