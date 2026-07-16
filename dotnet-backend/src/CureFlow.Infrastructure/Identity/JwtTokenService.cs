using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CureFlow.Infrastructure.Identity;

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public string Issue(Guid userId, Guid tenantId, string email, string role, IEnumerable<string> permissions, TimeSpan? expires = null)
    {
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured");
        var issuer = config["Jwt:Issuer"] ?? "cureflow";
        var audience = config["Jwt:Audience"] ?? "cureflow-api";
        var lifetime = expires ?? TimeSpan.FromHours(24);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(CureFlowPermissions.ClaimType, permission));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string IssuePlatformUser(Guid platformUserId, string email, TimeSpan? expires = null)
    {
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured");
        var issuer = config["Jwt:Issuer"] ?? "cureflow";
        var audience = config["Jwt:Audience"] ?? "cureflow-api";
        var lifetime = expires ?? TimeSpan.FromHours(8);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, platformUserId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("platform_user", "true"),
            new(ClaimTypes.Role, "platform_ops"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
