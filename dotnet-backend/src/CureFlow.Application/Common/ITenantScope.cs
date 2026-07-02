using System;
using System.Collections.Generic;
using System.Text;

namespace CureFlow.Application.Common;

/// <summary>Sets the tenant for a unit of work (used by background jobs / tests).</summary>
public interface ITenantScope : IDisposable
{
    Guid TenantId { get; }
}

