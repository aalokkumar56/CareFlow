using CureFlow.Application.Common;
using CureFlow.Application.DTOs.Notification;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Services.Notifications;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// HTTP-facing NotificationService / NotificationPreferenceService gaps
/// not covered by NotificationServiceTests (mark/delete visibility paths).
/// </summary>
public class NotificationServiceUnauthAndListGapsTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid EventId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Mock<ICureFlowDbSession> NewDb()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        return db;
    }

    private static Mock<ITenantContext> Tenant(Guid? userId)
    {
        var t = new Mock<ITenantContext>();
        t.SetupGet(x => x.UserId).Returns(userId);
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        return t;
    }

    private static NotificationService Service(
        Mock<ICureFlowDbSession> db,
        Mock<ITenantContext> tenant,
        Mock<IUserPermissionService>? perms = null,
        Mock<INotificationPreferenceService>? prefs = null)
        => new(
            db.Object,
            tenant.Object,
            (perms ?? new Mock<IUserPermissionService>()).Object,
            (prefs ?? new Mock<INotificationPreferenceService>()).Object);

    [Fact]
    public async Task ListAsync_returns_empty_when_unauthenticated()
    {
        var db = NewDb();
        var prefs = new Mock<INotificationPreferenceService>();
        var svc = Service(db, Tenant(userId: null), prefs: prefs);

        var result = await svc.ListAsync(unreadOnly: false, limit: 50);

        result.Should().BeEmpty();
        prefs.Verify(x => x.GetEffectivePreferencesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_no_in_app_types_enabled()
    {
        var db = NewDb();
        var prefs = new Mock<INotificationPreferenceService>();
        prefs.Setup(x => x.GetEffectivePreferencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotificationPreferenceDto>());
        var svc = Service(db, Tenant(UserId), prefs: prefs);

        var result = await svc.ListAsync(unreadOnly: false, limit: 50);

        result.Should().BeEmpty();
        db.Verify(x => x.QueryFirstOrDefaultAsync<NotificationFeedCursor>(
            It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUnreadCountAsync_returns_zero_when_unauthenticated()
    {
        var svc = Service(NewDb(), Tenant(userId: null));

        var count = await svc.GetUnreadCountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCountAsync_returns_zero_when_no_allowed_types()
    {
        var prefs = new Mock<INotificationPreferenceService>();
        prefs.Setup(x => x.GetEffectivePreferencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotificationPreferenceDto>());
        var db = NewDb();
        var svc = Service(db, Tenant(UserId), prefs: prefs);

        var count = await svc.GetUnreadCountAsync();

        count.Should().Be(0);
        db.Verify(x => x.QuerySingleAsync<int>(
            It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_throws_forbidden_when_unauthenticated()
    {
        var svc = Service(NewDb(), Tenant(userId: null));

        var act = async () => await svc.MarkReadAsync(EventId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Not authenticated*");
    }

    [Fact]
    public async Task DeleteAsync_throws_forbidden_when_unauthenticated()
    {
        var svc = Service(NewDb(), Tenant(userId: null));

        var act = async () => await svc.DeleteAsync(EventId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Not authenticated*");
    }

    [Fact]
    public async Task MarkAllReadAsync_is_noop_when_unauthenticated()
    {
        var db = NewDb();
        var svc = Service(db, Tenant(userId: null));

        await svc.MarkAllReadAsync();

        db.Verify(x => x.UpdateAsync(
            It.IsAny<NotificationFeedCursor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        db.Verify(x => x.InsertAsync(
            It.IsAny<NotificationFeedCursor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_updates_existing_receipt_instead_of_inserting()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<NotificationEvent>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationEvent
            {
                Id = EventId,
                TenantId = TenantId,
                Type = NotificationTypeCodes.ClinicalLabUploaded,
                AudienceMode = NotificationAudienceMode.Permission,
                CreatedByUserId = CreatorId,
            });
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, TenantId = TenantId });
        perms.Setup(x => x.GetPermissionCodesAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CureFlowPermissions.ClinicalView]);

        var existing = new NotificationReceipt
        {
            Id = Guid.NewGuid(),
            NotificationEventId = EventId,
            UserId = UserId,
            ReadAt = null,
        };
        db.Setup(x => x.QueryFirstOrDefaultAsync<NotificationReceipt>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var svc = Service(db, Tenant(UserId), perms);
        await svc.MarkReadAsync(EventId);

        existing.ReadAt.Should().NotBeNull();
        db.Verify(x => x.UpdateAsync(existing, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(x => x.InsertAsync(It.IsAny<NotificationReceipt>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_directed_event_with_invalid_target_json_is_not_visible()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<NotificationEvent>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationEvent
            {
                Id = EventId,
                TenantId = TenantId,
                Type = NotificationTypeCodes.TaskAssigned,
                AudienceMode = NotificationAudienceMode.Directed,
                CreatedByUserId = CreatorId,
                TargetUserIdsJson = "{not-json",
            });
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, TenantId = TenantId });
        perms.Setup(x => x.GetPermissionCodesAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CureFlowPermissions.DashboardView]);

        var svc = Service(db, Tenant(UserId), perms);
        var act = async () => await svc.MarkReadAsync(EventId);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class NotificationPreferenceServiceGapsTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Mock<ICureFlowDbSession> NewDb()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        return db;
    }

    private static Mock<ITenantContext> Tenant(Guid? userId)
    {
        var t = new Mock<ITenantContext>();
        t.SetupGet(x => x.UserId).Returns(userId);
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        return t;
    }

    private static NotificationPreferenceService Service(
        Mock<ICureFlowDbSession> db,
        Mock<ITenantContext> tenant,
        Mock<IUserPermissionService>? perms = null)
        => new(db.Object, tenant.Object, (perms ?? new Mock<IUserPermissionService>()).Object);

    [Fact]
    public async Task GetEffectivePreferencesAsync_returns_empty_when_unauthenticated()
    {
        var result = await Service(NewDb(), Tenant(null)).GetEffectivePreferencesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateUserPreferencesAsync_throws_forbidden_when_unauthenticated()
    {
        var act = async () => await Service(NewDb(), Tenant(null)).UpdateUserPreferencesAsync(
            new UpdateNotificationPreferencesRequest
            {
                Preferences =
                [
                    new NotificationPreferenceUpdateItem
                    {
                        NotificationType = NotificationTypeCodes.AppointmentCreated,
                        InAppEnabled = false,
                    },
                ],
            });

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Not authenticated*");
    }

    [Fact]
    public async Task UpdateUserPreferencesAsync_forbids_types_user_lacks_permission_for()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, TenantId = TenantId });
        perms.Setup(x => x.GetPermissionCodesAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CureFlowPermissions.ConversationView]);

        var act = async () => await Service(db, Tenant(UserId), perms).UpdateUserPreferencesAsync(
            new UpdateNotificationPreferencesRequest
            {
                Preferences =
                [
                    new NotificationPreferenceUpdateItem
                    {
                        NotificationType = NotificationTypeCodes.ClinicalLabUploaded,
                        InAppEnabled = true,
                    },
                ],
            });

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Cannot configure notification type*");
        db.Verify(x => x.InsertAsync(
            It.IsAny<NotificationPreference>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserPreferencesAsync_skips_unknown_notification_types()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, TenantId = TenantId });
        perms.Setup(x => x.GetPermissionCodesAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CureFlowPermissions.ClinicalView]);

        await Service(db, Tenant(UserId), perms).UpdateUserPreferencesAsync(
            new UpdateNotificationPreferencesRequest
            {
                Preferences =
                [
                    new NotificationPreferenceUpdateItem
                    {
                        NotificationType = "not.a.real.type",
                        InAppEnabled = true,
                    },
                ],
            });

        db.Verify(x => x.InsertAsync(
            It.IsAny<NotificationPreference>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        db.Verify(x => x.UpdateAsync(
            It.IsAny<NotificationPreference>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetToRoleDefaultsAsync_is_noop_when_unauthenticated()
    {
        var db = NewDb();

        await Service(db, Tenant(null)).ResetToRoleDefaultsAsync();

        db.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetToRoleDefaultsAsync_soft_deletes_user_overrides()
    {
        var db = NewDb();
        db.Setup(x => x.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await Service(db, Tenant(UserId)).ResetToRoleDefaultsAsync();

        db.Verify(x => x.ExecuteAsync(
            It.Is<string>(s => s.Contains("NotificationPreferences") && s.Contains("IsDeleted")),
            It.IsAny<object?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsInAppEnabledForUserAsync_returns_false_for_unknown_type()
    {
        var enabled = await Service(NewDb(), Tenant(UserId))
            .IsInAppEnabledForUserAsync(UserId, "unknown.type.code");

        enabled.Should().BeFalse();
    }

    [Fact]
    public async Task IsInAppEnabledForUserAsync_returns_false_without_required_permissions()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, TenantId = TenantId });
        perms.Setup(x => x.GetPermissionCodesAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CureFlowPermissions.ConversationView]);

        var enabled = await Service(db, Tenant(UserId), perms)
            .IsInAppEnabledForUserAsync(UserId, NotificationTypeCodes.ClinicalLabUploaded);

        enabled.Should().BeFalse();
    }
}
