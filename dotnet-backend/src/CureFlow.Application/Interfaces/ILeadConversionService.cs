namespace CureFlow.Application.Interfaces;
public record LeadConversionCandidate(Guid PatientId,string Name,string Phone,string PatientTag);
public record LeadConversionPreview(Guid LeadId,string LeadTag,IReadOnlyList<LeadConversionCandidate> Candidates,string ReviewTag);
public record LeadConversionInput(Guid FacilityId,string ExpectedLeadTag,string ExpectedReviewTag,string Decision,Guid? PatientId,
    string? ExpectedPatientTag,bool AcknowledgePotentialDuplicates,string EvidenceReference,string Reason,string? ReferralSource=null,Guid? ReferringDoctorId=null);
public record LeadConversionView(Guid Id,Guid LeadId,Guid PatientId,Guid FacilityId,string Decision,DateTime RecordedAt,Guid? RecordedBy,
    string EvidenceReference,string Reason,string? ReferralSource,Guid? ReferringDoctorId,string PolicyVersion);
public interface ILeadConversionService
{
    Task<LeadConversionPreview> PreviewAsync(Guid leadId,CancellationToken ct);
    Task<IdempotentCommandResult> ConvertAsync(Guid leadId,LeadConversionInput input,string key,CancellationToken ct);
    Task<LeadConversionView> GetAsync(Guid leadId,CancellationToken ct);
}