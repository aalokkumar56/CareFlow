using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;

namespace CureFlow.Infrastructure.Services.Ehr;

internal static class ClinicalAssociationGuard
{
    // Call inside the write transaction. Shared parent locks hold association/soft-delete state through the insert.
    public static async Task RequireAsync(ICureFlowDbSession db, Guid patientId, Guid? visitId, Guid? appointmentId, CancellationToken ct)
    {
        if (patientId == Guid.Empty || visitId == Guid.Empty || appointmentId == Guid.Empty)
            throw new ValidationException("Select valid patient and encounter references.");
        _ = await db.QueryFirstOrDefaultAsync<Patient>("""
            SELECT * FROM "Patients" WHERE "Id" = @id AND "TenantId" = @TenantId AND NOT "IsDeleted" FOR SHARE
            """, new { id = patientId }, ct: ct) ?? throw new NotFoundException("Patient");
        Visit? visit = null;
        if (visitId.HasValue)
        {
            visit = await db.QueryFirstOrDefaultAsync<Visit>("""
                SELECT * FROM "Visits" WHERE "Id" = @id AND "TenantId" = @TenantId AND NOT "IsDeleted" FOR SHARE
                """, new { id = visitId.Value }, ct: ct) ?? throw new NotFoundException("Visit");
            if (visit.PatientId != patientId)
                throw new ValidationException("The visit does not belong to the selected patient.");
            if (appointmentId.HasValue && visit.AppointmentId != appointmentId)
                throw new ValidationException("The appointment does not match the selected visit.");
        }
        // Verify the visit's appointment as well when the caller omitted the optional appointment field.
        var linkedAppointment = appointmentId ?? visit?.AppointmentId;
        if (linkedAppointment.HasValue)
        {
            var appointment = await db.QueryFirstOrDefaultAsync<Appointment>("""
                SELECT * FROM "Appointments" WHERE "Id" = @id AND "TenantId" = @TenantId AND NOT "IsDeleted" FOR SHARE
                """, new { id = linkedAppointment.Value }, ct: ct) ?? throw new NotFoundException("Appointment");
            if (appointment.PatientId != patientId)
                throw new ValidationException("The appointment does not belong to the selected patient.");
        }
    }
}
