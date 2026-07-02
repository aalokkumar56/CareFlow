using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.External;

/// <summary>
/// Claude Sonnet 4.5 client (Anthropic). Uses Anthropic.SDK NuGet package.
/// Configure via appsettings: Anthropic:ApiKey
/// </summary>
public class ClaudeAiClient : IAiService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeAiClient> _logger;
    private const string Model = AnthropicModels.Claude35Sonnet; // upgrade to Claude 4.5 when SDK supports

    private const string SystemPromptBase =
        "You are CureFlow AI, an assistant for hospital reception/marketing staff. " +
        "You ONLY help organize, draft, summarize, route, and schedule follow-ups. " +
        "You NEVER diagnose, prescribe medication, recommend specific treatments, or give clinical advice. " +
        "If a patient asks medical questions, recommend they speak to a doctor in person. " +
        "Be concise, professional, warm, and use simple English suitable for Indian patients.";

    public ClaudeAiClient(IConfiguration config, ILogger<ClaudeAiClient> logger)
    {
        var apiKey = config["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic:ApiKey missing");
        _client = new AnthropicClient(apiKey);
        _logger = logger;
    }

    public async Task<string> DraftReplyAsync(
        IEnumerable<(string direction, string body)> history,
        string? patientName, string? department, string? instruction,
        CancellationToken ct = default)
    {
        var convoText = string.Join("\n", history.TakeLast(12)
            .Select(m => $"{(m.direction == "inbound" ? "Patient" : "Hospital")}: {m.body}"));

        var prompt =
            $"Conversation so far:\n{convoText}\n\n" +
            $"Patient: {patientName ?? "Unknown"} | Department: {department ?? "—"}\n\n" +
            $"Draft a short polite WhatsApp reply (<60 words). " +
            $"{(instruction != null ? "Special instruction: " + instruction : "")}\n" +
            "Reply ONLY with the message body — no quotes, no preamble.";

        var resp = await SendAsync(SystemPromptBase, prompt, ct);
        return resp.Trim().Trim('"');
    }

    public async Task<(string summary, string urgency, string category, string? suggestedFollowUp)>
        SummarizeAsync(IEnumerable<(string direction, string body)> history, string? patientName, CancellationToken ct = default)
    {
        var convoText = string.Join("\n", history.TakeLast(20)
            .Select(m => $"{(m.direction == "inbound" ? "Patient" : "Hospital")}: {m.body}"));

        var prompt =
            $"Conversation with patient '{patientName ?? "Unknown"}':\n{convoText}\n\n" +
            "Return STRICT JSON: {\"summary\":\"...\",\"urgency\":\"low|medium|high|emergency\"," +
            "\"category\":\"appointment_inquiry|package_inquiry|emergency|reports|follow_up|referral|general\"," +
            "\"suggested_follow_up\":\"24 hours|3 days|1 week|null\"}. JSON ONLY.";

        var json = await SendAsync(SystemPromptBase, prompt, ct);
        var match = System.Text.RegularExpressions.Regex.Match(json, @"\{[\s\S]*\}");
        if (!match.Success) return ("Unable to parse", "medium", "general", null);
        var parsed = JsonDocument.Parse(match.Value).RootElement;
        return (
            parsed.GetProperty("summary").GetString() ?? "",
            parsed.GetProperty("urgency").GetString() ?? "medium",
            parsed.GetProperty("category").GetString() ?? "general",
            parsed.TryGetProperty("suggested_follow_up", out var f) ? f.GetString() : null
        );
    }

    public async Task<(string urgency, string category)> ClassifyMessageAsync(string text, CancellationToken ct = default)
    {
        var prompt =
            $"Message: \"{text}\". Return STRICT JSON: {{\"urgency\":\"low|medium|high|emergency\"," +
            $"\"category\":\"appointment_inquiry|package_inquiry|emergency|reports|follow_up|referral|general\"}}. JSON only.";

        try
        {
            var json = await SendAsync(SystemPromptBase, prompt, ct);
            var match = System.Text.RegularExpressions.Regex.Match(json, @"\{[\s\S]*\}");
            if (!match.Success) return ("medium", "general");
            var parsed = JsonDocument.Parse(match.Value).RootElement;
            return (parsed.GetProperty("urgency").GetString() ?? "medium",
                    parsed.GetProperty("category").GetString() ?? "general");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Classify failed");
            return ("medium", "general");
        }
    }

    public async Task<object> ExtractHospitalProfileFromHtmlAsync(string aggregatedText, CancellationToken ct = default)
    {
        var prompt =
            "Extract a hospital profile as STRICT JSON with keys: name, tagline, about, address, " +
            "phones[], emails[], working_hours, emergency_24x7(bool), departments[{name,description}], " +
            "services[], doctors[{name,role,specialty}], packages[{name,price,description}], " +
            "faqs[{question,answer}]. Empty array if missing. JSON only.\n\nCONTENT:\n" + aggregatedText;

        var json = await SendAsync("You are a precise data extractor. Output only JSON.", prompt, ct);
        var match = System.Text.RegularExpressions.Regex.Match(json, @"\{[\s\S]*\}");
        if (!match.Success) throw new ValidationException("AI could not extract hospital profile from the website. Try a different URL or edit manually.");
        return JsonSerializer.Deserialize<JsonElement>(match.Value);
    }

    private async Task<string> SendAsync(string system, string user, CancellationToken ct)
    {
        try
        {
            var req = new MessageParameters
            {
                Model = Model,
                MaxTokens = 1024,
                Temperature = 0.4m,
                System = new List<SystemMessage> { new SystemMessage(system) },
                Messages = new List<Anthropic.SDK.Messaging.Message>
                {
                    new() { Role = RoleType.User, Content = new List<ContentBase> { new TextContent { Text = user } } }
                }
            };
            var resp = await _client.Messages.GetClaudeMessageAsync(req, ctx: ct);
            return resp.Message.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic API call failed");
            throw new ValidationException("AI service is unavailable. Check Anthropic API key configuration.");
        }
    }
}
