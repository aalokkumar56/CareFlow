namespace CureFlow.Application.Notifications;

public static class NotificationRbac
{
    public static bool HasAllPermissions(IReadOnlyList<string> userPermissions, IReadOnlyList<string> required)
    {
        if (required.Count == 0) return true;
        var set = userPermissions as HashSet<string> ?? userPermissions.ToHashSet(StringComparer.Ordinal);
        return required.All(p => set.Contains(p));
    }
}
