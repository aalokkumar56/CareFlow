using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.Services.Ehr;

public class PatientDocumentService : IPatientDocumentService
{
    private const long MaxBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png",
    };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png",
    };

    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;
    private readonly StorageOptions _storage;

    public PatientDocumentService(ICureFlowDbSession db, ITenantContext tenant, IOptions<StorageOptions> storage)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage.Value;
    }

    public async Task<IReadOnlyList<PatientDocumentDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        if (await _db.GetByIdAsync<Patient>(patientId, ct: ct) is null)
            throw new NotFoundException("Patient");

        var rows = await _db.QueryAsync<PatientDocument>(
            """SELECT * FROM "PatientDocuments" WHERE "PatientId" = @patientId ORDER BY "CapturedAt" DESC""",
            new { patientId }, ct: ct);
        return rows.Select(Map).ToList();
    }

    public async Task<PatientDocumentUploadResultDto> UploadAsync(
        Guid patientId,
        Guid? visitId,
        string? title,
        string documentType,
        Stream file,
        string fileName,
        string? contentType,
        long fileSize,
        CancellationToken ct = default)
    {
        if (fileSize <= 0)
            throw new DomainException("File is required", 400);
        if (fileSize > MaxBytes)
            throw new DomainException("File size exceeds 10 MB limit", 400);

        var ext = Path.GetExtension(Path.GetFileName(fileName));
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new DomainException("Allowed file types: PDF, JPEG, PNG", 400);

        if (!string.IsNullOrWhiteSpace(contentType) && !AllowedContentTypes.Contains(contentType))
            throw new DomainException("Allowed file types: PDF, JPEG, PNG", 400);

        if (await _db.GetByIdAsync<Patient>(patientId, ct: ct) is null)
            throw new NotFoundException("Patient");

        Guid? appointmentId = null;
        if (visitId.HasValue)
        {
            var visit = await _db.QueryFirstOrDefaultAsync<Visit>(
                """SELECT * FROM "Visits" WHERE "Id" = @visitId AND "PatientId" = @patientId LIMIT 1""",
                new { visitId = visitId.Value, patientId }, ct: ct)
                ?? throw new NotFoundException("Visit");
            appointmentId = visit.AppointmentId;
        }

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var relativeDir = string.IsNullOrWhiteSpace(_storage.PatientDocumentsPath)
            ? "patient-documents"
            : _storage.PatientDocumentsPath.Trim('/');
        var absoluteDir = Path.Combine(Directory.GetCurrentDirectory(), relativeDir, patientId.ToString("N"));
        Directory.CreateDirectory(absoluteDir);
        var absolutePath = Path.Combine(absoluteDir, storedName);
        await using (var fs = File.Create(absolutePath))
            await file.CopyToAsync(fs, ct);

        var fileUrl = $"/{relativeDir}/{patientId:N}/{storedName}";
        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(fileName)
            : title.Trim();
        if (string.IsNullOrWhiteSpace(resolvedTitle))
            resolvedTitle = "Paper note";

        var uploader = _tenant.UserId.HasValue
            ? await _db.GetByIdAsync<User>(_tenant.UserId.Value, ct: ct)
            : null;

        var doc = new PatientDocument
        {
            PatientId = patientId,
            VisitId = visitId,
            AppointmentId = appointmentId,
            DocumentType = string.IsNullOrWhiteSpace(documentType) ? "paper_note" : documentType.Trim(),
            Title = resolvedTitle,
            OriginalFileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? GuessContentType(ext) : contentType!,
            FileSizeBytes = fileSize,
            FileUrl = fileUrl,
            UploadedByUserId = _tenant.UserId,
            UploadedByName = uploader?.Name,
            CapturedAt = DateTime.UtcNow,
        };
        await _db.InsertAsync(doc, ct: ct);
        return new PatientDocumentUploadResultDto(doc.Id, fileUrl, doc.Title);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _db.GetByIdAsync<PatientDocument>(id, ct: ct)
            ?? throw new NotFoundException("Patient document");

        if (string.IsNullOrWhiteSpace(doc.FileUrl))
            throw new NotFoundException("Patient document file");

        var relativePath = doc.FileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        if (!File.Exists(absolutePath))
            throw new NotFoundException("Patient document file");

        var ext = Path.GetExtension(absolutePath).ToLowerInvariant();
        var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
            ? GuessContentType(ext)
            : doc.ContentType;
        var downloadName = string.IsNullOrWhiteSpace(doc.OriginalFileName)
            ? $"{SanitizeFileName(doc.Title)}{ext}"
            : doc.OriginalFileName;
        return (File.OpenRead(absolutePath), contentType, downloadName);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _db.GetByIdAsync<PatientDocument>(id, ct: ct)
            ?? throw new NotFoundException("Patient document");
        doc.IsDeleted = true;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.UpdateAsync(doc, ct: ct);
    }

    private static PatientDocumentDto Map(PatientDocument d) => new(
        d.Id, d.PatientId, d.VisitId, d.AppointmentId, d.DocumentType, d.Title,
        d.OriginalFileName, d.ContentType, d.FileSizeBytes, d.FileUrl,
        d.UploadedByName, d.CapturedAt, d.CreatedAt);

    private static string GuessContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream",
    };

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
