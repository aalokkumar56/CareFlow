using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patients")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientIdentityController(IPatientIdentityVerificationService identities):ControllerBase
{
    [HttpPost("unknown-intake"),Authorize(Policy="Permission:Patient.Create")]
    public async Task<IActionResult> Unknown(UnknownPatientRegistrationInput input,CancellationToken ct)
        =>Receipt(await identities.RegisterUnknownAsync(input,Key(),ct));
    [HttpGet("{patientId:guid}/identity-status"),Authorize(Policy="Permission:Patient.View")]
    public async Task<IActionResult> Status(Guid patientId,CancellationToken ct)=>Ok(await identities.GetAsync(patientId,ct));
    [HttpPost("{patientId:guid}/verify-identity"),Authorize(Policy="Permission:PatientIdentity.Verify")]
    public async Task<IActionResult> Verify(Guid patientId,PatientIdentityVerificationInput input,CancellationToken ct)
        =>Receipt(await identities.VerifyAsync(patientId,input,Key(),ct));
    private string Key()
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        return key[0]!;
    }
    private IActionResult Receipt(IdempotentCommandResult result)
    {
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}
