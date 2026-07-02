using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface ITagService
{
    Task<IReadOnlyList<object>> ListAsync(CancellationToken ct = default);
}
