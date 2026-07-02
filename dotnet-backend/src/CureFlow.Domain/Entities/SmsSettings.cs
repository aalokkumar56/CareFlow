using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class SmsSettings : TenantEntity
{
    public string? GatewayUrl { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public string? SenderId { get; set; }

    /// <summary>When true and credentials are set, SMS may be sent.</summary>
    public bool Enabled { get; set; }
}
