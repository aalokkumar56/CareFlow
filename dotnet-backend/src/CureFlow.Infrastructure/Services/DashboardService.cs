using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Caching.Memory;
using TaskStatusEnum = CureFlow.Domain.Enums.TaskStatus;

namespace CureFlow.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    // Default estimated loss per missed event (₹) when no linked fee is available.
    private const decimal DefaultUnansweredInquiryLoss = 500m;
    private const decimal DefaultMissedAppointmentLoss = 1500m;
    private const decimal DefaultLostFollowupLoss = 800m;
    private const decimal DefaultInactivePatientLoss = 2000m;
    private const decimal RecoverableRevenueFactor = 0.65m;

    private readonly ICureFlowDbSession _db;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;

    public DashboardService(ICureFlowDbSession db, IMemoryCache cache, ITenantContext tenant)
    {
        _db = db;
        _cache = cache;
        _tenant = tenant;
    }

    public Task<object> GetOverviewAsync(CancellationToken ct = default)
    {
        var cacheKey = $"dashboard:overview:{_tenant.TenantId}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await BuildOverviewAsync(ct);
        })!;
    }

    public async Task<object> GetMissedRevenueAsync(CancellationToken ct = default)
    {
        var fifteenMinAgo = DateTime.UtcNow.AddMinutes(-15);
        var convWhere = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var apptWhere = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var taskWhere = SqlFragments.WhereActive<TaskItem>(ignoreTenant: false);
        var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);

        var unanswered = await _db.QueryAsync<Conversation>(
            $"""
            SELECT * FROM "Conversations"
            WHERE {convWhere}
              AND "AwaitingReplySince" IS NOT NULL AND "AwaitingReplySince" < @fifteenMinAgo
            ORDER BY "AwaitingReplySince"
            LIMIT 100
            """,
            new { fifteenMinAgo }, ct: ct);
        var missed = await _db.QueryAsync<Appointment>(
            $"""
            SELECT * FROM "Appointments"
            WHERE {apptWhere} AND "Status" = @status
            ORDER BY "ScheduledAt" DESC
            LIMIT 100
            """,
            new { status = (int)AppointmentStatus.NoShow }, ct: ct);
        var overdue = await _db.QueryAsync<TaskItem>(
            $"""
            SELECT * FROM "Tasks"
            WHERE {taskWhere} AND "Status" = @status AND "DueAt" < @now
            ORDER BY "DueAt"
            LIMIT 100
            """,
            new { status = (int)TaskStatusEnum.Pending, now = DateTime.UtcNow }, ct: ct);
        var inactive = await _db.QueryAsync<Patient>(
            $"""
            SELECT * FROM "Patients"
            WHERE {patientWhere} AND "Status" = @status
            ORDER BY "LastContactAt"
            LIMIT 100
            """,
            new { status = (int)LeadStatus.ReEngagement }, ct: ct);

        static decimal ApptLoss(Appointment a) =>
            a.ConsultationFee is > 0 ? a.ConsultationFee.Value : DefaultMissedAppointmentLoss;

        var unansweredLoss = unanswered.Count * DefaultUnansweredInquiryLoss;
        var missedApptLoss = missed.Sum(ApptLoss);
        var followupLoss = overdue.Count * DefaultLostFollowupLoss;
        var inactiveLoss = inactive.Count * DefaultInactivePatientLoss;
        var estimatedLoss = unansweredLoss + missedApptLoss + followupLoss + inactiveLoss;

        return new
        {
            unanswered_inquiries = unanswered.Select(c => new
            {
                c.Id,
                c.Name,
                c.WaPhone,
                c.LastMessagePreview,
                c.AwaitingReplySince,
                estimated_loss = DefaultUnansweredInquiryLoss,
            }),
            missed_appointments = missed.Select(a => new
            {
                a.Id,
                a.PatientName,
                a.DoctorName,
                a.Department,
                a.ScheduledAt,
                a.ConsultationFee,
                estimated_loss = ApptLoss(a),
            }),
            lost_followups = overdue.Select(t => new
            {
                t.Id,
                t.Title,
                t.PatientName,
                t.Type,
                t.DueAt,
                estimated_loss = DefaultLostFollowupLoss,
            }),
            inactive_patients = inactive.Select(p => new
            {
                p.Id,
                p.Name,
                p.Phone,
                p.Department,
                p.LastContactAt,
                estimated_loss = DefaultInactivePatientLoss,
            }),
            estimated_loss = estimatedLoss,
            recoverable_revenue = Math.Round(estimatedLoss * RecoverableRevenueFactor, 0),
            category_totals = new
            {
                unanswered_inquiries = new { count = unanswered.Count, estimated_loss = unansweredLoss },
                missed_appointments = new { count = missed.Count, estimated_loss = missedApptLoss },
                lost_followups = new { count = overdue.Count, estimated_loss = followupLoss },
                inactive_patients = new { count = inactive.Count, estimated_loss = inactiveLoss },
            },
        };
    }

    private async Task<object> BuildOverviewAsync(CancellationToken ct)
    {
        var timeZoneId = await GetTenantTimezoneAsync(ct);
        var hospitalNow = TenantTimeHelper.UtcToHospitalLocal(DateTime.UtcNow, timeZoneId);
        var hospitalToday = hospitalNow.Date;
        var (todayStart, todayEnd) = TenantTimeHelper.HospitalDayRangeUtc(hospitalToday, timeZoneId);
        var (yesterdayStart, _) = TenantTimeHelper.HospitalDayRangeUtc(hospitalToday.AddDays(-1), timeZoneId);
        var weekAgo = TenantTimeHelper.HospitalLocalToUtc(hospitalToday.AddDays(-6), timeZoneId);
        var hospitalMonthStart = new DateTime(hospitalNow.Year, hospitalNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var monthStart = TenantTimeHelper.HospitalLocalToUtc(hospitalMonthStart, timeZoneId);
        var prevMonthStart = TenantTimeHelper.HospitalLocalToUtc(hospitalMonthStart.AddMonths(-1), timeZoneId);
        var fifteenMinAgo = DateTime.UtcNow.AddMinutes(-15);
        var weekEnd = todayEnd;
        var lastWeekStart = TenantTimeHelper.HospitalLocalToUtc(hospitalToday.AddDays(-13), timeZoneId);
        var lastWeekEnd = TenantTimeHelper.HospitalLocalToUtc(hospitalToday.AddDays(-6), timeZoneId);

        var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var apptWhere = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var taskWhere = SqlFragments.WhereActive<TaskItem>(ignoreTenant: false);
        var convWhere = SqlFragments.WhereActive<Conversation>(ignoreTenant: false);
        var referralWhere = SqlFragments.WhereActive<Referral>(ignoreTenant: false);

        var totalPatients = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Patients" WHERE {patientWhere}""", ct: ct);
        var newInquiriesToday = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Patients" WHERE {patientWhere} AND "CreatedAt" >= @todayStart""",
            new { todayStart }, ct: ct);
        var newInquiriesYesterday = await _db.QuerySingleAsync<int>(
            $"""
            SELECT COUNT(*) FROM "Patients"
            WHERE {patientWhere} AND "CreatedAt" >= @yesterdayStart AND "CreatedAt" < @todayStart
            """,
            new { yesterdayStart, todayStart }, ct: ct);
        var apptsToday = await _db.QuerySingleAsync<int>(
            $"""
            SELECT COUNT(*) FROM "Appointments"
            WHERE {apptWhere} AND "ScheduledAt" >= @todayStart AND "ScheduledAt" < @todayEnd
            """,
            new { todayStart, todayEnd }, ct: ct);
        var apptsYesterday = await _db.QuerySingleAsync<int>(
            $"""
            SELECT COUNT(*) FROM "Appointments"
            WHERE {apptWhere} AND "ScheduledAt" >= @yesterdayStart AND "ScheduledAt" < @todayStart
            """,
            new { yesterdayStart, todayStart }, ct: ct);
        var followUpsDue = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Tasks" WHERE {taskWhere} AND "Status" = @status AND "DueAt" <= @now""",
            new { status = (int)TaskStatusEnum.Pending, now = DateTime.UtcNow }, ct: ct);
        var unanswered = await _db.QuerySingleAsync<int>(
            $"""
            SELECT COUNT(*) FROM "Conversations"
            WHERE {convWhere}
              AND "AwaitingReplySince" IS NOT NULL AND "AwaitingReplySince" < @fifteenMinAgo
            """,
            new { fifteenMinAgo }, ct: ct);
        var inactive = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Patients" WHERE {patientWhere} AND "Status" = @status""",
            new { status = (int)LeadStatus.ReEngagement }, ct: ct);
        var visited = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Patients" WHERE {patientWhere} AND "Status" = @status""",
            new { status = (int)LeadStatus.Visited }, ct: ct);

        var revenueMtdAppointments = await _db.QuerySingleAsync<decimal>(
            $"""
            SELECT COALESCE(SUM("ConsultationFee"), 0) FROM "Appointments"
            WHERE {apptWhere} AND "Status" = @status AND "ScheduledAt" >= @monthStart
            """,
            new { status = (int)AppointmentStatus.Completed, monthStart }, ct: ct);
        var revenueMtdReferrals = await _db.QuerySingleAsync<decimal>(
            $"""SELECT COALESCE(SUM("Revenue"), 0) FROM "Referrals" WHERE {referralWhere} AND "CreatedAt" >= @monthStart""",
            new { monthStart }, ct: ct);
        var revenueMtd = revenueMtdAppointments + revenueMtdReferrals;

        var revenuePrevMonthAppts = await _db.QuerySingleAsync<decimal>(
            $"""
            SELECT COALESCE(SUM("ConsultationFee"), 0) FROM "Appointments"
            WHERE {apptWhere} AND "Status" = @status
              AND "ScheduledAt" >= @prevMonthStart AND "ScheduledAt" < @monthStart
            """,
            new { status = (int)AppointmentStatus.Completed, prevMonthStart, monthStart }, ct: ct);
        var revenuePrevMonthReferrals = await _db.QuerySingleAsync<decimal>(
            $"""
            SELECT COALESCE(SUM("Revenue"), 0) FROM "Referrals"
            WHERE {referralWhere} AND "CreatedAt" >= @prevMonthStart AND "CreatedAt" < @monthStart
            """,
            new { prevMonthStart, monthStart }, ct: ct);
        var revenuePrevMonth = revenuePrevMonthAppts + revenuePrevMonthReferrals;

        var depts = await _db.QueryAsync<DeptCountRow>(
            $"""
            SELECT "Department" AS "Name", COUNT(*)::int AS "Count" FROM "Patients"
            WHERE {patientWhere} AND "Department" IS NOT NULL
            GROUP BY "Department"
            ORDER BY "Count" DESC
            LIMIT 6
            """, ct: ct);
        var sources = await _db.QueryAsync<DeptCountRow>(
            $"""
            SELECT COALESCE("InquirySource", 'unknown') AS "Name", COUNT(*)::int AS "Count" FROM "Patients"
            WHERE {patientWhere}
            GROUP BY "InquirySource"
            ORDER BY "Count" DESC
            LIMIT 6
            """, ct: ct);
        var statusBreakdown = await _db.QueryAsync<StatusCountRow>(
            $"""
            SELECT "Status" AS "Status", COUNT(*)::int AS "Count" FROM "Patients"
            WHERE {patientWhere}
            GROUP BY "Status"
            """, ct: ct);
        var trendRaw = await _db.QueryAsync<DateCountRow>(
            $"""
            SELECT (timezone(@tz, "CreatedAt"))::date AS "Date", COUNT(*)::int AS "Count" FROM "Patients"
            WHERE {patientWhere} AND "CreatedAt" >= @weekAgo
            GROUP BY (timezone(@tz, "CreatedAt"))::date
            """,
            new { weekAgo, tz = timeZoneId }, ct: ct);

        var apptWeekRaw = await _db.QueryAsync<ApptWeekRow>(
            $"""
            SELECT (timezone(@tz, "ScheduledAt"))::date AS "Date",
                   COUNT(*) FILTER (WHERE "Status" IN (@scheduled, @confirmed))::int AS "Scheduled",
                   COUNT(*) FILTER (WHERE "Status" = @completed)::int AS "Completed"
            FROM "Appointments"
            WHERE {apptWhere} AND "ScheduledAt" >= @weekAgo AND "ScheduledAt" < @weekEnd
            GROUP BY (timezone(@tz, "ScheduledAt"))::date
            """,
            new
            {
                weekAgo,
                weekEnd,
                tz = timeZoneId,
                scheduled = (int)AppointmentStatus.Scheduled,
                confirmed = (int)AppointmentStatus.Confirmed,
                completed = (int)AppointmentStatus.Completed,
            }, ct: ct);

        var apptLastWeekRaw = await _db.QueryAsync<ApptWeekRow>(
            $"""
            SELECT (timezone(@tz, "ScheduledAt"))::date AS "Date",
                   COUNT(*) FILTER (WHERE "Status" IN (@scheduled, @confirmed))::int AS "Scheduled",
                   COUNT(*) FILTER (WHERE "Status" = @completed)::int AS "Completed"
            FROM "Appointments"
            WHERE {apptWhere} AND "ScheduledAt" >= @lastWeekStart AND "ScheduledAt" < @lastWeekEnd
            GROUP BY (timezone(@tz, "ScheduledAt"))::date
            """,
            new
            {
                lastWeekStart,
                lastWeekEnd,
                tz = timeZoneId,
                scheduled = (int)AppointmentStatus.Scheduled,
                confirmed = (int)AppointmentStatus.Confirmed,
                completed = (int)AppointmentStatus.Completed,
            }, ct: ct);

        var revenueWeekRaw = await _db.QueryAsync<DateAmountRow>(
            $"""
            SELECT (timezone(@tz, "ScheduledAt"))::date AS "Date", COALESCE(SUM("ConsultationFee"), 0) AS "Amount"
            FROM "Appointments"
            WHERE {apptWhere} AND "Status" = @status
              AND "ScheduledAt" >= @weekAgo AND "ScheduledAt" < @weekEnd
            GROUP BY (timezone(@tz, "ScheduledAt"))::date
            """,
            new { status = (int)AppointmentStatus.Completed, weekAgo, weekEnd, tz = timeZoneId }, ct: ct);

        var revenueLastWeekRaw = await _db.QueryAsync<DateAmountRow>(
            $"""
            SELECT (timezone(@tz, "ScheduledAt"))::date AS "Date", COALESCE(SUM("ConsultationFee"), 0) AS "Amount"
            FROM "Appointments"
            WHERE {apptWhere} AND "Status" = @status
              AND "ScheduledAt" >= @lastWeekStart AND "ScheduledAt" < @lastWeekEnd
            GROUP BY (timezone(@tz, "ScheduledAt"))::date
            """,
            new { status = (int)AppointmentStatus.Completed, lastWeekStart, lastWeekEnd, tz = timeZoneId }, ct: ct);

        var revenueMonthRaw = await _db.QueryAsync<DateAmountRow>(
            $"""
            SELECT (timezone(@tz, "ScheduledAt"))::date AS "Date", COALESCE(SUM("ConsultationFee"), 0) AS "Amount"
            FROM "Appointments"
            WHERE {apptWhere} AND "Status" = @status
              AND "ScheduledAt" >= @monthStart AND "ScheduledAt" < @weekEnd
            GROUP BY (timezone(@tz, "ScheduledAt"))::date
            """,
            new { status = (int)AppointmentStatus.Completed, monthStart, weekEnd, tz = timeZoneId }, ct: ct);

        var revenueLastMonthRaw = await _db.QueryAsync<DateAmountRow>(
            $"""
            SELECT (timezone(@tz, "ScheduledAt"))::date AS "Date", COALESCE(SUM("ConsultationFee"), 0) AS "Amount"
            FROM "Appointments"
            WHERE {apptWhere} AND "Status" = @status
              AND "ScheduledAt" >= @prevMonthStart AND "ScheduledAt" < @monthStart
            GROUP BY (timezone(@tz, "ScheduledAt"))::date
            """,
            new { status = (int)AppointmentStatus.Completed, prevMonthStart, monthStart, tz = timeZoneId }, ct: ct);

        var upcoming = await _db.QueryAsync<UpcomingApptRow>(
            $"""
            SELECT "Id", "PatientName", "DoctorName", "Department", "ScheduledAt",
                   "DurationMinutes", "Status",
                   COALESCE("ChiefComplaint", 'Consultation') AS "AppointmentType"
            FROM "Appointments"
            WHERE {apptWhere} AND "ScheduledAt" >= @now
              AND "Status" NOT IN (@cancelled, @noShow, @completed)
            ORDER BY "ScheduledAt"
            LIMIT 5
            """,
            new
            {
                now = DateTime.UtcNow,
                cancelled = (int)AppointmentStatus.Cancelled,
                noShow = (int)AppointmentStatus.NoShow,
                completed = (int)AppointmentStatus.Completed,
            }, ct: ct);

        var conversionRate = totalPatients == 0 ? 0 : Math.Round((double)visited / totalPatients * 100, 1);
        var completedAppts = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Appointments" WHERE {apptWhere} AND "Status" = @status""",
            new { status = (int)AppointmentStatus.Completed }, ct: ct);
        var totalAppts = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Appointments" WHERE {apptWhere} AND "Status" <> @status""",
            new { status = (int)AppointmentStatus.Cancelled }, ct: ct);
        var completionRate = totalAppts == 0 ? 0 : (double)completedAppts / totalAppts;
        var careScore = Math.Round(Math.Min(5.0, 3.6 + conversionRate / 40.0 + completionRate * 0.8), 1);

        static bool SameHospitalDate(DateTime sqlDate, DateTime hospitalDate) =>
            sqlDate.Year == hospitalDate.Year && sqlDate.Month == hospitalDate.Month && sqlDate.Day == hospitalDate.Day;

        var trend = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = hospitalToday.AddDays(-(6 - offset));
                var count = trendRaw.FirstOrDefault(x => SameHospitalDate(x.Date, date))?.Count ?? 0;
                return new { date = date.ToString("yyyy-MM-dd"), count };
            })
            .ToList();

        static List<object> BuildApptSeries(IEnumerable<ApptWeekRow> raw, DateTime hospitalRangeStart, int days)
        {
            return Enumerable.Range(0, days)
                .Select(offset =>
                {
                    var date = hospitalRangeStart.AddDays(offset);
                    var row = raw.FirstOrDefault(x => SameHospitalDate(x.Date, date));
                    return (object)new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        label = date.ToString("ddd dd"),
                        scheduled = row?.Scheduled ?? 0,
                        completed = row?.Completed ?? 0,
                    };
                })
                .ToList();
        }

        static List<object> BuildRevenueSeries(IEnumerable<DateAmountRow> raw, DateTime hospitalRangeStart, int days)
        {
            return Enumerable.Range(0, days)
                .Select(offset =>
                {
                    var date = hospitalRangeStart.AddDays(offset);
                    var amount = raw.FirstOrDefault(x => SameHospitalDate(x.Date, date))?.Amount ?? 0m;
                    return (object)new { date = date.ToString("yyyy-MM-dd"), label = date.ToString("dd MMM"), amount };
                })
                .ToList();
        }

        var appointments_week = BuildApptSeries(apptWeekRaw, hospitalToday.AddDays(-6), 7);
        var appointments_last_week = BuildApptSeries(apptLastWeekRaw, hospitalToday.AddDays(-13), 7);

        var revenue_week = BuildRevenueSeries(revenueWeekRaw, hospitalToday.AddDays(-6), 7);
        var revenue_last_week = BuildRevenueSeries(revenueLastWeekRaw, hospitalToday.AddDays(-13), 7);
        var mtdDays = Math.Max(1, (hospitalToday - hospitalMonthStart).Days + 1);
        var revenue_month = BuildRevenueSeries(revenueMonthRaw, hospitalMonthStart, mtdDays);
        var prevMonthDays = Math.Max(1, (hospitalMonthStart - hospitalMonthStart.AddMonths(-1)).Days);
        var revenue_last_month = BuildRevenueSeries(revenueLastMonthRaw, hospitalMonthStart.AddMonths(-1), prevMonthDays);
        var revenue_last_month_total = revenuePrevMonthAppts + revenuePrevMonthReferrals;

        static double PctChange(int current, int previous) =>
            previous == 0 ? (current > 0 ? 100 : 0) : Math.Round((double)(current - previous) / previous * 100, 0);

        static double PctChangeDecimal(decimal current, decimal previous) =>
            previous == 0m ? (current > 0 ? 100 : 0) : Math.Round((double)(current - previous) / (double)previous * 100, 0);

        return new
        {
            total_patients = totalPatients,
            new_inquiries_today = newInquiriesToday,
            new_inquiries_yesterday = newInquiriesYesterday,
            appointments_today = apptsToday,
            appointments_yesterday = apptsYesterday,
            follow_ups_due = followUpsDue,
            unanswered_leads = unanswered,
            inactive_patients = inactive,
            conversion_rate = conversionRate,
            revenue_mtd = revenueMtd,
            revenue_mtd_change_pct = PctChangeDecimal(revenueMtd, revenuePrevMonth),
            appointments_today_change_pct = PctChange(apptsToday, apptsYesterday),
            new_patients_change_pct = PctChange(newInquiriesToday, newInquiriesYesterday),
            care_score = careScore,
            care_score_change = 0.3,
            departments = depts,
            sources,
            trend,
            appointments_week,
            appointments_last_week,
            revenue_week,
            revenue_last_week,
            revenue_month,
            revenue_last_month,
            revenue_last_month_total,
            upcoming_appointments = upcoming.Select(a => new
            {
                a.Id,
                a.PatientName,
                a.DoctorName,
                a.Department,
                scheduled_at = a.ScheduledAt,
                duration_minutes = a.DurationMinutes,
                status = a.Status.ToString().ToLowerInvariant(),
                appointment_type = a.AppointmentType,
            }),
            status_breakdown = statusBreakdown.ToDictionary(
                x => x.Status switch
                {
                    LeadStatus.NewInquiry => "new_inquiry",
                    LeadStatus.Contacted => "contacted",
                    LeadStatus.AppointmentScheduled => "appointment_scheduled",
                    LeadStatus.FollowUpPending => "follow_up_pending",
                    LeadStatus.Visited => "visited",
                    LeadStatus.NoResponse => "no_response",
                    LeadStatus.Lost => "lost",
                    LeadStatus.ReEngagement => "re_engagement",
                    _ => "unknown"
                },
                x => x.Count),
        };
    }

    public async Task<object> GetClinicalOverviewAsync(DateTime? date, string? scope, Guid? doctorUserId, CancellationToken ct = default)
    {
        var timeZoneId = await GetTenantTimezoneAsync(ct);
        DateTime dayStart;
        DateTime dayEnd;
        string hospitalDateLabel;
        if (date.HasValue)
        {
            var hospitalDay = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Unspecified);
            (dayStart, dayEnd) = TenantTimeHelper.HospitalDayRangeUtc(hospitalDay, timeZoneId);
            hospitalDateLabel = hospitalDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            (dayStart, dayEnd) = TenantTimeHelper.HospitalTodayRangeUtc(timeZoneId);
            hospitalDateLabel = TenantTimeHelper.UtcToHospitalLocal(DateTime.UtcNow, timeZoneId)
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        var viewAll = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
        Guid? filterDoctorId = null;
        if (!viewAll)
        {
            filterDoctorId = doctorUserId ?? _tenant.UserId;
        }
        else if (doctorUserId.HasValue)
        {
            filterDoctorId = doctorUserId;
        }

        string? doctorName = null;
        if (_tenant.UserId.HasValue)
        {
            var userWhere = SqlFragments.WhereActive<User>(ignoreTenant: false);
            var me = await _db.QueryFirstOrDefaultAsync<User>(
                $"""SELECT * FROM "Users" WHERE "Id" = @id AND {userWhere}""",
                new { id = _tenant.UserId.Value },
                ct: ct);
            doctorName = me?.Name;
        }

        var apptWhere = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var filters = new List<string>
        {
            @"""ScheduledAt"" >= @dayStart",
            @"""ScheduledAt"" < @dayEnd",
        };
        var param = SqlParam.Merge(new { dayStart, dayEnd });
        if (filterDoctorId.HasValue)
        {
            filters.Add(@"""DoctorUserId"" = @filterDoctorId");
            param["filterDoctorId"] = filterDoctorId.Value;
        }

        var extra = " AND " + string.Join(" AND ", filters);
        var rows = await _db.QueryAsync<Appointment>(
            $"""
            SELECT * FROM "Appointments"
            WHERE {apptWhere}{extra}
            ORDER BY "ScheduledAt" ASC
            """,
            param,
            ct: ct);

        var appointments = rows.ToList();
        var myUserId = _tenant.UserId;
        var completed = appointments.Count(a => a.Status == AppointmentStatus.Completed);
        var waitingCheckIn = appointments.Count(a => a.Status == AppointmentStatus.Scheduled);

        var doctors = await _db.QueryAsync<DoctorOptionRow>(
            $"""
            SELECT DISTINCT u."Id" AS "UserId", u."Name" AS "Name"
            FROM "Appointments" a
            INNER JOIN "Users" u ON u."Id" = a."DoctorUserId"
            WHERE a."TenantId" = @tenantId
              AND a."IsDeleted" = false
              AND u."TenantId" = @tenantId
              AND u."IsDeleted" = false
              AND a."ScheduledAt" >= @dayStart AND a."ScheduledAt" < @dayEnd
            ORDER BY u."Name"
            """,
            new { tenantId = _tenant.TenantId, dayStart, dayEnd },
            ct: ct);

        return new
        {
            doctor_name = doctorName,
            date = hospitalDateLabel,
            scope = viewAll ? "all" : "mine",
            filter_doctor_user_id = filterDoctorId,
            stats = new
            {
                appointments_today = appointments.Count,
                completed_today = completed,
                waiting_check_in = waitingCheckIn,
            },
            doctors = doctors.Select(d => new { user_id = d.UserId, name = d.Name }),
            appointments_today = appointments.Select(a => new
            {
                a.Id,
                patient_id = a.PatientId,
                patient_name = a.PatientName,
                patient_phone = a.PatientPhone,
                doctor_user_id = a.DoctorUserId,
                doctor_name = a.DoctorName,
                a.Department,
                scheduled_at = a.ScheduledAt,
                status = a.Status.ToString().ToLowerInvariant(),
                chief_complaint = a.ChiefComplaint,
                notes = a.Notes,
                is_mine = myUserId.HasValue && a.DoctorUserId == myUserId,
            }),
        };
    }

    private async Task<string> GetTenantTimezoneAsync(CancellationToken ct)
    {
        var tz = await _db.QueryFirstOrDefaultAsync<string>(
            """SELECT "Timezone" FROM "Tenants" WHERE "Id" = @tenantId LIMIT 1""",
            new { tenantId = _tenant.TenantId },
            ignoreTenant: true,
            ct: ct);
        return TenantTimeHelper.NormalizeTimeZoneId(tz);
    }

    private sealed class DoctorOptionRow
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class DeptCountRow
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    private sealed class StatusCountRow
    {
        public LeadStatus Status { get; set; }
        public int Count { get; set; }
    }

    private sealed class DateCountRow
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    private sealed class ApptWeekRow
    {
        public DateTime Date { get; set; }
        public int Scheduled { get; set; }
        public int Completed { get; set; }
    }

    private sealed class DateAmountRow
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class UpcomingApptRow
    {
        public Guid Id { get; set; }
        public string PatientName { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string Department { get; set; } = "";
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        public AppointmentStatus Status { get; set; }
        public string AppointmentType { get; set; } = "";
    }
}
