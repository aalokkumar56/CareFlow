using CureFlow.Application.Common;
using System.Text.Json;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
namespace CureFlow.Infrastructure.Storage;

// Default deployment has no approved scanning provider. It fails closed.
public sealed class UnavailableDocumentScanner : IDocumentScanner
{
    public Task<DocumentScanResult> ScanAsync(Stream content, CancellationToken ct) =>
        Task.FromResult(new DocumentScanResult("failed", "unconfigured", "none", "ScannerUnavailable"));
}

public sealed class DocumentScanConsumer(IPrivateDocumentStorage objects, IDocumentScanner scanner, ICureFlowDbSession db,
    ITenantContext tenant, IClinicalAccessAuditService audit) : IPreparedTransactionalOutboxConsumer
{
    public string EventType => "document.scan";
    public string ConsumerName => "document-scan";
    public bool SupportsSchema(int version) => version == 1;
    public Task HandleAsync(ICureFlowDbSession transaction, OutboxEnvelope envelope, CancellationToken ct) =>
        throw new InvalidOperationException("Document scanning requires read-only preparation outside the domain transaction.");

    public async Task<object> PrepareAsync(OutboxEnvelope envelope, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<DocumentScanRequest>(envelope.PayloadJson)
            ?? throw new InvalidOperationException("Scan event has no version identity.");
        if (request.DocumentId != envelope.AggregateId || request.Version.ToString("D") != envelope.AggregateVersion
            || request.Length <= 0 || request.Length > 10 * 1024 * 1024 || request.Sha256.Length != 64)
            throw new InvalidOperationException("Scan event version identity is invalid.");
        // Check current row visibility before opening private bytes outside the transaction.
        // Restricted charts fail closed for workers without an approved actor scope.
        var visible = await db.QueryFirstOrDefaultAsync<Guid?>("""
            SELECT "Id" FROM "PatientDocuments" WHERE "TenantId"=@TenantId AND "Id"=@id
              AND "StorageVersion"=@version AND "StorageKey"=@key AND "Sha256"=@hash
              AND "StoredLength"=@length AND NOT "IsDeleted"
            """, new { id = request.DocumentId, version = request.Version, key = request.ObjectKey,
                hash = request.Sha256, length = request.Length }, ct: ct);
        if (!visible.HasValue) throw new ForbiddenException();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                await using var content = await objects.OpenReadAsync(tenant.TenantId, request.ObjectKey, request.Sha256, request.Length, timeout.Token);
                var result = await scanner.ScanAsync(content, timeout.Token);
                if (result.Verdict is not ("clean" or "rejected" or "failed") || result.Engine.Length is < 1 or > 64
                    || result.EngineVersion.Length is < 1 or > 64 || result.Code.Length is < 1 or > 64
                    || (result.Verdict == "clean" && (result.Engine == "unconfigured" || result.EngineVersion == "none")))
                    return new Prepared(request, new("failed", "validation", "1", "ScannerEvidenceInvalid"));
                return new Prepared(request, result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) when (attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct); }
            catch (Exception) { return new Prepared(request, new("failed", "scanner", "unknown", "ScannerFailed")); }
        }
        throw new InvalidOperationException("Scan attempts exhausted.");
    }

    public async Task HandlePreparedAsync(ICureFlowDbSession transaction, OutboxEnvelope envelope, object preparation, CancellationToken ct)
    {
        if (preparation is not Prepared prepared) throw new InvalidOperationException("Invalid scanner preparation.");
        var request = prepared.Request;
        var doc = await transaction.QueryFirstOrDefaultAsync<PatientDocument>(
            "SELECT * FROM \"PatientDocuments\" WHERE \"Id\"=@id AND \"TenantId\"=@TenantId AND NOT \"IsDeleted\" FOR UPDATE",
            new { id = request.DocumentId }, ct: ct);
        if (doc is null) return; // Deleted metadata never releases bytes.
        if (doc.StorageVersion != request.Version || doc.StorageKey != request.ObjectKey || doc.Sha256 != request.Sha256
            || doc.StoredLength != request.Length) throw new InvalidOperationException("Document version changed before scan commit.");
        if (doc.ScanStatus != "quarantined") return;
        await PrivateDocumentVersionGuard.RequireAsync(transaction, request.Version, doc.Id, "patient_document", request.ObjectKey, request.Sha256, request.Length, ct);
        var patient = await transaction.GetByIdAsync<Patient>(doc.PatientId, ct: ct);
        var associationValid = patient is not null;
        if (associationValid && doc.VisitId.HasValue)
            associationValid = await transaction.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT \"Id\" FROM \"Visits\" WHERE \"Id\"=@id AND \"PatientId\"=@patient AND NOT \"IsDeleted\"",
                new { id = doc.VisitId.Value, patient = doc.PatientId }, ct: ct) is not null;
        var result = associationValid ? prepared.Result : new DocumentScanResult("rejected", "association", "1", "AssociationUnavailable");
        var evidence = new DocumentScanEvidence { DocumentId = doc.Id, Version = request.Version, Sha256 = request.Sha256,
            Verdict = result.Verdict, Engine = result.Engine, EngineVersion = result.EngineVersion, Code = result.Code };
        await transaction.InsertAsync(evidence, ct: ct);
        doc.ScanStatus = result.Verdict == "clean" ? "available" : result.Verdict;
        doc.ScanEvidenceId = evidence.Id; doc.ScannedAt = DateTime.UtcNow;
        await transaction.UpdateAsync(doc, ct: ct);
        await audit.RecordAsync(new(doc.PatientId, "PatientDocument", doc.Id, "scan", doc.ScanStatus,
            request.Version.ToString("D"), "operations"), ct, transaction);
    }
    private record Prepared(DocumentScanRequest Request, DocumentScanResult Result);
}

