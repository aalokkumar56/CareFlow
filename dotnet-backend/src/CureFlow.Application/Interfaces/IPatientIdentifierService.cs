using CureFlow.Domain.Entities;
namespace CureFlow.Application.Interfaces;
public record PatientIdentifierView(Guid Id,Guid FacilityId,string Namespace,string Value);
public interface IPatientIdentifierService
{
    Task<IReadOnlyList<PatientIdentifierView>> ListAsync(ICureFlowDbSession store,Guid facilityId,Guid patientId,CancellationToken ct);
    Task<PatientIdentifier> AllocateLocalMrnAsync(ICureFlowDbSession store,Guid facilityId,Guid patientId,CancellationToken ct);
}