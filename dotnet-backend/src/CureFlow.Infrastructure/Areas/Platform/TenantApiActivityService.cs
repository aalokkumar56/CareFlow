using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;

namespace CureFlow.Infrastructure.Areas.Platform;

public sealed class TenantApiActivityService(IPlatformDbSession db, PlatformAccess access)
{
    public async Task<object> List(Guid? tenantId, string? search, int? statusCode, bool errorsOnly,
        string? method, DateTime? from, DateTime? to, int page, int pageSize)
    {
        await access.RequireAsync("audit.read");
        if (from > to) throw new DomainException("The start date must be before the end date.", 400);
        if (statusCode is < 100 or > 599) throw new DomainException("Status code must be between 100 and 599.", 400);
        page = Math.Clamp(page, 1, 100000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (search?.Length > 200) throw new DomainException("Search must be 200 characters or fewer.", 400);
        if (method?.Length > 16) throw new DomainException("HTTP method must be 16 characters or fewer.", 400);
        var values = new
        {
            TenantId = tenantId, Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
            StatusCode = statusCode, ErrorsOnly = errorsOnly,
            Method = string.IsNullOrWhiteSpace(method) ? null : method.Trim().ToUpperInvariant(),
            From = from?.ToUniversalTime(), To = to?.ToUniversalTime(),
            Limit = pageSize, Offset = (page - 1) * pageSize
        };
        const string where = """
            FROM "TenantApiActivities" a LEFT JOIN "Tenants" t ON t."Id" = a."TenantId"
            WHERE a."IsDeleted" = false
              AND (CAST(@TenantId AS uuid) IS NULL OR a."TenantId" = @TenantId)
              AND (CAST(@Search AS text) IS NULL OR a."Route" ILIKE @Search OR a."CorrelationId" ILIKE @Search OR t."Name" ILIKE @Search)
              AND (CAST(@StatusCode AS integer) IS NULL OR a."StatusCode" = @StatusCode)
              AND (NOT @ErrorsOnly OR a."StatusCode" >= 400)
              AND (CAST(@Method AS text) IS NULL OR a."Method" = @Method)
              AND (CAST(@From AS timestamptz) IS NULL OR a."CreatedAt" >= @From)
              AND (CAST(@To AS timestamptz) IS NULL OR a."CreatedAt" <= @To)
            """;
        var total = await db.QuerySingleAsync<long>("SELECT COUNT(*) " + where, values, true);
        var rows = await db.QueryAsync<ActivityRow>("SELECT a.*, t.\"Name\" AS \"TenantName\" " + where
            + " ORDER BY a.\"CreatedAt\" DESC, a.\"Id\" DESC LIMIT @Limit OFFSET @Offset", values, true);
        return new
        {
            items = rows.Select(a => new
            {
                id = a.Id, tenant_id = a.TenantId, tenant_name = a.TenantName,
                actor_id = a.ActorId, method = a.Method, route = a.Route,
                status_code = a.StatusCode, duration_ms = a.DurationMs,
                correlation_id = a.CorrelationId, error_code = a.ErrorCode,
                error_summary = a.ErrorSummary, created_at = a.CreatedAt
            }),
            total, page, page_size = pageSize
        };
    }

    private sealed class ActivityRow
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string? TenantName { get; set; }
        public Guid? ActorId { get; set; }
        public string Method { get; set; } = "";
        public string Route { get; set; } = "";
        public int StatusCode { get; set; }
        public long DurationMs { get; set; }
        public string CorrelationId { get; set; } = "";
        public string? ErrorCode { get; set; }
        public string? ErrorSummary { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
