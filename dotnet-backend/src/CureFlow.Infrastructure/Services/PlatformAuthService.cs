using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;

namespace CureFlow.Infrastructure.Services;

public class PlatformAuthService : IPlatformAuthService
{
    private readonly ICureFlowDbSession _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public PlatformAuthService(ICureFlowDbSession db, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<PlatformSetupStatusResponse> GetSetupStatusAsync(CancellationToken ct = default)
    {
        var count = await CountActiveOwnersAsync(ct);
        return new PlatformSetupStatusResponse(NeedsSetup: count == 0);
    }

    public async Task<PlatformAuthResponse> BootstrapOwnerAsync(PlatformBootstrapRequest req, CancellationToken ct = default)
    {
        var name = req.Name?.Trim() ?? string.Empty;
        var email = req.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var password = req.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            throw new ValidationException("Name is required (min 2 characters).");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ValidationException("A valid email is required.");
        if (password.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");

        var existingCount = await CountActiveOwnersAsync(ct);
        if (existingCount > 0)
            throw new ConflictException("Platform owner already exists. Use sign-in instead.");

        var user = new PlatformUser
        {
            Name = name,
            Email = email,
            PasswordHash = _hasher.Hash(password),
            IsActive = true,
            IsDeleted = false,
        };

        await _db.InsertAsync(user, ignoreTenant: true, ct);

        var token = _jwt.IssuePlatformUser(user.Id, user.Email);
        return new PlatformAuthResponse(token, user.Email, user.Name);
    }

    public async Task<PlatformAuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await _db.QueryFirstOrDefaultAsync<PlatformUser>(
            """
            SELECT * FROM "PlatformUsers"
            WHERE LOWER("Email") = @Email AND "IsActive" = true AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Email = req.Email.ToLower() },
            ignoreTenant: true,
            ct);

        if (user == null || !_hasher.Verify(req.Password, user.PasswordHash))
            throw new DomainException("Invalid email or password", 401);

        var token = _jwt.IssuePlatformUser(user.Id, user.Email);
        return new PlatformAuthResponse(token, user.Email, user.Name);
    }

    private async Task<int> CountActiveOwnersAsync(CancellationToken ct)
    {
        var count = await _db.QueryFirstOrDefaultAsync<int?>(
            """
            SELECT COUNT(*)::int FROM "PlatformUsers"
            WHERE "IsActive" = true AND "IsDeleted" = false
            """,
            ignoreTenant: true,
            ct: ct);
        return count ?? 0;
    }
}
