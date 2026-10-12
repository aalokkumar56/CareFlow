using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
namespace CureFlow.Infrastructure.Services;
// Legacy rows have no venue/resource allocation. Check only their recorded practitioner/time;
// never interpret invisible confidential bookings as free capacity.
internal static class LegacyAppointmentOccupancy
{
    internal static async Task RequireAvailableAsync(ICureFlowDbSession store,Guid practitioner,DateTime starts,DateTime ends,CancellationToken ct)
    {
        if(await store.QueryFirstOrDefaultAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "PatientConfidentialityStates" s WHERE s."TenantId"=@TenantId AND s."Restricted"
             AND NOT public.cureflow_patient_read_allowed(s."TenantId",s."PatientId"))
            """,ct:ct))throw new ForbiddenException();
        if(await store.QueryFirstOrDefaultAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "Appointments" a WHERE a."TenantId"=@TenantId AND a."DoctorUserId"=@practitioner
             AND NOT a."IsDeleted" AND a."Status" IN (0,1) AND a."ReservationId" IS NULL AND a."ScheduledAt"<@ends
             AND (a."DurationMinutes" NOT BETWEEN 1 AND 480 OR a."ScheduledAt"+make_interval(mins=>LEAST(GREATEST(a."DurationMinutes",1),480))>@starts))
            """,new{practitioner,starts,ends},ct:ct))
            throw new ConflictException("Recorded legacy practitioner bookings conflict or require duration review.");
    }
}