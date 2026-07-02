using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiConversationService _svc;

    public AiController(IAiConversationService svc) => _svc = svc;

    public record DraftRequest(Guid ConversationId, string? Instruction);

    [HttpPost("draft-reply")]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> Draft([FromBody] DraftRequest req, CancellationToken ct) =>
        Ok(await _svc.DraftReplyAsync(req.ConversationId, req.Instruction, ct));

    [HttpPost("summarize/{conversationId:guid}")]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> Summarize(Guid conversationId, CancellationToken ct) =>
        Ok(await _svc.SummarizeAsync(conversationId, ct));
}
