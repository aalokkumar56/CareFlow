using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Ehr;
namespace CureFlow.Infrastructure.Storage;

public static class PrivateDocumentVersionGuard
{
    public static async Task RequireAsync(ICureFlowDbSession db, Guid? versionId, Guid documentId, string kind, string key, string? sha256, long? length, CancellationToken ct)
    {
        var version = versionId.HasValue ? await db.GetByIdAsync<PrivateDocumentVersion>(versionId.Value, ct: ct) : null;
        if (version is null || version.DocumentId != documentId || version.DocumentKind != kind || version.ObjectKey != key
            || version.Sha256 != sha256 || version.Length != length)
            throw new DomainException("Document version mapping requires operator review.", 409);
    }
}
