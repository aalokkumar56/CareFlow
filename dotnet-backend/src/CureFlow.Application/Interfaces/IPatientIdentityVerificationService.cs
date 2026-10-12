namespace CureFlow.Application.Interfaces;
public record UnknownPatientRegistrationInput(Guid FacilityId,string Reason,string? SourceReference);
public record PatientIdentityVerificationInput(string ExpectedPatientTag,long DemographicRevision,string ProofReference,string Reason);
public record PatientIdentityStatusView(Guid PatientId,string RecordedStatus,bool VerificationIsCurrent,string? TemporaryIdentifier,Guid? TemporaryFacilityId,long? DemographicRevision);
public interface IPatientIdentityVerificationService
{
    Task<IdempotentCommandResult> RegisterUnknownAsync(UnknownPatientRegistrationInput input,string key,CancellationToken ct);
    Task<IdempotentCommandResult> VerifyAsync(Guid patientId,PatientIdentityVerificationInput input,string key,CancellationToken ct);
    Task<PatientIdentityStatusView> GetAsync(Guid patientId,CancellationToken ct);
}
