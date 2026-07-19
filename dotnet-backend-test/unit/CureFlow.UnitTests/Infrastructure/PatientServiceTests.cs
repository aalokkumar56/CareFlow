using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Application.Validation;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

public class CreatePatientValidatorTests
{
    private readonly CreatePatientValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string name)
    {
        var result = _validator.TestValidate(ValidRequest() with { Name = name });
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_rejects_over_200_chars()
    {
        var result = _validator.TestValidate(ValidRequest() with { Name = new string('a', 201) });
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Phone_is_required(string phone)
    {
        var result = _validator.TestValidate(ValidRequest() with { Phone = phone });
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Age_must_be_between_0_and_150()
    {
        _validator.TestValidate(ValidRequest() with { Age = -1 })
            .ShouldHaveValidationErrorFor(x => x.Age);
        _validator.TestValidate(ValidRequest() with { Age = 151 })
            .ShouldHaveValidationErrorFor(x => x.Age);
        _validator.TestValidate(ValidRequest() with { Age = 42 })
            .ShouldNotHaveValidationErrorFor(x => x.Age);
    }

    [Fact]
    public void Email_must_be_valid_when_provided()
    {
        _validator.TestValidate(ValidRequest() with { Email = "not-an-email" })
            .ShouldHaveValidationErrorFor(x => x.Email);
        _validator.TestValidate(ValidRequest() with { Email = "patient@example.com" })
            .ShouldNotHaveValidationErrorFor(x => x.Email);
        _validator.TestValidate(ValidRequest() with { Email = null })
            .ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    private static CreatePatientRequest ValidRequest() => new(
        Name: "Riya Sharma",
        Phone: "9876543210",
        Age: 30,
        Gender: Gender.Female,
        BloodGroup: null,
        Department: "Cardiology",
        InquirySource: "manual",
        ReferringDoctorId: null,
        Email: "riya@example.com",
        AddressLine1: null,
        City: null,
        State: null,
        Pincode: null,
        Tags: null,
        Notes: null,
        Occupation: null);
}

public class UpdatePatientValidatorTests
{
    private readonly UpdatePatientValidator _validator = new();

    [Fact]
    public void Empty_patch_passes()
    {
        var result = _validator.TestValidate(EmptyRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Name_when_present_must_not_be_empty()
    {
        _validator.TestValidate(EmptyRequest() with { Name = "" })
            .ShouldHaveValidationErrorFor(x => x.Name);
        _validator.TestValidate(EmptyRequest() with { Name = "Updated" })
            .ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Phone_when_present_must_not_be_empty()
    {
        _validator.TestValidate(EmptyRequest() with { Phone = "" })
            .ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Age_when_present_must_be_in_range()
    {
        _validator.TestValidate(EmptyRequest() with { Age = 200 })
            .ShouldHaveValidationErrorFor(x => x.Age);
    }

    private static UpdatePatientRequest EmptyRequest() => new(
        Name: null, Phone: null, Email: null, EmailNotificationsEnabled: null,
        Age: null, Gender: null, BloodGroup: null, DateOfBirth: null,
        Department: null, Status: null, AssignedStaffId: null,
        Tags: null, Notes: null, FollowUpDate: null,
        Occupation: null, MaritalStatus: null,
        GovIdType: null, GovIdNumber: null,
        AddressLine1: null, AddressLine2: null, City: null, State: null, Pincode: null,
        EmergencyContactName: null, EmergencyContactPhone: null, EmergencyContactRelation: null);
}

/// <summary>PatientService: create/update/delete, lead status changes, tenant-scoped reads.</summary>
public class PatientServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (PatientService Svc, Mock<ICureFlowDbSession> Db, Mock<IAuditService> Audit, Mock<INotificationPublisher> Notifications) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        db.SetupGet(x => x.UserId).Returns(UserId);

        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var notifications = new Mock<INotificationPublisher>();
        notifications.Setup(x => x.PublishAsync(It.IsAny<NotificationPublishRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.TenantId).Returns(TenantId);
        tenant.SetupGet(x => x.UserId).Returns(UserId);

        var svc = new PatientService(db.Object, audit.Object, notifications.Object, tenant.Object);
        return (svc, db, audit, notifications);
    }

    [Fact]
    public void WhereActive_patient_includes_tenant_and_soft_delete()
    {
        var fragment = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        fragment.Should().Contain(@"""IsDeleted"" = false");
        fragment.Should().Contain(@"""TenantId"" = @TenantId");
    }

    [Fact]
    public async Task CreateAsync_inserts_normalized_patient_and_publishes()
    {
        var (svc, db, audit, notifications) = Build();
        Patient? inserted = null;
        db.Setup(x => x.InsertAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, bool, CancellationToken>((p, _, _) => inserted = p)
            .Returns(Task.CompletedTask);

        var id = await svc.CreateAsync(new CreatePatientRequest(
            Name: "  Riya Sharma  ",
            Phone: "9876543210",
            Age: 30,
            Gender: Gender.Female,
            BloodGroup: BloodGroup.APos,
            Department: "Cardiology",
            InquirySource: null,
            ReferringDoctorId: null,
            Email: "riya@example.com",
            AddressLine1: null, City: null, State: null, Pincode: null,
            Tags: null, Notes: null, Occupation: null));

        id.Should().NotBeEmpty();
        inserted.Should().NotBeNull();
        inserted!.Name.Should().Be("Riya Sharma");
        inserted.Phone.Should().Be("919876543210");
        inserted.InquirySource.Should().Be("manual");
        inserted.Status.Should().Be(LeadStatus.NewInquiry);

        audit.Verify(x => x.LogAsync("patient.create", "patient", inserted.Id.ToString(), null, It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.PublishAsync(
            It.Is<NotificationPublishRequest>(r =>
                r.Type == NotificationTypeCodes.PatientCreated &&
                r.EntityType == "patient" &&
                r.EntityId == inserted.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_throws_when_patient_missing_or_out_of_tenant()
    {
        var (svc, db, _, _) = Build();
        db.Setup(x => x.GetByIdAsync<Patient>(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var act = () => svc.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Patient*");
        db.Verify(x => x.GetByIdAsync<Patient>(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_returns_detail_for_tenant_scoped_patient()
    {
        var (svc, db, _, _) = Build();
        var patientId = Guid.NewGuid();
        db.Setup(x => x.GetByIdAsync<Patient>(patientId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient
            {
                Id = patientId,
                TenantId = TenantId,
                Name = "Riya",
                Phone = "919876543210",
                Status = LeadStatus.Contacted,
            });

        var dto = await svc.GetAsync(patientId);

        dto.Id.Should().Be(patientId);
        dto.Name.Should().Be("Riya");
        dto.Status.Should().Be(LeadStatus.Contacted);
    }

    [Fact]
    public async Task ListAsync_uses_tenant_scoped_where_clause()
    {
        var (svc, db, _, _) = Build();
        string? countSql = null;
        string? listSql = null;

        db.Setup(x => x.QuerySingleAsync<int>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((sql, _, _, _) => countSql = sql)
            .ReturnsAsync(0);
        db.Setup(x => x.QueryAsync<Patient>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((sql, _, _, _) => listSql = sql)
            .ReturnsAsync(Array.Empty<Patient>());

        await svc.ListAsync(null, null, null, null, null, 1, 20);

        countSql.Should().Contain(@"""TenantId"" = @TenantId");
        countSql.Should().Contain(@"""IsDeleted"" = false");
        listSql.Should().Contain(@"""TenantId"" = @TenantId");
        listSql.Should().Contain(@"""IsDeleted"" = false");
    }

    [Theory]
    [InlineData(LeadStatus.Contacted)]
    [InlineData(LeadStatus.AppointmentScheduled)]
    [InlineData(LeadStatus.Visited)]
    [InlineData(LeadStatus.Lost)]
    [InlineData(LeadStatus.ReEngagement)]
    public async Task UpdateAsync_transitions_lead_status(LeadStatus nextStatus)
    {
        var (svc, db, audit, _) = Build();
        var patientId = Guid.NewGuid();
        var patient = new Patient
        {
            Id = patientId,
            TenantId = TenantId,
            Name = "Riya",
            Phone = "919876543210",
            Status = LeadStatus.NewInquiry,
        };
        db.Setup(x => x.GetByIdAsync<Patient>(patientId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);
        db.Setup(x => x.UpdateAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.UpdateAsync(patientId, new UpdatePatientRequest(
            Name: null, Phone: null, Email: null, EmailNotificationsEnabled: null,
            Age: null, Gender: null, BloodGroup: null, DateOfBirth: null,
            Department: null, Status: nextStatus, AssignedStaffId: null,
            Tags: null, Notes: null, FollowUpDate: null,
            Occupation: null, MaritalStatus: null,
            GovIdType: null, GovIdNumber: null,
            AddressLine1: null, AddressLine2: null, City: null, State: null, Pincode: null,
            EmergencyContactName: null, EmergencyContactPhone: null, EmergencyContactRelation: null));

        patient.Status.Should().Be(nextStatus);
        db.Verify(x => x.UpdateAsync(patient, false, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.LogAsync("patient.update", "patient", patientId.ToString(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_patient()
    {
        var (svc, db, audit, _) = Build();
        var patientId = Guid.NewGuid();
        var patient = new Patient
        {
            Id = patientId,
            TenantId = TenantId,
            Name = "Riya",
            Phone = "919876543210",
            IsDeleted = false,
        };
        db.Setup(x => x.GetByIdAsync<Patient>(patientId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);
        db.Setup(x => x.UpdateAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.DeleteAsync(patientId);

        patient.IsDeleted.Should().BeTrue();
        db.Verify(x => x.UpdateAsync(patient, false, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.LogAsync("patient.delete", "patient", patientId.ToString(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_patient_not_in_tenant_scope()
    {
        var (svc, db, _, _) = Build();
        db.Setup(x => x.GetByIdAsync<Patient>(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var act = () => svc.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
