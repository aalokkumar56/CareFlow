using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

public class CampaignAudienceIsolationTests
{
    private static readonly Guid TenantId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid UserId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private static (CampaignService Svc, Mock<ICureFlowDbSession> Db) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        db.SetupGet(x => x.UserId).Returns(UserId);
        db.Setup(x => x.InsertAsync(It.IsAny<Campaign>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.UpdateAsync(It.IsAny<Campaign>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Patient>());

        var tenant = new CurrentTenant { TenantId = TenantId, UserId = UserId, IsAuthenticated = true };
        var svc = new CampaignService(
            db.Object,
            Mock.Of<IWhatsappMessagingService>(),
            tenant,
            Mock.Of<ILogger<CampaignService>>(),
            Mock.Of<INotificationPublisher>());
        return (svc, db);
    }

    [Fact]
    public async Task PreviewAudienceAsync_always_filters_by_tenant_and_soft_delete()
    {
        string? sql = null;
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync(Array.Empty<Patient>());

        await svc.PreviewAudienceAsync(new { });

        sql.Should().NotBeNullOrEmpty();
        sql.Should().Contain(@"""TenantId"" = @TenantId");
        sql.Should().Contain(@"""IsDeleted"" = false");
        sql.Should().Contain(@"""Phone"" <> ''");
        // Audience resolution must never opt out of tenant scoping.
        sql.Should().NotContain("1=1");
    }

    [Fact]
    public async Task PreviewAudienceAsync_applies_department_and_status_filters()
    {
        string? sql = null;
        object? param = null;
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, p, _, _) =>
            {
                sql = s;
                param = p;
            })
            .ReturnsAsync(Array.Empty<Patient>());

        await svc.PreviewAudienceAsync(new
        {
            departments = new[] { "Cardiology", "Ortho" },
            statuses = new[] { "visited", "contacted" },
        });

        sql.Should().Contain(@"""Department"" = ANY(@departments)");
        sql.Should().Contain(@"""Status"" = ANY(@statuses)");
        sql.Should().Contain(@"""TenantId"" = @TenantId");

        var dict = param.Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        dict["departments"].Should().BeEquivalentTo(new[] { "Cardiology", "Ortho" });
        dict["statuses"].Should().BeEquivalentTo(new[] { (int)LeadStatus.Visited, (int)LeadStatus.Contacted });
    }

    [Fact]
    public async Task PreviewAudienceAsync_applies_tag_filter_with_tenant_scope()
    {
        string? sql = null;
        object? param = null;
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, p, _, _) =>
            {
                sql = s;
                param = p;
            })
            .ReturnsAsync(Array.Empty<Patient>());

        await svc.PreviewAudienceAsync(new { tags = new[] { "vip", "diabetes" } });

        sql.Should().Contain("jsonb_array_elements_text");
        sql.Should().Contain("@tags");
        sql.Should().Contain(@"""TenantId"" = @TenantId");
        var dict = param.Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        dict["tags"].Should().BeEquivalentTo(new[] { "vip", "diabetes" });
    }

    [Fact]
    public async Task PreviewAudienceAsync_returns_only_mocked_tenant_patients()
    {
        var tenantPatients = new List<Patient>
        {
            new() { Id = Guid.NewGuid(), Name = "A", Phone = "9000000001", TenantId = TenantId },
            new() { Id = Guid.NewGuid(), Name = "B", Phone = "9000000002", TenantId = TenantId },
        };
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantPatients);

        var (count, sample) = await svc.PreviewAudienceAsync(new { departments = new[] { "General" } });

        count.Should().Be(2);
        sample.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_stores_audience_json_and_tenant_scoped_recipient_count()
    {
        Campaign? captured = null;
        string? audienceSql = null;
        var patients = new List<Patient>
        {
            new() { Id = Guid.NewGuid(), Name = "A", Phone = "9000000001", TenantId = TenantId },
        };
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => audienceSql = s)
            .ReturnsAsync(patients);
        db.Setup(x => x.InsertAsync(It.IsAny<Campaign>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Campaign, bool, CancellationToken>((c, _, _) => captured = c)
            .Returns(Task.CompletedTask);

        var audience = new { departments = new[] { "ENT" } };
        var id = await svc.CreateAsync("ENT blast", "desc", "Hello {{name}}", audience);

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.TotalRecipients.Should().Be(1);
        captured.CreatedByUserId.Should().Be(UserId);
        captured.AudienceJson.Should().Contain("ENT");
        audienceSql.Should().Contain(@"""TenantId"" = @TenantId");
    }

    [Fact]
    public async Task ListAsync_scopes_campaigns_to_tenant()
    {
        string? sql = null;
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Campaign>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync(Array.Empty<Campaign>());

        await svc.ListAsync();

        sql.Should().Contain(@"""TenantId"" = @TenantId");
        sql.Should().Contain(@"""IsDeleted"" = false");
    }

    [Fact]
    public async Task GetAsync_scopes_campaign_and_recipients_to_tenant()
    {
        var captured = new List<string>();
        var campaignId = Guid.NewGuid();
        var (svc, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Campaign>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => captured.Add(s))
            .ReturnsAsync(new Campaign
            {
                Id = campaignId,
                Name = "Test",
                MessageBody = "Hi",
                AudienceJson = """{"departments":["General"]}""",
                TenantId = TenantId,
            });
        db.Setup(x => x.QueryAsync<CampaignRecipient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => captured.Add(s))
            .ReturnsAsync(Array.Empty<CampaignRecipient>());

        await svc.GetAsync(campaignId);

        captured.Should().HaveCount(2);
        captured.Should().OnlyContain(s => s.Contains(@"""TenantId"" = @TenantId"));
    }

    [Fact]
    public async Task ScheduleAsync_rejects_already_sent_campaign()
    {
        var (svc, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Campaign>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Campaign
            {
                Id = Guid.NewGuid(),
                Name = "Done",
                MessageBody = "x",
                Status = CampaignStatus.Sent,
                TenantId = TenantId,
            });

        var act = () => svc.ScheduleAsync(Guid.NewGuid(), DateTime.UtcNow.AddDays(1));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already sent*");
    }

    [Fact]
    public async Task UpdateAsync_rejects_editing_sent_campaign()
    {
        var (svc, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Campaign>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Campaign
            {
                Id = Guid.NewGuid(),
                Name = "Done",
                MessageBody = "x",
                Status = CampaignStatus.Sending,
                TenantId = TenantId,
            });

        var act = () => svc.UpdateAsync(Guid.NewGuid(), new UpdateCampaignRequest("new name", null, null, null, false, null));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already sent*");
    }
}
