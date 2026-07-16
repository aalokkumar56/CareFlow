namespace CureFlow.Application.Options;

/// <summary>
/// WhatsApp integration configuration. Switch providers via <see cref="Provider"/>.
/// </summary>
public class WhatsappOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>WhatsBiz (default) or MetaCloud.</summary>
    public string Provider { get; set; } = "WhatsBiz";

    public string? PhoneNumberId { get; set; }
    public string? AccessToken { get; set; }
    public string? AppSecret { get; set; }
    public string? VerifyToken { get; set; }

    /// <summary>WhatsBiz / WPBox API token.</summary>
    public string? ApiToken { get; set; }

    public bool Enabled { get; set; }

    /// <summary>WhatsBiz API base URL (trailing slash recommended).</summary>
    public string? WhatsBizBaseUrl { get; set; }

    /// <summary>Meta Graph API base URL.</summary>
    public string MetaGraphApiBaseUrl { get; set; } = "https://graph.facebook.com";

    /// <summary>Meta Graph API version segment (e.g. v18.0).</summary>
    public string MetaGraphApiVersion { get; set; } = "v18.0";

    /// <summary>External URL to forward normalized webhook payloads (e.g. Emergent agent endpoint).</summary>
    public string? RelayWebhookUrl { get; set; }

    /// <summary>When false, relay forwarding is skipped even if RelayWebhookUrl is set.</summary>
    public bool RelayWebhookEnabled { get; set; }

    /// <summary>Number of delivery attempts (includes the first try).</summary>
    public int RelayWebhookRetryCount { get; set; } = 3;

    /// <summary>Delay between relay retry attempts in milliseconds.</summary>
    public int RelayWebhookRetryDelayMs { get; set; } = 1000;

    /// <summary>Public base URL used when building hosted media links (e.g. https://api.example.com).</summary>
    public string? RelayPublicBaseUrl { get; set; }

    /// <summary>Relative directory under content root for downloaded webhook media.</summary>
    public string RelayMediaStoragePath { get; set; } = "webhook-media";

    public bool IsWhatsBizProvider =>
        string.Equals(Provider, "WhatsBiz", StringComparison.OrdinalIgnoreCase);

    public bool IsMetaCloudProvider =>
        string.Equals(Provider, "MetaCloud", StringComparison.OrdinalIgnoreCase);

    public bool IsRelayConfigured =>
        RelayWebhookEnabled && !string.IsNullOrWhiteSpace(RelayWebhookUrl);
}
