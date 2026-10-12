using System.Security.Claims;
namespace CureFlow.Api.Authorization;

public static class ActorTokenBoundary
{
    public static bool IsValid(ClaimsPrincipal? principal, string tenantAudience, string platformAudience)
    {
        if (principal is null) return false;
        string? Single(string type)
        {
            var values = principal.FindAll(type).Select(x => x.Value).ToArray();
            return values.Length == 1 ? values[0] : null;
        }
        var actor = Single("actor_kind");
        var audience = Single("aud");
        var subject = Single(ClaimTypes.NameIdentifier) ?? Single("sub");
        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty) return false;
        if (actor == "hospital")
            return audience == tenantAudience && !principal.HasClaim(x => x.Type == "platform_user" || x.Type.StartsWith("platform_session_", StringComparison.Ordinal))
                && Guid.TryParse(Single("tenant_id"), out var tenant) && tenant != Guid.Empty
                && Guid.TryParse(Single("tenant_session_id"), out var family) && family != Guid.Empty
                && int.TryParse(Single("tenant_session_revision"), out var revision) && revision > 0;
        if (actor == "platform")
            return audience == platformAudience && Single("platform_user") == "true"
                && !principal.HasClaim(x => x.Type == "tenant_id" || x.Type.StartsWith("tenant_session_", StringComparison.Ordinal))
                && Guid.TryParse(Single("platform_session_id"), out var session) && session != Guid.Empty
                && int.TryParse(Single("platform_session_revision"), out var revision) && revision > 0;
        return false;
    }
}
