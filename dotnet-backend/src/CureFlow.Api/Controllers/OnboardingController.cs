using Microsoft.AspNetCore.Authorization;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboarding;

    public OnboardingController(IOnboardingService onboarding) => _onboarding = onboarding;

    [HttpGet]
    public async Task<ActionResult<OnboardingStateDto>> GetState(CancellationToken ct) =>
        Ok(await _onboarding.GetStateAsync(ct));

    [Authorize(Policy = "Permission:Settings.Edit")]
    [HttpPost("profile")]
    public async Task<ActionResult<OnboardingStateDto>> MarkProfile(CancellationToken ct) =>
        Ok(await _onboarding.MarkProfileCompleteAsync(ct));

    [Authorize(Policy = "Permission:Settings.Edit")]
    [HttpPost("whatsapp")]
    public async Task<ActionResult<OnboardingStateDto>> MarkWhatsApp(CancellationToken ct) =>
        Ok(await _onboarding.MarkWhatsAppConnectedAsync(ct));

    [Authorize(Policy = "Permission:Settings.Edit")]
    [HttpPost("team")]
    public async Task<ActionResult<OnboardingStateDto>> MarkTeam(CancellationToken ct) =>
        Ok(await _onboarding.MarkTeamInvitedAsync(ct));

    [Authorize(Policy = "Permission:Settings.Edit")]
    [HttpPost("complete")]
    public async Task<ActionResult<OnboardingStateDto>> Complete(CancellationToken ct) =>
        Ok(await _onboarding.CompleteAsync(ct));
}

