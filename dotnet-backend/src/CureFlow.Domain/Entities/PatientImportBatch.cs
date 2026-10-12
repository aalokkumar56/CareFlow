using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class PatientImportBatch:TenantEntity
{
    public Guid FacilityId{get;set;}
    public string FileSha256{get;set;}="";
    public string Format{get;set;}="";
    public long FileLength{get;set;}
    public string SourceObjectKey{get;set;}="";
    public Guid SourceObjectVersion{get;set;}
    public string MappingJson{get;set;}="";
    public string MappingFingerprint{get;set;}="";
    public string ScanEngine{get;set;}="";
    public string ScanEngineVersion{get;set;}="";
    public string PolicyVersion{get;set;}="";
    public string? ParseErrorCode{get;set;}
    public int RowCount{get;set;}
    public DateTime ExpiresAt{get;set;}
}
public sealed class PatientImportRow:TenantEntity
{
    public Guid BatchId{get;set;}
    public int RowNumber{get;set;}
    public string RawValuesJson{get;set;}="[]";
    public string? DraftJson{get;set;}
    public string ErrorsJson{get;set;}="[]";
    public bool RepeatedFileRow{get;set;}
    public string SourceHash{get;set;}="";
    public string Decision{get;set;}="pending";
    public Guid? PatientId{get;set;}
    public string? ReviewReason{get;set;}
    public Guid? ReviewedBy{get;set;}
    public DateTime? ReviewedAt{get;set;}
    public string? ReviewPolicyVersion{get;set;}
}
