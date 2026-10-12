using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patients/identity-reviews")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientIdentityReviewsController(IPatientIdentityReviewService reviews):ControllerBase
{
    [HttpGet("{reviewId:guid}"),Authorize(Policy="Permission:IdentityReview.View")]
    public async Task<IActionResult> Get(Guid reviewId,CancellationToken ct)=>Ok(await reviews.GetAsync(reviewId,ct));
    [HttpPost,Authorize(Policy="Permission:IdentityReview.Manage")]
    public Task<IActionResult> Create(IdentityReviewInput request,CancellationToken ct)=>Save(null,request,ct);
    [HttpPut("{reviewId:guid}"),Authorize(Policy="Permission:IdentityReview.Manage")]
    public Task<IActionResult> Update(Guid reviewId,IdentityReviewInput request,CancellationToken ct)=>Save(reviewId,request,ct);
    private async Task<IActionResult> Save(Guid? reviewId,IdentityReviewInput input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await reviews.SaveAsync(reviewId,input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}