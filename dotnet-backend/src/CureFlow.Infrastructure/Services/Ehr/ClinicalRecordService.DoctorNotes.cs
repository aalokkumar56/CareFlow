using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Enums;

namespace CureFlow.Infrastructure.Services.Ehr;

public partial class ClinicalRecordService
{
    private async Task<User> RequireDoctorAsync(CancellationToken ct)
    {
        EnsureTenant();
        var user = await _db.GetByIdAsync<User>(_tenant.UserId ?? Guid.Empty, ct: ct);
        if (user is not { Role: UserRole.Doctor, IsActive: true })
            throw new ForbiddenException("Only an authenticated doctor can write Doctor Notes.");
        return user;
    }

    private static void ValidateContent(params string?[] fields)
    {
        if (fields.Sum(f => f?.Length ?? 0) > 20000)
            throw new ValidationException("A note cannot exceed 20,000 characters.");
    }

    private static bool HasContent(ClinicalNote note) =>
        !string.IsNullOrWhiteSpace(note.Subjective) || !string.IsNullOrWhiteSpace(note.Objective) ||
        !string.IsNullOrWhiteSpace(note.Assessment) || !string.IsNullOrWhiteSpace(note.Plan);

    private async Task ValidateAssociationAsync(ICureFlowDbSession db, Guid patientId,
        Guid? visitId, Guid? appointmentId, CancellationToken ct)
    {
        if (await db.GetByIdAsync<Patient>(patientId, ct: ct) == null)
            throw new NotFoundException("Patient");
        if (visitId == null)
            throw new ValidationException("Start a consultation before writing a Doctor Note.");
        var visit = await db.GetByIdAsync<Visit>(visitId.Value, ct: ct)
            ?? throw new NotFoundException("Visit");
        if (visit.PatientId != patientId || visit.AppointmentId != appointmentId)
            throw new ValidationException("The visit does not match the patient and appointment.");
        if (appointmentId.HasValue)
        {
            var appointment = await db.GetByIdAsync<Appointment>(appointmentId.Value, ct: ct)
                ?? throw new NotFoundException("Appointment");
            if (appointment.PatientId != patientId)
                throw new ValidationException("The appointment belongs to another patient.");
        }
    }

    private async Task<Guid> CreateDoctorDraftAsync(CreateClinicalNoteRequest r, CancellationToken ct)
    {
        var user = await RequireDoctorAsync(ct);
        DoctorNoteRules.ValidateLanguage(r.OriginalLanguage);
        ValidateContent(r.Subjective, r.Objective, r.Assessment, r.Plan);
        if (!r.Id.HasValue || r.Id == Guid.Empty)
            throw new ValidationException("A client-generated note id is required for safe retries.");
        var noteId = r.Id.Value;
        await _db.TransactionAsync(async db =>
        {
            // Serialize retries and prevent multiple browser tabs from creating a
            // new draft for the same doctor and consultation.
            await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
                new { key = $"doctor-note:{_tenant.TenantId}:{noteId}" }, ct: ct);
            await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
                new { key = $"doctor-note-draft:{_tenant.TenantId}:{user.Id}:{r.PatientId}:{r.VisitId}:{r.AppointmentId}" }, ct: ct);
            var existing = await db.GetByIdAsync<ClinicalNote>(noteId, ct: ct);
            if (existing != null)
            {
                if (existing.AuthorUserId != user.Id || existing.PatientId != r.PatientId ||
                    existing.VisitId != r.VisitId || existing.AppointmentId != r.AppointmentId)
                    throw new ConflictException("This note id already belongs to another record.");
                return; // A lost create response must never overwrite an existing draft.
            }
            var activeDraft = await db.QueryFirstOrDefaultAsync<ClinicalNote>(
                """
                SELECT * FROM "ClinicalNotes" WHERE "TenantId" = @TenantId AND "IsDeleted" = false
                AND "AuthorUserId" = @authorUserId AND "PatientId" = @patientId
                AND "VisitId" IS NOT DISTINCT FROM @visitId
                AND "AppointmentId" IS NOT DISTINCT FROM @appointmentId
                AND "FinalizedAt" IS NULL
                ORDER BY "CreatedAt" DESC LIMIT 1
                """, new { authorUserId = user.Id, r.PatientId, r.VisitId, r.AppointmentId }, ct: ct);
            if (activeDraft != null)
            {
                noteId = activeDraft.Id;
                return;
            }
            await ValidateAssociationAsync(db, r.PatientId, r.VisitId, r.AppointmentId, ct);
            var now = DateTime.UtcNow;
            await db.InsertAsync(new ClinicalNote
            {
                Id = noteId, PatientId = r.PatientId, VisitId = r.VisitId, AppointmentId = r.AppointmentId,
                AuthorUserId = user.Id, AuthorName = user.Name, NoteType = r.NoteType ?? "progress",
                Subjective = r.Subjective ?? "", Objective = r.Objective ?? "",
                Assessment = r.Assessment ?? "", Plan = r.Plan ?? "",
                OriginalLanguage = r.OriginalLanguage, LastAutoSavedAt = now, Revision = 1,
            }, ct: ct);
        }, ct);
        return noteId;
    }

    private async Task SaveDoctorNoteAsync(Guid id, UpdateClinicalNoteRequest r, CancellationToken ct)
    {
        var user = await RequireDoctorAsync(ct);
        await _db.TransactionAsync(async db =>
        {
            var note = await db.QueryFirstOrDefaultAsync<ClinicalNote>(
                """
                SELECT * FROM "ClinicalNotes" WHERE "Id" = @id AND "TenantId" = @TenantId
                AND "IsDeleted" = false FOR UPDATE
                """, new { id }, ct: ct) ?? throw new NotFoundException("Clinical note");
            var now = DateTime.UtcNow;
            DoctorNoteRules.ValidateEdit(note, user.Id, true, now,
                r.PatientId, r.VisitId, r.AppointmentId, r.Revision);
            await ValidateAssociationAsync(db, note.PatientId, note.VisitId, note.AppointmentId, ct);
            var language = r.OriginalLanguage ?? note.OriginalLanguage;
            DoctorNoteRules.ValidateLanguage(language);
            note.NoteType = r.NoteType ?? note.NoteType;
            note.Subjective = r.Subjective ?? note.Subjective;
            note.Objective = r.Objective ?? note.Objective;
            note.Assessment = r.Assessment ?? note.Assessment;
            note.Plan = r.Plan ?? note.Plan;
            ValidateContent(note.Subjective, note.Objective, note.Assessment, note.Plan);
            if (r.Finalize && string.IsNullOrWhiteSpace(
                    note.Subjective + note.Objective + note.Assessment + note.Plan))
                throw new ValidationException("Write a note before finishing.");
            note.OriginalLanguage = language;
            if (r.Finalize && note.FinalizedAt == null)
            {
                note.FinalizedAt = now;
                note.EditableUntil = now.AddHours(24);
            }
            if (note.FinalizedAt == null) note.LastAutoSavedAt = now;
            note.Revision++;
            await db.UpdateAsync(note, ct: ct);
        }, ct);
    }

    public async Task<ClinicalNoteDto> GetNoteAsync(Guid id, CancellationToken ct = default)
    {
        EnsureTenant();
        var note = await _db.GetByIdAsync<ClinicalNote>(id, ct: ct)
            ?? throw new NotFoundException("Clinical note");
        if (note.FinalizedAt == null && note.AuthorUserId != _tenant.UserId)
            throw new NotFoundException("Clinical note");
        var user = await _db.GetByIdAsync<User>(_tenant.UserId ?? Guid.Empty, ct: ct);
        return MapDoctorNote(note, user);
    }

    private ClinicalNoteDto MapDoctorNote(ClinicalNote n, User? user)
    {
        var now = DateTime.UtcNow;
        return new(n.Id, n.PatientId, n.VisitId, n.AuthorName, n.CreatedAt,
            n.NoteType, n.Subjective, n.Objective, n.Assessment, n.Plan,
            n.AppointmentId, n.AuthorUserId, n.OriginalLanguage, DoctorNoteRules.Status(n, now),
            n.FinalizedAt, n.EditableUntil, n.UpdatedAt, n.LastAutoSavedAt, n.Revision,
            DoctorNoteRules.CanEdit(n, _tenant.UserId, user is { Role: UserRole.Doctor, IsActive: true }, now), now);
    }
}
