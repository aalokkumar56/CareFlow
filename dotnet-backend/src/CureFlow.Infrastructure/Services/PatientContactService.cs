using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientContactService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IPatientContactService
{
    private async Task RequireAsync(ICureFlowDbSession store,Guid patientId,string capability,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "Id"=@id AND "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId.Value},ct:ct);
        if(actor is null||!(await permissions.GetPermissionCodesAsync(actor,ct,store)).Contains(capability,StringComparer.Ordinal))throw new ForbiddenException();
        _=await store.QueryFirstOrDefaultAsync<Guid?>("""
            SELECT "Id" FROM "Patients" WHERE "Id"=@patientId AND "TenantId"=@TenantId AND NOT "IsDeleted" AND (NOT @needActive OR "IsActive") FOR SHARE
            """,new{patientId,needActive=capability==CureFlowPermissions.PatientEdit},ct:ct)??throw new NotFoundException("Patient");
    }
    public static void Validate(PatientContactInput input)
    {
        if(input.ContactType is not ("phone" or "email" or "person")||input.Usage is not ("normal" or "emergency" or "guardian")
           ||string.IsNullOrWhiteSpace(input.DisplayName)||input.DisplayName.Length>200||input.DisplayName.Any(char.IsControl)
           ||input.Value?.Length>254||input.Value?.Any(char.IsControl)==true||input.Relationship?.Length>100||input.Relationship?.Any(char.IsControl)==true
           ||input.SourceReference?.Length>200||input.SourceReference?.Any(char.IsControl)==true
           ||input.EffectiveFrom.Kind!=DateTimeKind.Utc||input.EffectiveUntil.HasValue&&(input.EffectiveUntil.Value.Kind!=DateTimeKind.Utc||input.EffectiveUntil<=input.EffectiveFrom))
            throw new ValidationException("Supply bounded local contact fields and a UTC effective interval.");
        if(input.ContactType=="email"&&(!MailAddress.TryCreate(input.Value,out var email)||email.Address!=input.Value))throw new ValidationException("Supply an email address.");
        if(input.ContactType=="phone"&&(string.IsNullOrWhiteSpace(input.Value)||input.Value.Length>20||input.Value.Count(char.IsAsciiDigit)<5||input.Value.Any(c=>!char.IsAsciiDigit(c)&&c!='+')||input.Value.LastIndexOf('+')>0))
            throw new ValidationException("Supply a bounded local phone number.");
        if(input.ContactType=="person"&&!string.IsNullOrWhiteSpace(input.Value))throw new ValidationException("Named contacts have no channel value.");
        if(input.Usage=="guardian"&&string.IsNullOrWhiteSpace(input.Relationship))throw new ValidationException("Record the local reported guardian relationship.");
    }
    public async Task<IReadOnlyList<PatientContactView>> ListAsync(Guid patientId,int limit,CancellationToken ct)
    {
        await RequireAsync(db,patientId,CureFlowPermissions.PatientView,ct);
        var rows=await db.QueryAsync<PatientContactVersion>("""
            SELECT * FROM (SELECT DISTINCT ON ("ContactId") * FROM "PatientContactVersions"
                WHERE "TenantId"=@TenantId AND "PatientId"=@patientId ORDER BY "ContactId","Revision" DESC) latest
            ORDER BY "ContactId" LIMIT @limit
            """,new{patientId,limit=Math.Clamp(limit,1,100)},ct:ct);
        await audit.RecordAsync(new(patientId,"PatientContact",null,"read","authorized",Purpose:"registration"),ct);
        var now=DateTime.UtcNow;
        return rows.Select(r=>new PatientContactView(r.ContactId,r.Revision,r.ContactType,r.Usage,r.DisplayName,r.Value,r.Relationship,r.SourceReference,r.VerificationStatus,r.Preferred,r.ContactIsActive,r.EffectiveFrom,r.EffectiveUntil,r.ContactIsActive&&r.EffectiveFrom<=now&&(!r.EffectiveUntil.HasValue||r.EffectiveUntil>now))).ToList();
    }
    public Task<IdempotentCommandResult> SaveAsync(Guid patientId,Guid? contactId,PatientContactInput input,string key,CancellationToken ct)
    {
        Validate(input);
        if(contactId==Guid.Empty)throw new ValidationException("Supply a valid contact identity.");
        if(contactId.HasValue&&input.ExpectedRevision is not >0)throw new DomainException("Supply the current contact revision.",428);
        if(!contactId.HasValue&&input.ExpectedRevision.HasValue)throw new ValidationException("New contacts have no prior revision.");
        var horizon=config.GetValue<int?>("PatientContacts:ApprovedRetryHorizonHours");
        if(horizon is not >0 or >8760)throw new DomainException("Contact retry policy is not configured.",503);
        return commands.ExecuteAsync("patient.contact.save",key,new{patientId,contactId,input},TimeSpan.FromHours(horizon.Value),
            (store,token)=>RequireAsync(store,patientId,CureFlowPermissions.PatientEdit,token),async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=$"patient-contacts:{tenant.TenantId:D}:{patientId:D}"},ct:token);
                var id=contactId??Guid.NewGuid();
                var prior=await store.QueryFirstOrDefaultAsync<PatientContactVersion>("""
                    SELECT * FROM "PatientContactVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId AND "ContactId"=@id ORDER BY "Revision" DESC LIMIT 1
                    """,new{patientId,id},ct:token);
                if(contactId.HasValue&&prior is null)throw new NotFoundException("Patient contact");
                if(prior is not null&&prior.Revision!=input.ExpectedRevision)throw new DomainException("Contact changed; reload.",412);
                var row=new PatientContactVersion{PatientId=patientId,ContactId=id,Revision=checked((prior?.Revision??0)+1),ContactType=input.ContactType,Usage=input.Usage,
                    DisplayName=input.DisplayName.Trim(),Value=input.Value?.Trim(),Relationship=input.Relationship?.Trim(),SourceReference=input.SourceReference?.Trim(),
                    Preferred=input.Preferred,ContactIsActive=input.IsActive,EffectiveFrom=input.EffectiveFrom,EffectiveUntil=input.EffectiveUntil};
                // User-provided local relationship/evidence is never treated as identity verification or proxy authority.
                await store.InsertAsync(row,ct:token);
                await audit.RecordAsync(new(patientId,"PatientContact",id,prior is null?"create":"update","committed",row.Revision.ToString(),"registration"),token,store);
                return(new CommandReceipt(id,row.Revision,prior is null?CommandOutcome.Created:CommandOutcome.Updated),prior is null?201:200);
            },ct);
    }
}