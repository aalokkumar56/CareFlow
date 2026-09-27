using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Areas.Platform;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.Tests;

public sealed class TenantApiActivityServiceTests
{
    private static (TenantApiActivityService Service, Mock<ICureFlowDbSession> Db) Build(string? role = "owner", bool active = true)
    {
        var db = new Mock<ICureFlowDbSession>();
        var id = Guid.NewGuid();
        db.SetupGet(x => x.UserId).Returns(role is null ? null : id);
        db.Setup(x => x.GetByIdAsync<PlatformUser>(id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformUser { Id = id, Role = role ?? "", IsActive = active });
        db.Setup(x => x.QueryFirstOrDefaultAsync<PlatformRole>(
            It.IsAny<string>(), It.IsAny<object?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformRole?)null);
        return (new TenantApiActivityService(db.Object, new PlatformAccess(db.Object)), db);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("viewer")]
    public async Task Missing_session_or_audit_permission_cannot_query_activity(string? role)
    {
        var (service, db) = Build(role);
        var act = () => service.List(null, null, null, false, null, null, null, 1, 50);
        await act.Should().ThrowAsync<DomainException>();
        db.Verify(x => x.QuerySingleAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deactivated_operator_cannot_query_activity()
    {
        var (service, _) = Build(active: false);
        await FluentActions.Awaiting(() => service.List(null, null, null, false, null, null, null, 1, 50))
            .Should().ThrowAsync<DomainException>();
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public async Task Rejects_invalid_status_filter_before_querying(int status)
    {
        var (service, db) = Build();
        await FluentActions.Awaiting(() => service.List(null, null, status, false, null, null, null, 1, 50))
            .Should().ThrowAsync<DomainException>().WithMessage("*Status code*");
        db.Verify(x => x.QuerySingleAsync<long>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rejects_reversed_date_range()
    {
        var (service, _) = Build();
        await FluentActions.Awaiting(() => service.List(null, null, null, false, null,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), 1, 50))
            .Should().ThrowAsync<DomainException>().WithMessage("*start date*");
    }

    [Fact]
    public async Task Rejects_unbounded_search_input()
    {
        var (service, _) = Build();
        await FluentActions.Awaiting(() => service.List(null, new string('x', 201), null, false, null, null, null, 1, 50))
            .Should().ThrowAsync<DomainException>().WithMessage("*200 characters*");
    }
}
