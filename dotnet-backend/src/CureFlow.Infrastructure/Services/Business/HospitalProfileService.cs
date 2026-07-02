using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CureFlow.Infrastructure.Services.Business;

public class HospitalProfileService : IHospitalProfileService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ICureFlowDbSession _db;
    private readonly IAiService _ai;
    private readonly IHospitalWebsiteScraper _scraper;
    private readonly ITenantContext _tenant;
    private readonly IMemoryCache _cache;

    public HospitalProfileService(
        ICureFlowDbSession db,
        IAiService ai,
        IHospitalWebsiteScraper scraper,
        ITenantContext tenant,
        IMemoryCache cache)
    {
        _db = db;
        _ai = ai;
        _scraper = scraper;
        _tenant = tenant;
        _cache = cache;
    }

    public Task<object> GetAsync(CancellationToken ct = default)
    {
        var cacheKey = $"hospital-profile:{_tenant.TenantId}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await LoadProfileAsync(ct);
        })!;
    }

    private async Task<object> LoadProfileAsync(CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<HospitalProfile>(ignoreTenant: false);
        var p = await _db.QueryFirstOrDefaultAsync<HospitalProfile>(
            $"""SELECT * FROM "HospitalProfiles" WHERE {where} LIMIT 1""", ct: ct);
        if (p == null)
        {
            return new
            {
                name = "Hospital",
                tagline = "",
                about = "",
                address = "",
                phones = new List<string>(),
                emails = new List<string>(),
                website = "",
                working_hours = "",
                emergency_24x7 = false,
                departments = new List<object>(),
                services = new List<string>(),
                doctors = new List<object>(),
                packages = new List<object>(),
                faqs = new List<object>()
            };
        }

        return ToViewModel(p);
    }

    public Task<IReadOnlyList<string>> ListDepartmentsAsync(CancellationToken ct = default)
        => DepartmentCatalog.LoadAsync(_db, ct);

    private void InvalidateCache() => _cache.Remove($"hospital-profile:{_tenant.TenantId}");

    public async Task UpdateAsync(object profile, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<HospitalProfile>(ignoreTenant: false);
        var p = await _db.QueryFirstOrDefaultAsync<HospitalProfile>(
            $"""SELECT * FROM "HospitalProfiles" WHERE {where} LIMIT 1""", ct: ct);
        var isNew = p == null;
        p ??= new HospitalProfile();

        if (profile is JsonElement payload)
            ApplyPayload(p, payload);

        if (isNew)
            await _db.InsertAsync(p, ct: ct);
        else
            await _db.UpdateAsync(p, ct: ct);

        InvalidateCache();
    }

    public async Task<object> ImportFromUrlAsync(string url, CancellationToken ct = default)
    {
        var text = await _scraper.FetchPagesAsync(url, ct);
        if (string.IsNullOrWhiteSpace(text))
            throw new ValidationException("Could not fetch website");
        var extracted = await _ai.ExtractHospitalProfileFromHtmlAsync(text, ct);

        var where = SqlFragments.WhereActive<HospitalProfile>(ignoreTenant: false);
        var p = await _db.QueryFirstOrDefaultAsync<HospitalProfile>(
            $"""SELECT * FROM "HospitalProfiles" WHERE {where} LIMIT 1""", ct: ct);
        var isNew = p == null;
        p ??= new HospitalProfile();

        if (extracted is JsonElement payload)
            ApplyPayload(p, payload);

        if (isNew)
            await _db.InsertAsync(p, ct: ct);
        else
            await _db.UpdateAsync(p, ct: ct);

        InvalidateCache();
        return ToViewModel(p);
    }

    private static void ApplyPayload(HospitalProfile p, JsonElement payload)
    {
        if (payload.TryGetProperty("name", out var name)) p.Name = name.GetString() ?? p.Name;
        if (payload.TryGetProperty("tagline", out var tagline)) p.Tagline = tagline.GetString();
        if (payload.TryGetProperty("about", out var about)) p.About = about.GetString();
        if (payload.TryGetProperty("address", out var address)) p.Address = address.GetString();
        if (payload.TryGetProperty("website", out var website)) p.Website = website.GetString();
        if (payload.TryGetProperty("working_hours", out var workingHours)) p.WorkingHours = workingHours.GetString();
        if (payload.TryGetProperty("emergency_24x7", out var emergencyFlag) && emergencyFlag.ValueKind is JsonValueKind.True or JsonValueKind.False)
            p.Emergency24x7 = emergencyFlag.GetBoolean();

        p.Phones = ReadStringArray(payload, "phones");
        p.Emails = ReadStringArray(payload, "emails");
        p.DepartmentsJson = ReadRawJsonArray(payload, "departments");
        p.ServicesJson = ReadRawJsonArray(payload, "services");
        p.DoctorsJson = ReadRawJsonArray(payload, "doctors");
        p.PackagesJson = ReadRawJsonArray(payload, "packages");
        p.FaqsJson = ReadRawJsonArray(payload, "faqs");
    }

    private static List<string> ReadStringArray(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array) return new List<string>();
        return arr.EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
    }

    private static string ReadRawJsonArray(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array) return "[]";
        return arr.GetRawText();
    }

    private static object ToViewModel(HospitalProfile p)
    {
        static object ParseJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<object>();
            try
            {
                return JsonSerializer.Deserialize<object>(raw) ?? new List<object>();
            }
            catch
            {
                return new List<object>();
            }
        }

        return new
        {
            id = p.Id,
            name = p.Name,
            tagline = p.Tagline,
            about = p.About,
            address = p.Address,
            phones = p.Phones ?? new List<string>(),
            emails = p.Emails ?? new List<string>(),
            website = p.Website,
            working_hours = p.WorkingHours,
            emergency_24x7 = p.Emergency24x7,
            departments = ParseJson(p.DepartmentsJson),
            services = ParseJson(p.ServicesJson),
            doctors = ParseJson(p.DoctorsJson),
            packages = ParseJson(p.PackagesJson),
            faqs = ParseJson(p.FaqsJson)
        };
    }
}
