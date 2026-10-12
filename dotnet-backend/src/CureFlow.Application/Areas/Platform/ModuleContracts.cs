using System.ComponentModel.DataAnnotations;
namespace CureFlow.Application.Areas.Platform;
public record PlanModuleInput(bool IsEnabled, [Required, StringLength(500, MinimumLength=3)] string Reason);
public sealed record SupportedModule(string Key, string Name, string Description);
/// <summary>Deployable capabilities. Adding a catalog record cannot create code or API routes.</summary>
public static class SupportedModules
{
    public static readonly SupportedModule[] All = [
        new("patients", "Patients", "Patient registration, profiles and documents"),
        new("appointments", "Appointments", "Appointment scheduling and booking"),
        new("leads", "Leads", "Lead acquisition and conversion"),
        new("communications", "Communications", "WhatsApp, email, templates and message assistance"),
        new("campaigns", "Campaigns", "Patient outreach campaigns"),
        new("clinical", "Clinical records", "Visits, notes, prescriptions, allergies and laboratory reports"),
        new("referrals", "Referral CRM", "Referring doctors and referral relationships"),
        new("follow-ups", "Follow-ups", "Follow-up tasks and reminders"),
        new("analytics", "Analytics", "Dashboard and clinical reporting"),
        new("staff", "Hospital staff", "Staff profiles and schedules")
    ];
    // Dependencies follow existing patient-linked clinical/scheduling and communications-backed campaign services.
    // This commercial dependency map grants no clinical capability or facility assignment.
    public static string[] Dependencies(string key) => key switch
    {
        "appointments" or "clinical" => ["patients"],
        "campaigns" => ["communications"],
        _ => []
    };
    public static string[] RequiredForApi(string path)
    {
        var p = path.ToLowerInvariant().Trim('/').Split('/');
        if (p.Length < 2 || p[0] != "api") return [];
        var area = p[1];
        if (area == "facilities" && p.Length > 3 && p[3] == "clinical-worklist") return ["clinical", "appointments", "patients"];
        if (area == "patients" && p.Length > 3 && p[3] == "appointments") return ["patients", "appointments"];
        if (area == "facilities" && p.Length > 3 && p[3] == "appointment-no-shows") return ["appointments", "follow-ups", "clinical", "patients"];
        if (area == "facilities" && p.Length > 3 && p[3] is "queue-arrivals" or "queue-dispatch") return ["appointments"];
        if (area == "leads" && p.Length > 3 && p[3] == "conversion") return ["leads", "patients"];
        if (area == "leads" && p.Length > 3 && p[3] == "appointment") return ["leads", "appointments"];
        if (area == "staff" && p.Length > 2 && p[2] == "booking-options") return ["appointments"];
        if (area == "patients" && p.Length > 3 && new[]{"visits","timeline","visit-chart","lifestyle","holistic-view"}.Contains(p[3])) return ["patients", "clinical"];
        return area switch {
            "patients" or "patient-documents" or "patient-imports" or "patient-exports" => ["patients"], "appointments" or "appointment-reservations" or "appointment-bookings" or "appointment-waitlists" => ["appointments"],
            "named-follow-ups" => ["follow-ups", "clinical", "appointments", "patients"],
            "leads" => ["leads"], "doctors" or "referrals" => ["referrals"], "tasks" => ["follow-ups"],
            "staff" => ["staff"], "dashboard" => ["analytics"], "campaigns" => ["campaigns"],
            "clinical" or "visits" or "prescriptions" or "allergies" or "lab-reports" => ["clinical"],
            "conversations" or "email" or "templates" or "whatsapp" or "ai" => ["communications"],
            "settings" when p.Length > 2 && new[]{"email","sms","whatsapp"}.Contains(p[2]) => ["communications"],
            _ => []
        };
    }
}
