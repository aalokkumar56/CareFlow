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
}
