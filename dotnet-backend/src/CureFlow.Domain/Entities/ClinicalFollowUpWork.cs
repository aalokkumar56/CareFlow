using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public class ClinicalFollowUpWork : TenantEntity
{
 public Guid PatientId { get; set; }
 public string SourceType { get; set; } = "appointment";
 public Guid SourceId { get; set; }
 public string SourceVersion { get; set; } = "";
 public Guid? AssignedUserId { get; set; }
 public Guid EscalationOwnerUserId { get; set; }
 public DateTime DueAtUtc { get; set; }
 public string State { get; set; } = "open";
 public long Revision { get; set; } = 1;
 public DateTime? AcknowledgedAtUtc { get; set; }
 public DateTime? CompletedAtUtc { get; set; }
}
public class ClinicalFollowUpVersion : TenantEntity
{
 public Guid WorkId { get; set; }
 public Guid PatientId { get; set; }
 public long Revision { get; set; }
 public Guid? AssignedUserId { get; set; }
 public Guid EscalationOwnerUserId { get; set; }
 public DateTime DueAtUtc { get; set; }
 public string State { get; set; } = "open";
 public DateTime? AcknowledgedAtUtc { get; set; }
 public DateTime? CompletedAtUtc { get; set; }
 public string Action { get; set; } = "";
 public string Reason { get; set; } = "";
 public Guid? EvidenceNoteId { get; set; }
 public int? EvidenceNoteRevision { get; set; }
}
