using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class WhatsAppContact : BaseEntity
{
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>WhatsBiz external group id from API (not an EF FK).</summary>
    public string ExternalGroupId { get; set; } = string.Empty;
    /// <summary>FK to cached <see cref="WhatsAppGroup"/> row.</summary>
    public Guid? WhatsAppGroupId { get; set; }
    public WhatsAppGroup? Group { get; set; }
    public string? CustomFields { get; set; } // JSON serialized
    public string ExternalId { get; set; } = string.Empty;
    public DateTime ExternalCreatedAt { get; set; }
    public DateTime ExternalUpdatedAt { get; set; }
    public DateTime CachedAt { get; set; }
}
