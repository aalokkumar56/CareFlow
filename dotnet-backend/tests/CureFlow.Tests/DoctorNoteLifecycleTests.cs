using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Services.Ehr;
using Moq;
using Xunit;

namespace CureFlow.Tests;

public class DoctorNoteLifecycleTests
{
    private readonly Guid doctor = Guid.NewGuid();
    private readonly Guid patient = Guid.NewGuid();
    private readonly Guid visit = Guid.NewGuid();
    private readonly Guid appointment = Guid.NewGuid();

    private ClinicalNote Note() => new()
    {
        PatientId = patient, VisitId = visit, AppointmentId = appointment,
        AuthorUserId = doctor, Subjective = "Original", Revision = 3,
    };

    [Fact]
    public void DraftHasNoTimeLimitButOnlyItsAuthorCanEdit()
    {
        var note = Note();
        Assert.True(DoctorNoteRules.CanEdit(note, doctor, true, DateTime.UtcNow.AddYears(1)));
        Assert.False(DoctorNoteRules.CanEdit(note, Guid.NewGuid(), true, DateTime.UtcNow));
        Assert.False(DoctorNoteRules.CanEdit(note, doctor, false, DateTime.UtcNow));
    }

    [Fact]
    public void FinalNoteLocksAtExactServerDeadline()
    {
        var note = Note();
        note.FinalizedAt = DateTime.UtcNow;
        note.EditableUntil = note.FinalizedAt.Value.AddHours(24);
        Assert.True(DoctorNoteRules.CanEdit(note, doctor, true, note.EditableUntil.Value.AddTicks(-1)));
        Assert.False(DoctorNoteRules.CanEdit(note, doctor, true, note.EditableUntil.Value));
        Assert.Equal("final_locked", DoctorNoteRules.Status(note, note.EditableUntil.Value));
    }

    [Fact]
    public void WrongPatientVisitAppointmentAndStaleRevisionAreRejected()
    {
        var note = Note();
        Assert.Throws<ValidationException>(() => DoctorNoteRules.ValidateEdit(note, doctor, true,
            DateTime.UtcNow, Guid.NewGuid(), visit, appointment, 3));
        Assert.Throws<ValidationException>(() => DoctorNoteRules.ValidateEdit(note, doctor, true,
            DateTime.UtcNow, patient, Guid.NewGuid(), appointment, 3));
        Assert.Throws<ValidationException>(() => DoctorNoteRules.ValidateEdit(note, doctor, true,
            DateTime.UtcNow, patient, visit, Guid.NewGuid(), 3));
        Assert.Throws<ConflictException>(() => DoctorNoteRules.ValidateEdit(note, doctor, true,
            DateTime.UtcNow, patient, visit, appointment, 2));
    }

    private (ClinicalRecordService service, Mock<ICureFlowDbSession> db) Setup(ClinicalNote? note = null,
        UserRole role = UserRole.Doctor, Guid? currentUser = null)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        tenant.SetupGet(t => t.UserId).Returns(currentUser ?? doctor);
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(d => d.TransactionAsync(It.IsAny<Func<ICureFlowDbSession, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<ICureFlowDbSession, Task> action, CancellationToken _) => action(db.Object));
        db.Setup(d => d.GetByIdAsync<User>(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = currentUser ?? doctor, Role = role, Name = "Authenticated doctor" });
        db.Setup(d => d.GetByIdAsync<Patient>(patient, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = patient });
        db.Setup(d => d.GetByIdAsync<Visit>(visit, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Visit { Id = visit, PatientId = patient, AppointmentId = appointment });
        db.Setup(d => d.GetByIdAsync<Appointment>(appointment, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Appointment { Id = appointment, PatientId = patient });
        db.Setup(d => d.QueryFirstOrDefaultAsync<ClinicalNote>(It.IsAny<string>(), It.IsAny<object>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);
        db.Setup(d => d.GetByIdAsync<ClinicalNote>(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);
        return (new ClinicalRecordService(db.Object, tenant.Object), db);
    }

    private UpdateClinicalNoteRequest Update(ClinicalNote note, bool finalize = false) =>
        new(null, "Corrected", null, null, null, patient, visit, appointment, note.Revision, "gu-IN", finalize);

    [Fact]
    public async Task FinalizationStartsWindowExactlyOnceAndAutosaveStaysDraft()
    {
        var note = Note();
        var (service, _) = Setup(note);
        await service.UpdateNoteAsync(note.Id, Update(note));
        Assert.Null(note.FinalizedAt);
        Assert.NotNull(note.LastAutoSavedAt);
        await service.UpdateNoteAsync(note.Id, Update(note, true));
        var deadline = note.EditableUntil;
        Assert.Equal(note.FinalizedAt!.Value.AddHours(24), deadline);
        await service.UpdateNoteAsync(note.Id, Update(note, true));
        Assert.Equal(deadline, note.EditableUntil);
        Assert.Equal(6, note.Revision);
    }

    [Fact]
    public async Task DuplicateFinishAndStaleAutosaveCannotOverwriteFinalizedContent()
    {
        var note = Note();
        var (service, _) = Setup(note);
        var request = Update(note, true);
        await service.UpdateNoteAsync(note.Id, request);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateNoteAsync(note.Id, request));
        Assert.Equal(4, note.Revision);
        Assert.Equal("Corrected", note.Subjective);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Nurse)]
    [InlineData(UserRole.Staff)]
    public async Task NonDoctorsCannotWriteEvenWithClinicalEditPermission(UserRole role)
    {
        var note = Note();
        var (service, db) = Setup(note, role);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateNoteAsync(note.Id, Update(note)));
        db.Verify(d => d.UpdateAsync(It.IsAny<ClinicalNote>(), false, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OtherDoctorCannotEditOrRetrieveDraft()
    {
        var note = Note();
        var (service, _) = Setup(note, currentUser: Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateNoteAsync(note.Id, Update(note)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetNoteAsync(note.Id));
        note.FinalizedAt = DateTime.UtcNow;
        note.EditableUntil = DateTime.UtcNow.AddHours(24);
        var saved = await service.GetNoteAsync(note.Id);
        Assert.False(saved.CanEdit);
        Assert.Equal("Original", saved.Subjective);
    }

    [Fact]
    public async Task LockedNoteIsUnchanged()
    {
        var note = Note();
        note.FinalizedAt = DateTime.UtcNow.AddDays(-2);
        note.EditableUntil = DateTime.UtcNow.AddDays(-1);
        var (service, db) = Setup(note);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateNoteAsync(note.Id, Update(note)));
        Assert.Equal("Original", note.Subjective);
        db.Verify(d => d.UpdateAsync(It.IsAny<ClinicalNote>(), false, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NewDraftUsesAuthenticatedIdentityAndValidatesVisit()
    {
        var (service, db) = Setup();
        ClinicalNote? inserted = null;
        db.Setup(d => d.InsertAsync(It.IsAny<ClinicalNote>(), false, It.IsAny<CancellationToken>()))
            .Callback<ClinicalNote, bool, CancellationToken>((n, _, _) => inserted = n)
            .Returns(Task.CompletedTask);
        var id = Guid.NewGuid();
        await service.AddNoteAsync(new(patient, appointment, visit, "progress", "Gujarati note", null, null, null, id, "gu-IN"));
        Assert.Equal(doctor, inserted!.AuthorUserId);
        Assert.Equal("Authenticated doctor", inserted.AuthorName);
        Assert.Equal(visit, inserted.VisitId);
        Assert.Null(inserted.FinalizedAt);
        await Assert.ThrowsAsync<ValidationException>(() => service.AddNoteAsync(
            new(patient, Guid.NewGuid(), visit, "progress", "Wrong visit", null, null, null, Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateRetryRecoversSameNoteWithoutReplacingItsContent()
    {
        var note = Note();
        var (service, db) = Setup(note);
        var id = await service.AddNoteAsync(new(patient, appointment, visit, "progress", "Older text", null, null, null, note.Id));
        Assert.Equal(note.Id, id);
        Assert.Equal("Original", note.Subjective);
        db.Verify(d => d.InsertAsync(It.IsAny<ClinicalNote>(), false, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotesListingDoesNotExposeAnotherDoctorsDraft()
    {
        var own = Note();
        var other = Note();
        other.AuthorUserId = Guid.NewGuid();
        var final = Note();
        final.AuthorUserId = other.AuthorUserId;
        final.FinalizedAt = DateTime.UtcNow.AddDays(-2);
        final.EditableUntil = DateTime.UtcNow.AddDays(-1);
        var (service, db) = Setup();
        db.Setup(d => d.QueryAsync<ClinicalNote>(It.IsAny<string>(), It.IsAny<object>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { own, other, final });
        var notes = await service.ListNotesAsync(patient);
        Assert.Equal(2, notes.Count);
        Assert.DoesNotContain(notes, n => n.Id == other.Id);
        Assert.Contains(notes, n => n.Id == own.Id && n.CanEdit);
        Assert.Contains(notes, n => n.Id == final.Id && !n.CanEdit);
    }
}
