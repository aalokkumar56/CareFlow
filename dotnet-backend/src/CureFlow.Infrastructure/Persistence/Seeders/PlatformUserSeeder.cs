using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;

namespace CureFlow.Infrastructure.Persistence.Seeders;

/// <summary>Seeds default CureFlow platform operator for ops console login.</summary>
public static class PlatformUserSeeder
{
    public const string DefaultEmail = "ops@cureflow.in";
    public const string DefaultPassword = "OpsAdmin123!";
    public const string DefaultName = "CureFlow Ops";

    public static async Task SeedAsync(ICureFlowDbSession db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        var existing = await db.QueryFirstOrDefaultAsync<int?>(
            """
            SELECT 1 FROM "PlatformUsers"
            WHERE LOWER("Email") = @Email AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Email = DefaultEmail.ToLower() },
            ignoreTenant: true,
            ct);

        if (existing != null)
            return;

        await db.InsertAsync(new PlatformUser
        {
            Name = DefaultName,
            Email = DefaultEmail.ToLower(),
            PasswordHash = hasher.Hash(DefaultPassword),
        }, ignoreTenant: true, ct);
    }
}
