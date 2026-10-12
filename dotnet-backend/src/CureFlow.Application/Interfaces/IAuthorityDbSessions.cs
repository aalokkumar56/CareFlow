namespace CureFlow.Application.Interfaces;

// Authority is selected by the service dependency, never by a request flag.
public interface IIdentityDbSession : ICureFlowDbSession { }
public interface ICatalogueDbSession : ICureFlowDbSession { }
public interface IPlatformIdentityDbSession : ICureFlowDbSession { }
public interface IPlatformDbSession : ICureFlowDbSession { }
public interface IProvisioningDbSession : ICureFlowDbSession { }
