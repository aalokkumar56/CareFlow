using CureFlow.Application.Common;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace CureFlow.Tests;

public class WhatsappNotificationTypeTests
{
    [Fact]
    public void Inbound_message_requires_conversation_view_permission()
    {
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.WhatsappInboundMessage);

        def.RequiredPermissions.Should().Contain(CureFlowPermissions.ConversationView);
        NotificationRbac.HasAllPermissions(
            [CureFlowPermissions.ConversationView],
            def.RequiredPermissions).Should().BeTrue();
    }

    [Fact]
    public void Inbound_message_denied_without_conversation_view()
    {
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.WhatsappInboundMessage);

        NotificationRbac.HasAllPermissions(
            [CureFlowPermissions.DashboardView],
            def.RequiredPermissions).Should().BeFalse();
    }

    [Fact]
    public void Lead_escalation_requires_conversation_manage_permission()
    {
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.WhatsappLeadEscalation);

        def.RequiredPermissions.Should().Contain(CureFlowPermissions.ConversationManage);
        def.DefaultSeverity.Should().Be(NotificationSeverity.Warning);
    }

    [Fact]
    public void Lead_escalation_uses_permission_holders_strategy()
    {
        var def = NotificationTypeDefinitions.Get(NotificationTypeCodes.WhatsappLeadEscalation);

        def.RecipientStrategy.Should().Be(NotificationRecipientStrategy.PermissionHolders);
    }
}
