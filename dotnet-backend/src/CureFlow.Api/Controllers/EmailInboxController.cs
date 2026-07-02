using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/email")]
public class EmailInboxController : ControllerBase
{
    private readonly IEmailInboxService _inbox;
    private readonly IEmailService _email;

    public EmailInboxController(IEmailInboxService inbox, IEmailService email)
    {
        _inbox = inbox;
        _email = email;
    }

    public record SendEmailRequest(Guid PatientId, string Subject, string Body);

    [HttpGet("threads")]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> ListThreads([FromQuery] string? q, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var status = await _email.GetStatusAsync(ct);
        var threads = await _inbox.ListThreadsAsync(q, limit, ct);
        return Ok(new { status, threads });
    }

    [HttpGet("threads/{patientId:guid}")]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> GetThread(Guid patientId, CancellationToken ct)
    {
        var thread = await _inbox.GetThreadAsync(patientId, ct);
        return Ok(thread);
    }

    [HttpPost("send")]
    [Authorize(Policy = "Permission:Conversation.Manage")]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest req, CancellationToken ct)
    {
        var (ok, messageId, error) = await _email.SendToPatientAsync(req.PatientId, req.Subject, req.Body, ct);
        if (!ok) return BadRequest(new { detail = error });
        return Ok(new { ok = true, message_id = messageId });
    }
}
