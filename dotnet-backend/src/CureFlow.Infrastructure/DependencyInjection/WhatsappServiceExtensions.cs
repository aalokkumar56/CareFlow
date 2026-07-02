using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Services.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.DependencyInjection;

public static class WhatsappServiceExtensions
{
    public static IServiceCollection AddWhatsappIntegration(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<WhatsappOptions>()
            .Bind(config.GetSection(WhatsappOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<WhatsappOptions>, WhatsappOptionsValidator>();

        services.Configure<WhatsappMediaOptions>(config.GetSection(WhatsappMediaOptions.SectionName));

        services.AddHttpClient("WhatsBiz", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("MetaCloud", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("WebhookRelay", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IWhatsAppSettingsService, WhatsAppSettingsService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<EmailService>();
        services.AddScoped<IEmailService>(sp => sp.GetRequiredService<EmailService>());
        services.AddScoped<IEmailInboxService>(sp => sp.GetRequiredService<EmailService>());

        services.AddScoped<External.Providers.WhatsBizProvider>();
        services.AddScoped<External.Providers.MetaCloudProvider>();
        services.AddScoped<IWhatsappProvider, RoutingWhatsappProvider>();

        services.AddScoped<IWhatsappMediaStore, External.WhatsappMediaStore>();
        services.AddScoped<IWhatsappWebhookRelayService, External.WhatsappWebhookRelayService>();
        services.AddScoped<IWhatsappMessagingService, Services.WhatsappMessagingService>();
        services.AddScoped<IWhatsappApiService, Services.WhatsappApiService>();
        services.AddScoped<IWhatsappService, External.WhatsappCloudClient>();
        services.AddHostedService<WhatsappStartupValidationHostedService>();

        return services;
    }
}
