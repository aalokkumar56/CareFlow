using System.ComponentModel.DataAnnotations;
namespace CureFlow.Application.Areas.Platform;
public record PlanModuleInput(bool IsEnabled, [property: Required, StringLength(500, MinimumLength=3)] string Reason);
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
    public static string[] RequiredForApi(string path)
    {
        var p = path.ToLowerInvariant().Trim('/').Split('/');
        if (p.Length < 2 || p[0] != "api") return [];
        var area = p[1];
        if (area == "leads" && p.Length > 3 && p[3] == "appointment") return ["leads", "appointments"];
        if (area == "staff" && p.Length > 2 && p[2] == "booking-options") return ["appointments"];
        if (area == "patients" && p.Length > 3 && new[]{"visits","timeline","visit-chart","lifestyle","holistic-view"}.Contains(p[3])) return ["patients", "clinical"];
        return area switch {
            "patients" or "patient-documents" => ["patients"], "appointments" => ["appointments"],
            "leads" => ["leads"], "doctors" or "referrals" => ["referrals"], "tasks" => ["follow-ups"],
            "staff" => ["staff"], "dashboard" => ["analytics"], "campaigns" => ["campaigns"],
            "clinical" or "visits" or "prescriptions" or "allergies" or "lab-reports" => ["clinical"],
            "conversations" or "email" or "templates" or "whatsapp" or "ai" => ["communications"],
            "settings" when p.Length > 2 && new[]{"email","sms","whatsapp"}.Contains(p[2]) => ["communications"],
            _ => []
        };
    }
}
