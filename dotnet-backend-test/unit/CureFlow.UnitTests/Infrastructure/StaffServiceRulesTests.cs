using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>Staff profile creation is limited to clinical/front-desk roles.</summary>
public class StaffServiceRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid UserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static (StaffService Svc, Mock<ICureFlowDbSession> Db) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(TenantId);

        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (new StaffService(db.Object, tenant.Object, audit.Object), db);
    }

    private static CreateStaffProfileRequest Req(Guid userId) =>
        new(userId, null, null, null, null, null, null, null);

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.TenantOwner)]
    [InlineData(UserRole.Marketing)]
    public async Task CreateAsync_rejects_roles_outside_staff_set(UserRole role)
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = UserId,
                TenantId = TenantId,
                Role = role,
                Name = "X",
                Email = "x@h.com",
            });

        var act = async () => await svc.CreateAsync(Req(UserId));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Doctor, Nurse, Staff, or Reception*");
        db.Verify(x => x.InsertAsync(It.IsAny<StaffProfile>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.Nurse)]
    [InlineData(UserRole.Staff)]
    [InlineData(UserRole.Reception)]
    public async Task CreateAsync_accepts_staff_eligible_roles(UserRole role)
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = UserId,
                TenantId = TenantId,
                Role = role,
                Name = "Staffer",
                Email = "s@h.com",
                Specialty = "General",
            });
        db.Setup(x => x.QueryFirstOrDefaultAsync<StaffProfile>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaffProfile?)null);

        StaffProfile? inserted = null;
        db.Setup(x => x.InsertAsync(It.IsAny<StaffProfile>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<StaffProfile, bool, CancellationToken>((p, _, _) => inserted = p)
            .Returns(Task.CompletedTask);

        var id = await svc.CreateAsync(Req(UserId));

        id.Should().NotBeEmpty();
        inserted.Should().NotBeNull();
        inserted!.UserId.Should().Be(UserId);
        inserted.Department.Should().Be("General");
    }

    [Fact]
    public async Task CreateAsync_rejects_when_profile_already_exists()
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = UserId,
                TenantId = TenantId,
                Role = UserRole.Doctor,
                Name = "Doc",
                Email = "d@h.com",
            });
        db.Setup(x => x.QueryFirstOrDefaultAsync<StaffProfile>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffProfile { Id = Guid.NewGuid(), UserId = UserId });

        var act = async () => await svc.CreateAsync(Req(UserId));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_throws_when_user_missing()
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = async () => await svc.CreateAsync(Req(UserId));

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*User*");
    }
}
