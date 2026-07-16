using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.External;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CureFlow.Api.Controllers;

/// <summary>
/// Development helper — simulates an inbound WhatsApp webhook without Meta/WhatsBiz.
/// </summary>
[ApiController]
[Route("api/whatsapp/demo")]
public class WhatsappDemoController : ControllerBase
{
    private readonly IWhatsappService _wa;
    private readonly IWhatsAppSettingsService _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WhatsappOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WhatsappDemoController> _logger;

    public WhatsappDemoController(
        IWhatsappService wa,
        IWhatsAppSettingsService settings,
        IHttpClientFactory httpClientFactory,
        IOptions<WhatsappOptions> options,
        IWebHostEnvironment env,
        ILogger<WhatsappDemoController> logger)
    {
        _wa = wa;
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// POST body: { "message": "Hello", "phone": "7600174070" }
    /// Phone defaults to the shared test handset when omitted.
    /// </summary>
    [HttpPost("inbound")]
    [AllowAnonymous]
    public async Task<IActionResult> SimulateInbound([FromBody] DemoInboundRequest request, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required" });

        var phone = WhatsappPhoneHelper.Normalize(request.Phone ?? WhatsappPhoneHelper.TestPhoneLocal);
        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { error = "invalid phone" });

        var payload = JsonSerializer.Serialize(new
        {
            @event = "message_received",
            phone_number_id = request.PhoneNumberId,
            from = $"+{phone}",
            message_id = $"demo-{Guid.NewGuid():N}",
            type = "text",
            text = new { body = request.Message.Trim() },
            contact = new { name = "Test User", wa_id = phone },
        });

        _logger.LogInformation("Simulating inbound WhatsApp from {Phone}: {Preview}", phone, request.Message.Trim());
        await _wa.ProcessIncomingWebhookAsync(payload, signatureHeader: null, ct);

        return Ok(new
        {
            ok = true,
            phone,
            message = request.Message.Trim(),
            hint = "Open WhatsApp Inbox — the conversation should appear within ~10s (auto-refresh).",
        });
    }

    /// <summary>
    /// Meta Cloud API phone registration (dev only).
    /// See https://developers.facebook.com/docs/whatsapp/cloud-api/reference/registration
    /// </summary>
    [HttpPost("register-phone")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterPhone([FromBody] RegisterPhoneRequest? request, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var runtime = await _settings.GetAsync(ct);
        var phoneNumberId = request?.PhoneNumberId ?? runtime.PhoneNumberId;
        var wabaId = request?.WabaId ?? runtime.WabaId;
        var pin = request?.Pin ?? "760017";
        var token = runtime.AccessToken;

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(wabaId))
            return BadRequest(new { error = "phone_number_id and waba_id are required in settings or request body" });
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Meta access_token is not saved in WhatsApp settings" });
        if (pin.Length != 6 || !pin.All(char.IsDigit))
            return BadRequest(new { error = "pin must be a 6-digit number" });

        var version = string.IsNullOrWhiteSpace(_options.MetaGraphApiVersion) ? "v21.0" : _options.MetaGraphApiVersion.Trim('/');
        var baseUrl = (_options.MetaGraphApiBaseUrl ?? "https://graph.facebook.com").TrimEnd('/');

        var registerPayload = JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            pin,
            data_localization_region = request?.DataLocalizationRegion ?? "IN",
        });

        var registerUrl = $"{baseUrl}/{version}/{phoneNumberId}/register";
        var registerResponse = await PostGraphAsync(registerUrl, registerPayload, token, ct);

        JsonElement? subscribeResponse = null;
        if (request?.SubscribeWaba != false)
        {
            var subscribeUrl = $"{baseUrl}/{version}/{wabaId}/subscribed_apps";
            subscribeResponse = await PostGraphAsync(subscribeUrl, "{}", token, ct);
        }

        var phoneStatusUrl =
            $"{baseUrl}/{version}/{phoneNumberId}?fields=display_phone_number,verified_name,status,code_verification_status,platform_type,name_status";
        var phoneStatus = await GetGraphAsync(phoneStatusUrl, token, ct);

        JsonElement? healthStatus = null;
        if (!string.IsNullOrWhiteSpace(wabaId))
        {
            var healthUrl = $"{baseUrl}/{version}/{wabaId}?fields=health_status";
            healthStatus = await GetGraphAsync(healthUrl, token, ct);
        }

        return Ok(new
        {
            register = registerResponse,
            subscribe_waba = subscribeResponse,
            phone_status = phoneStatus,
            waba_health = healthStatus,
            phone_number_id = phoneNumberId,
            waba_id = wabaId,
        });
    }

    private async Task<JsonElement> PostGraphAsync(string url, string jsonBody, string token, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("Graph POST {Url} -> {Status}: {Body}", url, (int)response.StatusCode, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private async Task<JsonElement> GetGraphAsync(string url, string token, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("Graph GET {Url} -> {Status}: {Body}", url, (int)response.StatusCode, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    public sealed record DemoInboundRequest(string Message, string? Phone = null, string? PhoneNumberId = null);

    public sealed record RegisterPhoneRequest(
        string? PhoneNumberId,
        string? WabaId,
        string? Pin,
        string? DataLocalizationRegion,
        bool SubscribeWaba = true);
}
