using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;

namespace CureFlow.Infrastructure.Commands;

public sealed class IdempotentCommandExecutor(ICureFlowDbSession db, ITenantContext tenant) : IIdempotentCommandExecutor
{
    public async Task<IdempotentCommandResult> ExecuteAsync<TIntent>(string commandNamespace, string key, TIntent intent,
        TimeSpan approvedRetryHorizon, Func<ICureFlowDbSession, CancellationToken, Task> authorizeCurrent,
        Func<ICureFlowDbSession, CancellationToken, Task<(CommandReceipt Receipt, int StatusCode)>> mutate, CancellationToken ct = default)
    {
        if (!tenant.IsAuthenticated || tenant.IsPlatformUser || tenant.TenantId == Guid.Empty || !tenant.UserId.HasValue)
            throw new DomainException("An authenticated hospital actor is required.", 403);
        if (string.IsNullOrWhiteSpace(commandNamespace) || commandNamespace.Length > 96
            || commandNamespace.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_')))
            throw new InvalidOperationException("A bounded server-defined command namespace is required.");
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl)) throw new ValidationException("Supply a bounded idempotency key.");
        if (approvedRetryHorizon <= TimeSpan.Zero || approvedRetryHorizon > TimeSpan.FromDays(365))
            throw new InvalidOperationException("The command must supply its approved finite retry horizon (up to one year).");
        ArgumentNullException.ThrowIfNull(authorizeCurrent); ArgumentNullException.ThrowIfNull(mutate);
        var actorId = tenant.UserId.Value;
        var keyHash = Digest(Encoding.UTF8.GetBytes(key));
        var requestHash = Digest(CanonicalIntent(intent));
        IdempotentCommandResult? result = null;
        await db.TransactionAsync(async store =>
        {
            // Live permission/session/domain scope must be checked even for a replay.
            await authorizeCurrent(store, ct);
            var lockScope = $"{tenant.TenantId:N}:{actorId:N}:{commandNamespace}:{keyHash}";
            var acquired = await store.QuerySingleAsync<bool>("SELECT pg_try_advisory_xact_lock(hashtextextended(@scope,0))", new { scope = lockScope }, ct: ct);
            if (!acquired) throw new DomainException("A command with this key is in progress. Retry after a short delay.", 409);
            var prior = await store.QueryFirstOrDefaultAsync<IdempotencyRecord>(
                "SELECT * FROM \"IdempotencyRecords\" WHERE \"TenantId\"=@TenantId AND \"ActorId\"=@actorId AND \"CommandNamespace\"=@commandNamespace AND \"KeyHash\"=@keyHash FOR UPDATE",
                new { actorId, commandNamespace, keyHash }, ct: ct);
            if (prior is not null)
            {
                if (prior.RequestHash != requestHash) throw new DomainException("Idempotency key was already used for a different command intent.", 409);
                if (prior.ExpiresAt <= DateTime.UtcNow) throw new DomainException("This command retry window has ended. Use a new key and current domain state.", 409);
                if (prior.State != "completed") throw new DomainException("A command with this key is in progress. Retry after a short delay.", 409);
                await authorizeCurrent(store, ct);
                result = new(JsonSerializer.Deserialize<CommandReceipt>(prior.ResponseJson) ?? throw new InvalidOperationException("Invalid stored command receipt."), prior.ResponseStatus, true);
                return;
            }
            var row = new IdempotencyRecord { ActorId = actorId, CommandNamespace = commandNamespace, KeyHash = keyHash,
                RequestHash = requestHash, ExpiresAt = DateTime.UtcNow.Add(approvedRetryHorizon) };
            // DB unique constraint claims the key, independently of the advisory lock.
            await store.InsertAsync(row, ct: ct);
            var (receipt, status) = await mutate(store, ct);
            if (receipt.ResourceId == Guid.Empty || receipt.Revision < 0 || !Enum.IsDefined(receipt.Outcome) || status is not (200 or 201 or 202 or 204)) throw new InvalidOperationException("Command must produce a minimal successful receipt.");
            row.ResponseJson = JsonSerializer.Serialize(receipt); row.ResponseStatus = status; row.State = "completed";
            await store.UpdateAsync(row, ct: ct);
            result = new(receipt, status, false);
        }, ct);
        return result!;
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] CanonicalIntent<T>(T value)
    {
        using var json = JsonSerializer.SerializeToDocument(value);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) WriteCanonical(writer, json.RootElement);
        if (output.Length > 65536) throw new ValidationException("Command intent exceeds its 64 KB canonical limit.");
        return output.ToArray();
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); }
        else if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
        else value.WriteTo(writer);
    }
}
