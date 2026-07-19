using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CureFlow.Application.Common;
using Microsoft.IdentityModel.Tokens;

namespace CureFlow.IntegrationTests;

/// <summary>Issues JWTs signed with <see cref="Secret"/> (override host Jwt:Secret to match).</summary>
internal static class TestJwtHelper
{
    public const string Secret = "cureflow-integration-test-jwt-secret-key-32chars-min!!";
    public const string Issuer = "cureflow";
    public const string Audience = "cureflow-api";

    public static Dictionary<string, string?> JwtConfigOverrides() => new()
    {
        ["Jwt:Secret"] = Secret,
        ["Jwt:Issuer"] = Issuer,
        ["Jwt:Audience"] = Audience,
    };

    public static string CreateToken(
        Guid userId,
        Guid tenantId,
        IEnumerable<string> permissions,
        string email = "integration@cureflow.test",
        string role = RoleNames.Staff,
        TimeSpan? lifetime = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        string? audience = null,
        bool asPlatformUser = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // Bypass TenantMiddleware Users-table lookup / lifecycle checks for mocked API contracts.
        if (asPlatformUser)
            claims.Add(new Claim("platform_user", "true"));

        foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(CureFlowPermissions.ClaimType, permission));

        var nbf = notBefore ?? DateTime.UtcNow.AddMinutes(-1);
        var exp = expires ?? DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience ?? Audience,
            claims: claims,
            notBefore: nbf,
            expires: exp,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Flips one character in the JWT signature segment so validation fails.</summary>
    public static string TamperSignature(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new ArgumentException("Expected a compact JWT with three segments.", nameof(token));

        var sig = parts[2];
        var chars = sig.ToCharArray();
        chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
        parts[2] = new string(chars);
        return string.Join('.', parts);
    }
}
