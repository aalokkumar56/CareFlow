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
    private readonly IPrivateDocumentStorage _objects;
    private readonly IClinicalAccessAuditService _clinicalAudit;
    private readonly IOutboxWriter _outbox;

    public PatientDocumentService(ICureFlowDbSession db, ITenantContext tenant, IPrivateDocumentStorage objects, IClinicalAccessAuditService clinicalAudit, IOutboxWriter outbox)
    {
        _db = db;
        _tenant = tenant;
        _objects = objects; _clinicalAudit = clinicalAudit; _outbox = outbox;
    }

    public async Task<IReadOnlyList<PatientDocumentDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        if (await _db.GetByIdAsync<Patient>(patientId, ct: ct) is null)
            throw new NotFoundException("Patient");

        var rows = await _db.QueryAsync<PatientDocument>(
            """SELECT * FROM "PatientDocuments" WHERE "PatientId" = @patientId AND NOT "IsDeleted" ORDER BY "CapturedAt" DESC""",
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
                """SELECT * FROM "Visits" WHERE "Id" = @visitId AND "PatientId" = @patientId AND NOT "IsDeleted" LIMIT 1""",
                new { visitId = visitId.Value, patientId }, ct: ct)
                ?? throw new NotFoundException("Visit");
            appointmentId = visit.AppointmentId;
        }

        var stored = await _objects.StageAsync(_tenant.TenantId, file, GuessContentType(ext), fileSize, MaxBytes, ct);
        await _objects.PublishAsync(_tenant.TenantId, stored, ct);
        await using (var bytes = await _objects.OpenReadAsync(_tenant.TenantId, stored.Key, stored.Sha256, stored.Length, ct))
        {
            var header = new byte[8];
            var read = await bytes.ReadAtLeastAsync(header, header.Length, false, ct);
            var detected = read >= 8 && header.AsSpan().SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }) ? "image/png"
                : read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255 ? "image/jpeg"
                : read >= 5 && header.AsSpan(0,5).SequenceEqual("%PDF-"u8) ? "application/pdf" : null;
            if (detected is null || detected != GuessContentType(ext)
                || (!string.IsNullOrWhiteSpace(contentType) && !string.Equals(contentType, detected, StringComparison.OrdinalIgnoreCase)))
                throw new DomainException("File bytes do not match the supported document type.", 400);
        }
        // Published invalid objects remain retained for explicit orphan reconciliation.
        var documentId = Guid.NewGuid();
        var fileUrl = $"/api/patient-documents/{documentId:D}/download";
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
            Id = documentId,
            ScanStatus = "quarantined",
            StorageKey = stored.Key,
            StorageVersion = stored.Version,
            Sha256 = stored.Sha256,
            StoredLength = stored.Length,
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
        await _db.TransactionAsync(async store =>
        {
            await ClinicalAssociationGuard.RequireAsync(store, patientId, doc.VisitId, doc.AppointmentId, ct);
            await store.InsertAsync(new PrivateDocumentVersion { Id = stored.Version, DocumentId = doc.Id,
                DocumentKind = "patient_document", ObjectKey = stored.Key, Sha256 = stored.Sha256,
                Length = stored.Length, ContentType = stored.ContentType }, ct: ct);
            await store.InsertAsync(doc, ct: ct);
            await _outbox.WriteAsync(store, Guid.NewGuid(), "document.scan", 1, "PatientDocument", doc.Id,
                stored.Version.ToString("D"), new DocumentScanRequest(doc.Id, stored.Version, stored.Sha256, stored.Length, stored.Key), ct);
        }, ct);
        return new PatientDocumentUploadResultDto(doc.Id, fileUrl, doc.Title, doc.ScanStatus, $"/api/patient-documents/{doc.Id:D}/status");
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _db.GetByIdAsync<PatientDocument>(id, ct: ct)
            ?? throw new NotFoundException("Patient document");

        if (doc.ScanStatus != "available" || !doc.ScanEvidenceId.HasValue)
            throw new DomainException("Document is unavailable until verification succeeds.", 409);
        var scan = await _db.GetByIdAsync<DocumentScanEvidence>(doc.ScanEvidenceId.Value, ct: ct);
        if (scan is null || scan.DocumentId != doc.Id || scan.Version != doc.StorageVersion
            || scan.Sha256 != doc.Sha256 || scan.Verdict != "clean")
            throw new DomainException("Document verification evidence is unavailable.", 409);

        if (string.IsNullOrWhiteSpace(doc.FileUrl))
            throw new NotFoundException("Patient document file");

        if (await _db.GetByIdAsync<Patient>(doc.PatientId, ct: ct) is null) throw new NotFoundException("Patient");
        if (doc.StorageKey is not null)
            await CureFlow.Infrastructure.Storage.PrivateDocumentVersionGuard.RequireAsync(_db, doc.StorageVersion, doc.Id, "patient_document", doc.StorageKey, doc.Sha256, doc.StoredLength, ct);
        await _clinicalAudit.RecordAsync(new(doc.PatientId, "PatientDocument", doc.Id, "download", "authorized", doc.StorageVersion?.ToString()), ct);
        var stream = doc.StorageKey is not null
            ? await _objects.OpenReadAsync(_tenant.TenantId, doc.StorageKey, doc.Sha256 ?? string.Empty, doc.StoredLength ?? 0, ct)
            : await _objects.OpenLegacyReadAsync(doc.PatientId, doc.FileUrl, false, ct);
        return (stream, doc.ContentType, Path.GetFileName(doc.OriginalFileName));
    }
    public async Task<PatientDocumentStatusDto> GetStatusAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _db.GetByIdAsync<PatientDocument>(id, ct: ct) ?? throw new NotFoundException("Patient document");
        if (await _db.GetByIdAsync<Patient>(doc.PatientId, ct: ct) is null) throw new NotFoundException("Patient");
        return new(doc.Id, doc.ScanStatus, doc.ScannedAt);
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
        d.OriginalFileName, d.ContentType, d.FileSizeBytes, $"/api/patient-documents/{d.Id:D}/download",
        d.UploadedByName, d.CapturedAt, d.CreatedAt, d.ScanStatus);

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

