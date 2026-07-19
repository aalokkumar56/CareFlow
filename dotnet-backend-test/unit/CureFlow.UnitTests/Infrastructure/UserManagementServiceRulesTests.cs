using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>
/// Service rules for roles/users: SuperAdmin is immutable, role names normalize,
/// and tenant-owner / self-modification guards.
/// </summary>
public class UserManagementServiceRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TargetId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static (UserManagementService Svc, Mock<ICureFlowDbSession> Db, Mock<IAuditService> Audit) Build(
        Guid? actorUserId = null, string? actorRole = "admin")
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(TenantId);
        tenant.SetupGet(x => x.UserId).Returns(actorUserId ?? ActorId);
        tenant.SetupGet(x => x.UserRole).Returns(actorRole);

        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new UserManagementService(
            db.Object,
            tenant.Object,
            Mock.Of<IPasswordHasher>(),
            audit.Object,
            Mock.Of<IUserPermissionService>());

        return (svc, db, audit);
    }

    private static void SetupRoleLookup(Mock<ICureFlowDbSession> db, Role role)
    {
        db.Setup(x => x.QueryFirstOrDefaultAsync<Role>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
    }

    private static void SetupUser(Mock<ICureFlowDbSession> db, User user)
    {
        db.Setup(x => x.GetByIdAsync<User>(user.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    [Fact]
    public async Task UpdateRoleAsync_rejects_modifying_protected_SuperAdmin()
    {
        var (svc, db, _) = Build();
        SetupRoleLookup(db, new Role
        {
            Id = Guid.NewGuid(),
            Name = RoleNames.SuperAdmin,
            IsSystem = true,
        });

        var act = async () => await svc.UpdateRoleAsync(
            RoleNames.SuperAdmin,
            new UpdateRoleRequest("new desc", [CureFlowPermissions.DashboardView]));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*SuperAdmin*cannot be modified*");
        db.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData("SuperAdmin")]
    [InlineData("SUPERADMIN")]
    public async Task UpdateRoleAsync_protects_SuperAdmin_case_insensitively(string storedName)
    {
        var (svc, db, _) = Build();
        SetupRoleLookup(db, new Role { Id = Guid.NewGuid(), Name = storedName, IsSystem = true });

        var act = async () => await svc.UpdateRoleAsync(
            storedName, new UpdateRoleRequest(null, [CureFlowPermissions.DashboardView]));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*SuperAdmin*");
    }

    [Fact]
    public async Task UpdateRoleAsync_allows_mutating_non_protected_role()
    {
        var (svc, db, _) = Build();
        var role = new Role { Id = Guid.NewGuid(), Name = "Billing_Manager", IsSystem = false };
        SetupRoleLookup(db, role);

        db.Setup(x => x.QueryAsync<Guid>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        db.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        db.Setup(x => x.QueryAsync<string>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var result = await svc.UpdateRoleAsync(
            "Billing_Manager", new UpdateRoleRequest("Updated", Array.Empty<string>()));

        result.Name.Should().Be("Billing_Manager");
        result.Description.Should().Be("Updated");
        role.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task CreateRoleAsync_normalizes_spaces_to_underscores()
    {
        var (svc, db, _) = Build();
        Role? inserted = null;

        db.Setup(x => x.QueryFirstOrDefaultAsync<int>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        db.Setup(x => x.InsertAsync(It.IsAny<Role>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Role, bool, CancellationToken>((r, _, _) => inserted = r)
            .Returns(Task.CompletedTask);
        db.Setup(x => x.QueryAsync<Guid>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        db.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        db.Setup(x => x.QueryAsync<string>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var result = await svc.CreateRoleAsync(
            new CreateRoleRequest("  Front Desk  ", "desk", Array.Empty<string>()));

        inserted.Should().NotBeNull();
        inserted!.Name.Should().Be("Front_Desk");
        result.Name.Should().Be("Front_Desk");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateRoleAsync_rejects_blank_name_after_normalize(string? name)
    {
        var (svc, _, _) = Build();

        var act = async () => await svc.CreateRoleAsync(
            new CreateRoleRequest(name!, null, Array.Empty<string>()));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Role name is required*");
    }

    [Fact]
    public async Task CreateRoleAsync_rejects_duplicate_normalized_name()
    {
        var (svc, db, _) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<int>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = async () => await svc.CreateRoleAsync(
            new CreateRoleRequest("Billing Manager", null, Array.Empty<string>()));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already exists*");
        db.Verify(x => x.InsertAsync(It.IsAny<Role>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_looks_up_role_by_normalized_name()
    {
        var (svc, db, _) = Build();
        object? capturedParam = null;
        db.Setup(x => x.QueryFirstOrDefaultAsync<Role>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((_, p, _, _) => capturedParam = p)
            .ReturnsAsync(new Role { Id = Guid.NewGuid(), Name = "Billing_Manager", IsSystem = false });

        db.Setup(x => x.QueryAsync<Guid>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        db.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        db.Setup(x => x.QueryAsync<string>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        await svc.UpdateRoleAsync("  Billing Manager  ", new UpdateRoleRequest(null, Array.Empty<string>()));

        capturedParam.Should().NotBeNull();
        var normalized = capturedParam!.GetType().GetProperty("normalized")!.GetValue(capturedParam);
        normalized.Should().Be("Billing_Manager");
    }

    [Fact]
    public async Task DeleteRoleAsync_rejects_built_in_system_roles()
    {
        var (svc, db, _) = Build();
        SetupRoleLookup(db, new Role { Id = Guid.NewGuid(), Name = RoleNames.Admin, IsSystem = true });

        var act = async () => await svc.DeleteRoleAsync(RoleNames.Admin);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Built-in roles cannot be deleted*");
    }

    [Fact]
    public async Task SetActiveAsync_cannot_disable_tenant_owner()
    {
        var (svc, db, _) = Build();
        SetupUser(db, new User
        {
            Id = TargetId,
            TenantId = TenantId,
            Role = UserRole.TenantOwner,
            IsActive = true,
            Email = "owner@h.com",
            Name = "Owner",
        });

        var act = async () => await svc.SetActiveAsync(TargetId, isActive: false);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Cannot disable the tenant owner*");
        db.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_cannot_modify_own_account()
    {
        var (svc, db, _) = Build(actorUserId: ActorId);
        SetupUser(db, new User
        {
            Id = ActorId,
            TenantId = TenantId,
            Role = UserRole.Admin,
            Email = "me@h.com",
            Name = "Me",
        });

        var act = async () => await svc.UpdateAsync(
            ActorId, new UpdateUserRequest("Other", null, null, null));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cannot modify your own account*");
    }

    [Fact]
    public async Task AssignRoleAsync_cannot_demote_tenant_owner()
    {
        var (svc, db, _) = Build(actorRole: "tenant_owner");
        SetupUser(db, new User
        {
            Id = TargetId,
            TenantId = TenantId,
            Role = UserRole.TenantOwner,
            Email = "owner@h.com",
            Name = "Owner",
        });

        var act = async () => await svc.AssignRoleAsync(TargetId, new AssignRolesRequest(UserRole.Admin));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Cannot change tenant owner role*");
    }

    [Fact]
    public async Task AssignRoleAsync_only_tenant_owner_can_assign_tenant_owner()
    {
        var (svc, db, _) = Build(actorRole: "admin");
        SetupUser(db, new User
        {
            Id = TargetId,
            TenantId = TenantId,
            Role = UserRole.Doctor,
            Email = "doc@h.com",
            Name = "Doc",
        });

        var act = async () => await svc.AssignRoleAsync(
            TargetId, new AssignRolesRequest(UserRole.TenantOwner));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Only tenant owner can assign tenant owner role*");
    }
}
