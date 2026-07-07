using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IOnboardingService
{
    Task<OnboardingStateDto> GetStateAsync(CancellationToken ct = default);
    Task<OnboardingStateDto> MarkProfileCompleteAsync(CancellationToken ct = default);
    Task<OnboardingStateDto> MarkWhatsAppConnectedAsync(CancellationToken ct = default);
    Task<OnboardingStateDto> MarkTeamInvitedAsync(CancellationToken ct = default);
    Task<OnboardingStateDto> CompleteAsync(CancellationToken ct = default);
}
