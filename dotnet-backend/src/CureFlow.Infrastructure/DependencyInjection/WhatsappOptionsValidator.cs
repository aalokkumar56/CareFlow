using CureFlow.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.DependencyInjection;

internal sealed class WhatsappOptionsValidator : IValidateOptions<WhatsappOptions>
{
    private readonly ILogger<WhatsappOptionsValidator> _logger;

    public WhatsappOptionsValidator(ILogger<WhatsappOptionsValidator> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, WhatsappOptions options)
    {
        if (options.RelayWebhookEnabled && string.IsNullOrWhiteSpace(options.RelayWebhookUrl))
            _logger.LogWarning("WhatsApp RelayWebhookEnabled is true but RelayWebhookUrl is empty — relay forwarding is disabled.");

        if (options.IsRelayConfigured && string.IsNullOrWhiteSpace(options.RelayPublicBaseUrl))
            _logger.LogInformation("WhatsApp relay is configured but RelayPublicBaseUrl is empty — hosted media URLs will use relative paths.");

        return ValidateOptionsResult.Success;
    }
}
