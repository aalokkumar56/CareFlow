using System.Net;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.External;

/// <summary>
/// Fetches a hospital website's homepage + key sub-pages and returns aggregated stripped text
/// ready to feed into the AI extractor.
/// </summary>
public class HospitalWebsiteScraper : IHospitalWebsiteScraper
{
    private static readonly string[] _candidatePaths =
        { "", "/about-us/", "/about/", "/services/", "/contact-us/", "/contact/", "/faqs/", "/faq/", "/team/", "/doctors/", "/departments/" };

    private readonly HttpClient _http;
    private readonly ILogger<HospitalWebsiteScraper> _logger;

    public HospitalWebsiteScraper(HttpClient http, ILogger<HospitalWebsiteScraper> logger)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; CureFlow/1.0)");
        _logger = logger;
    }

    public async Task<string> FetchPagesAsync(string baseUrl, CancellationToken ct = default)
    {
        if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            baseUrl = "https://" + baseUrl;

        // User-supplied URL — reject private/loopback/metadata targets (SSRF).
        var validation = await SafeRemoteUrl.ValidatePublicHttpsAsync(baseUrl, Dns.GetHostAddressesAsync, ct);
        if (!validation.IsAllowed || validation.Uri is null)
            throw new ValidationException(validation.Reason ?? "URL is not allowed.");

        baseUrl = validation.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');

        var seen = new HashSet<string>();
        var parts = new List<string>();

        foreach (var p in _candidatePaths)
        {
            var url = string.IsNullOrEmpty(p) ? baseUrl : baseUrl + p;
            try
            {
                var html = await _http.GetStringAsync(url, ct);
                var text = StripHtml(html);
                if (!string.IsNullOrWhiteSpace(text) && seen.Add(text.GetHashCode().ToString()))
                {
                    parts.Add($"--- {url} ---\n{text[..Math.Min(5000, text.Length)]}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to fetch {url}", url);
            }
        }

        var combined = string.Join("\n\n", parts);
        return combined.Length > 25000 ? combined[..25000] : combined;
    }

    private static string StripHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var n in doc.DocumentNode.SelectNodes("//script|//style")?.ToList() ?? new List<HtmlNode>())
            n.Remove();
        var text = doc.DocumentNode.InnerText;
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }
}
