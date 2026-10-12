using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;

public sealed class OutboxEvent : TenantEntity
{
    public long Revision { get; set; } = 1;
    public int OperatorRetries { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string AggregateVersion { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime DueAt { get; set; } = DateTime.UtcNow;
    public string State { get; set; } = "pending";
    public Guid? LeaseOwner { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureCode { get; set; }
}
public sealed class OutboxConsumerReceipt : TenantEntity
{
    public Guid EventId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
}

