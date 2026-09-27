using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Saas;
public class PlatformRole : BaseEntity
{
 public string Key { get; set; } = "";
 public string Name { get; set; } = "";
 public string PermissionsJson { get; set; } = "[]";
}
public class PlatformCommand : BaseEntity
{
 public Guid ActorId { get; set; }
 public string RequestId { get; set; } = "";
 public string Fingerprint { get; set; } = "";
 public string ResponseJson { get; set; } = "{}";
}

