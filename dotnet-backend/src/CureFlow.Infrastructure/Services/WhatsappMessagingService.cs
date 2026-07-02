using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.External;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services;

public class WhatsappMessagingService : IWhatsappMessagingService
{
    private readonly IWhatsappProvider _provider;
    private readonly IWhatsAppSettingsService _settings;
    private readonly ILogger<WhatsappMessagingService> _logger;

    public WhatsappMessagingService(
        IWhatsappProvider provider,
        IWhatsAppSettingsService settings,
        ILogger<WhatsappMessagingService> logger)
    {
        _provider = provider;
        _settings = settings;
        _logger = logger;
    }

    public async Task<(bool ok, string? messageId, string? error)> SendTextAsync(string toPhone, string body, CancellationToken ct = default)
    {
        var phone = WhatsappPhoneHelper.ForApi(toPhone);
        if (string.IsNullOrWhiteSpace(phone))
            return (false, null, "Invalid phone number");

        if (string.IsNullOrWhiteSpace(body))
            return (false, null, "Message body is required");

        var status = await _settings.GetStatusAsync(ct);
        if (!status.Enabled || !status.IsConfigured)
        {
            _logger.LogInformation("WhatsApp send skipped for {Phone}: {Reason}", phone, status.Message);
            return (false, null, status.Message);
        }

        try
        {
            var result = await _provider.SendTextAsync(toPhone, body, ct);
            if (!result.Success)
                _logger.LogWarning("WhatsApp send failed for {Phone}: {Error}", phone, result.Error);
            return (result.Success, result.MessageId, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp send exception for {Phone}.", phone);
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool ok, string? messageId, string? error)> SendTemplateAsync(
        SendTemplateMessageRequest request,
        CancellationToken ct = default)
    {
        var phone = WhatsappPhoneHelper.ForApi(request.Phone);
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(request.TemplateName))
            return (false, null, "Phone and template name are required");

        var status = await _settings.GetStatusAsync(ct);
        if (!status.Enabled || !status.IsConfigured)
            return (false, null, status.Message);

        try
        {
            var result = await _provider.SendTemplateMessageAsync(request, ct);
            return (result.Success, result.MessageId, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp template send exception for {Phone}.", phone);
            return (false, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<CampaignBatchSendResult>> SendCampaignBatchAsync(
        IReadOnlyList<CampaignBatchSendItem> recipients,
        CancellationToken ct = default)
    {
        var results = new List<CampaignBatchSendResult>(recipients.Count);
        foreach (var item in recipients)
        {
            var (ok, msgId, error) = await SendTextAsync(item.Phone, item.Body, ct);
            results.Add(new CampaignBatchSendResult(item.Phone, item.PatientId, ok, msgId, error));
        }

        return results;
    }
}
