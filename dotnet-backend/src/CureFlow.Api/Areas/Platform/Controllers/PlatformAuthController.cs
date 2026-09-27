using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/platform/auth")]
public class PlatformAuthController : ControllerBase
{
    private readonly IPlatformAuthService _auth;

    public PlatformAuthController(IPlatformAuthService auth) => _auth = auth;

    [HttpGet("setup-status")]
    [AllowAnonymous]
    public async Task<ActionResult<PlatformSetupStatusResponse>> SetupStatus(CancellationToken ct) =>
        Ok(await _auth.GetSetupStatusAsync(ct));

    [HttpPost("bootstrap")]
    [AllowAnonymous]
    [EnableRateLimiting("platform-bootstrap")]
    public async Task<ActionResult<PlatformAuthResponse>> Bootstrap(
        [FromBody] PlatformBootstrapRequest req,
        CancellationToken ct) =>
        Ok(await _auth.BootstrapOwnerAsync(req, ct));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<PlatformAuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(req, ct));
}
