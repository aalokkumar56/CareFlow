using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class WhatsAppSettings : TenantEntity
{
    /// <summary>WhatsBiz or MetaCloud</summary>
    public string Provider { get; set; } = "MetaCloud";

    public string? PhoneNumberId { get; set; }
    public string? WabaId { get; set; }
    public string? AccessTokenEncrypted { get; set; }
    public string? ApiTokenEncrypted { get; set; }
    public string? WhatsBizBaseUrl { get; set; }
    public string? VerifyToken { get; set; }
    public string? AppSecretEncrypted { get; set; }
    public string BusinessName { get; set; } = "Hospital";

    /// <summary>When false, outbound WhatsApp API calls are skipped.</summary>
    public bool Enabled { get; set; }
}
