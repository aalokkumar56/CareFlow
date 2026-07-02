namespace CureFlow.Application.Interfaces;

public interface IWhatsAppSettingsService
{
    Task<WhatsAppRuntimeSettings> GetAsync(CancellationToken ct = default);
    Task<IntegrationStatusDto> GetStatusAsync(CancellationToken ct = default);
}

public sealed record WhatsAppRuntimeSettings(
    string Provider,
    bool Enabled,
    bool IsConfigured,
    string? PhoneNumberId,
    string? WabaId,
    string? AccessToken,
    string? ApiToken,
    string? WhatsBizBaseUrl,
    string? VerifyToken,
    string? AppSecret,
    string BusinessName);

public sealed record IntegrationStatusDto(
    bool Enabled,
    bool IsConfigured,
    string Message);
