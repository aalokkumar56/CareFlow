using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _svc;
    public DashboardController(IDashboardService svc) => _svc = svc;

    [HttpGet("overview")]
    [Authorize(Policy = "Permission:Dashboard.View")]
    public async Task<IActionResult> Overview(CancellationToken ct) => Ok(await _svc.GetOverviewAsync(ct));

    [HttpGet("missed-revenue")]
    [Authorize(Policy = "Permission:Dashboard.View")]
    public async Task<IActionResult> Missed(CancellationToken ct) => Ok(await _svc.GetMissedRevenueAsync(ct));
}
