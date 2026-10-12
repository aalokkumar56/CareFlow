using CureFlow.Domain.Entities.Organization;
using CureFlow.Application.Common;
using CureFlow.Domain.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Entities.Lifestyle;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CureFlow.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private static readonly ValueComparer<List<string>> StringListComparer =
        new(
            (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
            list => list == null ? 0 : list.Aggregate(0, (acc, item) => HashCode.Combine(acc, item == null ? 0 : item.GetHashCode())),
            list => list == null ? new List<string>() : list.ToList());

    private readonly ITenantContext _tenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    // SaaS
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<User> Users => Set<User>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AppointmentBookingChange> AppointmentBookingChanges => Set<AppointmentBookingChange>();
    public DbSet<QueuePriorityOverride> QueuePriorityOverrides => Set<QueuePriorityOverride>();
    public DbSet<EncounterTransition> EncounterTransitions => Set<EncounterTransition>();
    public DbSet<QueueStart> QueueStarts => Set<QueueStart>();
    public DbSet<AppointmentNoShow> AppointmentNoShows => Set<AppointmentNoShow>();
    public DbSet<ClinicalFollowUpWork> ClinicalFollowUpWorks => Set<ClinicalFollowUpWork>();
    public DbSet<ClinicalFollowUpVersion> ClinicalFollowUpVersions => Set<ClinicalFollowUpVersion>();
    public DbSet<AppointmentWaitlistEntry> AppointmentWaitlistEntries => Set<AppointmentWaitlistEntry>();
    public DbSet<AppointmentWaitlistOffer> AppointmentWaitlistOffers => Set<AppointmentWaitlistOffer>();
    public DbSet<AppointmentWaitlistDecision> AppointmentWaitlistDecisions => Set<AppointmentWaitlistDecision>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<QueueCounter> QueueCounters => Set<QueueCounter>();
    public DbSet<AppointmentReservation> AppointmentReservations => Set<AppointmentReservation>();
    public DbSet<AppointmentReservationSeat> AppointmentReservationSeats => Set<AppointmentReservationSeat>();
    public DbSet<LeadConversion> LeadConversions => Set<LeadConversion>();
    public DbSet<PatientExportJob> PatientExportJobs => Set<PatientExportJob>();
    public DbSet<PatientImportBatch> PatientImportBatches => Set<PatientImportBatch>();
    public DbSet<PatientImportRow> PatientImportRows => Set<PatientImportRow>();
    public DbSet<PatientConfidentialityState> PatientConfidentialityStates => Set<PatientConfidentialityState>();
    public DbSet<PatientAlertVersion> PatientAlertVersions => Set<PatientAlertVersion>();
    public DbSet<TemporaryPatientIdentity> TemporaryPatientIdentities => Set<TemporaryPatientIdentity>();
    public DbSet<PatientIdentityVerification> PatientIdentityVerifications => Set<PatientIdentityVerification>();
    public DbSet<PatientDemographicVersion> PatientDemographicVersions => Set<PatientDemographicVersion>();
    public DbSet<PatientMerge> PatientMerges => Set<PatientMerge>();
    public DbSet<PatientLinkJournal> PatientLinkJournals => Set<PatientLinkJournal>();
    public DbSet<PatientIdentityReviewVersion> PatientIdentityReviewVersions => Set<PatientIdentityReviewVersion>();
    public DbSet<PatientContactVersion> PatientContactVersions => Set<PatientContactVersion>();
    public DbSet<PatientIdentifier> PatientIdentifiers => Set<PatientIdentifier>();
    public DbSet<PatientIdentifierCounter> PatientIdentifierCounters => Set<PatientIdentifierCounter>();
    public DbSet<OrganizationStructureVersion> OrganizationStructureVersions => Set<OrganizationStructureVersion>();
    public DbSet<FacilityResource> FacilityResources => Set<FacilityResource>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<FacilityLocation> FacilityLocations => Set<FacilityLocation>();
    public DbSet<FacilityOperatingSetting> FacilityOperatingSettings => Set<FacilityOperatingSetting>();
    public DbSet<TenantProcessingPolicy> TenantProcessingPolicies => Set<TenantProcessingPolicy>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<OutboxConsumerReceipt> OutboxConsumerReceipts => Set<OutboxConsumerReceipt>();
    public DbSet<PlatformRole> PlatformRoles => Set<PlatformRole>();
    public DbSet<PlatformCommand> PlatformCommands => Set<PlatformCommand>();
    public DbSet<TenantApiActivity> TenantApiActivities => Set<TenantApiActivity>();
    public DbSet<PlanModule> PlanModules => Set<PlanModule>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<PlatformSession> PlatformSessions => Set<PlatformSession>();
    public DbSet<TenantOnboardingState> TenantOnboardingStates => Set<TenantOnboardingState>();
    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();
    public DbSet<PlatformModule> PlatformModules => Set<PlatformModule>();
    public DbSet<TenantEntitlement> TenantEntitlements => Set<TenantEntitlement>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<PlatformOperationJob> PlatformOperationJobs => Set<PlatformOperationJob>();
    public DbSet<PlatformSupportSession> PlatformSupportSessions => Set<PlatformSupportSession>();

    // CRM core
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<InternalNote> InternalNotes => Set<InternalNote>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    // Referrals & Campaigns
    public DbSet<ReferringDoctor> ReferringDoctors => Set<ReferringDoctor>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
    public DbSet<MarketingCalendarEvent> MarketingCalendarEvents => Set<MarketingCalendarEvent>();

    // EHR
    public DbSet<Allergy> Allergies => Set<Allergy>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Injection> Injections => Set<Injection>();
    public DbSet<VitalSigns> VitalSigns => Set<VitalSigns>();
    public DbSet<LabReport> LabReports => Set<LabReport>();
    public DbSet<DocumentScanEvidence> DocumentScanEvidence => Set<DocumentScanEvidence>();
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<PrivateDocumentVersion> PrivateDocumentVersions => Set<PrivateDocumentVersion>();
    public DbSet<ClinicalAuditEvent> ClinicalAuditEvents => Set<ClinicalAuditEvent>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<MedicalHistoryItem> MedicalHistory => Set<MedicalHistoryItem>();
    public DbSet<FamilyHistoryItem> FamilyHistory => Set<FamilyHistoryItem>();
    public DbSet<Visit> Visits => Set<Visit>();

    // Staff
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<ScheduleRule> ScheduleRules => Set<ScheduleRule>();
    public DbSet<ScheduleException> ScheduleExceptions => Set<ScheduleException>();
    public DbSet<AppointmentRescheduleWork> AppointmentRescheduleWorks => Set<AppointmentRescheduleWork>();

    // Lifestyle
    public DbSet<LifestyleProfile> LifestyleProfiles => Set<LifestyleProfile>();

    // System
    public DbSet<HospitalProfile> HospitalProfiles => Set<HospitalProfile>();
    public DbSet<QuickTemplate> Templates => Set<QuickTemplate>();
    public DbSet<TemplatePlaceholder> TemplatePlaceholders => Set<TemplatePlaceholder>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<WhatsAppSettings> WhatsAppSettings => Set<WhatsAppSettings>();
    public DbSet<SmsSettings> SmsSettings => Set<SmsSettings>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<IntegrationEvent> IntegrationEvents => Set<IntegrationEvent>();
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<NotificationReceipt> NotificationReceipts => Set<NotificationReceipt>();
    public DbSet<NotificationFeedCursor> NotificationFeedCursors => Set<NotificationFeedCursor>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<RoleNotificationDefault> RoleNotificationDefaults => Set<RoleNotificationDefault>();

    // WhatsApp API Integration
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<WhatsAppGroup> WhatsAppGroups => Set<WhatsAppGroup>();
    public DbSet<WhatsAppCampaign> WhatsAppCampaigns => Set<WhatsAppCampaign>();
    public DbSet<WhatsAppContact> WhatsAppContacts => Set<WhatsAppContact>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();

    // RBAC (global reference data + tenant-scoped assignments)
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        mb.Entity<Facility>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<User>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<Appointment>().Property(a=>a.BookingKind).HasMaxLength(20);
        mb.Entity<Appointment>().Property(a=>a.BookingPolicyVersion).HasMaxLength(100);
        mb.Entity<Appointment>().Property(a=>a.Revision).HasDefaultValue(1L);
        mb.Entity<Appointment>().HasOne<Facility>().WithMany().HasForeignKey(a=>new{a.TenantId,a.FacilityId}).HasPrincipalKey(a=>new{a.TenantId,a.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Appointment>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(a=>new{a.TenantId,a.ReservationId}).HasPrincipalKey(a=>new{a.TenantId,a.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Appointment>().HasOne<Appointment>().WithMany().HasForeignKey(a=>new{a.TenantId,a.FollowUpAppointmentId}).HasPrincipalKey(a=>new{a.TenantId,a.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Appointment>().HasOne<Patient>().WithMany().HasForeignKey(a=>new{a.TenantId,a.PatientId}).HasPrincipalKey(a=>new{a.TenantId,a.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Appointment>().HasIndex(a=>new{a.TenantId,a.ReservationId}).IsUnique().HasFilter("\"ReservationId\" IS NOT NULL");
        mb.Entity<Appointment>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<QueueEntry>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<QueuePriorityOverride>().HasIndex(p=>new{p.TenantId,p.QueueEntryId,p.Revision}).IsUnique();
        mb.Entity<QueuePriorityOverride>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.ArrivalDate});
        mb.Entity<QueuePriorityOverride>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<QueuePriorityOverride>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<QueuePriorityOverride>().HasOne<QueueEntry>().WithMany().HasForeignKey(p=>new{p.TenantId,p.QueueEntryId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueuePriorityOverride>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueuePriorityOverride>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueueStart>().HasIndex(p=>new{p.TenantId,p.QueueEntryId}).IsUnique();
        mb.Entity<QueueStart>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.ArrivalDate});
        mb.Entity<QueueStart>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<QueueStart>().HasOne<QueueEntry>().WithMany().HasForeignKey(p=>new{p.TenantId,p.QueueEntryId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueueStart>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueueStart>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);

        mb.Entity<QueueEntry>().HasIndex(p=>new{p.TenantId,p.AppointmentId}).IsUnique();
        mb.Entity<QueueEntry>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.ArrivalDate,p.Token}).IsUnique();
        mb.Entity<QueueEntry>().Property(p=>p.Timezone).HasMaxLength(100);
        mb.Entity<QueueEntry>().HasOne<Appointment>().WithMany().HasForeignKey(p=>new{p.TenantId,p.AppointmentId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueueEntry>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueueEntry>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentBookingChange>().HasIndex(c=>new{c.TenantId,c.AppointmentId,c.AppointmentRevision}).IsUnique();
        mb.Entity<AppointmentBookingChange>().HasOne<Appointment>().WithMany().HasForeignKey(c=>new{c.TenantId,c.AppointmentId}).HasPrincipalKey(c=>new{c.TenantId,c.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentBookingChange>().HasOne<Patient>().WithMany().HasForeignKey(c=>new{c.TenantId,c.PatientId}).HasPrincipalKey(c=>new{c.TenantId,c.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentBookingChange>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(c=>new{c.TenantId,c.PreviousReservationId}).HasPrincipalKey(c=>new{c.TenantId,c.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentBookingChange>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(c=>new{c.TenantId,c.ReplacementReservationId}).HasPrincipalKey(c=>new{c.TenantId,c.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentBookingChange>().Property(c=>c.Action).HasMaxLength(20);
        mb.Entity<AppointmentBookingChange>().Property(c=>c.Reason).HasMaxLength(1000);
        mb.Entity<AppointmentBookingChange>().Property(c=>c.PolicyVersion).HasMaxLength(100);
        mb.Entity<Appointment>().HasIndex(a=>new{a.TenantId,a.PatientId,a.ScheduledAt,a.Id});
        mb.Entity<Visit>().Property(v=>v.Revision).HasDefaultValue(1L);
        mb.Entity<Visit>().HasAlternateKey(v=>new{v.TenantId,v.Id});
        mb.Entity<Visit>().HasIndex(v=>new{v.TenantId,v.AppointmentId});
        mb.Entity<Appointment>().HasIndex(a=>new{a.TenantId,a.FacilityId,a.DoctorUserId,a.ScheduledAt,a.Id});
        mb.Entity<EncounterTransition>().HasIndex(v=>new{v.TenantId,v.VisitId,v.Revision}).IsUnique();
        mb.Entity<EncounterTransition>().HasIndex(v=>new{v.TenantId,v.PatientId,v.CreatedAt});
        mb.Entity<EncounterTransition>().Property(v=>v.Action).HasMaxLength(20);
        mb.Entity<EncounterTransition>().Property(v=>v.Reason).HasMaxLength(1000);
        mb.Entity<EncounterTransition>().Property(v=>v.PolicyVersion).HasMaxLength(100);
        mb.Entity<EncounterTransition>().Property(v=>v.ContentSnapshotJson).HasMaxLength(90000);
        mb.Entity<EncounterTransition>().HasOne<Visit>().WithMany().HasForeignKey(v=>new{v.TenantId,v.VisitId}).HasPrincipalKey(v=>new{v.TenantId,v.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EncounterTransition>().HasOne<Patient>().WithMany().HasForeignKey(v=>new{v.TenantId,v.PatientId}).HasPrincipalKey(v=>new{v.TenantId,v.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EncounterTransition>().HasOne<Appointment>().WithMany().HasForeignKey(v=>new{v.TenantId,v.AppointmentId}).HasPrincipalKey(v=>new{v.TenantId,v.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentNoShow>().HasIndex(p=>new{p.TenantId,p.AppointmentId}).IsUnique();
        mb.Entity<AppointmentNoShow>().HasIndex(p=>new{p.TenantId,p.FollowUpWorkId}).IsUnique();
        mb.Entity<AppointmentNoShow>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.RecordedAtUtc});
        mb.Entity<AppointmentNoShow>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<AppointmentNoShow>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<AppointmentNoShow>().HasOne<Appointment>().WithMany().HasForeignKey(p=>new{p.TenantId,p.AppointmentId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentNoShow>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentNoShow>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentNoShow>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(p=>new{p.TenantId,p.ReservationId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentNoShow>().HasOne<ClinicalFollowUpWork>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FollowUpWorkId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClinicalFollowUpWork>().HasAlternateKey(w=>new{w.TenantId,w.Id});
        mb.Entity<ClinicalFollowUpWork>().HasOne<Patient>().WithMany().HasForeignKey(w=>new{w.TenantId,w.PatientId}).HasPrincipalKey(w=>new{w.TenantId,w.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClinicalFollowUpWork>().HasOne<Appointment>().WithMany().HasForeignKey(w=>new{w.TenantId,w.SourceId}).HasPrincipalKey(w=>new{w.TenantId,w.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClinicalFollowUpWork>().HasOne<User>().WithMany().HasForeignKey(w=>new{w.TenantId,w.AssignedUserId}).HasPrincipalKey(w=>new{w.TenantId,w.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClinicalFollowUpWork>().HasOne<User>().WithMany().HasForeignKey(w=>new{w.TenantId,w.EscalationOwnerUserId}).HasPrincipalKey(w=>new{w.TenantId,w.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClinicalFollowUpWork>().HasIndex(w=>new{w.TenantId,w.EscalationOwnerUserId,w.State,w.DueAtUtc});
        mb.Entity<ClinicalFollowUpWork>().Property(w=>w.SourceType).HasMaxLength(30);
        mb.Entity<ClinicalFollowUpWork>().Property(w=>w.SourceVersion).HasMaxLength(30);
        mb.Entity<ClinicalFollowUpWork>().Property(w=>w.State).HasMaxLength(20);
        mb.Entity<ClinicalFollowUpVersion>().HasOne<ClinicalFollowUpWork>().WithMany().HasForeignKey(w=>new{w.TenantId,w.WorkId}).HasPrincipalKey(w=>new{w.TenantId,w.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClinicalFollowUpVersion>().HasOne<Patient>().WithMany().HasForeignKey(w=>new{w.TenantId,w.PatientId}).HasPrincipalKey(w=>new{w.TenantId,w.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ClinicalFollowUpVersion>().HasIndex(w=>new{w.TenantId,w.WorkId,w.Revision}).IsUnique();
        mb.Entity<ClinicalFollowUpVersion>().Property(w=>w.State).HasMaxLength(20);
        mb.Entity<ClinicalFollowUpVersion>().Property(w=>w.Action).HasMaxLength(30);
        mb.Entity<ClinicalFollowUpVersion>().Property(w=>w.Reason).HasMaxLength(1000);
        mb.Entity<AppointmentWaitlistEntry>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<AppointmentWaitlistOffer>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<AppointmentWaitlistEntry>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistEntry>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistEntry>().HasOne<User>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PractitionerUserId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistEntry>().HasOne<Appointment>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FollowUpAppointmentId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistEntry>().HasOne<AppointmentWaitlistOffer>().WithMany().HasForeignKey(p=>new{p.TenantId,p.CurrentOfferId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistEntry>().Property(p=>p.ResourceIdsJson).HasColumnType("text");
        mb.Entity<AppointmentWaitlistEntry>().Property(p=>p.BookingKind).HasMaxLength(20);
        mb.Entity<AppointmentWaitlistEntry>().Property(p=>p.State).HasMaxLength(20);
        mb.Entity<AppointmentWaitlistEntry>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<AppointmentWaitlistEntry>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.State,p.CreatedAt});
        mb.Entity<AppointmentWaitlistEntry>().HasIndex(p=>new{p.TenantId,p.PatientId,p.State,p.ExpiresAtUtc});
        mb.Entity<AppointmentWaitlistOffer>().HasOne<AppointmentWaitlistEntry>().WithMany().HasForeignKey(p=>new{p.TenantId,p.EntryId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistOffer>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistOffer>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(p=>new{p.TenantId,p.ReleasedReservationId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistOffer>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(p=>new{p.TenantId,p.ReservationId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistOffer>().HasOne<User>().WithMany().HasForeignKey(p=>new{p.TenantId,p.OwnerUserId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistOffer>().HasOne<Appointment>().WithMany().HasForeignKey(p=>new{p.TenantId,p.AppointmentId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistOffer>().HasIndex(p=>new{p.TenantId,p.ReservationId}).IsUnique();
        mb.Entity<AppointmentWaitlistOffer>().HasIndex(p=>new{p.TenantId,p.EntryId}).IsUnique().HasFilter("\"State\" = 'offered'");
        mb.Entity<AppointmentWaitlistOffer>().Property(p=>p.State).HasMaxLength(20);
        mb.Entity<AppointmentWaitlistOffer>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<AppointmentWaitlistDecision>().HasOne<AppointmentWaitlistEntry>().WithMany().HasForeignKey(p=>new{p.TenantId,p.EntryId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistDecision>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistDecision>().HasOne<AppointmentWaitlistOffer>().WithMany().HasForeignKey(p=>new{p.TenantId,p.OfferId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentWaitlistDecision>().HasIndex(p=>new{p.TenantId,p.EntryId,p.EntryRevision}).IsUnique();
        mb.Entity<AppointmentWaitlistDecision>().Property(p=>p.Action).HasMaxLength(30);
        mb.Entity<AppointmentWaitlistDecision>().Property(p=>p.Reason).HasMaxLength(1000);
        mb.Entity<QueueEntry>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(p=>new{p.TenantId,p.ReservationId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueueEntry>().HasOne<FacilityOperatingSetting>().WithMany().HasForeignKey(p=>p.OperatingSettingId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<QueueCounter>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.ArrivalDate}).IsUnique();
        mb.Entity<QueueCounter>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);

        mb.Entity<AppointmentReservation>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<AppointmentReservation>().HasOne<User>().WithMany().HasForeignKey(p=>new{p.TenantId,p.OwnerUserId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentReservation>().HasOne<User>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PractitionerUserId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentReservation>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentReservation>().HasOne<Appointment>().WithMany().HasForeignKey(p=>new{p.TenantId,p.AppointmentId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentReservation>().HasIndex(p=>new{p.TenantId,p.AppointmentId}).IsUnique().HasFilter("\"AppointmentId\" IS NOT NULL AND \"State\" = 'confirmed'");
        mb.Entity<AppointmentReservation>().HasIndex(p=>new{p.TenantId,p.OwnerUserId,p.State,p.HoldExpiresAtUtc});
        mb.Entity<AppointmentReservation>().Property(p=>p.State).HasMaxLength(20);
        mb.Entity<AppointmentReservation>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<AppointmentReservationSeat>().HasOne<AppointmentReservation>().WithMany().HasForeignKey(p=>new{p.TenantId,p.ReservationId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentReservationSeat>().HasIndex(p=>new{p.TenantId,p.ReservationId,p.TargetType,p.TargetId}).IsUnique();
        mb.Entity<AppointmentReservationSeat>().HasIndex(p=>new{p.TenantId,p.TargetType,p.TargetId,p.StartsAtUtc,p.EndsAtUtc});
        mb.Entity<AppointmentReservationSeat>().Property(p=>p.TargetType).HasMaxLength(20);
        mb.Entity<AppointmentReservationSeat>().Property(p=>p.State).HasMaxLength(20);
        mb.Entity<Lead>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<ReferringDoctor>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<LeadConversion>().HasIndex(p=>new{p.TenantId,p.LeadId}).IsUnique();
        mb.Entity<LeadConversion>().HasOne<Lead>().WithMany().HasForeignKey(p=>new{p.TenantId,p.LeadId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LeadConversion>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LeadConversion>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LeadConversion>().HasOne<ReferringDoctor>().WithMany().HasForeignKey(p=>new{p.TenantId,p.ReferringDoctorId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LeadConversion>().Property(p=>p.Decision).HasMaxLength(20);
        mb.Entity<LeadConversion>().Property(p=>p.SourceLeadTag).HasMaxLength(64);
        mb.Entity<LeadConversion>().Property(p=>p.CandidateReviewTag).HasMaxLength(64);
        mb.Entity<LeadConversion>().Property(p=>p.SelectedPatientTag).HasMaxLength(200);
        mb.Entity<LeadConversion>().Property(p=>p.EvidenceReference).HasMaxLength(200);
        mb.Entity<LeadConversion>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<LeadConversion>().Property(p=>p.ReferralSource).HasMaxLength(200);
        mb.Entity<LeadConversion>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<PatientExportJob>().HasIndex(p=>new{p.TenantId,p.RequesterId,p.CreatedAt});
        mb.Entity<PatientExportJob>().HasOne<User>().WithMany().HasForeignKey(p=>p.RequesterId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientExportJob>().Property(p=>p.State).HasMaxLength(20);
        mb.Entity<PatientExportJob>().Property(p=>p.Purpose).HasMaxLength(500);
        mb.Entity<PatientExportJob>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<PatientExportJob>().Property(p=>p.ObjectKey).HasMaxLength(500);
        mb.Entity<PatientExportJob>().Property(p=>p.Sha256).HasMaxLength(64);
        mb.Entity<PatientImportBatch>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<PatientImportBatch>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.FileSha256,p.MappingFingerprint}).IsUnique();
        mb.Entity<PatientImportBatch>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientImportBatch>().Property(p=>p.FileSha256).HasMaxLength(64);
        mb.Entity<PatientImportBatch>().Property(p=>p.MappingFingerprint).HasMaxLength(64);
        mb.Entity<PatientImportBatch>().Property(p=>p.Format).HasMaxLength(8);
        mb.Entity<PatientImportBatch>().Property(p=>p.SourceObjectKey).HasMaxLength(500);
        mb.Entity<PatientImportBatch>().Property(p=>p.ScanEngine).HasMaxLength(100);
        mb.Entity<PatientImportBatch>().Property(p=>p.ScanEngineVersion).HasMaxLength(200);
        mb.Entity<PatientImportBatch>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<PatientImportBatch>().Property(p=>p.ParseErrorCode).HasMaxLength(100);
        mb.Entity<PatientImportRow>().HasIndex(p=>new{p.TenantId,p.BatchId,p.RowNumber}).IsUnique();
        mb.Entity<PatientImportRow>().HasOne<PatientImportBatch>().WithMany().HasForeignKey(p=>new{p.TenantId,p.BatchId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientImportRow>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientImportRow>().Property(p=>p.SourceHash).HasMaxLength(64);
        mb.Entity<PatientImportRow>().Property(p=>p.Decision).HasMaxLength(20);
        mb.Entity<PatientImportRow>().Property(p=>p.ReviewReason).HasMaxLength(500);
        mb.Entity<PatientImportRow>().Property(p=>p.ReviewPolicyVersion).HasMaxLength(100);
        mb.Entity<PatientConfidentialityState>().HasIndex(p=>new{p.TenantId,p.PatientId}).IsUnique();
        mb.Entity<PatientConfidentialityState>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientAlertVersion>().HasIndex(p=>new{p.TenantId,p.PatientId,p.Category,p.Key,p.Revision}).IsUnique();
        mb.Entity<PatientAlertVersion>().HasIndex(p=>new{p.TenantId,p.AlertId,p.Revision}).IsUnique();
        mb.Entity<PatientAlertVersion>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientAlertVersion>().Property(p=>p.Key).HasMaxLength(80);
        mb.Entity<PatientAlertVersion>().Property(p=>p.Category).HasMaxLength(32);
        mb.Entity<PatientAlertVersion>().Property(p=>p.Title).HasMaxLength(120);
        mb.Entity<PatientAlertVersion>().Property(p=>p.Detail).HasMaxLength(1000);
        mb.Entity<PatientAlertVersion>().Property(p=>p.Severity).HasMaxLength(16);
        mb.Entity<PatientAlertVersion>().Property(p=>p.SourceType).HasMaxLength(32);
        mb.Entity<PatientAlertVersion>().Property(p=>p.SourceReference).HasMaxLength(200);
        mb.Entity<PatientAlertVersion>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<PatientAlertVersion>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<Patient>().Property(p=>p.IdentityStatus).HasMaxLength(24).HasDefaultValue("unreviewed");
        mb.Entity<TemporaryPatientIdentity>().HasIndex(p=>new{p.TenantId,p.PatientId}).IsUnique();
        mb.Entity<TemporaryPatientIdentity>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Value}).IsUnique();
        mb.Entity<TemporaryPatientIdentity>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<TemporaryPatientIdentity>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<TemporaryPatientIdentity>().Property(p=>p.Value).HasMaxLength(40);
        mb.Entity<TemporaryPatientIdentity>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<TemporaryPatientIdentity>().Property(p=>p.SourceReference).HasMaxLength(200);
        mb.Entity<PatientDemographicVersion>().HasAlternateKey(p=>new{p.TenantId,p.PatientId,p.Id});
        mb.Entity<PatientIdentityVerification>().HasIndex(p=>new{p.TenantId,p.PatientId,p.CreatedAt});
        mb.Entity<PatientIdentityVerification>().HasOne<PatientDemographicVersion>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId,p.DemographicVersionId}).HasPrincipalKey(p=>new{p.TenantId,p.PatientId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientIdentityVerification>().Property(p=>p.ProofReference).HasMaxLength(200);
        mb.Entity<PatientIdentityVerification>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<PatientIdentityVerification>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<PatientIdentityVerification>().Property(p=>p.PatientTag).HasMaxLength(128);
        mb.Entity<Patient>().Property(p=>p.BirthDatePrecision).HasMaxLength(24).HasDefaultValue("legacy_unspecified");
        mb.Entity<PatientDemographicVersion>().HasIndex(p=>new{p.TenantId,p.PatientId,p.Revision}).IsUnique();
        mb.Entity<PatientDemographicVersion>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientDemographicVersion>().Property(p=>p.SourceType).HasMaxLength(32);
        mb.Entity<PatientDemographicVersion>().Property(p=>p.SourceReference).HasMaxLength(200);
        mb.Entity<PatientDemographicVersion>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<PatientDemographicVersion>().Property(p=>p.VerificationStatus).HasMaxLength(24);
        mb.Entity<PatientMerge>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<PatientMerge>().HasIndex(p=>new{p.TenantId,p.SourcePatientId}).IsUnique().HasFilter("\"ReversedAt\" IS NULL");
        mb.Entity<PatientMerge>().HasIndex(p=>new{p.TenantId,p.SurvivorPatientId});
        mb.Entity<PatientMerge>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.SourcePatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientMerge>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.SurvivorPatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientIdentityReviewVersion>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<PatientMerge>().HasOne<PatientIdentityReviewVersion>().WithMany().HasForeignKey(p=>new{p.TenantId,p.ReviewVersionId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientMerge>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<PatientMerge>().Property(p=>p.PolicyVersion).HasMaxLength(100);
        mb.Entity<PatientMerge>().Property(p=>p.SourcePatientTag).HasMaxLength(128);
        mb.Entity<PatientMerge>().Property(p=>p.SurvivorPatientTag).HasMaxLength(128);
        mb.Entity<PatientMerge>().Property(p=>p.ReversalReason).HasMaxLength(500);
        mb.Entity<PatientLinkJournal>().HasIndex(p=>new{p.TenantId,p.MergeId,p.Operation}).IsUnique();
        mb.Entity<PatientLinkJournal>().HasOne<PatientMerge>().WithMany().HasForeignKey(p=>new{p.TenantId,p.MergeId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientLinkJournal>().Property(p=>p.Operation).HasMaxLength(16);
        mb.Entity<PatientLinkJournal>().Property(p=>p.EntityType).HasMaxLength(80);
        mb.Entity<PatientLinkJournal>().Property(p=>p.BeforeValue).HasMaxLength(1000);
        mb.Entity<PatientLinkJournal>().Property(p=>p.AfterValue).HasMaxLength(1000);
        mb.Entity<PatientIdentityReviewVersion>().HasIndex(p=>new{p.TenantId,p.FirstPatientId,p.SecondPatientId,p.Revision}).IsUnique();
        mb.Entity<PatientIdentityReviewVersion>().HasIndex(p=>new{p.TenantId,p.ReviewId,p.Revision}).IsUnique();
        mb.Entity<PatientIdentityReviewVersion>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FirstPatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientIdentityReviewVersion>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.SecondPatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientIdentityReviewVersion>().Property(p=>p.Decision).HasMaxLength(32);
        mb.Entity<PatientIdentityReviewVersion>().Property(p=>p.EvidenceReference).HasMaxLength(200);
        mb.Entity<PatientIdentityReviewVersion>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<PatientIdentityReviewVersion>().Property(p=>p.FirstPatientTag).HasMaxLength(128);
        mb.Entity<PatientIdentityReviewVersion>().Property(p=>p.SecondPatientTag).HasMaxLength(128);
        mb.Entity<PatientContactVersion>().HasIndex(p=>new{p.TenantId,p.PatientId,p.ContactId,p.Revision}).IsUnique();
        mb.Entity<PatientContactVersion>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientContactVersion>().Property(p=>p.ContactType).HasMaxLength(16);
        mb.Entity<PatientContactVersion>().Property(p=>p.Usage).HasMaxLength(16);
        mb.Entity<PatientContactVersion>().Property(p=>p.DisplayName).HasMaxLength(200);
        mb.Entity<PatientContactVersion>().Property(p=>p.Value).HasMaxLength(254);
        mb.Entity<PatientContactVersion>().Property(p=>p.Relationship).HasMaxLength(100);
        mb.Entity<PatientContactVersion>().Property(p=>p.SourceReference).HasMaxLength(200);
        mb.Entity<PatientContactVersion>().Property(p=>p.VerificationStatus).HasMaxLength(16);
        mb.Entity<Patient>().HasAlternateKey(p=>new{p.TenantId,p.Id});
        mb.Entity<PatientIdentifierCounter>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Namespace}).IsUnique();
        mb.Entity<PatientIdentifierCounter>().Property(p=>p.Namespace).HasMaxLength(64);
        mb.Entity<PatientIdentifierCounter>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientIdentifier>().Property(p=>p.Namespace).HasMaxLength(64);
        mb.Entity<PatientIdentifier>().Property(p=>p.Value).HasMaxLength(64);
        mb.Entity<PatientIdentifier>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Namespace,p.Value}).IsUnique();
        mb.Entity<PatientIdentifier>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Namespace,p.PatientId}).IsUnique();
        mb.Entity<PatientIdentifier>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientIdentifier>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<OrganizationStructureVersion>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.NodeCategory,p.NodeId,p.Revision}).IsUnique();
        mb.Entity<OrganizationStructureVersion>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.NodeCategory,p.NodeId,p.EffectiveFrom});
        mb.Entity<OrganizationStructureVersion>().Property(p=>p.NodeCategory).HasMaxLength(16);
        mb.Entity<OrganizationStructureVersion>().Property(p=>p.Name).HasMaxLength(200);
        mb.Entity<OrganizationStructureVersion>().Property(p=>p.Code).HasMaxLength(64);
        mb.Entity<OrganizationStructureVersion>().Property(p=>p.Kind).HasMaxLength(32);
        mb.Entity<OrganizationStructureVersion>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<FacilityResource>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Code}).IsUnique();
        mb.Entity<FacilityResource>().Property(p=>p.Name).HasMaxLength(200);
        mb.Entity<FacilityResource>().Property(p=>p.Code).HasMaxLength(64);
        mb.Entity<FacilityResource>().Property(p=>p.ResourceType).HasMaxLength(32);
        mb.Entity<FacilityResource>().Property(p=>p.OperationalStatus).HasMaxLength(32);
        mb.Entity<FacilityResource>().Property(p=>p.Revision).HasDefaultValue(1L);
        mb.Entity<FacilityResource>().HasOne<FacilityLocation>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId,p.LocationId}).HasPrincipalKey(p=>new{p.TenantId,p.FacilityId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<OrganizationUnit>().HasAlternateKey(p=>new{p.TenantId,p.FacilityId,p.Id});
        mb.Entity<OrganizationUnit>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Code}).IsUnique();
        mb.Entity<OrganizationUnit>().Property(p=>p.Name).HasMaxLength(200);
        mb.Entity<OrganizationUnit>().Property(p=>p.Code).HasMaxLength(64);
        mb.Entity<OrganizationUnit>().Property(p=>p.Kind).HasMaxLength(32);
        mb.Entity<OrganizationUnit>().Property(p=>p.Revision).HasDefaultValue(1L);
        mb.Entity<OrganizationUnit>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<OrganizationUnit>().HasOne<OrganizationUnit>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId,p.ParentId}).HasPrincipalKey(p=>new{p.TenantId,p.FacilityId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<FacilityLocation>().HasAlternateKey(p=>new{p.TenantId,p.FacilityId,p.Id});
        mb.Entity<FacilityLocation>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Code}).IsUnique();
        mb.Entity<FacilityLocation>().Property(p=>p.Name).HasMaxLength(200);
        mb.Entity<FacilityLocation>().Property(p=>p.Code).HasMaxLength(64);
        mb.Entity<FacilityLocation>().Property(p=>p.Kind).HasMaxLength(32);
        mb.Entity<FacilityLocation>().Property(p=>p.Revision).HasDefaultValue(1L);
        mb.Entity<FacilityLocation>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<FacilityLocation>().HasOne<FacilityLocation>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId,p.ParentId}).HasPrincipalKey(p=>new{p.TenantId,p.FacilityId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleException>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Id});
        mb.Entity<ScheduleException>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.StartsAtUtc,p.EndsAtUtc});
        mb.Entity<ScheduleException>().Property(p=>p.Timezone).HasMaxLength(100);
        mb.Entity<ScheduleException>().Property(p=>p.Kind).HasMaxLength(16);
        mb.Entity<ScheduleException>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<ScheduleException>().Property(p=>p.Revision).HasDefaultValue(1L);
        mb.Entity<ScheduleException>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleException>().HasOne<User>().WithMany().HasForeignKey(p=>p.PractitionerUserId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleException>().HasOne<FacilityResource>().WithMany().HasForeignKey(p=>p.ResourceId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleException>().HasOne<FacilityOperatingSetting>().WithMany().HasForeignKey(p=>p.OperatingSettingId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentRescheduleWork>().HasIndex(p=>new{p.TenantId,p.ExceptionId,p.AppointmentId}).IsUnique();
        mb.Entity<AppointmentRescheduleWork>().HasIndex(p=>new{p.TenantId,p.ExceptionId,p.Id});
        mb.Entity<AppointmentRescheduleWork>().Property(p=>p.State).HasMaxLength(32);
        mb.Entity<AppointmentRescheduleWork>().HasOne<ScheduleException>().WithMany().HasForeignKey(p=>p.ExceptionId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentRescheduleWork>().HasOne<Appointment>().WithMany().HasForeignKey(p=>p.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<AppointmentRescheduleWork>().HasOne<Patient>().WithMany().HasForeignKey(p=>new{p.TenantId,p.PatientId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleRule>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Id});
        mb.Entity<ScheduleRule>().HasIndex(p=>new{p.TenantId,p.PractitionerUserId,p.EffectiveFrom,p.EffectiveThrough});
        mb.Entity<ScheduleRule>().HasIndex(p=>new{p.TenantId,p.ResourceId,p.EffectiveFrom,p.EffectiveThrough});
        mb.Entity<ScheduleRule>().Property(p=>p.Timezone).HasMaxLength(100);
        mb.Entity<ScheduleRule>().Property(p=>p.AmbiguousTimePolicy).HasMaxLength(16);
        mb.Entity<ScheduleRule>().Property(p=>p.Revision).HasDefaultValue(1L);
        mb.Entity<ScheduleRule>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleRule>().HasOne<User>().WithMany().HasForeignKey(p=>p.PractitionerUserId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleRule>().HasOne<FacilityResource>().WithMany().HasForeignKey(p=>p.ResourceId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ScheduleRule>().HasOne<FacilityOperatingSetting>().WithMany().HasForeignKey(p=>p.OperatingSettingId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<FacilityOperatingSetting>().HasIndex(p=>new{p.TenantId,p.FacilityId,p.Revision}).IsUnique();
        mb.Entity<FacilityOperatingSetting>().HasOne<Facility>().WithMany().HasForeignKey(p=>new{p.TenantId,p.FacilityId}).HasPrincipalKey(p=>new{p.TenantId,p.Id}).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<FacilityOperatingSetting>().Property(p=>p.Timezone).HasMaxLength(100);
        mb.Entity<FacilityOperatingSetting>().Property(p=>p.Language).HasMaxLength(35);
        mb.Entity<FacilityOperatingSetting>().Property(p=>p.DateFormat).HasMaxLength(20);
        mb.Entity<FacilityOperatingSetting>().Property(p=>p.TimeFormat).HasMaxLength(20);
        mb.Entity<FacilityOperatingSetting>().Property(p=>p.Reason).HasMaxLength(500);
        mb.Entity<TenantProcessingPolicy>().HasIndex(p => new { p.TenantId, p.Provider, p.Purpose, p.Revision }).IsUnique();
        mb.Entity<TenantProcessingPolicy>().Property(p => p.Provider).HasMaxLength(64);
        mb.Entity<TenantProcessingPolicy>().Property(p => p.Purpose).HasMaxLength(64);
        mb.Entity<TenantProcessingPolicy>().Property(p => p.ActorKind).HasMaxLength(16);
        mb.Entity<TenantProcessingPolicy>().Property(p => p.ApprovalReference).HasMaxLength(128);
        mb.Entity<TenantProcessingPolicy>().Property(p => p.BasisReference).HasMaxLength(128);
        mb.Entity<OutboxEvent>().Property(p => p.Revision).HasDefaultValue(1L);
        mb.Entity<OutboxEvent>().HasIndex(p => new { p.TenantId, p.State, p.DueAt, p.LeaseUntil });
        mb.Entity<OutboxEvent>().Property(p => p.EventType).HasMaxLength(96);
        mb.Entity<OutboxEvent>().Property(p => p.AggregateType).HasMaxLength(64);
        mb.Entity<OutboxEvent>().Property(p => p.AggregateVersion).HasMaxLength(128);
        mb.Entity<OutboxEvent>().Property(p => p.State).HasMaxLength(32);
        mb.Entity<OutboxEvent>().Property(p => p.FailureCode).HasMaxLength(64);
        mb.Entity<OutboxConsumerReceipt>().HasIndex(p => new { p.TenantId, p.EventId, p.ConsumerName }).IsUnique();
        mb.Entity<OutboxConsumerReceipt>().Property(p => p.ConsumerName).HasMaxLength(48);
        mb.Entity<OutboxConsumerReceipt>().HasOne<OutboxEvent>().WithMany().HasForeignKey(p => new { p.TenantId, p.EventId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<IdempotencyRecord>().HasIndex(p => new { p.TenantId, p.ActorId, p.CommandNamespace, p.KeyHash }).IsUnique();
        mb.Entity<IdempotencyRecord>().Property(p => p.CommandNamespace).HasMaxLength(96);
        mb.Entity<IdempotencyRecord>().Property(p => p.KeyHash).HasMaxLength(64);
        mb.Entity<IdempotencyRecord>().Property(p => p.RequestHash).HasMaxLength(64);
        mb.Entity<IdempotencyRecord>().Property(p => p.State).HasMaxLength(32);
        mb.Entity<IdempotencyRecord>().Property(p => p.ResponseJson).HasMaxLength(1024);
        mb.Entity<User>().Property(e => e.SecurityRevision).HasDefaultValue(1);
        mb.Entity<RefreshSession>().HasIndex(e => new { e.FamilyId, e.UserId, e.SecurityRevision });
        mb.Entity<RefreshSession>().HasKey(e => e.TokenHash);
        mb.Entity<RefreshSession>().HasIndex(e => e.FamilyId);

        // Apply configurations from assembly
        var hasEntityConfigurations = typeof(ApplicationDbContext).Assembly
            .GetTypes()
            .Any(type => !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)));
        if (hasEntityConfigurations)
        {
            mb.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        // ===== GLOBAL FILTERS =====
        // Soft-delete + tenant scoping for every TenantEntity
        foreach (var et in mb.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(et.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplyTenantAndSoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(et.ClrType);
                method.Invoke(this, new object[] { mb });
            }
            else if (typeof(BaseEntity).IsAssignableFrom(et.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(et.ClrType);
                method.Invoke(this, new object[] { mb });
            }
        }

        // Indexes & uniques
        mb.Entity<Lead>().HasIndex(l => new { l.TenantId, l.Phone }).IsUnique().HasFilter("\"IsDeleted\" = false");
        mb.Entity<Lead>().Property(l => l.Phone).HasMaxLength(15);
        mb.Entity<Lead>().Property(l => l.Name).HasMaxLength(200);
        mb.Entity<Lead>().HasIndex(l => new { l.TenantId, l.ConvertedAt });
        mb.Entity<Patient>().HasIndex(p => p.SourceLeadId).IsUnique().HasFilter("\"SourceLeadId\" IS NOT NULL");
        mb.Entity<CampaignRecipient>().HasIndex(r => r.LeadId);
        mb.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        mb.Entity<Tenant>().HasIndex(t => t.LifecycleStatus);
        mb.Entity<Tenant>().Property(t => t.TenantType).HasDefaultValue(TenantType.Hospital);
        mb.Entity<ClinicalAuditEvent>().HasIndex(p => new { p.TenantId, p.CreatedAt });
        mb.Entity<ClinicalAuditEvent>().Property(p => p.ActorKind).HasMaxLength(32);
        mb.Entity<ClinicalAuditEvent>().Property(p => p.ServiceIdentity).HasMaxLength(64);
        mb.Entity<ClinicalAuditEvent>().Property(p => p.ResourceType).HasMaxLength(64);
        mb.Entity<ClinicalAuditEvent>().Property(p => p.Action).HasMaxLength(64);
        mb.Entity<ClinicalAuditEvent>().Property(p => p.Purpose).HasMaxLength(64);
        mb.Entity<ClinicalAuditEvent>().Property(p => p.Outcome).HasMaxLength(32);
        mb.Entity<ClinicalAuditEvent>().Property(p => p.CorrelationId).HasMaxLength(128);
        mb.Entity<PrivateDocumentVersion>().HasIndex(p => new { p.TenantId, p.ObjectKey }).IsUnique();
        mb.Entity<PrivateDocumentVersion>().Property(p => p.ObjectKey).HasMaxLength(65);
        mb.Entity<PrivateDocumentVersion>().Property(p => p.Sha256).HasMaxLength(64);
        mb.Entity<PrivateDocumentVersion>().Property(p => p.DocumentKind).HasMaxLength(32);
        mb.Entity<PrivateDocumentVersion>().Property(p => p.ContentType).HasMaxLength(128);
        mb.Entity<PatientDocument>().Property(p => p.ScanStatus).HasMaxLength(32).HasDefaultValue("unresolved");
        mb.Entity<PatientDocument>().HasIndex(p => new { p.TenantId, p.ScanStatus });
        mb.Entity<DocumentScanEvidence>().HasIndex(p => new { p.TenantId, p.DocumentId, p.Version }).IsUnique();
        mb.Entity<DocumentScanEvidence>().Property(p => p.Sha256).HasMaxLength(64);
        mb.Entity<DocumentScanEvidence>().Property(p => p.Verdict).HasMaxLength(32);
        mb.Entity<DocumentScanEvidence>().Property(p => p.Engine).HasMaxLength(64);
        mb.Entity<DocumentScanEvidence>().Property(p => p.EngineVersion).HasMaxLength(64);
        mb.Entity<DocumentScanEvidence>().Property(p => p.Code).HasMaxLength(64);
        mb.Entity<PatientDocument>().HasOne<DocumentScanEvidence>().WithMany().HasForeignKey(p => p.ScanEvidenceId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PatientDocument>().HasOne<PrivateDocumentVersion>().WithMany().HasForeignKey(p => p.StorageVersion).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LabReport>().HasOne<PrivateDocumentVersion>().WithMany().HasForeignKey(p => p.StorageVersion).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PlatformUser>().HasIndex(p => p.Email).IsUnique();
        mb.Entity<PlatformSession>().HasOne<PlatformUser>().WithMany().HasForeignKey(p => p.PlatformUserId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PlatformSession>().HasIndex(p => new { p.PlatformUserId, p.ExpiresAt });
        mb.Entity<PlatformSession>().Property(p => p.SecurityRevision).HasDefaultValue(1);
        mb.Entity<PlatformUser>().Property(p => p.Role).HasDefaultValue("owner");
        mb.Entity<PlatformRole>().HasIndex(p => p.Key).IsUnique();
        mb.Entity<PlanModule>().HasIndex(p => new { p.Plan, p.PlatformModuleId }).IsUnique();
        mb.Entity<PlanModule>().HasOne<PlatformModule>().WithMany().HasForeignKey(p => p.PlatformModuleId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<TenantApiActivity>().HasIndex(p => new { p.TenantId, p.CreatedAt });
        mb.Entity<TenantApiActivity>().HasIndex(p => new { p.StatusCode, p.CreatedAt });
        mb.Entity<PlatformModule>().HasIndex(p => p.Key).IsUnique();
        mb.Entity<PlatformCommand>().HasIndex(p => new { p.ActorId, p.RequestId }).IsUnique();
        mb.Entity<TenantEntitlement>().HasIndex(p => new { p.TenantId, p.PlatformModuleId }).IsUnique();
        mb.Entity<Facility>().HasIndex(p => p.TenantId).IsUnique().HasFilter("\"IsPrimary\" = true AND \"IsDeleted\" = false");
        mb.Entity<Facility>().HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<TenantEntitlement>().HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<TenantEntitlement>().HasOne<PlatformModule>().WithMany().HasForeignKey(p => p.PlatformModuleId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PlatformSupportSession>().HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PlatformSupportSession>().HasOne<PlatformUser>().WithMany().HasForeignKey(p => p.PlatformUserId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<TenantOnboardingState>().HasIndex(o => o.TenantId).IsUnique();
        mb.Entity<User>().HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        mb.Entity<Patient>().HasIndex(p => new { p.TenantId, p.Phone });
        mb.Entity<Patient>().HasIndex(p => new { p.TenantId, p.Status });
        mb.Entity<Patient>().HasIndex(p => new { p.TenantId, p.CreatedAt });
        mb.Entity<Conversation>().HasIndex(c => new { c.TenantId, c.WaPhone });
        mb.Entity<Conversation>().HasIndex(c => new { c.TenantId, c.PatientId });
        mb.Entity<Message>().HasIndex(m => m.ConversationId);
        mb.Entity<Message>().HasIndex(m => new { m.TenantId, m.ConversationId });
        mb.Entity<Message>().HasIndex(m => m.WaMessageId);
        mb.Entity<Appointment>().HasIndex(a => new { a.TenantId, a.ScheduledAt });
        mb.Entity<Appointment>().HasIndex(a => new { a.TenantId, a.PatientId });
        mb.Entity<Appointment>().HasIndex(a => new { a.TenantId, a.Status, a.ScheduledAt });
        mb.Entity<Visit>().HasIndex(v => new { v.TenantId, v.PatientId });
        mb.Entity<Visit>().HasIndex(v => new { v.TenantId, v.DoctorUserId, v.VisitDate });
        mb.Entity<Visit>().HasIndex(v => new { v.TenantId, v.Status, v.VisitDate });
        mb.Entity<Allergy>().HasIndex(a => new { a.TenantId, a.PatientId });
        mb.Entity<Prescription>().HasIndex(p => new { p.TenantId, p.PatientId });
        mb.Entity<ClinicalNote>().HasIndex(n => new { n.TenantId, n.PatientId });
        mb.Entity<VitalSigns>().HasIndex(v => new { v.TenantId, v.PatientId });
        mb.Entity<LifestyleProfile>().HasIndex(l => new { l.TenantId, l.PatientId }).IsUnique();
        mb.Entity<HospitalProfile>().HasIndex(h => h.TenantId).IsUnique();
        mb.Entity<StaffProfile>().HasIndex(s => new { s.TenantId, s.UserId }).IsUnique();
        mb.Entity<UserRoleAssignment>().HasIndex(a => new { a.TenantId, a.UserId });
        mb.Entity<Permission>().HasIndex(p => p.Code).IsUnique();
        mb.Entity<Role>().HasIndex(r => r.Name).IsUnique();
        mb.Entity<RolePermission>().HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
        mb.Entity<Campaign>().HasIndex(c => new { c.TenantId, c.Status, c.ScheduledAt });
        mb.Entity<MarketingCalendarEvent>().HasIndex(e => new { e.TenantId, e.EventDate });
        mb.Entity<TemplatePlaceholder>().HasIndex(p => new { p.TenantId, p.Key }).IsUnique();
        mb.Entity<NotificationEvent>().HasIndex(e => new { e.TenantId, e.CreatedAt });
        mb.Entity<NotificationEvent>().HasIndex(e => new { e.TenantId, e.Type });
        mb.Entity<NotificationEvent>()
            .HasIndex(e => new { e.TenantId, e.Type, e.DedupeKey })
            .IsUnique()
            .HasFilter(@"""DedupeKey"" IS NOT NULL");
        mb.Entity<NotificationReceipt>()
            .HasIndex(r => new { r.TenantId, r.UserId, r.NotificationEventId })
            .IsUnique()
            .HasFilter(@"""IsDeleted"" = false");
        mb.Entity<NotificationFeedCursor>()
            .HasIndex(c => new { c.TenantId, c.UserId })
            .IsUnique()
            .HasFilter(@"""IsDeleted"" = false");
        mb.Entity<NotificationPreference>()
            .HasIndex(p => new { p.TenantId, p.UserId, p.NotificationType, p.Channel })
            .IsUnique()
            .HasFilter(@"""IsDeleted"" = false");
        mb.Entity<RoleNotificationDefault>()
            .HasIndex(r => new { r.RoleId, r.NotificationType, r.Channel })
            .IsUnique();

        mb.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<UserRoleAssignment>()
            .HasOne(a => a.Role)
            .WithMany(r => r.UserAssignments)
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Permission>()
            .HasOne(p => p.Group)
            .WithMany(g => g.Permissions)
            .HasForeignKey(p => p.PermissionGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // List<string> & object JSON columns
        mb.Entity<Patient>().Property(p => p.Tags).HasColumnType("jsonb")
            .HasConversion(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                           v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());
        mb.Entity<Patient>().Property(p => p.Tags).Metadata.SetValueComparer(StringListComparer);
        mb.Entity<Conversation>().Property(c => c.Tags).HasColumnType("jsonb")
            .HasConversion(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                           v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());
        mb.Entity<Conversation>().Property(c => c.Tags).Metadata.SetValueComparer(StringListComparer);
        mb.Entity<ReferringDoctor>().Property(d => d.Tags).HasColumnType("jsonb")
            .HasConversion(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                           v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());
        mb.Entity<ReferringDoctor>().Property(d => d.Tags).Metadata.SetValueComparer(StringListComparer);
        mb.Entity<HospitalProfile>().Property(h => h.Phones).HasColumnType("jsonb")
            .HasConversion(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                           v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());
        mb.Entity<HospitalProfile>().Property(h => h.Phones).Metadata.SetValueComparer(StringListComparer);
        mb.Entity<HospitalProfile>().Property(h => h.Emails).HasColumnType("jsonb")
            .HasConversion(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                           v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());
        mb.Entity<HospitalProfile>().Property(h => h.Emails).Metadata.SetValueComparer(StringListComparer);

        // Prescription -> items (cascade)
        mb.Entity<PrescriptionItem>()
            .HasOne(i => i.Prescription)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Injection>()
            .HasOne(i => i.Prescription)
            .WithMany(p => p.Injections)
            .HasForeignKey(i => i.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<StaffProfile>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<DoctorSchedule>()
            .HasOne(s => s.StaffProfile)
            .WithMany()
            .HasForeignKey(s => s.StaffProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<WhatsAppContact>()
            .Property(c => c.ExternalGroupId)
            .HasMaxLength(64);

        mb.Entity<WhatsAppContact>()
            .HasOne(c => c.Group)
            .WithMany(g => g.Contacts)
            .HasForeignKey(c => c.WhatsAppGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder mb) where TEntity : TenantEntity
    {
        mb.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted && e.TenantId == _tenant.TenantId);
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder mb) where TEntity : BaseEntity
    {
        mb.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        PrepareForSave();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        PrepareForSave();
        return await base.SaveChangesAsync(ct);
    }

    private void PrepareForSave()
    {
        var now = DateTime.UtcNow;
        var userId = _tenant.UserId;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
                if (userId.HasValue)
                    entry.Entity.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                if (userId.HasValue)
                    entry.Entity.UpdatedBy = userId;
            }
        }

        if (_tenant.TenantId != Guid.Empty)
        {
            foreach (var entry in ChangeTracker.Entries<TenantEntity>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                    entry.Entity.TenantId = _tenant.TenantId;
            }
        }

        NormalizeDateTimesToUtc();
    }

    private void NormalizeDateTimesToUtc()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType == typeof(DateTime) && property.CurrentValue is DateTime dt)
                    property.CurrentValue = DateTimeHelper.EnsureUtc(dt);
                else if (property.Metadata.ClrType == typeof(DateTime?) && property.CurrentValue is DateTime ndt)
                    property.CurrentValue = DateTimeHelper.EnsureUtc(ndt);
            }
        }
    }
}





