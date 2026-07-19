using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.External;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsappWebhookController : ControllerBase
{
    private readonly IWhatsappService _wa;
    private readonly WhatsappOptions _options;
    private readonly ILogger<WhatsappWebhookController> _logger;

    public WhatsappWebhookController(
        IWhatsappService wa,
        IOptions<WhatsappOptions> options,
        ILogger<WhatsappWebhookController> logger)
    {
        _wa = wa;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("webhook")]
    [AllowAnonymous]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (_wa.VerifyWebhookSubscription(mode ?? string.Empty, token ?? string.Empty, out _, challenge))
            return Content(challenge ?? string.Empty);

        _logger.LogWarning("Invalid WhatsApp webhook verification request");
        return Forbid();
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var sigHeader = Request.Headers["X-Hub-Signature-256"].ToString();
        var sig = string.IsNullOrWhiteSpace(sigHeader) ? null : sigHeader;

        _logger.LogInformation("WhatsApp webhook received ({Length} bytes): {Preview}",
            body.Length,
            body.Length <= 500 ? body : body[..500] + "...");

        // Gateway check: when AppSecret is configured, reject before any processing.
        if (!WhatsappWebhookProcessor.TryVerifyWebhookSignature(body, sig, _options.AppSecret, _logger))
        {
            _logger.LogWarning("Rejecting WhatsApp webhook due to missing or invalid signature");
            return Unauthorized(new { ok = false, error = "invalid_signature" });
        }

        await _wa.ProcessIncomingWebhookAsync(body, sig, ct);
        return Ok(new { ok = true });
    }
}
