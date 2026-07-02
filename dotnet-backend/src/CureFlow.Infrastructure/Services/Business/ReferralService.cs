using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Business;

public class ReferralService : IReferralService
{
    private readonly ICureFlowDbSession _db;

    public ReferralService(ICureFlowDbSession db) => _db = db;

    public async Task<Guid> CreateDoctorAsync(string name, string? clinic, string? specialty, string? phone,
        DoctorCategory category, int reconnectDays, CancellationToken ct = default)
    {
        var d = new ReferringDoctor
        {
            Name = name, Clinic = clinic, Specialty = specialty,
            Phone = phone == null ? null : WhatsappPhoneHelper.Normalize(phone),
            Category = category, ReconnectEveryDays = reconnectDays,
        };
        await _db.InsertAsync(d, ct: ct);
        return d.Id;
    }

    public async Task<IReadOnlyList<object>> ListDoctorsAsync(string? q, string? category = null, CancellationToken ct = default)
    {
        var filters = new List<string> { SqlFragments.WhereActive<ReferringDoctor>(ignoreTenant: false) };
        var param = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            filters.Add("(\"Name\" ILIKE @q OR (\"Clinic\" IS NOT NULL AND \"Clinic\" ILIKE @q))");
            param["q"] = $"%{q}%";
        }
        if (!string.IsNullOrWhiteSpace(category) && EnumParseHelper.TryParseSnakeCase<DoctorCategory>(category, out var cat))
        {
            filters.Add("\"Category\" = @category");
            param["category"] = (int)cat;
        }

        var where = " WHERE " + string.Join(" AND ", filters);
        var docs = await _db.QueryAsync<ReferringDoctor>(
            $"""SELECT * FROM "ReferringDoctors"{where} ORDER BY "CreatedAt" DESC""",
            param, ct: ct);

        var referralWhere = SqlFragments.WhereActive<Referral>(ignoreTenant: false);
        var refAgg = await _db.QueryAsync<ReferralAggRow>(
            $"""
            SELECT "DoctorId", COUNT(*)::int AS "ReferralCount", COALESCE(SUM("Revenue"), 0) AS "Revenue"
            FROM "Referrals"
            WHERE {referralWhere}
            GROUP BY "DoctorId"
            """, ct: ct);

        return docs.Select(d =>
        {
            var a = refAgg.FirstOrDefault(x => x.DoctorId == d.Id);
            var needsReconnect = !d.LastContactAt.HasValue ||
                (DateTime.UtcNow - d.LastContactAt.Value).TotalDays >= d.ReconnectEveryDays;
            return (object)new
            {
                d.Id, d.Name, d.Clinic, d.Specialty, d.Phone, d.Category, d.Tags, d.Notes,
                d.ReconnectEveryDays, d.LastContactAt,
                patients_referred = a?.ReferralCount ?? 0,
                total_revenue = a?.Revenue ?? 0m,
                needs_reconnect = needsReconnect,
            };
        }).ToList();
    }

    public async Task<Guid> CreateReferralAsync(Guid doctorId, Guid patientId, decimal revenue,
        string? notes, CancellationToken ct = default)
    {
        var d = await _db.GetByIdAsync<ReferringDoctor>(doctorId, ct: ct)
            ?? throw new NotFoundException("Doctor");
        var p = await _db.GetByIdAsync<Patient>(patientId, ct: ct)
            ?? throw new NotFoundException("Patient");

        var r = new Referral
        {
            DoctorId = doctorId, DoctorName = d.Name,
            PatientId = patientId, PatientName = p.Name,
            Revenue = revenue, Notes = notes,
        };

        await _db.TransactionAsync(async tx =>
        {
            await tx.InsertAsync(r, ct: ct);
            p.ReferralDoctor = d.Name;
            p.ReferringDoctorId = d.Id;
            await tx.UpdateAsync(p, ct: ct);
        }, ct);

        return r.Id;
    }

    public async Task<object> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var referralWhere = SqlFragments.WhereActive<Referral>(ignoreTenant: false);
        var totalReferrals = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Referrals" WHERE {referralWhere}""", ct: ct);
        var totalRevenue = await _db.QuerySingleAsync<decimal>(
            $"""SELECT COALESCE(SUM("Revenue"), 0) FROM "Referrals" WHERE {referralWhere}""", ct: ct);
        var top = (await _db.QueryAsync<TopDoctorRow>(
            $"""
            SELECT "DoctorId", "DoctorName", COUNT(*)::int AS "Count", COALESCE(SUM("Revenue"), 0) AS "Revenue"
            FROM "Referrals"
            WHERE {referralWhere}
            GROUP BY "DoctorId", "DoctorName"
            ORDER BY "Count" DESC
            LIMIT 10
            """, ct: ct)).Select(x => new
        {
            doctor_id = x.DoctorId,
            doctor_name = x.DoctorName,
            count = x.Count,
            revenue = x.Revenue,
        }).ToList();
        return new { total_referrals = totalReferrals, total_revenue = totalRevenue, top_doctors = top };
    }

    private sealed class ReferralAggRow
    {
        public Guid DoctorId { get; set; }
        public int ReferralCount { get; set; }
        public decimal Revenue { get; set; }
    }

    private sealed class TopDoctorRow
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public int Count { get; set; }
        public decimal Revenue { get; set; }
    }
}
