using System.Text.Json;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/hospital-profile")]
public class HospitalProfileController : ControllerBase
{
    private readonly IHospitalProfileService _svc;
    public HospitalProfileController(IHospitalProfileService svc) => _svc = svc;

    [HttpGet]
    [Authorize(Policy = "Permission:Settings.View")]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _svc.GetAsync(ct));

    [HttpGet("departments")]
    [Authorize]
    public async Task<IActionResult> ListDepartments(CancellationToken ct) =>
        Ok(await _svc.ListDepartmentsAsync(ct));

    [HttpPut]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> Update([FromBody] JsonElement profile, CancellationToken ct)
    { await _svc.UpdateAsync(profile, ct); return Ok(new { ok = true }); }

    public record ImportRequest(string Url);
    [HttpPost("import")]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> Import([FromBody] ImportRequest r, CancellationToken ct) =>
        Ok(await _svc.ImportFromUrlAsync(r.Url, ct));
}
