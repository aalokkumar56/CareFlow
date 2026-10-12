namespace CureFlow.Application.Interfaces;
public record IdentityReviewInput(Guid FirstPatientId,Guid SecondPatientId,string FirstPatientTag,string SecondPatientTag,string Decision,string EvidenceReference,string Reason,long? ExpectedRevision);
public record IdentityReviewPatient(Guid Id,string Name,string Phone,string ReviewedVersionTag,string CurrentVersionTag);
public record IdentityReviewView(Guid ReviewId,long Revision,string Decision,string EvidenceReference,string Reason,bool ExactNameSignal,bool SharedPhoneSignal,IdentityReviewPatient First,IdentityReviewPatient Second,bool EvidenceIsCurrent);
public interface IPatientIdentityReviewService
{
    Task<IdentityReviewView> GetAsync(Guid reviewId,CancellationToken ct);
    Task<IdempotentCommandResult> SaveAsync(Guid? reviewId,IdentityReviewInput input,string key,CancellationToken ct);
}