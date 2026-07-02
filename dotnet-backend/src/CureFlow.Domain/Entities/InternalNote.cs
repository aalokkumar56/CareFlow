using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class InternalNote : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
