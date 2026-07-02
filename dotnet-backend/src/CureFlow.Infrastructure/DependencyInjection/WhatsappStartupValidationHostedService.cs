using CureFlow.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace CureFlow.Infrastructure.DependencyInjection;

internal sealed class WhatsappStartupValidationHostedService : IHostedService
{
    private readonly IOptions<WhatsappOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WhatsappStartupValidationHostedService> _logger;

    public WhatsappStartupValidationHostedService(
        IOptions<WhatsappOptions> options,
        IHostEnvironment environment,
        ILogger<WhatsappStartupValidationHostedService> logger)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;

        _logger.LogInformation(
            "WhatsApp relay config: Relay={RelayConfigured}, RelayUrl={RelayUrl}",
            opts.IsRelayConfigured,
            string.IsNullOrWhiteSpace(opts.RelayWebhookUrl) ? "(none)" : opts.RelayWebhookUrl);

        // Defense-in-depth: inbound webhook signature verification requires AppSecret. Warn
        // (do not hard-fail) when it is missing so non-production/dev still works unchanged.
        if (string.IsNullOrWhiteSpace(opts.AppSecret))
        {
            _logger.LogWarning(
                "WhatsApp webhook AppSecret is not configured ({Environment}); inbound webhook signature verification is disabled. Set WhatsApp:AppSecret to enforce signed webhooks in production.",
                _environment.EnvironmentName);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
