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

namespace CureFlow.UnitTests.Infrastructure;

public class NotificationRbacTests
{
    [Fact]
    public void Reception_without_clinical_permission_fails_clinical_type()
    {
        var perms = new[] { CureFlowPermissions.ConversationView, CureFlowPermissions.AppointmentView };
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.ClinicalLabUploaded);

        NotificationRbac.HasAllPermissions(perms, def.RequiredPermissions).Should().BeFalse();
    }

    [Fact]
    public void User_with_clinical_permission_passes_clinical_type()
    {
        var perms = new[] { CureFlowPermissions.ClinicalView };
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.ClinicalLabUploaded);

        NotificationRbac.HasAllPermissions(perms, def.RequiredPermissions).Should().BeTrue();
    }
}

public class NotificationPreferenceResolverTests
{
    [Fact]
    public void User_override_wins_over_role_default()
    {
        NotificationPreferenceResolver.ResolveInAppEnabled(
            hasRequiredPermissions: true,
            userOverride: false,
            roleDefault: true,
            systemDefault: true).Should().BeFalse();
    }

    [Fact]
    public void Role_default_used_when_no_user_override()
    {
        NotificationPreferenceResolver.ResolveInAppEnabled(
            hasRequiredPermissions: true,
            userOverride: null,
            roleDefault: false,
            systemDefault: true).Should().BeFalse();
    }

    [Fact]
    public void Missing_permissions_always_disabled()
    {
        NotificationPreferenceResolver.ResolveInAppEnabled(
            hasRequiredPermissions: false,
            userOverride: true,
            roleDefault: true,
            systemDefault: true).Should().BeFalse();
    }
}

/// <summary>Maps recipient strategies onto fan-out-on-read audiences (no DB enumeration).</summary>
public class NotificationAudienceResolverTests
{
    private static readonly NotificationRecipientResolver Resolver = new();

    [Fact]
    public void PermissionHolders_strategy_becomes_permission_broadcast()
    {
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.ClinicalLabUploaded);
        var request = new NotificationPublishRequest(NotificationTypeCodes.ClinicalLabUploaded, "Lab uploaded");

        var audience = Resolver.ResolveAudience(def, request);

        audience.Mode.Should().Be(NotificationAudienceMode.Permission);
        audience.TargetUserIds.Should().BeEmpty();
        audience.RequiredPermissions.Should().Contain(CureFlowPermissions.ClinicalView);
    }

    [Fact]
    public void Appointment_strategy_becomes_permission_broadcast()
    {
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.AppointmentCreated);
        var request = new NotificationPublishRequest(
            NotificationTypeCodes.AppointmentCreated, "Appt", DoctorUserId: Guid.NewGuid());

        var audience = Resolver.ResolveAudience(def, request);

        audience.Mode.Should().Be(NotificationAudienceMode.Permission);
    }

    [Fact]
    public void Task_assigned_is_directed_to_the_assignee_only()
    {
        var assignee = Guid.NewGuid();
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.TaskAssigned);
        var request = new NotificationPublishRequest(
            NotificationTypeCodes.TaskAssigned, "Task",
            TargetUserIds: [assignee],
            AssignedStaffId: assignee,
            CreatorUserId: Guid.NewGuid());

        var audience = Resolver.ResolveAudience(def, request);

        audience.Mode.Should().Be(NotificationAudienceMode.Directed);
        audience.TargetUserIds.Should().ContainSingle().Which.Should().Be(assignee);
    }

    [Fact]
    public void Creator_is_excluded_from_directed_targets()
    {
        var creator = Guid.NewGuid();
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.TaskAssigned);
        var request = new NotificationPublishRequest(
            NotificationTypeCodes.TaskAssigned, "Task",
            TargetUserIds: [creator],
            CreatorUserId: creator);

        var audience = Resolver.ResolveAudience(def, request);

        audience.TargetUserIds.Should().BeEmpty();
    }
}

/// <summary>Write path: exactly one event row per broadcast, event-level dedupe, creator stamped.</summary>
public class NotificationPublisherTests
{
    private static (NotificationPublisher Publisher, Mock<ICureFlowDbSession> Db) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        db.SetupGet(x => x.UserId).Returns((Guid?)null);
        var publisher = new NotificationPublisher(
            db.Object,
            new NotificationRecipientResolver(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<NotificationPublisher>>());
        return (publisher, db);
    }

    [Fact]
    public async Task Broadcast_creates_exactly_one_event_row_regardless_of_recipients()
    {
        var (publisher, db) = Build();

        await publisher.PublishAsync(new NotificationPublishRequest(
            NotificationTypeCodes.ClinicalLabUploaded,
            "Lab report uploaded",
            Body: "Lab report uploaded for a patient"));

        // No per-user fan-out: a single shared NotificationEvent row, no matter the audience size.
        db.Verify(x => x.InsertAsync(
            It.IsAny<NotificationEvent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Permission_broadcast_stores_permission_mode_and_creator()
    {
        var (publisher, db) = Build();
        NotificationEvent? captured = null;
        db.Setup(x => x.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, bool, CancellationToken>((e, _, _) => captured = e)
            .Returns(Task.CompletedTask);
        var creator = Guid.NewGuid();

        await publisher.PublishAsync(new NotificationPublishRequest(
            NotificationTypeCodes.ClinicalLabUploaded, "Lab", CreatorUserId: creator));

        captured.Should().NotBeNull();
        captured!.AudienceMode.Should().Be(NotificationAudienceMode.Permission);
        captured.CreatedByUserId.Should().Be(creator);
        captured.TargetUserIdsJson.Should().BeNull();
    }

    [Fact]
    public async Task Directed_event_stores_only_the_assignee()
    {
        var (publisher, db) = Build();
        NotificationEvent? captured = null;
        db.Setup(x => x.InsertAsync(It.IsAny<NotificationEvent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEvent, bool, CancellationToken>((e, _, _) => captured = e)
            .Returns(Task.CompletedTask);
        var assignee = Guid.NewGuid();

        await publisher.PublishAsync(new NotificationPublishRequest(
            NotificationTypeCodes.TaskAssigned, "Task",
            TargetUserIds: [assignee],
            CreatorUserId: Guid.NewGuid()));

        captured.Should().NotBeNull();
        captured!.AudienceMode.Should().Be(NotificationAudienceMode.Directed);
        captured.TargetUserIdsJson.Should().Contain(assignee.ToString());
    }

    [Fact]
    public async Task Skips_insert_when_event_dedupe_key_already_exists()
    {
        var (publisher, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Guid?>(
                It.Is<string>(s => s.Contains("DedupeKey")),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await publisher.PublishAsync(new NotificationPublishRequest(
            NotificationTypeCodes.AppointmentNoShow,
            "No-show",
            DedupeKey: "appointment.no_show:abc"));

        db.Verify(x => x.InsertAsync(
            It.IsAny<NotificationEvent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>Read path: creator exclusion, RBAC ceiling, single-user receipts, O(1) mark-all-read.</summary>
public class NotificationServiceReadPathTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DoctorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReceptionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid EventId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static Mock<ICureFlowDbSession> NewDb()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        return db;
    }

    private static Mock<ITenantContext> Tenant(Guid userId)
    {
        var t = new Mock<ITenantContext>();
        t.SetupGet(x => x.UserId).Returns(userId);
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        return t;
    }

    private static void SetupUserPerms(Mock<ICureFlowDbSession> db, Mock<IUserPermissionService> perms, Guid userId, string[] codes)
    {
        db.Setup(x => x.GetByIdAsync<User>(userId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, TenantId = TenantId });
        perms.Setup(x => x.GetPermissionCodesAsync(It.Is<User>(u => u.Id == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(codes);
    }

    private static void SetupEvent(Mock<ICureFlowDbSession> db, NotificationEvent evt)
    {
        db.Setup(x => x.QueryFirstOrDefaultAsync<NotificationEvent>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);
    }

    private static NotificationService Service(
        Mock<ICureFlowDbSession> db, Mock<ITenantContext> tenant, Mock<IUserPermissionService> perms)
        => new(db.Object, tenant.Object, perms.Object, Mock.Of<INotificationPreferenceService>());

    private static NotificationEvent PermissionEvent(Guid? createdBy) => new()
    {
        Id = EventId,
        TenantId = TenantId,
        Type = NotificationTypeCodes.ClinicalLabUploaded,
        Title = "Lab uploaded",
        AudienceMode = NotificationAudienceMode.Permission,
        CreatedByUserId = createdBy,
    };

    [Fact]
    public async Task Creator_cannot_see_or_mark_their_own_event()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        SetupEvent(db, PermissionEvent(createdBy: CreatorId));
        var svc = Service(db, Tenant(CreatorId), perms);

        var act = async () => await svc.MarkReadAsync(EventId);

        await act.Should().ThrowAsync<NotFoundException>();
        db.Verify(x => x.InsertAsync(It.IsAny<NotificationReceipt>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Permitted_user_marks_event_read_with_single_receipt()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        SetupEvent(db, PermissionEvent(createdBy: CreatorId));
        SetupUserPerms(db, perms, DoctorId, [CureFlowPermissions.ClinicalView]);
        // No pre-existing receipt.
        db.Setup(x => x.QueryFirstOrDefaultAsync<NotificationReceipt>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationReceipt?)null);
        NotificationReceipt? captured = null;
        db.Setup(x => x.InsertAsync(It.IsAny<NotificationReceipt>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationReceipt, bool, CancellationToken>((r, _, _) => captured = r)
            .Returns(Task.CompletedTask);

        var svc = Service(db, Tenant(DoctorId), perms);
        await svc.MarkReadAsync(EventId);

        db.Verify(x => x.InsertAsync(It.IsAny<NotificationReceipt>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(DoctorId);
        captured.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task User_without_permission_cannot_mark_event()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        SetupEvent(db, PermissionEvent(createdBy: CreatorId));
        SetupUserPerms(db, perms, ReceptionId, [CureFlowPermissions.ConversationView]);

        var svc = Service(db, Tenant(ReceptionId), perms);
        var act = async () => await svc.MarkReadAsync(EventId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Mark_all_read_updates_cursor_without_per_event_receipts()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        db.Setup(x => x.QueryFirstOrDefaultAsync<NotificationFeedCursor>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationFeedCursor { UserId = DoctorId, TenantId = TenantId, FeedSince = DateTime.UtcNow.AddDays(-1) });

        var svc = Service(db, Tenant(DoctorId), perms);
        await svc.MarkAllReadAsync();

        db.Verify(x => x.UpdateAsync(It.IsAny<NotificationFeedCursor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(x => x.InsertAsync(It.IsAny<NotificationReceipt>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_dismisses_event_for_requesting_user_only()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        SetupEvent(db, PermissionEvent(createdBy: CreatorId));
        SetupUserPerms(db, perms, DoctorId, [CureFlowPermissions.ClinicalView]);
        db.Setup(x => x.QueryFirstOrDefaultAsync<NotificationReceipt>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationReceipt?)null);
        NotificationReceipt? captured = null;
        db.Setup(x => x.InsertAsync(It.IsAny<NotificationReceipt>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationReceipt, bool, CancellationToken>((r, _, _) => captured = r)
            .Returns(Task.CompletedTask);

        var svc = Service(db, Tenant(DoctorId), perms);
        await svc.DeleteAsync(EventId);

        db.Verify(x => x.InsertAsync(It.IsAny<NotificationReceipt>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(DoctorId);
        captured.DismissedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Directed_event_not_targeting_user_is_not_visible()
    {
        var db = NewDb();
        var perms = new Mock<IUserPermissionService>();
        var directed = new NotificationEvent
        {
            Id = EventId,
            TenantId = TenantId,
            Type = NotificationTypeCodes.TaskAssigned,
            AudienceMode = NotificationAudienceMode.Directed,
            CreatedByUserId = CreatorId,
            TargetUserIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { ReceptionId.ToString() }),
        };
        SetupEvent(db, directed);
        SetupUserPerms(db, perms, DoctorId, [CureFlowPermissions.DashboardView]);

        var svc = Service(db, Tenant(DoctorId), perms);
        var act = async () => await svc.MarkReadAsync(EventId);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
