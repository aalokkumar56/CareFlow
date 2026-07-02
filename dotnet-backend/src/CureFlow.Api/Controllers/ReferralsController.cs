using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/referrals")]
public class ReferralsController : ControllerBase
{
    private readonly IReferralService _svc;
    public ReferralsController(IReferralService svc) => _svc = svc;

    public record CreateRefRequest(Guid DoctorId, Guid PatientId, decimal Revenue, string? Notes);

    [HttpPost]
    [Authorize(Policy = "Permission:Referral.Manage")]
    public async Task<IActionResult> Create([FromBody] CreateRefRequest r, CancellationToken ct)
    { var id = await _svc.CreateReferralAsync(r.DoctorId, r.PatientId, r.Revenue, r.Notes, ct); return Ok(new { id }); }

    [HttpGet("analytics")]
    [Authorize(Policy = "Permission:Referral.View")]
    public async Task<IActionResult> Analytics(CancellationToken ct) => Ok(await _svc.GetAnalyticsAsync(ct));
}
