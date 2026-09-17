using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CureFlow.Tests;

public class CampaignAudienceEditTests
{
    private readonly Mock<ICureFlowDbSession> db = new();
    private readonly Campaign campaign = new() { Name = "Campaign", AudienceJson = "{\"source\":\"patients\",\"patient_ids\":[]}", TotalRecipients = 9 };
    private CampaignService Service => new(db.Object, Mock.Of<IWhatsappMessagingService>(), Mock.Of<ITenantContext>(), NullLogger<CampaignService>.Instance, Mock.Of<INotificationPublisher>());

    public CampaignAudienceEditTests()
    {
        db.Setup(d => d.QueryFirstOrDefaultAsync<Campaign>(It.IsAny<string>(), It.IsAny<object>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(() => campaign);
        db.Setup(d => d.QueryAsync<CampaignRecipient>(It.IsAny<string>(), It.IsAny<object>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CampaignRecipient>());
        db.Setup(d => d.UpdateAsync(It.IsAny<Campaign>(), false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Theory]
    [InlineData("patients")]
    [InlineData("leads")]
    public async Task Save_and_reopen_returns_selected_audience_and_recalculates_count(string source)
    {
        var id = Guid.NewGuid();
        db.Setup(d => d.QueryAsync<Patient>(It.IsAny<string>(), It.IsAny<object>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Patient { Id = id, Name = "Selected patient", Phone = "919876543210" } });
        db.Setup(d => d.QueryAsync<Lead>(It.IsAny<string>(), It.IsAny<object>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Lead { Id = id, Name = "Selected lead", Phone = "919876543210" } });
        var json = JsonSerializer.Serialize(new Dictionary<string, object> { ["source"] = source, [source == "leads" ? "lead_ids" : "patient_ids"] = new[] { id.ToString() } });
        // Exercise the same JSON request deserialization used by the API.
        var request = JsonSerializer.Deserialize<UpdateCampaignRequest>("{\"audience\":" + json + "}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await Service.UpdateAsync(campaign.Id, request);
        db.Verify(d => d.UpdateAsync(It.Is<Campaign>(c => c.AudienceJson == json && c.TotalRecipients == 1), false, It.IsAny<CancellationToken>()), Times.Once);
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(await Service.GetAsync(campaign.Id)));
        Assert.Equal(source, result.RootElement.GetProperty("audience").GetProperty("source").GetString());
        Assert.Equal(id.ToString(), result.RootElement.GetProperty("audience").GetProperty(source == "leads" ? "lead_ids" : "patient_ids")[0].GetString());
    }

    [Fact]
    public async Task Editing_only_name_preserves_saved_audience_and_count()
    {
        var original = campaign.AudienceJson;
        await Service.UpdateAsync(campaign.Id, new UpdateCampaignRequest(Name: "Renamed"));
        Assert.Equal(original, campaign.AudienceJson);
        Assert.Equal(9, campaign.TotalRecipients);
    }

    [Theory]
    [InlineData(CampaignStatus.Sent)]
    [InlineData(CampaignStatus.Sending)]
    public async Task Cannot_change_audience_after_sending(CampaignStatus status)
    {
        campaign.Status = status;
        await Assert.ThrowsAsync<ValidationException>(() => Service.UpdateAsync(campaign.Id, new UpdateCampaignRequest(Audience: new { source = "leads" })));
        db.Verify(d => d.UpdateAsync(It.IsAny<Campaign>(), false, It.IsAny<CancellationToken>()), Times.Never);
    }
}
