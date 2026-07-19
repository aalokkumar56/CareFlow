using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

public class VisitServiceValidationTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PatientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VisitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DoctorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static (VisitService Svc, Mock<ICureFlowDbSession> Db) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        db.SetupGet(x => x.UserId).Returns(DoctorId);

        var tenant = new CurrentTenant
        {
            TenantId = TenantId,
            UserId = DoctorId,
            IsAuthenticated = true,
        };
        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storage = Options.Create(new StorageOptions { LabReportsPath = "lab-reports" });
        return (new VisitService(db.Object, tenant, audit.Object, storage), db);
    }

    [Fact]
    public async Task CreateAsync_throws_when_patient_missing()
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<Patient>(PatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var act = () => svc.CreateAsync(new CreateVisitRequest(
            PatientId, null, DoctorId, null, null, "fever", null, null, null, null));

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Patient*");
    }

    [Fact]
    public async Task CreateAsync_returns_existing_visit_for_same_appointment()
    {
        var appointmentId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var (svc, db) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Visit>(
                It.Is<string>(s => s.Contains("AppointmentId")),
                It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Visit { Id = existingId, PatientId = PatientId, AppointmentId = appointmentId });

        var id = await svc.CreateAsync(new CreateVisitRequest(
            PatientId, appointmentId, DoctorId, null, null, null, null, null, null, null));

        id.Should().Be(existingId);
        db.Verify(x => x.InsertAsync(It.IsAny<Visit>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_inserts_visit_when_patient_exists()
    {
        Visit? captured = null;
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<Patient>(PatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = PatientId, Name = "Pat", Department = "Ortho", TenantId = TenantId });
        db.Setup(x => x.GetByIdAsync<User>(DoctorId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = DoctorId, Name = "Dr. House", TenantId = TenantId });
        db.Setup(x => x.InsertAsync(It.IsAny<Visit>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Visit, bool, CancellationToken>((v, _, _) => captured = v)
            .Returns(Task.CompletedTask);

        var id = await svc.CreateAsync(new CreateVisitRequest(
            PatientId, null, DoctorId, null, "outpatient", "knee pain", "sprain", null, null, null));

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.PatientId.Should().Be(PatientId);
        captured.DoctorName.Should().Be("Dr. House");
        captured.Department.Should().Be("Ortho");
        captured.Symptoms.Should().Be("knee pain");
    }

    [Fact]
    public async Task GetAsync_throws_when_visit_missing()
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<Visit>(VisitId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Visit?)null);

        var act = () => svc.GetAsync(VisitId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Visit*");
    }

    [Fact]
    public async Task UpdateAsync_throws_when_visit_missing()
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<Visit>(VisitId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Visit?)null);

        var act = () => svc.UpdateAsync(VisitId, new UpdateVisitRequest("updated", null, null, null, null, null));

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Visit*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UploadLabReportAsync_rejects_empty_file(long size)
    {
        var (svc, _) = Build();
        await using var stream = new MemoryStream(new byte[] { 1 });

        var act = () => svc.UploadLabReportAsync(PatientId, null, "CBC", stream, "report.pdf", "application/pdf", size);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.StatusCode.Should().Be(400);
        ex.Which.Message.Should().Contain("File is required");
    }

    [Fact]
    public async Task UploadLabReportAsync_rejects_oversized_file()
    {
        var (svc, _) = Build();
        await using var stream = new MemoryStream(new byte[] { 1 });
        var tooBig = 10 * 1024 * 1024 + 1;

        var act = () => svc.UploadLabReportAsync(PatientId, null, "CBC", stream, "report.pdf", "application/pdf", tooBig);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Message.Should().Contain("10 MB");
    }

    [Theory]
    [InlineData("report.exe", "application/pdf")]
    [InlineData("report", "application/pdf")]
    [InlineData("report.pdf", "application/zip")]
    public async Task UploadLabReportAsync_rejects_disallowed_type(string fileName, string contentType)
    {
        var (svc, _) = Build();
        await using var stream = new MemoryStream(new byte[] { 1 });

        var act = () => svc.UploadLabReportAsync(PatientId, null, "CBC", stream, fileName, contentType, 100);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Message.Should().Contain("Allowed file types");
    }

    [Fact]
    public async Task UploadLabReportAsync_rejects_unknown_patient()
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<Patient>(PatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        await using var stream = new MemoryStream(new byte[] { 1 });

        var act = () => svc.UploadLabReportAsync(PatientId, null, "CBC", stream, "lab.pdf", "application/pdf", 100);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Patient*");
    }

    [Fact]
    public async Task UploadLabReportAsync_rejects_visit_not_belonging_to_patient()
    {
        var (svc, db) = Build();
        db.Setup(x => x.GetByIdAsync<Patient>(PatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = PatientId, Name = "Pat", TenantId = TenantId });
        db.Setup(x => x.QueryFirstOrDefaultAsync<Visit>(
                It.Is<string>(s => s.Contains("PatientId")),
                It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Visit?)null);
        await using var stream = new MemoryStream(new byte[] { 1 });

        var act = () => svc.UploadLabReportAsync(PatientId, VisitId, "CBC", stream, "lab.pdf", "application/pdf", 100);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Visit*");
    }
}
