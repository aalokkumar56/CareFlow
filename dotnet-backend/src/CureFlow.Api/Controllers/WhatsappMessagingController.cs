using System.IO;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Authorize]
public class WhatsappMessagingController : ControllerBase
{
    private readonly IWhatsappApiService _waApi;
    private readonly ILogger<WhatsappMessagingController> _logger;
    private readonly WhatsappOptions _whatsappOptions;
    private readonly IWebHostEnvironment _environment;

    public WhatsappMessagingController(
        IWhatsappApiService waApi,
        ILogger<WhatsappMessagingController> logger,
        IOptions<WhatsappOptions> whatsappOptions,
        IWebHostEnvironment environment)
    {
        _waApi = waApi;
        _logger = logger;
        _whatsappOptions = whatsappOptions.Value;
        _environment = environment;
    }
    [HttpPost("sendmessage")]
    [Authorize(Policy = "Permission:WhatsApp.Send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Phone and message are required" });

        var result = await _waApi.SendMessageAsync(request, ct);
        if (!result.Success)
            return BadRequest(new { error = result.Error ?? "Failed to send WhatsApp message", success = false, phone = result.Phone });

        return Ok(result);
    }

    [HttpPost("sendtemplatemessage")]
    [Authorize(Policy = "Permission:WhatsApp.Send")]
    public async Task<IActionResult> SendTemplateMessage([FromBody] SendTemplateMessageRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.TemplateName))
            return BadRequest(new { error = "Phone and template name are required" });

        var result = await _waApi.SendTemplateMessageAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("sendmedia")]
    [Authorize(Policy = "Permission:WhatsApp.Send")]
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> SendMedia(CancellationToken ct)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var phone = form["phone"].ToString();
            var mediaType = form["mediaType"].ToString();
            if (string.IsNullOrWhiteSpace(phone) && form.ContainsKey("phone"))
                phone = form["phone"].ToString();
            if (string.IsNullOrWhiteSpace(mediaType))
                mediaType = form["mediaType"].ToString();
            var mediaUrl = form["mediaUrl"].ToString();
            var caption = form["caption"].ToString();
            var mediaFile = form.Files.GetFile("mediaFile");

            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(mediaType))
                return BadRequest(new { error = "Phone and media type are required" });
            if (mediaFile == null && string.IsNullOrWhiteSpace(mediaUrl))
                return BadRequest(new { error = "Media file or URL is required" });

            if (mediaFile != null)
            {
                using var stream = mediaFile.OpenReadStream();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, ct);
                return Ok(await _waApi.SendMediaFileAsync(
                    phone, mediaType, memoryStream.ToArray(),
                    mediaFile.FileName, mediaFile.ContentType, caption, ct));
            }

            return Ok(await _waApi.SendMediaAsync(new SendMediaRequest
            {
                Phone = phone,
                MediaType = mediaType,
                MediaUrl = mediaUrl ?? string.Empty,
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption
            }, ct));
        }

        var request = await Request.ReadFromJsonAsync<SendMediaRequest>(cancellationToken: ct);
        if (request == null || string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.MediaType) || string.IsNullOrWhiteSpace(request.MediaUrl))
            return BadRequest(new { error = "Phone, media type, and media URL are required" });

        return Ok(await _waApi.SendMediaAsync(request, ct));
    }

    [HttpPost("sendcampaigns")]
    [Authorize(Policy = "Permission:Campaign.Manage")]
    public async Task<IActionResult> SendCampaign([FromBody] SendCampaignRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.CampaignId <= 0)
            return BadRequest(new { error = "Valid campaign ID is required" });

        var result = await _waApi.SendCampaignAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("makecontact")]
    [Authorize(Policy = "Permission:WhatsApp.Manage")]
    public async Task<IActionResult> MakeContact([FromBody] MakeContactRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Phone and name are required" });

        var result = await _waApi.MakeContactAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("media/{fileName}")]
    [Authorize(Policy = "Permission:WhatsApp.View")]
    public IActionResult GetHostedMedia(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal))
            return BadRequest(new { error = "Invalid file name" });

        var storagePath = string.IsNullOrWhiteSpace(_whatsappOptions.RelayMediaStoragePath)
            ? "webhook-media"
            : _whatsappOptions.RelayMediaStoragePath.Trim('/');
        var absolutePath = Path.Combine(_environment.ContentRootPath, storagePath, fileName);
        if (!System.IO.File.Exists(absolutePath))
            return NotFound();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream",
        };

        return PhysicalFile(absolutePath, contentType, enableRangeProcessing: true);
    }

    public class SendMediaFormRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string? MediaUrl { get; set; }
        public string? Caption { get; set; }
        public IFormFile? MediaFile { get; set; }
    }
}
