using CureFlow.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.DependencyInjection;

internal sealed class WhatsappStartupValidationHostedService : IHostedService
{
    private readonly IOptions<WhatsappOptions> _options;
    private readonly ILogger<WhatsappStartupValidationHostedService> _logger;

    public WhatsappStartupValidationHostedService(
        IOptions<WhatsappOptions> options,
        ILogger<WhatsappStartupValidationHostedService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;

        _logger.LogInformation(
            "WhatsApp relay config: Relay={RelayConfigured}, RelayUrl={RelayUrl}",
            opts.IsRelayConfigured,
            string.IsNullOrWhiteSpace(opts.RelayWebhookUrl) ? "(none)" : opts.RelayWebhookUrl);

        // Provider credentials (tokens, AppSecret, verify token) live in tenant WhatsAppSettings (DB),
        // not in env — configure under Settings → Integrations.
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
