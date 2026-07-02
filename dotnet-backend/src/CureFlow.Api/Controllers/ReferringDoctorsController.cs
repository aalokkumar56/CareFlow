using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/doctors")]
public class ReferringDoctorsController : ControllerBase
{
    private readonly IReferralService _svc;
    private readonly ICureFlowDbSession _db;

    public ReferringDoctorsController(IReferralService svc, ICureFlowDbSession db)
    {
        _svc = svc;
        _db = db;
    }

    public record CreateDoctorRequest(string Name, string? Clinic, string? Specialty, string? Phone, DoctorCategory Category, int ReconnectDays);

    [HttpPost]
    [Authorize(Policy = "Permission:Referral.Manage")]
    public async Task<IActionResult> Create([FromBody] CreateDoctorRequest r, CancellationToken ct)
    {
        var id = await _svc.CreateDoctorAsync(r.Name, r.Clinic, r.Specialty, r.Phone, r.Category, r.ReconnectDays, ct);
        return Ok(new { id });
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Referral.View")]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? category, CancellationToken ct)
        => Ok(await _svc.ListDoctorsAsync(q, category, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Referral.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var doctor = await _db.GetByIdAsync<ReferringDoctor>(id, ct: ct);
        if (doctor == null) return NotFound();

        var referralWhere = SqlFragments.WhereActive<Referral>(ignoreTenant: false);
        var agg = await _db.QuerySingleAsync<DoctorReferralAggRow>(
            $"""
            SELECT COUNT(*)::int AS "PatientsReferred", COALESCE(SUM("Revenue"), 0) AS "TotalRevenue"
            FROM "Referrals"
            WHERE {referralWhere} AND "DoctorId" = @doctorId
            """,
            new { doctorId = id }, ct: ct);
        var referrals = await _db.QueryAsync<Referral>(
            $"""
            SELECT * FROM "Referrals"
            WHERE {referralWhere} AND "DoctorId" = @doctorId
            ORDER BY "CreatedAt" DESC
            """,
            new { doctorId = id }, ct: ct);

        return Ok(new
        {
            doctor,
            patients_referred = agg.PatientsReferred,
            total_revenue = agg.TotalRevenue,
            referrals
        });
    }

    [HttpPost("{id:guid}/mark-contacted")]
    [Authorize(Policy = "Permission:Referral.Manage")]
    public async Task<IActionResult> MarkContacted(Guid id, CancellationToken ct)
    {
        var doctor = await _db.GetByIdAsync<ReferringDoctor>(id, ct: ct);
        if (doctor == null) return NotFound();
        doctor.LastContactAt = DateTime.UtcNow;
        await _db.UpdateAsync(doctor, ct: ct);
        return Ok(new { ok = true });
    }

    private sealed class DoctorReferralAggRow
    {
        public int PatientsReferred { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
