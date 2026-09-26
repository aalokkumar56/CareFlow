using CureFlow.Domain.Entities.Ehr;

namespace CureFlow.Application.Common;

public static class DoctorNoteRules
{
    public static readonly string[] Languages = ["en-IN", "hi-IN", "gu-IN", "mr-IN"];

    public static string Status(ClinicalNote note, DateTime now) =>
        note.FinalizedAt == null ? "draft" :
        note.EditableUntil > now ? "final_editable" : "final_locked";

    public static bool CanEdit(ClinicalNote note, Guid? userId, bool isDoctor, DateTime now) =>
        isDoctor && note.AuthorUserId == userId &&
        (note.FinalizedAt == null || note.EditableUntil > now);

    public static void ValidateLanguage(string language)
    {
        if (!Languages.Contains(language))
            throw new ValidationException("Select English, Hindi, Gujarati or Marathi.");
    }

    public static void ValidateEdit(ClinicalNote note, Guid? userId, bool isDoctor, DateTime now,
        Guid? patientId, Guid? visitId, Guid? appointmentId, int? revision)
    {
        if (!CanEdit(note, userId, isDoctor, now))
            throw new ForbiddenException("Only the originating doctor can edit within the editing window.");
        if (patientId != note.PatientId || visitId != note.VisitId || appointmentId != note.AppointmentId)
            throw new ValidationException("The patient, visit or appointment does not match this note.");
        if (revision != note.Revision)
            throw new ConflictException("This note changed in another session. Review the latest version before saving.");
    }
}
