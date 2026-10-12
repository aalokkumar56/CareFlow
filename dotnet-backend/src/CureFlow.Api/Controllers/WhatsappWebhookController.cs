using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsappWebhookController : ControllerBase
{
    private readonly IWhatsappService _wa;
    private readonly ILogger<WhatsappWebhookController> _logger;

    public WhatsappWebhookController(
        IWhatsappService wa,
        ILogger<WhatsappWebhookController> logger)
    {
        _wa = wa;
        _logger = logger;
    }

    [HttpGet("webhook")]
    [HttpGet("~/api/webhooks/whatsapp")]
    [AllowAnonymous]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (_wa.VerifyWebhookSubscription(mode ?? string.Empty, token ?? string.Empty, out _, challenge))
            return Content(challenge ?? string.Empty);

        _logger.LogWarning("Invalid WhatsApp webhook verification request");
        return Unauthorized();
    }

    [HttpPost("webhook")]
    [HttpPost("~/api/webhooks/whatsapp")]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var sig = Request.Headers["X-Hub-Signature-256"].ToString();

        // BUG-09: log only metadata (byte count), never body content which may contain PII
        _logger.LogDebug("WhatsApp webhook received ({Length} bytes)", body.Length);
        _logger.LogInformation("WhatsApp webhook received ({Length} bytes)", body.Length);

        await _wa.ProcessIncomingWebhookAsync(body, sig, ct);
        return Ok(new { ok = true });
    }
}
