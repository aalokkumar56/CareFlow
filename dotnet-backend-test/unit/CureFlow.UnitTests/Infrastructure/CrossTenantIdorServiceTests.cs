using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Services;
using CureFlow.Infrastructure.Services.CRM;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Service-level IDOR protections: when the session layer hides cross-tenant rows
/// (GetById / tenant-filtered query returns null), services must fail closed with NotFound.
/// DB-backed cases skip when CUREFLOW_TEST_CONNECTION is unset.
/// </summary>
public class CrossTenantIdorServiceTests
{
    [Fact]
    public async Task PatientService_GetAsync_ThrowsNotFound_WhenSessionHidesCrossTenantRow()
    {
        var foreignPatientId = Guid.NewGuid();
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(d => d.GetByIdAsync<Patient>(foreignPatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var sut = new PatientService(
            db.Object,
            Mock.Of<IAuditService>(),
            Mock.Of<INotificationPublisher>(),
            new CurrentTenant { TenantId = Guid.NewGuid(), IsAuthenticated = true });

        var act = () => sut.GetAsync(foreignPatientId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Patient*");
        db.Verify(d => d.GetByIdAsync<Patient>(foreignPatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatientService_DeleteAsync_ThrowsNotFound_WhenSessionHidesCrossTenantRow()
    {
        var foreignPatientId = Guid.NewGuid();
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(d => d.GetByIdAsync<Patient>(foreignPatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var sut = new PatientService(
            db.Object,
            Mock.Of<IAuditService>(),
            Mock.Of<INotificationPublisher>(),
            new CurrentTenant { TenantId = Guid.NewGuid(), IsAuthenticated = true });

        var act = () => sut.DeleteAsync(foreignPatientId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Patient*");
        db.Verify(d => d.UpdateAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AppointmentService_GetAsync_ThrowsNotFound_WhenTenantFilteredQueryReturnsNull()
    {
        var foreignAppointmentId = Guid.NewGuid();
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(d => d.QueryFirstOrDefaultAsync<Appointment>(
                It.Is<string>(sql => sql.Contains("Appointments", StringComparison.Ordinal)
                    && sql.Contains("TenantId", StringComparison.Ordinal)),
                It.IsAny<object>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var sut = new AppointmentService(
            db.Object,
            Mock.Of<IConversationService>(),
            Mock.Of<IEmailService>(),
            NullLogger<AppointmentService>.Instance,
            Mock.Of<INotificationPublisher>());

        var act = () => sut.GetAsync(foreignAppointmentId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Appointment*");
    }

    [Fact]
    public void PrepareInsert_StampsCurrentTenant_OnTenantEntity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new CurrentTenant
        {
            TenantId = tenantId,
            UserId = userId,
            IsAuthenticated = true,
        };
        var patient = new Patient { Name = "Cross Tenant Write", Phone = "+919900000001" };

        EntityPersistence.PrepareInsert(patient, tenant);

        patient.TenantId.Should().Be(tenantId);
        patient.CreatedBy.Should().Be(userId);
        patient.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void PrepareInsert_DoesNotOverwrite_ExistingTenantId()
    {
        var callerTenant = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();
        var tenant = new CurrentTenant { TenantId = callerTenant, IsAuthenticated = true };
        var patient = new Patient
        {
            Name = "Already Scoped",
            Phone = "+919900000002",
            TenantId = foreignTenant,
        };

        EntityPersistence.PrepareInsert(patient, tenant);

        patient.TenantId.Should().Be(foreignTenant,
            "PrepareInsert only fills empty TenantId; services must not accept client-supplied foreign tenant ids");
    }

    [Fact]
    public async Task DbSession_GetById_ReturnsNull_ForCrossTenantPatient_WhenSeeded()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        Guid alphaTenantId;
        Guid betaTenantId;
        Guid alphaPatientId;

        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
            alphaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = "care-cure-althan" });
            betaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = "city-hospital-surat" });
            alphaPatientId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """
                SELECT "Id" FROM "Patients"
                WHERE "TenantId" = @tenantId AND "Name" = @name AND "IsDeleted" = false
                LIMIT 1
                """,
                new { tenantId = alphaTenantId, name = "Althan Exclusive Patient" });
        }

        if (alphaTenantId == Guid.Empty || betaTenantId == Guid.Empty || alphaPatientId == Guid.Empty)
            return;

        var betaSession = new CureFlowDbSession(dataSource, new CurrentTenant
        {
            TenantId = betaTenantId,
            IsAuthenticated = true,
        });
        var sut = new PatientService(
            betaSession,
            Mock.Of<IAuditService>(),
            Mock.Of<INotificationPublisher>(),
            new CurrentTenant { TenantId = betaTenantId, IsAuthenticated = true });

        var act = () => sut.GetAsync(alphaPatientId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Patient*", "service must not leak Tenant A patient to Tenant B");
    }
}
