using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Services.Business;
using CureFlow.Infrastructure.Services.CRM;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>Service-level API isolation tests for tenant-scoped endpoints.</summary>
public class TenantApiIsolationTests
{
    [Fact]
    public async Task HospitalProfile_Get_IsScopedToCurrentTenant()
    {
        var connectionString = TestDbConnection.Require();
        await TestSeedHelper.EnsureMultiHospitalAsync(connectionString);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var (alphaId, betaId) = await ResolveE2eTenantIdsAsync(dataSource);
        alphaId.Should().NotBe(Guid.Empty);
        betaId.Should().NotBe(Guid.Empty);

        var alphaCtx = new CurrentTenant { TenantId = alphaId, IsAuthenticated = true };
        var alphaSession = new CureFlowDbSession(dataSource, alphaCtx);
        var alphaSvc = CreateHospitalProfileService(alphaSession, alphaCtx);

        var alphaProfile = await alphaSvc.GetAsync();
        alphaProfile.Should().NotBeNull();

        var betaCtx = new CurrentTenant { TenantId = betaId, IsAuthenticated = true };
        var betaSvc = CreateHospitalProfileService(new CureFlowDbSession(dataSource, betaCtx), betaCtx);
        var betaProfile = await betaSvc.GetAsync();

        var alphaName = GetProfileName(alphaProfile);
        var betaName = GetProfileName(betaProfile);
        alphaName.Should().NotBe(betaName);
        alphaName.Should().Contain("Althan");
        betaName.Should().Contain("Surat");
    }

    [Fact]
    public async Task Appointment_Get_ThrowsNotFound_ForOtherTenantAppointment()
    {
        var connectionString = TestDbConnection.Require();
        await TestSeedHelper.EnsureMultiHospitalAsync(connectionString);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var (alphaId, betaId) = await ResolveE2eTenantIdsAsync(dataSource);
        alphaId.Should().NotBe(Guid.Empty);
        betaId.Should().NotBe(Guid.Empty);

        var alphaAppointmentId = await FindAppointmentIdAsync(dataSource, alphaId);
        alphaAppointmentId.Should().NotBe(Guid.Empty, "multi-hospital seed must include an appointment for Althan");

        var betaCtx = new CurrentTenant { TenantId = betaId, IsAuthenticated = true };
        var betaSession = new CureFlowDbSession(dataSource, betaCtx);
        var betaApptSvc = CreateAppointmentService(betaSession);

        var act = () => betaApptSvc.GetAsync(alphaAppointmentId);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Conversation_Get_ThrowsNotFound_ForOtherTenantConversation()
    {
        var connectionString = TestDbConnection.Require();
        await TestSeedHelper.EnsureMultiHospitalAsync(connectionString);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var (alphaId, betaId) = await ResolveE2eTenantIdsAsync(dataSource);
        alphaId.Should().NotBe(Guid.Empty);
        betaId.Should().NotBe(Guid.Empty);

        var alphaConversationId = await FindConversationIdAsync(dataSource, alphaId);
        alphaConversationId.Should().NotBe(Guid.Empty, "multi-hospital seed must include a conversation for Althan");

        var betaCtx = new CurrentTenant { TenantId = betaId, IsAuthenticated = true };
        var betaSession = new CureFlowDbSession(dataSource, betaCtx);
        var betaConvSvc = CreateConversationService(betaSession, betaCtx);

        var act = () => betaConvSvc.GetAsync(alphaConversationId);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static HospitalProfileService CreateHospitalProfileService(CureFlowDbSession session, CurrentTenant tenant) =>
        new(
            session,
            Mock.Of<CureFlow.Application.Interfaces.IAiService>(),
            Mock.Of<CureFlow.Application.Interfaces.IHospitalWebsiteScraper>(),
            tenant,
            new MemoryCache(new MemoryCacheOptions()));

    private static AppointmentService CreateAppointmentService(CureFlowDbSession session) =>
        new(
            session,
            Mock.Of<CureFlow.Application.Interfaces.IConversationService>(),
            Mock.Of<CureFlow.Application.Interfaces.IEmailService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AppointmentService>.Instance,
            Mock.Of<CureFlow.Application.Interfaces.INotificationPublisher>());

    private static ConversationService CreateConversationService(CureFlowDbSession session, CurrentTenant tenant) =>
        new(
            session,
            Mock.Of<CureFlow.Application.Interfaces.IWhatsappService>(),
            Mock.Of<CureFlow.Application.Interfaces.IWhatsappProvider>(),
            Mock.Of<CureFlow.Application.Interfaces.IWhatsappMediaStore>(),
            Microsoft.Extensions.Options.Options.Create(new CureFlow.Application.Options.WhatsappMediaOptions()),
            tenant);

    private static async Task<(Guid Alpha, Guid Beta)> ResolveE2eTenantIdsAsync(NpgsqlDataSource dataSource)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var alpha = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<Guid>(conn,
            """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
            new { slug = "care-cure-althan" });
        var beta = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<Guid>(conn,
            """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
            new { slug = "city-hospital-surat" });
        return (alpha, beta);
    }

    private static async Task<Guid> FindAppointmentIdAsync(NpgsqlDataSource dataSource, Guid tenantId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
        return await Dapper.SqlMapper.QueryFirstOrDefaultAsync<Guid>(conn,
            """
            SELECT "Id" FROM "Appointments"
            WHERE "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId });
    }

    private static async Task<Guid> FindConversationIdAsync(NpgsqlDataSource dataSource, Guid tenantId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
        return await Dapper.SqlMapper.QueryFirstOrDefaultAsync<Guid>(conn,
            """
            SELECT "Id" FROM "Conversations"
            WHERE "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId });
    }

    private static string GetProfileName(object profile)
    {
        var prop = profile.GetType().GetProperty("name");
        return prop?.GetValue(profile)?.ToString() ?? "";
    }
}
