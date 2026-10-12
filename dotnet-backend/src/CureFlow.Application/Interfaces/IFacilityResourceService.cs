namespace CureFlow.Application.Interfaces;
public record FacilityResourceSave(Guid LocationId,string Name,string Code,string ResourceType,int Capacity,string OperationalStatus,DateTime EffectiveFrom,DateTime? EffectiveUntil,long? ExpectedRevision);
public record FacilityResourceView(Guid Id,Guid FacilityId,Guid LocationId,string Name,string Code,string ResourceType,int Capacity,string OperationalStatus,DateTime EffectiveFrom,DateTime? EffectiveUntil,long Revision,bool EligibleForNewAllocation);
public interface IFacilityResourceService
{
    Task<IReadOnlyList<FacilityResourceView>> ListAsync(Guid facilityId,bool eligibleOnly,int limit,CancellationToken ct);
    Task<IdempotentCommandResult> SaveAsync(Guid facilityId,Guid? id,FacilityResourceSave input,string key,CancellationToken ct);
}