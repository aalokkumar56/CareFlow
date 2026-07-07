using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Login with email + password. Returns JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(req, ct));

    /// <summary>Register a new hospital tenant. Public endpoint for SaaS signup.</summary>
    [HttpPost("register-tenant")]
    [AllowAnonymous]
    [EnableRateLimiting("register-tenant")]
    public async Task<ActionResult<TenantDto>> RegisterTenant([FromBody] RegisterTenantRequest req, CancellationToken ct) =>
        Ok(await _auth.RegisterTenantAsync(req, ct));

    /// <summary>Get current user info.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct) =>
        Ok(await _auth.GetMeAsync(ct));

    /// <summary>Current user + tenant lifecycle (for route gates).</summary>
    [HttpGet("session")]
    public async Task<ActionResult<SessionDto>> Session(CancellationToken ct) =>
        Ok(await _auth.GetSessionAsync(ct));

    /// <summary>Create a new user inside the current tenant (admin only).</summary>
    [HttpPost("register")]
    [Authorize(Policy = "Permission:User.Create")]
    public async Task<ActionResult<UserDto>> Register([FromBody] CreateUserRequest req, CancellationToken ct) =>
        Ok(await _auth.CreateUserAsync(req, ct));
}
