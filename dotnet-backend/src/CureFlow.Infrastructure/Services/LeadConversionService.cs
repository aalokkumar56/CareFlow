using System.Security.Cryptography;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class LeadConversionService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IPatientIdentifierService identifiers,IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):ILeadConversionService
{
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static string Tag(Lead lead)=>Hash(lead);
    private async Task RequireAsync(ICureFlowDbSession store,CancellationToken ct,bool review=false)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@actor AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{actor=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        foreach(var code in new[]{"Lead.View","Lead.Convert",CureFlowPermissions.PatientView,CureFlowPermissions.PatientCreate,CureFlowPermissions.IdentityReviewView})
            if(!codes.Contains(code,StringComparer.Ordinal))throw new ForbiddenException();
        if(review&&!codes.Contains(CureFlowPermissions.IdentityReviewManage,StringComparer.Ordinal))throw new ForbiddenException();
        if(await store.QueryFirstOrDefaultAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "PatientConfidentialityStates" s WHERE s."TenantId"=@TenantId AND s."Restricted"
              AND NOT public.cureflow_patient_read_allowed(s."TenantId",s."PatientId"))
            """,ct:ct))throw new ForbiddenException();
    }
    private async Task<Lead> LeadAsync(ICureFlowDbSession store,Guid id,bool update,CancellationToken ct)
        =>await store.QueryFirstOrDefaultAsync<Lead>($"""
            SELECT * FROM "Leads" WHERE "TenantId"=@TenantId AND "Id"=@id AND NOT "IsDeleted" {(update?"FOR UPDATE":"FOR SHARE")}
            """,new{id},ct:ct)??throw new NotFoundException("Lead");
    private async Task<LeadConversionPreview> PreviewCoreAsync(ICureFlowDbSession store,Lead lead,CancellationToken ct)
    {
        await RequireAsync(store,ct);
        var digits=new string(lead.Phone.Where(char.IsAsciiDigit).ToArray());
        var candidates=(await store.QueryAsync<Patient>("""
            SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted"
              AND ((@name<>'' AND lower(btrim("Name"))=lower(@name)) OR (@digits<>'' AND regexp_replace("Phone",'[^0-9]','','g')=@digits))
              ORDER BY "Id" LIMIT 6 FOR SHARE
            """,new{name=lead.Name?.Trim()??"",digits},ct:ct)).ToList();
        if(candidates.Count>5)throw new ConflictException("Review the identity candidate scope before conversion.");
        foreach(var candidate in candidates)await audit.RecordAsync(new(candidate.Id,"LeadConversionCandidate",lead.Id,"read","authorized",null,"registration"),ct,store);
        var projections=candidates.Select(p=>new LeadConversionCandidate(p.Id,p.Name,p.Phone,PatientConcurrency.Tag(p))).ToArray();
        return new(lead.Id,Tag(lead),projections,Hash(new{LeadTag=Tag(lead),Candidates=projections.Select(p=>new{p.PatientId,p.PatientTag}).ToArray()}));
    }
    public async Task<LeadConversionPreview> PreviewAsync(Guid leadId,CancellationToken ct)
    {
        LeadConversionPreview? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,ct);var lead=await LeadAsync(store,leadId,false,ct);
            if(!lead.IsActive||lead.ConvertedAt.HasValue)throw new ConflictException("Lead already converted or inactive; use retained conversion history.");
            result=await PreviewCoreAsync(store,lead,ct);
            await audit.RecordAsync(new(null,"LeadConversion",leadId,"preview","authorized",null,"registration"),ct,store);
        },ct);return result!;
    }
    public Task<IdempotentCommandResult> ConvertAsync(Guid leadId,LeadConversionInput input,string key,CancellationToken ct)
    {
        var policy=config["LeadConversions:ApprovedPolicyVersion"];var hours=config.GetValue<int?>("LeadConversions:ApprovedRetryHorizonHours");
        if(string.IsNullOrWhiteSpace(policy)||policy.Length>100||policy.Any(char.IsControl)||hours is not >0 or >8760)
            throw new DomainException("The accountable lead identity-conversion policy/retry horizon is not configured.",503);
        if(input.FacilityId==Guid.Empty||input.Decision is not ("create_new" or "link_existing")
            ||(input.Decision=="link_existing")!=input.PatientId.HasValue||input.PatientId==Guid.Empty
            ||string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Length>500||input.Reason.Any(char.IsControl)
            ||string.IsNullOrWhiteSpace(input.EvidenceReference)||input.EvidenceReference.Length>200||input.EvidenceReference.Any(char.IsControl)
            ||input.ReferralSource is not null&&(input.ReferralSource.Length>200||input.ReferralSource.Any(char.IsControl))
            ||input.ExpectedLeadTag is null||input.ExpectedLeadTag.Length!=64||input.ExpectedLeadTag.Any(c=>!char.IsAsciiHexDigit(c))
            ||input.ExpectedReviewTag is null||input.ExpectedReviewTag.Length!=64||input.ExpectedReviewTag.Any(c=>!char.IsAsciiHexDigit(c)))
            throw new ValidationException("Supply explicit facility, reviewed new/existing identity decision, current evidence tags and bounded source/reason.");
        if(input.PatientId.HasValue)PatientConcurrency.RequireTag(input.PatientId.Value,new[]{input.ExpectedPatientTag});
        return commands.ExecuteAsync("lead.identity-conversion",key,new{leadId,input},TimeSpan.FromHours(hours.Value),
            async(store,token)=>{
                await RequireAsync(store,token);
                var mapping=await store.QueryFirstOrDefaultAsync<LeadConversion>("""
                    SELECT * FROM "LeadConversions" WHERE "TenantId"=@TenantId AND "LeadId"=@leadId
                    """,new{leadId},ct:token);
                if(mapping is not null&&await store.GetByIdAsync<Patient>(mapping.PatientId,ct:token) is null)throw new NotFoundException("Patient");
            },async(store,token)=>{
                var lead=await LeadAsync(store,leadId,true,token);
                if(lead.ConvertedAt.HasValue||!lead.IsActive)throw new ConflictException("This lead already has a retained conversion or is inactive.");
                if(Tag(lead)!=input.ExpectedLeadTag)throw new DomainException("Lead evidence changed; refresh before conversion.",412);
                var preview=await PreviewCoreAsync(store,lead,token);
                if(preview.ReviewTag!=input.ExpectedReviewTag)throw new DomainException("Patient candidate evidence changed; review again.",412);
                var facility=await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@id)",new{id=input.FacilityId},ct:token);
                if(!facility.HasValue)throw new NotFoundException("Active facility");
                if(input.ReferringDoctorId.HasValue)
                {
                    _=await store.QueryFirstOrDefaultAsync<Guid?>("""
                        SELECT "Id" FROM "ReferringDoctors" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                        """,new{id=input.ReferringDoctorId},ct:token)??throw new NotFoundException("Active referring doctor");
                }
                Patient patient;
                if(input.Decision=="link_existing")
                {
                    await RequireAsync(store,token,true);
                    var candidate=preview.Candidates.SingleOrDefault(p=>p.PatientId==input.PatientId);
                    if(candidate is null||candidate.PatientTag!=input.ExpectedPatientTag)throw new ConflictException("Select and review a current identity candidate.");
                    patient=await store.QueryFirstOrDefaultAsync<Patient>("""
                        SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                        """,new{id=input.PatientId},ct:token)??throw new NotFoundException("Patient");
                    if(PatientConcurrency.Tag(patient)!=input.ExpectedPatientTag)throw new DomainException("Patient evidence changed.",412);
                    // Preserve the original patient's demographics, primary SourceLeadId and referring doctor.
                }
                else
                {
                    if(string.IsNullOrWhiteSpace(lead.Name)||lead.Name.Length>200)throw new ConflictException("Record a bounded nonplaceholder lead name before new registration.");
                    if(preview.Candidates.Count>0)
                    {
                        if(!input.AcknowledgePotentialDuplicates)throw new ConflictException("Review possible matches before creating a distinct new patient.");
                        await RequireAsync(store,token,true);
                    }
                    patient=new Patient{Name=lead.Name.Trim(),Phone=lead.Phone,Age=lead.Age,Gender=lead.Gender,
                        BirthDatePrecision="unknown",InquirySource="lead_conversion",SourceLeadId=lead.Id,ReferringDoctorId=input.ReferringDoctorId};
                    await store.ExecuteAsync("""
                        SELECT set_config('app.demographic_source_type','staff_observed',true),
                          set_config('app.demographic_source_reference',@reference,true),set_config('app.demographic_reason',@reason,true)
                        """,new{reference=$"lead:{leadId:N}",reason=input.Reason.Trim()},ct:token);
                    await store.InsertAsync(patient,ct:token);
                    await identifiers.AllocateLocalMrnAsync(store,input.FacilityId,patient.Id,token);
                    var persisted=await store.GetByIdAsync<Patient>(patient.Id,ct:token)??throw new NotFoundException("Patient");
                    foreach(var candidate in preview.Candidates)
                    {
                        var first=string.CompareOrdinal(patient.Id.ToString("N"),candidate.PatientId.ToString("N"))<0?patient.Id:candidate.PatientId;
                        await store.InsertAsync(new PatientIdentityReviewVersion{ReviewId=Guid.NewGuid(),FirstPatientId=first,
                            SecondPatientId=first==patient.Id?candidate.PatientId:patient.Id,Revision=1,Decision="open",
                            EvidenceReference=$"lead:{leadId:N}",Reason=input.Reason.Trim(),
                            FirstPatientTag=first==patient.Id?PatientConcurrency.Tag(persisted):candidate.PatientTag,
                            SecondPatientTag=first==patient.Id?candidate.PatientTag:PatientConcurrency.Tag(persisted),
                            ExactNameSignal=string.Equals(candidate.Name,patient.Name,StringComparison.OrdinalIgnoreCase),
                            SharedPhoneSignal=candidate.Phone==patient.Phone},ct:token);
                    }
                }
                var mapping=new LeadConversion{LeadId=leadId,PatientId=patient.Id,FacilityId=input.FacilityId,Decision=input.Decision,
                    OriginalLeadJson=JsonSerializer.Serialize(lead),SourceLeadTag=input.ExpectedLeadTag,CandidateReviewTag=input.ExpectedReviewTag,
                    SelectedPatientTag=input.ExpectedPatientTag,EvidenceReference=input.EvidenceReference.Trim(),Reason=input.Reason.Trim(),
                    ReferralSource=input.ReferralSource?.Trim(),ReferringDoctorId=input.ReferringDoctorId,PolicyVersion=policy};
                await store.InsertAsync(mapping,ct:token);
                await store.ExecuteAsync("""
                    UPDATE "Leads" SET "ConvertedPatientId"=@patient,"ConvertedAt"=CURRENT_TIMESTAMP,"IsActive"=false,
                      "UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"=@actor WHERE "TenantId"=@TenantId AND "Id"=@leadId
                    """,new{leadId,patient=patient.Id,actor=tenant.UserId},ct:token);
                await audit.RecordAsync(new(patient.Id,"LeadConversion",mapping.Id,input.Decision,"committed",null,"registration"),token,store);
                return(new CommandReceipt(mapping.Id,null,CommandOutcome.Created),201);
            },ct);
    }
    public async Task<LeadConversionView> GetAsync(Guid leadId,CancellationToken ct)
    {
        LeadConversionView? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,ct);
            var row=await store.QueryFirstOrDefaultAsync<LeadConversion>("""
                SELECT * FROM "LeadConversions" WHERE "TenantId"=@TenantId AND "LeadId"=@leadId
                """,new{leadId},ct:ct)??throw new NotFoundException("Lead conversion");
            _=await store.GetByIdAsync<Patient>(row.PatientId,ct:ct)??throw new NotFoundException("Patient");
            result=new(row.Id,row.LeadId,row.PatientId,row.FacilityId,row.Decision,row.CreatedAt,row.CreatedBy,row.EvidenceReference,row.Reason,row.ReferralSource,row.ReferringDoctorId,row.PolicyVersion);
            await audit.RecordAsync(new(row.PatientId,"LeadConversion",row.Id,"read","authorized",null,"registration"),ct,store);
        },ct);return result!;
    }
}