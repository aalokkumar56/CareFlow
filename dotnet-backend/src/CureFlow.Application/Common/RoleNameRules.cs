namespace CureFlow.Application.Common;

public static class RoleNameRules
{
    public static string Normalize(string? name) =>
        (name ?? string.Empty).Trim().Replace(' ', '_');

    public static bool IsProtected(string? name) =>
        Normalize(name).Equals(RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase);
}
