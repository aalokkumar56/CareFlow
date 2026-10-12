using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CureFlow.Api.Filters;

/// <summary>Protects delivery without altering retained source text or file bytes.</summary>
public sealed class ContentDeliverySafetyFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var response = context.HttpContext.Response;
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";

        if (context.Result is FileResult file)
        {
            // Every stored file is an attachment. Supplied MIME metadata does not authorize
            // running uploaded HTML, SVG, XML or other active content on the API origin.
            var contentType = SafeContentType(file.ContentType);
            FileResult safeFile = file switch
            {
                FileContentResult bytes => new FileContentResult(bytes.FileContents, contentType),
                FileStreamResult stream => new FileStreamResult(stream.FileStream, contentType),
                PhysicalFileResult physical => new PhysicalFileResult(physical.FileName, contentType),
                VirtualFileResult virtualFile => new VirtualFileResult(virtualFile.FileName, contentType)
                {
                    FileProvider = virtualFile.FileProvider
                },
                _ => throw new InvalidOperationException("Unsupported file response type.")
            };
            safeFile.FileDownloadName = SafeDownloadName(file.FileDownloadName);
            safeFile.LastModified = file.LastModified;
            safeFile.EntityTag = file.EntityTag;
            safeFile.EnableRangeProcessing = file.EnableRangeProcessing;
            context.Result = safeFile;
            response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; frame-ancestors 'none'";
            response.Headers["Cache-Control"] = "private, no-store";
        }
        else if (context.Result is ContentResult content &&
                 content.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Prescription printing needs inline styles, but no scripts, network requests,
            // forms or embedding. Patient values are encoded by the print renderer.
            response.Headers["Content-Security-Policy"] =
                "sandbox allow-modals; default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
            response.Headers["Cache-Control"] = "private, no-store";
        }
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    public static string SafeContentType(string? contentType) => contentType?.Trim().ToLowerInvariant() switch
    {
        "application/pdf" or "image/jpeg" or "image/png" or "image/gif" or "image/webp" or
        "audio/mpeg" or "audio/ogg" or "audio/ogg; codecs=opus" or "audio/aac" or "audio/mp4" or
        "audio/amr" or "video/mp4" or "video/3gpp" or "text/plain" or "text/csv" or
        "application/zip" or "application/msword" or "application/vnd.ms-excel" or
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or "application/vnd.ms-powerpoint" or
        "application/vnd.openxmlformats-officedocument.presentationml.presentation" => contentType.Trim().ToLowerInvariant(),
        _ => "application/octet-stream"
    };

    public static string SafeDownloadName(string? name)
    {
        // Handle either platform's path separator and reject control/format characters,
        // including CR/LF and bidi controls, before MVC creates Content-Disposition.
        var leaf = (name ?? string.Empty).Replace('\\', '/').Split('/').Last();
        var safe = new string(leaf.Where(c => !char.IsControl(c) &&
            char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format &&
            c is not '"' and not ':' and not ';').Take(160).ToArray()).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(safe) ? "download" : safe;
    }
}
