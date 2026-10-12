using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services;

public class SchedulerHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SchedulerHostedService> _logger;

    public SchedulerHostedService(IServiceProvider services, ILogger<SchedulerHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("CureFlow scheduler started");
        var lastEscalate = DateTime.UtcNow;
        var lastApptRemind = DateTime.UtcNow;

        var lastReEngage = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            if ((now - lastEscalate).TotalMinutes >= 2)
            {
                await EscalateUnansweredAsync(ct);
                lastEscalate = now;
            }
            if ((now - lastApptRemind).TotalMinutes >= 15)
            {
                await CreateAppointmentRemindersAsync(ct);
                lastApptRemind = now;
            }
            if ((now - lastReEngage).TotalHours >= 12)
            {
                await MarkInactivePatientsAsync(ct);
                lastReEngage = now;
            }
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private Task RunForAllTenantsAsync(Func<IServiceProvider, Tenant, Task> action, CancellationToken ct) =>
        TenantScopeRunner.RunForAllTenantsAsync(_services, action, ct);

    private async Task EscalateUnansweredAsync(CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-15);
        await RunForAllTenantsAsync(async (sp, _) =>
        {
            var db = sp.GetRequiredService<ICureFlowDbSession>();
            var publisher = sp.GetRequiredService<INotificationPublisher>();

            var convs = await db.QueryAsync<Conversation>(
                """
                SELECT * FROM "Conversations"
                WHERE "AwaitingReplySince" IS NOT NULL AND "AwaitingReplySince" < @threshold
                  AND "Escalated" = false AND "IsDeleted" = false AND "TenantId" = @TenantId
                """,
                new { threshold },
                ct: ct);

            foreach (var c in convs)
            {
                var task = new TaskItem
                {
                    Title = $"⚠️ Unanswered lead: {c.Name ?? c.WaPhone}",
                    Type = TaskType.LeadEscalation,
                    PatientId = c.PatientId,
                    PatientName = c.Name,
                    ConversationId = c.Id,
                    AssignedTo = c.AssignedStaffId,
                    Priority = Priority.High,
                    DueAt = DateTime.UtcNow,
                    Notes = "No reply for over 15 minutes. Please respond.",
                };
                await db.InsertAsync(task, ct: ct);
                c.Escalated = true;
                await db.UpdateAsync(c, ct: ct);

                await publisher.PublishAsync(new NotificationPublishRequest(
                    NotificationTypeCodes.WhatsappLeadEscalation,
                    "Lead needs attention",
                    $"Unanswered lead: {c.Name ?? c.WaPhone}",
                    NotificationSeverity.Warning,
                    EntityType: "conversation",
                    EntityId: c.Id,
                    ActionUrl: $"/inbox?c={c.Id}",
                    DedupeKey: $"whatsapp.lead_escalation:{c.Id}",
                    AssignedStaffId: c.AssignedStaffId,
                    TargetUserIds: c.AssignedStaffId.HasValue ? [c.AssignedStaffId.Value] : null), ct);
            }
        }, ct);
    }

    private async Task CreateAppointmentRemindersAsync(CancellationToken ct)
    {
        var start = DateTime.UtcNow.AddHours(23);
        var end = DateTime.UtcNow.AddHours(25);
        await RunForAllTenantsAsync(async (sp, _) =>
        {
            var db = sp.GetRequiredService<ICureFlowDbSession>();
            var appts = await db.QueryAsync<Appointment>(
                """
                SELECT * FROM "Appointments"
                WHERE "ScheduledAt" >= @start AND "ScheduledAt" <= @end
                  AND "Status" IN (@scheduled, @confirmed)
                  AND "ReminderSent" = false AND "IsDeleted" = false AND "TenantId" = @TenantId
                """,
                new { start, end, scheduled = (int)AppointmentStatus.Scheduled, confirmed = (int)AppointmentStatus.Confirmed },
                ct: ct);

            foreach (var a in appts)
            {
                await db.InsertAsync(new TaskItem
                {
                    Title = $"Send 24h reminder: {a.PatientName}",
                    Type = TaskType.AppointmentReminder,
                    PatientId = a.PatientId,
                    PatientName = a.PatientName,
                    Priority = Priority.Medium,
                    DueAt = DateTime.UtcNow,
                    Notes = $"Appointment with {a.DoctorName} ({a.Department})",
                }, ct: ct);
                a.ReminderSent = true;
                await db.UpdateAsync(a, ct: ct);
            }
        }, ct);
    }

    private async Task MarkInactivePatientsAsync(CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddDays(-90);
        await RunForAllTenantsAsync(async (sp, _) =>
        {
            var db = sp.GetRequiredService<ICureFlowDbSession>();
            var patients = await db.QueryAsync<Patient>(
                """
                SELECT * FROM "Patients"
                WHERE "LastContactAt" < @threshold
                  AND "Status" NOT IN (@reEngage, @lost)
                  AND "IsDeleted" = false AND "TenantId" = @TenantId
                """,
                new
                {
                    threshold,
                    reEngage = (int)LeadStatus.ReEngagement,
                    lost = (int)LeadStatus.Lost,
                },
                ct: ct);

            foreach (var p in patients)
            {
                p.Status = LeadStatus.ReEngagement;
                await db.UpdateAsync(p, ct: ct);
            }
        }, ct);
    }
}
