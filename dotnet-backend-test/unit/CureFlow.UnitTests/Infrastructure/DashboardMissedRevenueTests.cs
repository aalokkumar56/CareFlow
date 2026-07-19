using System.Reflection;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>
/// Dashboard missed-revenue loss helpers (default ₹ amounts + consultation fee override).
/// </summary>
public class DashboardMissedRevenueTests
{
    private static readonly Guid TenantId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid UserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static DashboardService Build(
        Mock<ICureFlowDbSession> db,
        out IMemoryCache cache)
    {
        cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(TenantId);
        tenant.SetupGet(x => x.UserId).Returns(UserId);
        return new DashboardService(db.Object, cache, tenant.Object);
    }

    private static void SetupEmptyMissedQueries(Mock<ICureFlowDbSession> db)
    {
        db.Setup(x => x.QueryAsync<Conversation>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Conversation>());
        db.Setup(x => x.QueryAsync<Appointment>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Appointment>());
        db.Setup(x => x.QueryAsync<TaskItem>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TaskItem>());
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Patient>());
    }

    private static decimal PropDecimal(object root, string name) =>
        Convert.ToDecimal(root.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)!
            .GetValue(root)!);

    private static object Prop(object root, string name) =>
        root.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)!
            .GetValue(root)!;

    [Fact]
    public async Task GetMissedRevenueAsync_zero_when_no_events()
    {
        var db = new Mock<ICureFlowDbSession>();
        SetupEmptyMissedQueries(db);
        var svc = Build(db, out _);

        var result = await svc.GetMissedRevenueAsync();

        PropDecimal(result, "estimated_loss").Should().Be(0m);
        PropDecimal(result, "recoverable_revenue").Should().Be(0m);
    }

    [Fact]
    public async Task GetMissedRevenueAsync_applies_default_loss_amounts()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryAsync<Conversation>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Conversation { Id = Guid.NewGuid(), AwaitingReplySince = DateTime.UtcNow.AddHours(-1) },
            });
        db.Setup(x => x.QueryAsync<Appointment>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Appointment { Id = Guid.NewGuid(), Status = AppointmentStatus.NoShow },
            });
        db.Setup(x => x.QueryAsync<TaskItem>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TaskItem { Id = Guid.NewGuid(), Status = CureFlow.Domain.Enums.TaskStatus.Pending, DueAt = DateTime.UtcNow.AddDays(-1) },
            });
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Patient { Id = Guid.NewGuid(), Status = LeadStatus.ReEngagement },
            });

        var svc = Build(db, out _);
        var result = await svc.GetMissedRevenueAsync();

        // 500 + 1500 + 800 + 2000 = 4800; recoverable = Round(4800 * 0.65, 0)
        PropDecimal(result, "estimated_loss").Should().Be(4800m);
        PropDecimal(result, "recoverable_revenue").Should().Be(3120m);
    }

    [Fact]
    public async Task GetMissedRevenueAsync_uses_consultation_fee_when_present()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(x => x.QueryAsync<Conversation>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Conversation>());
        db.Setup(x => x.QueryAsync<Appointment>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Appointment
                {
                    Id = Guid.NewGuid(),
                    Status = AppointmentStatus.NoShow,
                    ConsultationFee = 2500m,
                },
            });
        db.Setup(x => x.QueryAsync<TaskItem>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TaskItem>());
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Patient>());

        var svc = Build(db, out _);
        var result = await svc.GetMissedRevenueAsync();

        PropDecimal(result, "estimated_loss").Should().Be(2500m);
    }

    [Fact]
    public async Task GetMissedRevenueAsync_exposes_breakdown_collections()
    {
        var db = new Mock<ICureFlowDbSession>();
        SetupEmptyMissedQueries(db);
        db.Setup(x => x.QueryAsync<Conversation>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Conversation { Id = Guid.NewGuid(), Name = "Lead", WaPhone = "9198" },
            });

        var svc = Build(db, out _);
        var result = await svc.GetMissedRevenueAsync();

        var unanswered = Prop(result, "unanswered_inquiries") as System.Collections.IEnumerable;
        unanswered.Should().NotBeNull();
        unanswered!.Cast<object>().Should().ContainSingle();
    }
}
