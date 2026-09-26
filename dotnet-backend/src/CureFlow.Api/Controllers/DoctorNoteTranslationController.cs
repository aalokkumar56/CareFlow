using System.Net;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/clinical/notes")]
[Authorize(Policy = "Permission:Clinical.Translate")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DoctorNoteTranslationController(
    IClinicalRecordService notes, IHttpClientFactory clients, IConfiguration configuration) : ControllerBase
{
    public record TranslationRequest(string Language, int Revision);

    // This deliberately exposes only availability. The Google API key stays on
    // the server and is never sent to a browser.
    [HttpGet("translation/status")]
    public IActionResult Status()
        => Ok(new { available = !string.IsNullOrWhiteSpace(configuration["ClinicalTranslation:GoogleApiKey"]) });

    [HttpPost("{id:guid}/translation")]
    public async Task<IActionResult> Translate(Guid id, TranslationRequest request, CancellationToken ct)
    {
        DoctorNoteRules.ValidateLanguage(request.Language);
        var note = await notes.GetNoteAsync(id, ct);
        if (note.FinalizedAt == null)
            throw new ValidationException("Finish the note before translating it.");
        if (request.Revision != note.Revision)
            throw new ConflictException("The original note changed. Reload it before translating.");
        var text = string.Join("\n\n", new[] { note.Subjective, note.Objective, note.Assessment, note.Plan }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (request.Language == note.OriginalLanguage)
            return Ok(new { text, language = request.Language, revision = note.Revision });
        var key = configuration["ClinicalTranslation:GoogleApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            return StatusCode(503, new { message = "Translation has not been configured. Ask your administrator." });
        try
        {
            using var client = clients.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(12);
            using var outgoing = new HttpRequestMessage(HttpMethod.Post,
                "https://translation.googleapis.com/language/translate/v2");
            outgoing.Headers.Add("X-Goog-Api-Key", key);
            var payload = new Dictionary<string, string>
            {
                ["q"] = text, ["target"] = request.Language.Split('-')[0], ["format"] = "text",
            };
            if (note.OriginalLanguage != "und") payload["source"] = note.OriginalLanguage.Split('-')[0];
            outgoing.Content = JsonContent.Create(payload);
            using var response = await client.SendAsync(outgoing, ct);
            if (!response.IsSuccessStatusCode)
                return StatusCode(503, new { message = "Translation is temporarily unavailable. The original is unchanged." });
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var translated = json.RootElement.GetProperty("data").GetProperty("translations")[0]
                .GetProperty("translatedText").GetString();
            if (string.IsNullOrWhiteSpace(translated))
                return StatusCode(503, new { message = "No translation was returned." });
            return Ok(new { text = WebUtility.HtmlDecode(translated), language = request.Language, revision = note.Revision });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return StatusCode(503, new { message = "Translation failed. The original is unchanged." });
        }
    }
}
