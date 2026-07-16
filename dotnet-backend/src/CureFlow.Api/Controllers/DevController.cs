using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

/// <summary>Development-only helpers for E2E and local multi-hospital testing.</summary>
[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly ICureFlowDbSession _db;
    private readonly IPasswordHasher _hasher;
    private readonly IWebHostEnvironment _env;

    public DevController(ICureFlowDbSession db, IPasswordHasher hasher, IWebHostEnvironment env)
    {
        _db = db;
        _hasher = hasher;
        _env = env;
    }

    /// <summary>
    /// Idempotently seeds Alpha, Beta, and Gamma E2E hospitals (admin + patient + appointment + conversation each).
    /// Prefer this over repeated register-tenant calls when UI E2E needs stable credentials.
    /// </summary>
    [HttpPost("seed-multi-hospitals")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedMultiHospitals(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        await MultiHospitalE2eSeeder.SeedAsync(_db, _hasher, ct);

        return Ok(new
        {
            ok = true,
            password = MultiHospitalE2eSeeder.DefaultPassword,
            hospitals = MultiHospitalE2eSeeder.Hospitals.Select(h => new
            {
                slug = h.Slug,
                name = h.Name,
                admin_email = h.AdminEmail,
                exclusive_patient = h.ExclusivePatientName,
            }),
        });
    }
}
