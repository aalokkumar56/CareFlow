using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class PatientExportJob:TenantEntity
{
    public Guid RequesterId{get;set;}
    public string PatientIdsJson{get;set;}="[]";
    public string PatientTagsJson{get;set;}="{}";
    public string FieldsJson{get;set;}="[]";
    public string Purpose{get;set;}="";
    public string PolicyVersion{get;set;}="";
    public string State{get;set;}="pending";
    public long Revision{get;set;}=1;
    public DateTime ExpiresAt{get;set;}
    public string? ObjectKey{get;set;}
    public Guid? ObjectVersion{get;set;}
    public string? Sha256{get;set;}
    public long? Length{get;set;}
}