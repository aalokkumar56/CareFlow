using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services.CRM;

public class AppointmentService : IAppointmentService
{
    private readonly ICureFlowDbSession _db;
    private readonly IConversationService _conversations;
    private readonly IEmailService _email;
    private readonly ILogger<AppointmentService> _logger;
    private readonly INotificationPublisher _notifications;

    private const string DefaultConfirmationBody =
        "Namaste {name}, your appointment with {doctor} on {date} at {time_with_zone} is confirmed. Please reach 15 min early. - Cure & Care Hospital";
    private const string DefaultRescheduledBody =
        "Namaste {name}, your appointment with {doctor} ({department}) has been rescheduled to {date} at {time_with_zone}. Please reach 15 min early. - Cure & Care Hospital";
    private const string DefaultCancelledBody =
        "Namaste {name}, your appointment with {doctor} on {date} at {time_with_zone} has been cancelled. Reply here to rebook. - Cure & Care Hospital";

    public AppointmentService(
        ICureFlowDbSession db,
        IConversationService conversations,
        IEmailService email,
        ILogger<AppointmentService> logger,
        INotificationPublisher notifications)
    {
        _db = db;
        _conversations = conversations;
        _email = email;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<Guid> CreateAsync(Guid patientId, Guid? doctorUserId, string? doctorName, string department,
        DateTime scheduledAt, string? notes, CancellationToken ct = default)
    {
        if (!doctorUserId.HasValue)
            throw new ValidationException("Select a doctor from Hospital Staff");

        var (doctor, profile) = await ResolveStaffDoctorAsync(doctorUserId.Value, ct);
        var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var p = await _db.QueryFirstOrDefaultAsync<Patient>(
            $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
            new { patientId },
            ct: ct) ?? throw new NotFoundException("Patient");

        var resolvedDepartment = !string.IsNullOrWhiteSpace(department)
            ? department.Trim()
            : (profile.Department ?? doctor.Specialty ?? "General Medicine");

        var a = new Appointment
        {
            PatientId = patientId,
            PatientName = p.Name,
            PatientPhone = p.Phone,
            DoctorUserId = doctorUserId,
            DoctorName = doctor.Name,
            Department = resolvedDepartment,
            ScheduledAt = DateTimeHelper.EnsureUtc(scheduledAt),
            Notes = notes,
            ConsultationFee = profile.ConsultationFee,
        };
        await _db.InsertAsync(a, ct: ct);

        p.Status = LeadStatus.AppointmentScheduled;
        await _db.UpdateAsync(p, ct: ct);

        await TrySendAppointmentMessageAsync(AppointmentTemplateKeys.Confirmation, p, a, DefaultConfirmationBody, ct);

        var firstName = p.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? p.Name;
        await _notifications.PublishAsync(new NotificationPublishRequest(
            NotificationTypeCodes.AppointmentCreated,
            "New appointment",
            $"{firstName} scheduled with {a.DoctorName}",
            NotificationSeverity.Info,
            EntityType: "appointment",
            EntityId: a.Id,
            ActionUrl: $"/appointments?id={a.Id}",
            DoctorUserId: a.DoctorUserId,
            CreatorUserId: _db.UserId), ct);

        return a.Id;
    }

    public async Task<AppointmentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var a = await _db.QueryFirstOrDefaultAsync<Appointment>(
            $"""SELECT * FROM "Appointments" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Appointment");
        return Map(a);
    }

    public async Task<PagedResult<AppointmentDto>> ListAsync(string? status, DateTime? from, DateTime? to, Guid? doctorUserId, int page, int pageSize, CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize, skip) = Pagination.Normalize(page, pageSize);
        var where = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var filters = new List<string>();
        var param = SqlParam.Merge(new { skip, take = normalizedPageSize });

        if (!string.IsNullOrEmpty(status) && EnumParseHelper.TryParseSnakeCase<AppointmentStatus>(status, out var s))
        {
            filters.Add(@"""Status"" = @status");
            param["status"] = (int)s;
        }
        if (from.HasValue)
        {
            filters.Add(@"""ScheduledAt"" >= @from");
            param["from"] = from.Value;
        }
        if (to.HasValue)
        {
            filters.Add(@"""ScheduledAt"" <= @to");
            param["to"] = to.Value;
        }
        if (doctorUserId.HasValue)
        {
            filters.Add(@"""DoctorUserId"" = @doctorUserId");
            param["doctorUserId"] = doctorUserId.Value;
        }

        var extra = filters.Count > 0 ? " AND " + string.Join(" AND ", filters) : "";
        var countSql = $"""SELECT COUNT(*)::int FROM "Appointments" WHERE {where}{extra}""";
        var total = await _db.QuerySingleAsync<int>(countSql, param, ct: ct);

        var listSql = $"""
            SELECT * FROM "Appointments"
            WHERE {where}{extra}
            ORDER BY "ScheduledAt" ASC
            OFFSET @skip LIMIT @take
            """;
        var rows = await _db.QueryAsync<Appointment>(listSql, param, ct: ct);

        return new PagedResult<AppointmentDto>
        {
            Items = rows.Select(Map).ToList(),
            Total = total,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
        };
    }

    public async Task UpdateAsync(Guid id, UpdateAppointmentRequest req, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var a = await _db.QueryFirstOrDefaultAsync<Appointment>(
            $"""SELECT * FROM "Appointments" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Appointment");

        var oldScheduledAt = a.ScheduledAt;
        var oldDoctorUserId = a.DoctorUserId;
        var oldDoctorName = a.DoctorName;

        if (req.ScheduledAt.HasValue) a.ScheduledAt = DateTimeHelper.EnsureUtc(req.ScheduledAt.Value);
        if (req.Department != null) a.Department = req.Department;
        if (req.ChiefComplaint != null) a.ChiefComplaint = req.ChiefComplaint;
        if (req.Notes != null) a.Notes = req.Notes;
        if (req.DurationMinutes.HasValue) a.DurationMinutes = req.DurationMinutes.Value;

        if (req.DoctorUserId.HasValue || req.DoctorName != null)
        {
            if (req.DoctorUserId.HasValue)
            {
                var (doctor, profile) = await ResolveStaffDoctorAsync(req.DoctorUserId.Value, ct);
                a.DoctorUserId = req.DoctorUserId;
                a.DoctorName = doctor.Name;
                a.ConsultationFee = profile.ConsultationFee;
                if (string.IsNullOrWhiteSpace(req.Department))
                    a.Department = profile.Department ?? doctor.Specialty ?? a.Department;
            }
            else if (req.DoctorName != null)
            {
                a.DoctorName = req.DoctorName.Trim();
            }
        }

        await _db.UpdateAsync(a, ct: ct);

        var rescheduled = (req.ScheduledAt.HasValue && req.ScheduledAt.Value != oldScheduledAt)
            || (req.DoctorUserId.HasValue && req.DoctorUserId != oldDoctorUserId)
            || (req.DoctorName != null && !string.Equals(req.DoctorName.Trim(), oldDoctorName, StringComparison.Ordinal));

        if (rescheduled && a.Status != AppointmentStatus.Cancelled)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var patient = await _db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
                new { patientId = a.PatientId },
                ct: ct);
            if (patient != null)
                await TrySendAppointmentMessageAsync(AppointmentTemplateKeys.Rescheduled, patient, a, DefaultRescheduledBody, ct);
        }
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var a = await _db.QueryFirstOrDefaultAsync<Appointment>(
            $"""SELECT * FROM "Appointments" WHERE "Id" = @id AND {where}""",
            new { id },
            ct: ct) ?? throw new NotFoundException("Appointment");
        var previousStatus = a.Status;
        if (!EnumParseHelper.TryParseSnakeCase<AppointmentStatus>(status, out var s))
            throw new ValidationException("Invalid appointment status");

        a.Status = s;
        if (s == AppointmentStatus.Completed)
        {
            a.CompletedAt = DateTime.UtcNow;
            if (!a.ConsultationFee.HasValue && a.DoctorUserId.HasValue)
            {
                var profileWhere = SqlFragments.WhereActive<StaffProfile>(ignoreTenant: false);
                var profile = await _db.QueryFirstOrDefaultAsync<StaffProfile>(
                    $"""SELECT * FROM "StaffProfiles" WHERE "UserId" = @userId AND {profileWhere}""",
                    new { userId = a.DoctorUserId.Value },
                    ct: ct);
                if (profile?.ConsultationFee != null)
                    a.ConsultationFee = profile.ConsultationFee;
            }
        }
        await _db.UpdateAsync(a, ct: ct);

        if (s == AppointmentStatus.Cancelled && previousStatus != AppointmentStatus.Cancelled)
        {
            var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
            var patient = await _db.QueryFirstOrDefaultAsync<Patient>(
                $"""SELECT * FROM "Patients" WHERE "Id" = @patientId AND {patientWhere}""",
                new { patientId = a.PatientId },
                ct: ct);
            if (patient != null)
                await TrySendAppointmentMessageAsync(AppointmentTemplateKeys.Cancelled, patient, a, DefaultCancelledBody, ct);
        }

        if (s == AppointmentStatus.NoShow && previousStatus != AppointmentStatus.NoShow)
        {
            var firstName = a.PatientName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Patient";
            await _notifications.PublishAsync(new NotificationPublishRequest(
                NotificationTypeCodes.AppointmentNoShow,
                "Appointment no-show",
                $"{firstName} marked as no-show",
                NotificationSeverity.Warning,
                EntityType: "appointment",
                EntityId: a.Id,
                ActionUrl: $"/appointments?id={a.Id}",
                DedupeKey: $"appointment.no_show:{a.Id}",
                DoctorUserId: a.DoctorUserId,
                CreatorUserId: _db.UserId), ct);
        }
    }

    private static AppointmentDto Map(Appointment a) => new(
        a.Id, a.PatientId, a.PatientName, a.PatientPhone,
        a.DoctorUserId, a.DoctorName, a.Department,
        a.ScheduledAt, a.DurationMinutes, a.ChiefComplaint, a.Notes, a.Status,
        a.ConsultationFee, a.IsPaid,
        a.CreatedAt, a.UpdatedAt);

    private async Task<(User doctor, StaffProfile profile)> ResolveStaffDoctorAsync(Guid doctorUserId, CancellationToken ct)
    {
        var userWhere = SqlFragments.WhereActive<User>(ignoreTenant: false);
        var doctor = await _db.QueryFirstOrDefaultAsync<User>(
            $"""SELECT * FROM "Users" WHERE "Id" = @doctorUserId AND {userWhere}""",
            new { doctorUserId },
            ct: ct) ?? throw new NotFoundException("Doctor");
        if (doctor.Role != UserRole.Doctor)
            throw new ValidationException("Selected user is not a doctor");

        var profileWhere = SqlFragments.WhereActive<StaffProfile>(ignoreTenant: false);
        var profile = await _db.QueryFirstOrDefaultAsync<StaffProfile>(
            $"""SELECT * FROM "StaffProfiles" WHERE "UserId" = @doctorUserId AND {profileWhere}""",
            new { doctorUserId },
            ct: ct) ?? throw new ValidationException("Doctor must have a staff profile. Add them under Hospital Staff first.");
        if (!profile.IsAvailable)
            throw new ValidationException("Doctor is not available for booking");

        return (doctor, profile);
    }

    private async Task TrySendAppointmentMessageAsync(
        string templateKey, Patient patient, Appointment appointment, string defaultBody, CancellationToken ct)
    {
        try
        {
            var timeZoneId = await GetTenantTimezoneAsync(ct);
            var bodyTemplate = await ResolveTemplateBodyAsync(templateKey, defaultBody, ct);
            var body = MessageTemplateHelper.Render(bodyTemplate, BuildTemplateValues(patient, appointment, timeZoneId));
            await _conversations.SendToPatientAsync(patient.Id, body, ct);
            await TrySendAppointmentEmailAsync(templateKey, patient, appointment, body, timeZoneId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send appointment WhatsApp ({TemplateKey}) for patient {PatientId}",
                templateKey, patient.Id);
        }
    }

    private async Task TrySendAppointmentEmailAsync(
        string templateKey, Patient patient, Appointment appointment, string body, string? timeZoneId, CancellationToken ct)
    {
        try
        {
            var emailWhere = SqlFragments.WhereActive<EmailSettings>(ignoreTenant: false);
            var emailSettings = await _db.QueryFirstOrDefaultAsync<EmailSettings>(
                $"""SELECT * FROM "EmailSettings" WHERE {emailWhere} LIMIT 1""",
                ct: ct);
            if (emailSettings == null || !emailSettings.Enabled || !emailSettings.SendWithWhatsApp)
                return;

            var hospitalDate = TenantTimeHelper.FormatHospitalDate(appointment.ScheduledAt, timeZoneId);
            var subject = templateKey switch
            {
                AppointmentTemplateKeys.Confirmation => $"Appointment confirmed — {hospitalDate}",
                AppointmentTemplateKeys.Rescheduled => $"Appointment rescheduled — {hospitalDate}",
                AppointmentTemplateKeys.Cancelled => $"Appointment cancelled — {hospitalDate}",
                _ => "Appointment update — Cure & Care Hospital",
            };

            var (ok, _, error) = await _email.SendToPatientAsync(patient.Id, subject, body, ct);
            if (!ok && error != null
                && !error.Contains("disabled", StringComparison.OrdinalIgnoreCase)
                && !error.Contains("no email", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Appointment email ({TemplateKey}) failed for patient {PatientId}: {Error}",
                    templateKey, patient.Id, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send appointment email ({TemplateKey}) for patient {PatientId}",
                templateKey, patient.Id);
        }
    }

    private async Task<string> ResolveTemplateBodyAsync(string templateKey, string defaultBody, CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<QuickTemplate>(ignoreTenant: false);
        var template = await _db.QueryFirstOrDefaultAsync<QuickTemplate>(
            $"""
            SELECT * FROM "Templates"
            WHERE {where}
              AND ("Category" = @templateKey OR "Name" = @templateKey OR LOWER("Name") LIKE @likeKey)
            ORDER BY "CreatedAt" DESC
            LIMIT 1
            """,
            new { templateKey, likeKey = $"%{templateKey.Replace('_', ' ')}%" },
            ct: ct);

        if (template == null && templateKey == AppointmentTemplateKeys.Confirmation)
        {
            template = await _db.QueryFirstOrDefaultAsync<QuickTemplate>(
                $"""
                SELECT * FROM "Templates"
                WHERE {where}
                  AND ("Category" = 'appointment' OR LOWER("Name") LIKE '%appointment confirmation%')
                ORDER BY "CreatedAt" DESC
                LIMIT 1
                """,
                ct: ct);
        }

        return template?.Body ?? defaultBody;
    }

    private async Task<string> GetTenantTimezoneAsync(CancellationToken ct)
    {
        var tz = await _db.QueryFirstOrDefaultAsync<string>(
            """SELECT "Timezone" FROM "Tenants" WHERE "Id" = @tenantId LIMIT 1""",
            new { tenantId = _db.TenantId },
            ignoreTenant: true,
            ct: ct);
        return TenantTimeHelper.NormalizeTimeZoneId(tz);
    }

    private static Dictionary<string, string?> BuildTemplateValues(
        Patient patient, Appointment appointment, string? timeZoneId)
    {
        // patientTimeZoneId reserved for later dual-local WhatsApp; null = hospital only
        var parts = AppointmentMessageTime.Format(appointment.ScheduledAt, timeZoneId, patientTimeZoneId: null);
        var values = AppointmentMessageTime.ToTemplateValues(parts);
        values["name"] = patient.Name;
        values["doctor"] = appointment.DoctorName;
        values["department"] = appointment.Department;
        return values;
    }
}
