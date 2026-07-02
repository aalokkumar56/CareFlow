using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class EmailSettings : TenantEntity
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPasswordEncrypted { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }

    /// <summary>When true and SMTP is configured, email may be sent.</summary>
    public bool Enabled { get; set; }

    /// <summary>When true, appointment WhatsApp notifications also send email when patient opted in.</summary>
    public bool SendWithWhatsApp { get; set; } = true;
}
