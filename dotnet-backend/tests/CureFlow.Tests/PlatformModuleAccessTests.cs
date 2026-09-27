using CureFlow.Application.Areas.Platform;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Areas.Platform;
using FluentAssertions;
using Xunit;
namespace CureFlow.Tests;
public class PlatformModuleAccessTests
{
    [Theory]
    [InlineData("/api/patients", "patients")]
    [InlineData("/api/email/send", "communications")]
    [InlineData("/api/doctors", "referrals")]
    [InlineData("/api/tasks", "follow-ups")]
    [InlineData("/api/staff/booking-options", "appointments")]
    [InlineData("/api/appointments/booking-options", "appointments")]
    [InlineData("/api/lab-reports/id/download", "clinical")]
    [InlineData("/api/dashboard/overview", "analytics")]
    public void Real_api_routes_require_their_capability(string path,string key) => SupportedModules.RequiredForApi(path).Should().Contain(key);
    [Fact] public void Nested_clinical_and_lead_conversion_require_both_capabilities() {
        SupportedModules.RequiredForApi("/api/patients/id/visits").Should().BeEquivalentTo("patients","clinical");
        SupportedModules.RequiredForApi("/api/leads/id/appointment").Should().BeEquivalentTo("leads","appointments");
        SupportedModules.RequiredForApi("/api/tenant/modules").Should().BeEmpty();
    }
    [Fact] public void Patient_and_appointment_only_package_has_exactly_two_effective_modules() {
        var modules=SupportedModules.All.Select(m=>new PlatformModule{Key=m.Key,Name=m.Name,IsCore=true}).ToArray();
        var policies=modules.Select(m=>new PlanModule{PlatformModuleId=m.Id,IsEnabled=m.Key is "patients" or "appointments"}).ToArray();
        PlatformModuleAccess.Evaluate(modules,[],policies,DateTime.UtcNow).Where(m=>m.Effective).Select(m=>m.Key).Should().BeEquivalentTo("patients","appointments");
    }
    [Fact] public void Override_revoke_and_reset_are_effective_immediately() {
        var m=new PlatformModule{Key="patients",IsCore=true};
        var policy=new PlanModule{PlatformModuleId=m.Id,IsEnabled=true};
        var grant=new TenantEntitlement{PlatformModuleId=m.Id,IsEnabled=false};
        EffectiveModule Resolve()=>PlatformModuleAccess.Evaluate([m],[grant],[policy],DateTime.UtcNow).Single();
        Resolve().Effective.Should().BeFalse();
        grant.IsEnabled=true;Resolve().Effective.Should().BeTrue();
        grant.IsDeleted=true;Resolve().Source.Should().Be("plan");Resolve().Effective.Should().BeTrue();
        policy.IsEnabled=false;Resolve().Effective.Should().BeFalse();
    }
    [Fact] public void Expired_grant_and_disabled_module_never_allow_access() {
        var now=DateTime.UtcNow;
        var m=new PlatformModule{Key="patients",IsCore=true};
        var g=new TenantEntitlement{PlatformModuleId=m.Id,IsEnabled=true,ExpiresAt=now};
        PlatformModuleAccess.Evaluate([m],[g],[],now).Single().Effective.Should().BeFalse();
        g.ExpiresAt=null;m.IsActive=false;
        PlatformModuleAccess.Evaluate([m],[g],[],now).Single().Effective.Should().BeFalse();
    }
    [Fact] public void Unknown_database_catalog_entries_do_not_become_capabilities() =>
        PlatformModuleAccess.Evaluate([new PlatformModule{Key="invented",IsCore=true}],[],[],DateTime.UtcNow).Should().BeEmpty();
}
