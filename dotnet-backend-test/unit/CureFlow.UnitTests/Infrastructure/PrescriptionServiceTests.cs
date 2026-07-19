using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Services.Ehr;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

public class PrescriptionServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PatientId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid RxId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static (PrescriptionService Svc, Mock<ICureFlowDbSession> Db) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        db.SetupGet(x => x.UserId).Returns(UserId);
        db.Setup(x => x.GetByIdAsync<User>(UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Name = "Dr. Strange", TenantId = TenantId });
        db.Setup(x => x.TransactionAsync(It.IsAny<Func<ICureFlowDbSession, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ICureFlowDbSession, Task>, CancellationToken>(async (action, ct) => await action(db.Object));
        db.Setup(x => x.InsertAsync(It.IsAny<Prescription>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.InsertAsync(It.IsAny<PrescriptionItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(x => x.InsertAsync(It.IsAny<Injection>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenant = new CurrentTenant { TenantId = TenantId, UserId = UserId, IsAuthenticated = true };
        return (new PrescriptionService(db.Object, tenant, audit.Object), db);
    }

    private static CreatePrescriptionRequest Request(params PrescriptionItemRequest[] items) =>
        new(PatientId, null, null, "Flu", "Fever", null, null, null, items.ToList(), null);

    private static PrescriptionItemRequest Item(string drug = "Paracetamol") =>
        new(drug, null, "500mg", "tablet", PrescriptionRouteType.Oral, "1-0-1", "TID", "5 days",
            "after food", 10, "fever", null, null, false, true);

    [Fact]
    public async Task CreateAsync_persists_prescription_and_items_in_transaction()
    {
        Prescription? rx = null;
        var items = new List<PrescriptionItem>();
        var (svc, db) = Build();
        db.Setup(x => x.InsertAsync(It.IsAny<Prescription>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Prescription, bool, CancellationToken>((p, _, _) => rx = p)
            .Returns(Task.CompletedTask);
        db.Setup(x => x.InsertAsync(It.IsAny<PrescriptionItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<PrescriptionItem, bool, CancellationToken>((i, _, _) => items.Add(i))
            .Returns(Task.CompletedTask);

        var id = await svc.CreateAsync(Request(Item("Amoxicillin"), Item("Ibuprofen")));

        id.Should().NotBeEmpty();
        rx.Should().NotBeNull();
        rx!.PatientId.Should().Be(PatientId);
        rx.DoctorUserId.Should().Be(UserId);
        rx.DoctorName.Should().Be("Dr. Strange");
        rx.Diagnosis.Should().Be("Flu");
        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.PrescriptionId == rx.Id);
    }

    [Fact]
    public async Task CreateAsync_persists_injections_linked_to_prescription()
    {
        var injections = new List<Injection>();
        var (svc, db) = Build();
        db.Setup(x => x.InsertAsync(It.IsAny<Injection>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Injection, bool, CancellationToken>((j, _, _) => injections.Add(j))
            .Returns(Task.CompletedTask);

        var req = new CreatePrescriptionRequest(
            PatientId, null, null, null, null, null, null, null,
            [Item()],
            [new InjectionRequest(
                "TT", null, "0.5ml", "left arm", PrescriptionRouteType.Im,
                DateTime.UtcNow, "Nurse", UserId, null, null, "prophylaxis", null, null)]);

        var id = await svc.CreateAsync(req);

        id.Should().NotBeEmpty();
        injections.Should().ContainSingle();
        injections[0].PrescriptionId.Should().Be(id);
        injections[0].Name.Should().Be("TT");
    }

    [Fact]
    public async Task GetAsync_scopes_sql_to_tenant_and_throws_when_missing()
    {
        string? sql = null;
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Prescription>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync(Array.Empty<Prescription>());

        var act = () => svc.GetAsync(RxId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Prescription*");
        sql.Should().Contain(@"""TenantId"" = @TenantId");
        sql.Should().Contain(@"""IsDeleted"" = false");
        sql.Should().Contain(@"""Id"" = @id");
    }

    [Fact]
    public async Task ListByPatientAsync_scopes_sql_to_tenant_and_patient()
    {
        string? sql = null;
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Prescription>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync(Array.Empty<Prescription>());

        var list = await svc.ListByPatientAsync(PatientId);

        list.Should().BeEmpty();
        sql.Should().Contain(@"""TenantId"" = @TenantId");
        sql.Should().Contain(@"""PatientId"" = @patientId");
        sql.Should().Contain(@"""IsDeleted"" = false");
    }

    [Fact]
    public async Task GetAsync_loads_items_and_injections_with_tenant_filter()
    {
        var capturedSql = new List<string>();
        var (svc, db) = Build();
        db.Setup(x => x.QueryAsync<Prescription>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => capturedSql.Add(s))
            .ReturnsAsync([
                new Prescription
                {
                    Id = RxId,
                    PatientId = PatientId,
                    DoctorUserId = UserId,
                    DoctorName = "Dr. Strange",
                    PrescribedAt = DateTime.UtcNow,
                    TenantId = TenantId,
                }
            ]);
        db.Setup(x => x.QueryAsync<PrescriptionItem>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => capturedSql.Add(s))
            .ReturnsAsync([
                new PrescriptionItem
                {
                    Id = Guid.NewGuid(),
                    PrescriptionId = RxId,
                    DrugName = "Paracetamol",
                    ReasonForPrescribing = "fever",
                    TenantId = TenantId,
                }
            ]);
        db.Setup(x => x.QueryAsync<Injection>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => capturedSql.Add(s))
            .ReturnsAsync(Array.Empty<Injection>());

        var dto = await svc.GetAsync(RxId);

        dto.Id.Should().Be(RxId);
        dto.Items.Should().ContainSingle(i => i.DrugName == "Paracetamol");
        capturedSql.Should().OnlyContain(s => s.Contains(@"""TenantId"" = @TenantId"));
        capturedSql.Should().Contain(s => s.Contains("PrescriptionItems"));
        capturedSql.Should().Contain(s => s.Contains("Injections"));
    }
}
