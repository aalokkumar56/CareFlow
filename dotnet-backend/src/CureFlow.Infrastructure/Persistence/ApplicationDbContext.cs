using CureFlow.Application.Common;
using CureFlow.Domain.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Entities.Lifestyle;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Entities.Saas;
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
    public DbSet<User> Users => Set<User>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<TenantOnboardingState> TenantOnboardingStates => Set<TenantOnboardingState>();
    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();

    // CRM core
    public DbSet<Patient> Patients => Set<Patient>();
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
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<MedicalHistoryItem> MedicalHistory => Set<MedicalHistoryItem>();
    public DbSet<FamilyHistoryItem> FamilyHistory => Set<FamilyHistoryItem>();
    public DbSet<Visit> Visits => Set<Visit>();

    // Staff
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();

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
        mb.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        mb.Entity<Tenant>().HasIndex(t => t.LifecycleStatus);
        mb.Entity<PlatformUser>().HasIndex(p => p.Email).IsUnique();
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
