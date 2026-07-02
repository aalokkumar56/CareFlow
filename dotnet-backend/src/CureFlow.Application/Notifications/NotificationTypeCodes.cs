namespace CureFlow.Application.Notifications;

/// <summary>Snake_case notification type codes stored in the database.</summary>
public static class NotificationTypeCodes
{
    public const string WhatsappInboundMessage = "whatsapp.inbound_message";
    public const string WhatsappLeadEscalation = "whatsapp.lead_escalation";
    public const string AppointmentCreated = "appointment.created";
    public const string AppointmentRescheduled = "appointment.rescheduled";
    public const string AppointmentCancelled = "appointment.cancelled";
    public const string AppointmentReminderDue = "appointment.reminder_due";
    public const string AppointmentNoShow = "appointment.no_show";
    public const string TaskAssigned = "task.assigned";
    public const string TaskOverdue = "task.overdue";
    public const string CampaignScheduled = "campaign.scheduled";
    public const string CampaignCompleted = "campaign.completed";
    public const string CampaignFailed = "campaign.failed";
    public const string ReferralCreated = "referral.created";
    public const string ReferralReconnectDue = "referral.reconnect_due";
    public const string PatientCreated = "patient.created";
    public const string PatientReEngagement = "patient.re_engagement";
    public const string ClinicalLabUploaded = "clinical.lab_uploaded";
    public const string ClinicalPrescriptionAdded = "clinical.prescription_added";
    public const string AuditSecurityEvent = "audit.security_event";
    public const string SystemIntegrationError = "system.integration_error";
}
