using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Areas.Platform;
using Moq;
using Xunit;

namespace CureFlow.Tests;

public class PlatformEntitlementSaveTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Saving_override_persists_policy_even_after_reset(bool wasReset, bool enabled)
    {
        var db = new Mock<ICureFlowDbSession>();
        var tenant = new Tenant();
        var module = new PlatformModule { Key = "communications", IsCore = false };
        var actor = new PlatformUser { Role = "owner", IsActive = true };
        var persisted = new TenantEntitlement
        {
            TenantId = tenant.Id, PlatformModuleId = module.Id,
            IsEnabled = !enabled, IsDeleted = wasReset, Version = 2,
        };
        db.SetupGet(x => x.UserId).Returns(actor.Id);
        db.Setup(x => x.GetByIdAsync<PlatformUser>(actor.Id, true, default)).ReturnsAsync(actor);
        db.Setup(x => x.GetByIdAsync<Tenant>(tenant.Id, true, default)).ReturnsAsync(tenant);
        db.Setup(x => x.GetByIdAsync<PlatformModule>(module.Id, true, default)).ReturnsAsync(module);
        db.Setup(x => x.TransactionAsync(It.IsAny<Func<ICureFlowDbSession, Task>>(), default))
            .Returns((Func<ICureFlowDbSession, Task> action, CancellationToken _) => action(db.Object));
        db.Setup(x => x.QueryFirstOrDefaultAsync<TenantEntitlement>(It.IsAny<string>(), It.IsAny<object>(), true, default))
            .ReturnsAsync(() => new TenantEntitlement
            {
                Id = persisted.Id, TenantId = persisted.TenantId, PlatformModuleId = persisted.PlatformModuleId,
                IsDeleted = persisted.IsDeleted, IsEnabled = persisted.IsEnabled, Version = persisted.Version,
            });
        db.Setup(x => x.ExecuteAsync(It.Is<string>(s => s.StartsWith("UPDATE \"TenantEntitlements\"")), It.IsAny<object>(), true, default))
            .Returns((string sql, object parameters, bool _, CancellationToken ct) =>
            {
                Assert.Contains("\"TenantId\"=@tenant", sql);
                Assert.Contains("\"PlatformModuleId\"=@module", sql);
                Assert.Contains("SET \"IsDeleted\"=false", sql);
                Assert.Equal(tenant.Id, parameters.GetType().GetProperty("tenant")!.GetValue(parameters));
                Assert.Equal(module.Id, parameters.GetType().GetProperty("module")!.GetValue(parameters));
                persisted.IsDeleted = false;
                return Task.FromResult(1);
            });
        db.Setup(x => x.UpdateAsync(It.IsAny<TenantEntitlement>(), true, default))
            .Callback((TenantEntitlement row, bool _, CancellationToken ct) =>
            {
                // Mirror the DAL's WHERE NOT IsDeleted guard, using independent persisted state.
                if (!persisted.IsDeleted) persisted = row;
            }).Returns(Task.CompletedTask);
        var service = new PlatformAdminService(db.Object, new PlatformAccess(db.Object), Mock.Of<IPasswordHasher>());
        var expires = DateTime.UtcNow.AddDays(2);

        await service.Entitlement(tenant.Id, module.Id,
            new EntitlementInput(enabled, 123, expires, "Update communications access"), Guid.NewGuid().ToString());

        var effective = Assert.Single(PlatformModuleAccess.Evaluate([module], [persisted], [], DateTime.UtcNow));
        Assert.Equal("tenant", effective.Source);
        Assert.Equal(enabled, effective.Effective);
        Assert.Equal(123, effective.Limit);
        Assert.Equal(expires, effective.ExpiresAt);
        Assert.Equal(3, effective.Version);
        db.Verify(x => x.ExecuteAsync(It.Is<string>(s => s.StartsWith("UPDATE \"TenantEntitlements\"")),
            It.IsAny<object>(), true, default), wasReset ? Times.Once() : Times.Never());
    }
}