using System.Security.Cryptography;
using System.Text.Json;
using CureFlow.Domain.Entities;
namespace CureFlow.Application.Common;

public static class PatientConcurrency
{
    // Hash the full persisted row, including fields changed by other services. No new timestamp/revision column or PHI in the tag.
    public static string Tag(Patient patient) => $"\"patient:{patient.Id:N}:{Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(patient)))}\"";
    public static string RequireTag(Guid id, IReadOnlyList<string?> values)
    {
        if(values.Count==0)throw new DomainException("Supply the current patient ETag.",428);
        var prefix=$"\"patient:{id:N}:";
        var value=values.Count==1?values[0]:null;
        if(value is null||value.Length!=prefix.Length+65||!value.StartsWith(prefix,StringComparison.Ordinal)
            ||!value.EndsWith('"')||value[prefix.Length..^1].Any(c=>!char.IsAsciiHexDigit(c)))
            throw new ValidationException("Supply one resource-specific strong patient ETag.");
        return value;
    }
}
