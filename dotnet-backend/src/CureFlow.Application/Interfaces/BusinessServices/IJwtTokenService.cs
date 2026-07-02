using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IJwtTokenService
{
    string Issue(Guid userId, Guid tenantId, string email, string role, IEnumerable<string> permissions, TimeSpan? expires = null);
}
