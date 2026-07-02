using System.IO;
using System.Text.Json.Serialization;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private const long MaxUploadBytes = 100L * 1024 * 1024;

    private readonly IConversationService _svc;
    private readonly WhatsappMediaOptions _mediaOptions;

    public ConversationsController(IConversationService svc, IOptions<WhatsappMediaOptions> mediaOptions)
    {
        _svc = svc;
        _mediaOptions = mediaOptions.Value;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = Pagination.DefaultPageSize,
        CancellationToken ct = default) =>
        Ok(await _svc.ListAsync(q, page, page_size, ct));

    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> GetByPatient(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.GetOrCreateByPatientIdAsync(patientId, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _svc.GetAsync(id, ct));

    public record ConversationSendMessageRequest(
        [property: JsonPropertyName("conversation_id")] Guid ConversationId,
        string Body);

    [HttpPost("messages")]
    [Authorize(Policy = "Permission:Conversation.Manage")]
    public async Task<IActionResult> Send([FromBody] ConversationSendMessageRequest req, CancellationToken ct)
    {
        if (req.ConversationId == Guid.Empty)
            return BadRequest(new { error = "conversation_id is required" });

        if (string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "body is required" });

        var id = await _svc.SendMessageAsync(req.ConversationId, req.Body.Trim(), ct);
        return Ok(new { id });
    }

    [HttpPost("{id:guid}/media")]
    [Authorize(Policy = "Permission:Conversation.Manage")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> SendMedia(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "conversation id is required" });

        if (!Request.HasFormContentType)
            return BadRequest(new { error = "Request must be multipart/form-data" });

        var form = await Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.GetFile("media") ?? form.Files.FirstOrDefault();
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "A non-empty file is required" });

        var caption = form["caption"].ToString();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

        var validation = WhatsappMediaPolicy.Validate(contentType, file.Length, _mediaOptions);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Error });

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        await using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, ct);
            bytes = memory.ToArray();
        }

        var messageId = await _svc.SendMediaAsync(
            id,
            bytes,
            file.FileName,
            contentType,
            string.IsNullOrWhiteSpace(caption) ? null : caption,
            ct);

        return Ok(new { id = messageId });
    }

    [HttpGet("messages/{messageId:guid}/media")]
    [Authorize(Policy = "Permission:Conversation.View")]
    public async Task<IActionResult> GetMessageMedia(Guid messageId, CancellationToken ct)
    {
        var media = await _svc.GetMessageMediaAsync(messageId, ct);
        if (media == null)
            return NotFound();

        // Prevent MIME sniffing so the browser cannot reinterpret stored media (e.g. HTML/SVG) as
        // an executable document type when the endpoint is opened directly.
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return PhysicalFile(
            media.AbsolutePath,
            media.ContentType,
            string.IsNullOrWhiteSpace(media.FileName) ? null : media.FileName,
            enableRangeProcessing: true);
    }

    public record AssignRequest(Guid StaffId);

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = "Permission:Conversation.Manage")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest r, CancellationToken ct)
    {
        await _svc.AssignAsync(id, r.StaffId, ct);
        return Ok(new { ok = true });
    }

    public record NoteRequest(string Body);

    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = "Permission:Conversation.Manage")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] NoteRequest r, CancellationToken ct)
    {
        var nid = await _svc.AddNoteAsync(id, r.Body, ct);
        return Ok(new { id = nid });
    }
}
