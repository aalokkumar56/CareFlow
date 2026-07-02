namespace CureFlow.Application.Notifications;

public static class NotificationPreferenceResolver
{
    /// <summary>
    /// Resolves effective in-app delivery. RBAC is the ceiling; preferences only narrow within permitted types.
    /// </summary>
    public static bool ResolveInAppEnabled(
        bool hasRequiredPermissions,
        bool? userOverride,
        bool? roleDefault,
        bool systemDefault)
    {
        if (!hasRequiredPermissions) return false;
        if (userOverride.HasValue) return userOverride.Value;
        if (roleDefault.HasValue) return roleDefault.Value;
        return systemDefault;
    }
}
