using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
internal static class EncounterCommandHeaders
{
 internal static string Key(ControllerBase controller)
 {
  var keys=controller.Request.Headers["Idempotency-Key"];
  if(keys.Count!=1||string.IsNullOrWhiteSpace(keys[0]))throw new ValidationException("Supply one Idempotency-Key.");
  return keys[0]!;
 }
 internal static long Revision(ControllerBase controller,string aggregate,Guid id,long? bodyRevision)
 {
  var tags=controller.Request.Headers.IfMatch;
  if(tags.Count!=1||string.IsNullOrWhiteSpace(tags[0]))throw new DomainException("Supply the current If-Match version.",428);
  var tag=tags[0]!;var prefix=$"\"{aggregate}:{id:D}:";
  if(!tag.StartsWith(prefix,StringComparison.Ordinal)||!tag.EndsWith('"')||!long.TryParse(tag[prefix.Length..^1],out var revision)
   ||revision<1||bodyRevision.HasValue&&bodyRevision!=revision)throw new DomainException("Use the current record ETag and matching revision.",412);
  return revision;
 }
 internal static IActionResult Result(ControllerBase controller,IdempotentCommandResult result)
 {
  controller.Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
  return controller.StatusCode(result.StatusCode,result.Receipt);
 }
}
