using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Infrastructure.Services.Ehr;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

public class ClinicalRecordServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PatientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static (ClinicalRecordService Svc, Mock<ICureFlowDbSession> Db, CurrentTenant Tenant) Build(
        Guid? tenantId = null, Guid? userId = null)
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(tenantId ?? TenantId);
        db.SetupGet(x => x.UserId).Returns(userId ?? UserId);
        db.Setup(x => x.InsertAsync(It.IsAny<VitalSigns>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.InsertAsync(It.IsAny<ClinicalNote>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.InsertAsync(It.IsAny<MedicalHistoryItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.GetByIdAsync<User>(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Name = "Dr. Test", TenantId = TenantId });

        var tenant = new CurrentTenant
        {
            TenantId = tenantId ?? TenantId,
            UserId = userId ?? UserId,
            UserEmail = "doctor@test.local",
            IsAuthenticated = true,
        };
        return (new ClinicalRecordService(db.Object, tenant), db, tenant);
    }

    private static CreateVitalSignsRequest Vitals(Guid patientId, decimal? height = null, decimal? weight = null, decimal? hr = 72) =>
        new(patientId, null, null, null, height, weight, null, null, hr, null, null, null, null, null, null, null);

    [Fact]
    public async Task AddVitalsAsync_rejects_missing_tenant()
    {
        var (svc, _, _) = Build(tenantId: Guid.Empty);

        var act = () => svc.AddVitalsAsync(Vitals(PatientId));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Authentication*");
    }

    [Fact]
    public async Task AddVitalsAsync_rejects_empty_patient_id()
    {
        var (svc, _, _) = Build();

        var act = () => svc.AddVitalsAsync(Vitals(Guid.Empty));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*patient_id*");
    }

    [Fact]
    public async Task AddNoteAsync_rejects_missing_tenant()
    {
        var (svc, _, _) = Build(tenantId: Guid.Empty);
        var req = new CreateClinicalNoteRequest(PatientId, null, null, "progress", "S", "O", "A", "P");

        var act = () => svc.AddNoteAsync(req);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Authentication*");
    }

    [Fact]
    public async Task AddNoteAsync_rejects_empty_patient_id()
    {
        var (svc, _, _) = Build();
        var req = new CreateClinicalNoteRequest(Guid.Empty, null, null, "progress", "S", null, null, null);

        var act = () => svc.AddNoteAsync(req);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*patient_id*");
    }

    [Fact]
    public async Task AddMedicalHistoryAsync_rejects_empty_patient_id()
    {
        var (svc, _, _) = Build();
        var req = new CreateMedicalHistoryRequest(Guid.Empty, "allergy", "Penicillin", null, true, null);

        var act = () => svc.AddMedicalHistoryAsync(req);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*patient_id*");
    }

    [Fact]
    public async Task AddVitalsAsync_computes_bmi_and_rounds_whole_number_fields()
    {
        VitalSigns? captured = null;
        var (svc, db, _) = Build();
        db.Setup(x => x.InsertAsync(It.IsAny<VitalSigns>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<VitalSigns, bool, CancellationToken>((v, _, _) => captured = v)
            .Returns(Task.CompletedTask);

        var req = new CreateVitalSignsRequest(
            PatientId, null, null, null,
            HeightCm: 170, WeightKg: 68,
            SystolicBp: 120.4m, DiastolicBp: 80.6m,
            HeartRate: 71.5m, Temperature: 98.6m,
            RespiratoryRate: 16.4m, OxygenSaturation: 98.2m,
            null, null, null, null);

        await svc.AddVitalsAsync(req);

        captured.Should().NotBeNull();
        captured!.Bmi.Should().Be(23.5m);
        captured.SystolicBp.Should().Be(120);
        captured.DiastolicBp.Should().Be(81);
        captured.HeartRate.Should().Be(72);
        captured.RespiratoryRate.Should().Be(16);
        captured.OxygenSaturation.Should().Be(98);
        captured.Temperature.Should().Be(98.6m);
    }

    [Fact]
    public async Task ListVitalsAsync_scopes_query_to_tenant()
    {
        string? sql = null;
        var (svc, db, _) = Build();
        db.Setup(x => x.QueryAsync<VitalSigns>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync(Array.Empty<VitalSigns>());

        await svc.ListVitalsAsync(PatientId);

        sql.Should().NotBeNullOrEmpty();
        sql.Should().Contain(@"""TenantId"" = @TenantId");
        sql.Should().Contain(@"""IsDeleted"" = false");
        sql.Should().Contain(@"""PatientId"" = @patientId");
    }

    [Fact]
    public async Task ListNotesAsync_scopes_query_to_tenant()
    {
        string? sql = null;
        var (svc, db, _) = Build();
        db.Setup(x => x.QueryAsync<ClinicalNote>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync(Array.Empty<ClinicalNote>());

        await svc.ListNotesAsync(PatientId);

        sql.Should().Contain(@"""TenantId"" = @TenantId");
        sql.Should().Contain(@"""PatientId"" = @patientId");
    }
}
