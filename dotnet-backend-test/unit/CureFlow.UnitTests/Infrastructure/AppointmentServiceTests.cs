using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Application.Validation;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Services.CRM;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

public class CreateAppointmentValidatorTests
{
    private readonly CreateAppointmentValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PatientId_is_required()
    {
        _validator.TestValidate(ValidRequest() with { PatientId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.PatientId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Department_is_required(string department)
    {
        _validator.TestValidate(ValidRequest() with { Department = department })
            .ShouldHaveValidationErrorFor(x => x.Department);
    }

    [Fact]
    public void ScheduledAt_must_be_in_the_future()
    {
        _validator.TestValidate(ValidRequest() with { ScheduledAt = DateTime.UtcNow.AddHours(-1) })
            .ShouldHaveValidationErrorFor(x => x.ScheduledAt)
            .WithErrorMessage("Scheduled time must be in the future");
        _validator.TestValidate(ValidRequest() with { ScheduledAt = DateTime.UtcNow.AddHours(2) })
            .ShouldNotHaveValidationErrorFor(x => x.ScheduledAt);
    }

    private static CreateAppointmentRequest ValidRequest() => new(
        PatientId: Guid.NewGuid(),
        DoctorUserId: Guid.NewGuid(),
        DoctorName: null,
        Department: "Cardiology",
        ScheduledAt: DateTime.UtcNow.AddDays(1),
        Notes: null);
}

public class UpdateAppointmentValidatorTests
{
    private readonly UpdateAppointmentValidator _validator = new();

    [Fact]
    public void Empty_patch_passes()
    {
        _validator.TestValidate(EmptyRequest()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ScheduledAt_when_present_must_be_near_future_or_later()
    {
        _validator.TestValidate(EmptyRequest() with { ScheduledAt = DateTime.UtcNow.AddHours(-2) })
            .ShouldHaveValidationErrorFor(x => x.ScheduledAt);
        _validator.TestValidate(EmptyRequest() with { ScheduledAt = DateTime.UtcNow.AddMinutes(10) })
            .ShouldNotHaveValidationErrorFor(x => x.ScheduledAt);
    }

    [Fact]
    public void DurationMinutes_must_be_between_5_and_480()
    {
        _validator.TestValidate(EmptyRequest() with { DurationMinutes = 4 })
            .ShouldHaveValidationErrorFor(x => x.DurationMinutes);
        _validator.TestValidate(EmptyRequest() with { DurationMinutes = 481 })
            .ShouldHaveValidationErrorFor(x => x.DurationMinutes);
        _validator.TestValidate(EmptyRequest() with { DurationMinutes = 30 })
            .ShouldNotHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Fact]
    public void Department_when_present_must_not_be_empty()
    {
        _validator.TestValidate(EmptyRequest() with { Department = "" })
            .ShouldHaveValidationErrorFor(x => x.Department);
    }

    private static UpdateAppointmentRequest EmptyRequest() => new(
        ScheduledAt: null,
        DoctorUserId: null,
        DoctorName: null,
        Department: null,
        ChiefComplaint: null,
        Notes: null,
        DurationMinutes: null);
}

/// <summary>AppointmentService: doctor validation, status transitions, tenant-scoped queries.</summary>
public class AppointmentServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ActorUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static (
        AppointmentService Svc,
        Mock<ICureFlowDbSession> Db,
        Mock<IConversationService> Conversations,
        Mock<INotificationPublisher> Notifications) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        db.SetupGet(x => x.UserId).Returns(ActorUserId);

        var conversations = new Mock<IConversationService>();
        conversations.Setup(x => x.SendToPatientAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendToPatientAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (Guid?)null, (string?)null));

        var notifications = new Mock<INotificationPublisher>();
        notifications.Setup(x => x.PublishAsync(It.IsAny<NotificationPublishRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new AppointmentService(
            db.Object,
            conversations.Object,
            email.Object,
            Mock.Of<ILogger<AppointmentService>>(),
            notifications.Object);

        return (svc, db, conversations, notifications);
    }

    private static void SetupDoctorLookup(Mock<ICureFlowDbSession> db, Guid doctorId, User? doctor, StaffProfile? profile)
    {
        db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.Is<string>(sql => sql.Contains(@"""Users""")),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        db.Setup(x => x.QueryFirstOrDefaultAsync<StaffProfile>(
                It.Is<string>(sql => sql.Contains(@"""StaffProfiles""")),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
    }

    private static void SetupPatientLookup(Mock<ICureFlowDbSession> db, Patient? patient)
    {
        db.Setup(x => x.QueryFirstOrDefaultAsync<Patient>(
                It.Is<string>(sql => sql.Contains(@"""Patients""")),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);
    }

    private static void SetupQuietMessageSideEffects(Mock<ICureFlowDbSession> db)
    {
        db.Setup(x => x.QueryFirstOrDefaultAsync<string>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Asia/Kolkata");
        db.Setup(x => x.QueryFirstOrDefaultAsync<QuickTemplate>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuickTemplate?)null);
        db.Setup(x => x.QueryFirstOrDefaultAsync<EmailSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailSettings?)null);
    }

    [Fact]
    public void WhereActive_appointment_includes_tenant_and_soft_delete()
    {
        var fragment = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        fragment.Should().Contain(@"""IsDeleted"" = false");
        fragment.Should().Contain(@"""TenantId"" = @TenantId");
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_doctor()
    {
        var (svc, _, _, _) = Build();

        var act = () => svc.CreateAsync(
            Guid.NewGuid(), doctorUserId: null, doctorName: null,
            "Cardiology", DateTime.UtcNow.AddDays(1), notes: null);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Select a doctor*");
    }

    [Fact]
    public async Task CreateAsync_rejects_non_doctor_user()
    {
        var (svc, db, _, _) = Build();
        var doctorId = Guid.NewGuid();
        SetupDoctorLookup(db, doctorId,
            new User { Id = doctorId, Name = "Nurse Pat", Role = UserRole.Nurse, TenantId = TenantId },
            profile: null);

        var act = () => svc.CreateAsync(
            Guid.NewGuid(), doctorId, null, "Cardiology", DateTime.UtcNow.AddDays(1), null);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*not a doctor*");
    }

    [Fact]
    public async Task CreateAsync_rejects_doctor_without_staff_profile()
    {
        var (svc, db, _, _) = Build();
        var doctorId = Guid.NewGuid();
        SetupDoctorLookup(db, doctorId,
            new User { Id = doctorId, Name = "Dr. A", Role = UserRole.Doctor, TenantId = TenantId },
            profile: null);

        var act = () => svc.CreateAsync(
            Guid.NewGuid(), doctorId, null, "Cardiology", DateTime.UtcNow.AddDays(1), null);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*staff profile*");
    }

    [Fact]
    public async Task CreateAsync_rejects_unavailable_doctor()
    {
        var (svc, db, _, _) = Build();
        var doctorId = Guid.NewGuid();
        SetupDoctorLookup(db, doctorId,
            new User { Id = doctorId, Name = "Dr. A", Role = UserRole.Doctor, TenantId = TenantId },
            new StaffProfile { UserId = doctorId, IsAvailable = false, TenantId = TenantId });

        var act = () => svc.CreateAsync(
            Guid.NewGuid(), doctorId, null, "Cardiology", DateTime.UtcNow.AddDays(1), null);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public async Task CreateAsync_rejects_patient_outside_tenant_scope()
    {
        var (svc, db, _, _) = Build();
        var doctorId = Guid.NewGuid();
        SetupDoctorLookup(db, doctorId,
            new User { Id = doctorId, Name = "Dr. A", Role = UserRole.Doctor, TenantId = TenantId },
            new StaffProfile { UserId = doctorId, IsAvailable = true, ConsultationFee = 500, TenantId = TenantId });
        SetupPatientLookup(db, null);

        string? patientSql = null;
        db.Setup(x => x.QueryFirstOrDefaultAsync<Patient>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((sql, _, _, _) => patientSql = sql)
            .ReturnsAsync((Patient?)null);

        var act = () => svc.CreateAsync(
            Guid.NewGuid(), doctorId, null, "Cardiology", DateTime.UtcNow.AddDays(1), null);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Patient*");
        patientSql.Should().Contain(@"""TenantId"" = @TenantId");
        patientSql.Should().Contain(@"""IsDeleted"" = false");
    }

    [Fact]
    public async Task CreateAsync_schedules_appointment_and_sets_patient_lead_status()
    {
        var (svc, db, conversations, notifications) = Build();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var patient = new Patient
        {
            Id = patientId,
            TenantId = TenantId,
            Name = "Riya Sharma",
            Phone = "919876543210",
            Status = LeadStatus.NewInquiry,
        };
        Appointment? inserted = null;
        Patient? updatedPatient = null;

        SetupDoctorLookup(db, doctorId,
            new User
            {
                Id = doctorId,
                Name = "Dr. Mehta",
                Role = UserRole.Doctor,
                Specialty = "Cardiology",
                TenantId = TenantId,
            },
            new StaffProfile
            {
                UserId = doctorId,
                IsAvailable = true,
                ConsultationFee = 750,
                Department = "Cardiology",
                TenantId = TenantId,
            });
        SetupPatientLookup(db, patient);
        SetupQuietMessageSideEffects(db);

        db.Setup(x => x.InsertAsync(It.IsAny<Appointment>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Appointment, bool, CancellationToken>((a, _, _) => inserted = a)
            .Returns(Task.CompletedTask);
        db.Setup(x => x.UpdateAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, bool, CancellationToken>((p, _, _) => updatedPatient = p)
            .Returns(Task.CompletedTask);

        var id = await svc.CreateAsync(
            patientId, doctorId, null, "Cardiology",
            DateTime.UtcNow.AddDays(2), "Follow-up");

        id.Should().NotBeEmpty();
        inserted.Should().NotBeNull();
        inserted!.PatientId.Should().Be(patientId);
        inserted.DoctorUserId.Should().Be(doctorId);
        inserted.DoctorName.Should().Be("Dr. Mehta");
        inserted.ConsultationFee.Should().Be(750);
        inserted.Status.Should().Be(AppointmentStatus.Scheduled);
        updatedPatient!.Status.Should().Be(LeadStatus.AppointmentScheduled);

        conversations.Verify(x => x.SendToPatientAsync(patientId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.PublishAsync(
            It.Is<NotificationPublishRequest>(r =>
                r.Type == NotificationTypeCodes.AppointmentCreated &&
                r.EntityId == inserted.Id &&
                r.DoctorUserId == doctorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_uses_tenant_scoped_sql_and_throws_when_missing()
    {
        var (svc, db, _, _) = Build();
        string? sql = null;
        db.Setup(x => x.QueryFirstOrDefaultAsync<Appointment>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync((Appointment?)null);

        var act = () => svc.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
        sql.Should().Contain(@"""Appointments""");
        sql.Should().Contain(@"""TenantId"" = @TenantId");
        sql.Should().Contain(@"""IsDeleted"" = false");
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
        db.Setup(x => x.QueryAsync<Appointment>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((sql, _, _, _) => listSql = sql)
            .ReturnsAsync(Array.Empty<Appointment>());

        await svc.ListAsync(null, null, null, null, 1, 20);

        countSql.Should().Contain(@"""TenantId"" = @TenantId");
        countSql.Should().Contain(@"""IsDeleted"" = false");
        listSql.Should().Contain(@"""TenantId"" = @TenantId");
        listSql.Should().Contain(@"""IsDeleted"" = false");
    }

    [Fact]
    public async Task UpdateStatusAsync_rejects_invalid_status()
    {
        var (svc, db, _, _) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<Appointment>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Appointment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Status = AppointmentStatus.Scheduled,
                PatientName = "Riya",
            });

        var act = () => svc.UpdateStatusAsync(Guid.NewGuid(), "not_a_status");

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Invalid appointment status*");
    }

    [Theory]
    [InlineData("confirmed", AppointmentStatus.Confirmed)]
    [InlineData("completed", AppointmentStatus.Completed)]
    [InlineData("cancelled", AppointmentStatus.Cancelled)]
    [InlineData("no_show", AppointmentStatus.NoShow)]
    public async Task UpdateStatusAsync_transitions_status(string status, AppointmentStatus expected)
    {
        var (svc, db, conversations, notifications) = Build();
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointment = new Appointment
        {
            Id = appointmentId,
            TenantId = TenantId,
            PatientId = patientId,
            PatientName = "Riya Sharma",
            DoctorUserId = Guid.NewGuid(),
            DoctorName = "Dr. Mehta",
            Department = "Cardiology",
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            Status = AppointmentStatus.Scheduled,
            ConsultationFee = 500,
        };

        db.Setup(x => x.QueryFirstOrDefaultAsync<Appointment>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        db.Setup(x => x.UpdateAsync(It.IsAny<Appointment>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SetupQuietMessageSideEffects(db);
        SetupPatientLookup(db, new Patient
        {
            Id = patientId,
            TenantId = TenantId,
            Name = "Riya Sharma",
            Phone = "919876543210",
        });

        await svc.UpdateStatusAsync(appointmentId, status);

        appointment.Status.Should().Be(expected);
        if (expected == AppointmentStatus.Completed)
            appointment.CompletedAt.Should().NotBeNull();

        if (expected == AppointmentStatus.Cancelled)
            conversations.Verify(x => x.SendToPatientAsync(patientId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        if (expected == AppointmentStatus.NoShow)
        {
            notifications.Verify(x => x.PublishAsync(
                It.Is<NotificationPublishRequest>(r =>
                    r.Type == NotificationTypeCodes.AppointmentNoShow &&
                    r.EntityId == appointmentId &&
                    r.DedupeKey == $"appointment.no_show:{appointmentId}"),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task UpdateStatusAsync_does_not_resend_cancel_when_already_cancelled()
    {
        var (svc, db, conversations, _) = Build();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            PatientId = Guid.NewGuid(),
            PatientName = "Riya",
            Status = AppointmentStatus.Cancelled,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
        };
        db.Setup(x => x.QueryFirstOrDefaultAsync<Appointment>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        db.Setup(x => x.UpdateAsync(It.IsAny<Appointment>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.UpdateStatusAsync(appointment.Id, "cancelled");

        conversations.Verify(x => x.SendToPatientAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
