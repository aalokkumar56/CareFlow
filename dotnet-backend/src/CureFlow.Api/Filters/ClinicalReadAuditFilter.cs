using Microsoft.AspNetCore.Mvc.Infrastructure;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Entities.Lifestyle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CureFlow.Api.Filters;

// Explicit sensitive controller inventory. Runs after policy/domain checks and
// before any result executes; event fields never inspect notes or serialized bodies.
public sealed class ClinicalReadAuditFilter(IClinicalAccessAuditService audit, ICureFlowDbSession db) : IAsyncActionFilter
{
    private static readonly HashSet<string> Controllers = new(StringComparer.Ordinal)
    { "ClinicianWorklist", "PatientAppointments", "AppointmentNoShows", "NamedFollowUps", "Patients", "QueueDispatch", "QueueArrivals", "ScheduleExceptions", "PatientContacts", "PatientIdentityReviews", "PatientMappings", "PatientDemographics", "PatientIdentity", "PatientAlerts", "PatientImports", "PatientExports", "LeadConversions", "AppointmentReservations", "AppointmentBookings", "AppointmentBookingChanges", "AppointmentWaitlists", "Clinical", "Visits", "PatientVisits", "PatientDocuments", "LabReports", "Prescriptions", "Lifestyle" };

    internal static bool IsSensitiveController(string name) => Controllers.Contains(name);
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action || !Controllers.Contains(action.ControllerName)
            || !HttpMethods.IsGet(context.HttpContext.Request.Method)) { await next(); return; }
        var executed = await next();
        var status = executed.Result is IStatusCodeActionResult code ? code.StatusCode ?? 200 : 200;
        if (executed.Exception is not null || status >= 400)
        {
            await audit.RecordAsync(new(null, action.ControllerName, null, "read", "denied"), context.HttpContext.RequestAborted);
            return;
        }
        try
        {
            // These services record exact local patient subjects before returning PHI.
            // Keep denied paths in this filter and authorization middleware's shared inventory.
            if (action.ControllerName is "ClinicianWorklist" or "PatientAppointments" or "AppointmentNoShows" or "NamedFollowUps" or "PatientContacts" or "PatientIdentityReviews" or "PatientMappings" or "PatientDemographics" or "PatientIdentity" or "PatientAlerts" or "PatientImports" or "PatientExports" or "LeadConversions" or "ScheduleExceptions" or "AppointmentBookings" or "QueueArrivals" or "QueueDispatch" or "AppointmentBookingChanges" or "AppointmentWaitlists" or "AppointmentReservations") return;
            // These services audit the exact opened immutable version before file I/O.
            if (executed.Result is FileResult && action.ControllerName is "PatientDocuments" or "LabReports") return;
            var (patientId, resourceId, version) = await ResolveScope(context, action.ControllerName, executed.Result);
            await audit.RecordAsync(new(patientId, action.ControllerName, resourceId,
                executed.Result is FileResult ? "download" : "read", "success", version), context.HttpContext.RequestAborted);
        }
        catch
        {
            if (executed.Result is FileStreamResult file) await file.FileStream.DisposeAsync();
            throw;
        }
    }

    private async Task<(Guid? PatientId, Guid? ResourceId, string? Version)> ResolveScope(ActionExecutingContext context, string controller, IActionResult? result)
    {
        var ct = context.HttpContext.RequestAborted;
        var patientId = Argument(context, "patientId");
        var id = Argument(context, "id");
        string? version = null;
        if (controller == "Patients") patientId = id;
        else if (id.HasValue)
        {
            switch (controller)
            {
                case "Clinical":
                    var note = await db.GetByIdAsync<ClinicalNote>(id.Value, ct: ct) ?? throw new NotFoundException("Clinical note");
                    patientId = note.PatientId; version = result is ObjectResult { Value: CureFlow.Application.DTOs.ClinicalNoteDto dto }
                        ? dto.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : note.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case "PatientDocuments":
                    var doc = await db.GetByIdAsync<PatientDocument>(id.Value, ct: ct) ?? throw new NotFoundException("Document");
                    patientId = doc.PatientId; version = doc.StorageVersion?.ToString(); break;
                case "LabReports":
                    var lab = await db.GetByIdAsync<LabReport>(id.Value, ct: ct) ?? throw new NotFoundException("Lab report");
                    patientId = lab.PatientId; version = lab.StorageVersion?.ToString(); break;
                case "Visits":
                    patientId = (await db.GetByIdAsync<Visit>(id.Value, ct: ct) ?? throw new NotFoundException("Visit")).PatientId; break;
                case "Prescriptions":
                    patientId = (await db.GetByIdAsync<Prescription>(id.Value, ct: ct) ?? throw new NotFoundException("Prescription")).PatientId; break;
                default: throw new DomainException("Sensitive resource audit scope requires review.", 500);
            }
        }
        if (patientId.HasValue && await db.GetByIdAsync<Patient>(patientId.Value, ct: ct) is null) throw new NotFoundException("Patient");
        return (patientId, id ?? patientId, version);
    }

    private static Guid? Argument(ActionExecutingContext context, string name) =>
        context.ActionArguments.TryGetValue(name, out var value) && value is Guid id && id != Guid.Empty ? id : null;
}
