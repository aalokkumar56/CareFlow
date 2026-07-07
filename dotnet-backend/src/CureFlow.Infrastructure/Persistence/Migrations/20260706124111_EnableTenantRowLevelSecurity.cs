using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class EnableTenantRowLevelSecurity : Migration
{
    private static readonly string[] TenantTables =
    [
        "Users", "Patients", "Conversations", "Messages", "InternalNotes", "Appointments", "Tasks",
        "ReferringDoctors", "Referrals", "Campaigns", "CampaignRecipients", "MarketingCalendarEvents",
        "Allergies", "Prescriptions", "PrescriptionItems", "Injections", "VitalSigns", "LabReports",
        "ClinicalNotes", "MedicalHistory", "FamilyHistory", "Visits", "StaffProfiles", "DoctorSchedules",
        "LifestyleProfiles", "HospitalProfiles", "Templates", "TemplatePlaceholders", "AuditLogs",
        "WhatsAppSettings", "SmsSettings", "EmailSettings", "EmailMessages", "IntegrationEvents",
        "UserRoleAssignments", "NotificationEvents", "NotificationReceipts", "NotificationFeedCursors",
        "NotificationPreferences",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON "{table}";

                CREATE POLICY tenant_isolation ON "{table}"
                  USING (
                    current_setting('app.platform_admin', true) = 'true'
                    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                  )
                  WITH CHECK (
                    current_setting('app.platform_admin', true) = 'true'
                    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                  );
                """);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantTables)
        {
            migrationBuilder.Sql($"""
                DROP POLICY IF EXISTS tenant_isolation ON "{table}";
                ALTER TABLE "{table}" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "{table}" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
