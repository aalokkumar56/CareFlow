using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Persistence.Seeders;

/// <summary>
/// Idempotent E2E seed for three isolated hospitals (Phase 1 multi-tenant SaaS).
/// Each tenant gets: TenantOwner, HospitalProfile, 3 patients, 1 appointment,
/// 1 conversation + inbound message, and default templates (via RbacSeeder re-run).
///
/// E2E credentials (all hospitals share password):
///   Password: Test@12345
///   Hospital A — slug care-cure-althan, admin@care-cure-althan.e2e.cureflow.test
///   Hospital B — slug city-hospital-surat, admin@city-hospital-surat.e2e.cureflow.test
///   Hospital C — slug metro-clinic-pune, admin@metro-clinic-pune.e2e.cureflow.test
/// </summary>
public static class MultiHospitalE2eSeeder
{
    /// <summary>Shared deterministic password for Playwright / API E2E login.</summary>
    public const string DefaultPassword = "Test@12345";

    public static readonly IReadOnlyList<HospitalSeed> Hospitals =
    [
        new(
            "care-cure-althan",
            "Care & Cure Althan",
            "admin@care-cure-althan.e2e.cureflow.test",
            "Althan Exclusive Patient",
            ["Priya Mehta", "Ravi Shah"],
            "919100000001",
            ["General Medicine", "Cardiology", "Orthopedics"]),
        new(
            "city-hospital-surat",
            "City Hospital Surat",
            "admin@city-hospital-surat.e2e.cureflow.test",
            "Surat Exclusive Patient",
            ["Kiran Patel", "Anita Desai"],
            "919100000002",
            ["Orthopedics", "Dermatology", "ENT"]),
        new(
            "metro-clinic-pune",
            "Metro Clinic Pune",
            "admin@metro-clinic-pune.e2e.cureflow.test",
            "Pune Exclusive Patient",
            ["Suresh Kulkarni", "Neha Joshi"],
            "919100000003",
            ["Pediatrics", "General Medicine", "Gynecology"]),
    ];

    public static async Task SeedAsync(ICureFlowDbSession db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        foreach (var hospital in Hospitals)
            await SeedHospitalAsync(db, hasher, hospital, ct);
    }

    private static async Task SeedHospitalAsync(
        ICureFlowDbSession db,
        IPasswordHasher hasher,
        HospitalSeed hospital,
        CancellationToken ct)
    {
        var tenant = await db.QueryFirstOrDefaultAsync<Tenant>(
            """
            SELECT * FROM "Tenants"
            WHERE "Slug" = @slug AND "IsDeleted" = false
            LIMIT 1
            """,
            new { slug = hospital.Slug },
            ignoreTenant: true,
            ct: ct);

        if (tenant == null)
        {
            tenant = new Tenant
            {
                Slug = hospital.Slug,
                Name = hospital.Name,
                ContactEmail = hospital.AdminEmail,
                Plan = SubscriptionPlan.Trial,
                SubscriptionStatus = SubscriptionStatus.Trialing,
                TrialEndsAt = DateTime.UtcNow.AddDays(30),
            };
            await db.InsertAsync(tenant, ignoreTenant: true, ct: ct);
        }

        tenant.LifecycleStatus = TenantLifecycleStatus.Active;
        tenant.OnboardingComplete = true;
        tenant.IsActive = true;
        tenant.ApprovedAt ??= DateTime.UtcNow;
        await db.UpdateAsync(tenant, ignoreTenant: true, ct: ct);

        var onboarding = await db.QueryFirstOrDefaultAsync<TenantOnboardingState>(
            """
            SELECT * FROM "TenantOnboardingStates"
            WHERE "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId = tenant.Id },
            ignoreTenant: true,
            ct: ct);
        if (onboarding == null)
        {
            await db.InsertAsync(new TenantOnboardingState
            {
                TenantId = tenant.Id,
                ProfileComplete = true,
                WhatsAppConnected = true,
                TeamInvited = true,
                CompletedAt = DateTime.UtcNow,
            }, ignoreTenant: true, ct: ct);
        }

        var admin = await db.QueryFirstOrDefaultAsync<User>(
            """
            SELECT * FROM "Users"
            WHERE "TenantId" = @tenantId AND LOWER("Email") = @email AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId = tenant.Id, email = hospital.AdminEmail.ToLower() },
            ignoreTenant: true,
            ct: ct);

        if (admin == null)
        {
            admin = new User
            {
                TenantId = tenant.Id,
                Name = $"{hospital.Name} Admin",
                Email = hospital.AdminEmail,
                Role = UserRole.TenantOwner,
                PasswordHash = hasher.Hash(DefaultPassword),
            };
            await db.InsertAsync(admin, ignoreTenant: true, ct: ct);
        }

        await EnsureUserRoleAssignmentAsync(db, admin, ct);

        var hasProfile = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "HospitalProfiles"
            WHERE "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId = tenant.Id },
            ignoreTenant: true,
            ct: ct) == 1;

        if (!hasProfile)
        {
            var deptJson = System.Text.Json.JsonSerializer.Serialize(hospital.Departments);
            await db.InsertAsync(new HospitalProfile
            {
                TenantId = tenant.Id,
                Name = hospital.Name,
                Tagline = $"{hospital.Name} — multi-hospital E2E",
                WorkingHours = "OPD: 9am-8pm · Emergency: 24x7",
                Emergency24x7 = true,
                DepartmentsJson = deptJson,
            }, ignoreTenant: true, ct: ct);
        }

        var exclusivePatient = await EnsurePatientAsync(
            db, tenant.Id, hospital.ExclusivePatientName, hospital.PrimaryPhone, hospital.Departments[0], ct);

        var phoneOffset = 10;
        foreach (var extraName in hospital.ExtraPatientNames)
        {
            var phone = $"{hospital.PrimaryPhone[..^2]}{phoneOffset:D2}";
            phoneOffset += 10;
            await EnsurePatientAsync(db, tenant.Id, extraName, phone, hospital.Departments[1], ct);
        }

        await EnsureEhrSeedAsync(db, tenant.Id, exclusivePatient, admin, hospital, ct);
        await EnsureWhatsAppSettingsAsync(db, tenant.Id, hospital, ct);

        var conversation = await EnsureConversationAsync(db, tenant.Id, exclusivePatient, hospital, ct);
        await EnsureInboundMessageAsync(db, tenant.Id, conversation, exclusivePatient, hospital, ct);
        await EnsureAppointmentAsync(db, tenant.Id, exclusivePatient, admin, hospital, ct);
    }

    private static async Task EnsureEhrSeedAsync(
        ICureFlowDbSession db,
        Guid tenantId,
        Patient patient,
        User admin,
        HospitalSeed hospital,
        CancellationToken ct)
    {
        var hasAllergy = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "Allergies"
            WHERE "TenantId" = @tenantId AND "PatientId" = @patientId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, patientId = patient.Id },
            ignoreTenant: true,
            ct: ct) == 1;

        if (!hasAllergy)
        {
            await db.InsertAsync(new Allergy
            {
                TenantId = tenantId,
                PatientId = patient.Id,
                Type = AllergyType.Drug,
                Allergen = "Penicillin",
                Severity = AllergySeverity.Moderate,
                Reaction = "Rash",
                Notes = $"E2E allergy for {hospital.Slug}",
                RecordedByUserId = admin.Id,
                RecordedByName = admin.Name,
            }, ignoreTenant: true, ct: ct);
        }

        var hasVitals = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "VitalSigns"
            WHERE "TenantId" = @tenantId AND "PatientId" = @patientId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, patientId = patient.Id },
            ignoreTenant: true,
            ct: ct) == 1;

        if (!hasVitals)
        {
            await db.InsertAsync(new VitalSigns
            {
                TenantId = tenantId,
                PatientId = patient.Id,
                RecordedByUserId = admin.Id,
                RecordedByName = admin.Name,
                MeasuredAt = DateTime.UtcNow.AddDays(-1),
                SystolicBp = 120,
                DiastolicBp = 80,
                HeartRate = 72,
                Temperature = 36.8m,
                Notes = $"E2E vitals for {hospital.Slug}",
            }, ignoreTenant: true, ct: ct);
        }

        var prescriptionId = await db.QueryFirstOrDefaultAsync<Guid>(
            """
            SELECT "Id" FROM "Prescriptions"
            WHERE "TenantId" = @tenantId AND "PatientId" = @patientId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, patientId = patient.Id },
            ignoreTenant: true,
            ct: ct);

        if (prescriptionId == Guid.Empty)
        {
            var prescription = new Prescription
            {
                TenantId = tenantId,
                PatientId = patient.Id,
                DoctorUserId = admin.Id,
                DoctorName = admin.Name,
                Diagnosis = "Hypertension follow-up",
                ChiefComplaint = "Routine check",
                ClinicalNotes = $"E2E prescription for {hospital.Slug}",
            };
            await db.InsertAsync(prescription, ignoreTenant: true, ct: ct);

            await db.InsertAsync(new PrescriptionItem
            {
                TenantId = tenantId,
                PrescriptionId = prescription.Id,
                DrugName = "Amlodipine",
                Strength = "5mg",
                Form = "tablet",
                Dosage = "1-0-0",
                Frequency = "once daily",
                Duration = "30 days",
                ReasonForPrescribing = "Blood pressure control",
            }, ignoreTenant: true, ct: ct);
        }

        var hasNote = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "ClinicalNotes"
            WHERE "TenantId" = @tenantId AND "PatientId" = @patientId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, patientId = patient.Id },
            ignoreTenant: true,
            ct: ct) == 1;

        if (!hasNote)
        {
            await db.InsertAsync(new ClinicalNote
            {
                TenantId = tenantId,
                PatientId = patient.Id,
                AuthorUserId = admin.Id,
                AuthorName = admin.Name,
                NoteType = "progress",
                Subjective = "Patient reports feeling well",
                Objective = "Vitals stable",
                Assessment = "Controlled hypertension",
                Plan = "Continue current medications",
            }, ignoreTenant: true, ct: ct);
        }
    }

    private static async Task EnsureWhatsAppSettingsAsync(
        ICureFlowDbSession db,
        Guid tenantId,
        HospitalSeed hospital,
        CancellationToken ct)
    {
        var phoneNumberId = $"e2e-{hospital.Slug}";
        var exists = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "WhatsAppSettings"
            WHERE "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId },
            ignoreTenant: true,
            ct: ct) == 1;

        if (exists)
        {
            await db.ExecuteAsync(
                """
                UPDATE "WhatsAppSettings"
                SET "PhoneNumberId" = @phoneNumberId, "UpdatedAt" = NOW()
                WHERE "TenantId" = @tenantId AND "IsDeleted" = false
                  AND ("PhoneNumberId" IS NULL OR "PhoneNumberId" IS DISTINCT FROM @phoneNumberId)
                """,
                new { tenantId, phoneNumberId },
                ignoreTenant: true,
                ct: ct);
            return;
        }

        await db.InsertAsync(new WhatsAppSettings
        {
            TenantId = tenantId,
            Provider = "MetaCloud",
            PhoneNumberId = phoneNumberId,
            BusinessName = hospital.Name,
            Enabled = true,
        }, ignoreTenant: true, ct: ct);
    }

    private static async Task<Patient> EnsurePatientAsync(
        ICureFlowDbSession db,
        Guid tenantId,
        string name,
        string phone,
        string department,
        CancellationToken ct)
    {
        var patient = await db.QueryFirstOrDefaultAsync<Patient>(
            """
            SELECT * FROM "Patients"
            WHERE "TenantId" = @tenantId AND "Name" = @name AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, name },
            ignoreTenant: true,
            ct: ct);

        if (patient != null)
            return patient;

        patient = new Patient
        {
            TenantId = tenantId,
            Name = name,
            Phone = phone,
            Department = department,
            InquirySource = "e2e-seed",
            Status = LeadStatus.NewInquiry,
            Age = 35,
        };
        await db.InsertAsync(patient, ignoreTenant: true, ct: ct);
        return patient;
    }

    private static async Task<Conversation> EnsureConversationAsync(
        ICureFlowDbSession db,
        Guid tenantId,
        Patient patient,
        HospitalSeed hospital,
        CancellationToken ct)
    {
        var conversation = await db.QueryFirstOrDefaultAsync<Conversation>(
            """
            SELECT * FROM "Conversations"
            WHERE "TenantId" = @tenantId AND "PatientId" = @patientId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, patientId = patient.Id },
            ignoreTenant: true,
            ct: ct);

        if (conversation != null)
            return conversation;

        conversation = new Conversation
        {
            TenantId = tenantId,
            PatientId = patient.Id,
            WaPhone = patient.Phone ?? hospital.PrimaryPhone,
            Name = patient.Name,
            LastMessagePreview = $"E2E seed message for {hospital.Slug}",
            LastMessageAt = DateTime.UtcNow,
        };
        await db.InsertAsync(conversation, ignoreTenant: true, ct: ct);
        return conversation;
    }

    private static async Task EnsureInboundMessageAsync(
        ICureFlowDbSession db,
        Guid tenantId,
        Conversation conversation,
        Patient patient,
        HospitalSeed hospital,
        CancellationToken ct)
    {
        var hasMessage = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "Messages"
            WHERE "TenantId" = @tenantId AND "ConversationId" = @conversationId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, conversationId = conversation.Id },
            ignoreTenant: true,
            ct: ct) == 1;

        if (hasMessage)
            return;

        await db.InsertAsync(new Message
        {
            TenantId = tenantId,
            ConversationId = conversation.Id,
            PatientId = patient.Id,
            Direction = MessageDirection.Inbound,
            Type = "text",
            Body = $"Hello from {hospital.Slug} E2E patient",
            Status = MessageStatus.Received,
        }, ignoreTenant: true, ct: ct);
    }

    private static async Task EnsureAppointmentAsync(
        ICureFlowDbSession db,
        Guid tenantId,
        Patient patient,
        User admin,
        HospitalSeed hospital,
        CancellationToken ct)
    {
        var hasAppointment = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "Appointments"
            WHERE "TenantId" = @tenantId AND "PatientId" = @patientId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId, patientId = patient.Id },
            ignoreTenant: true,
            ct: ct) == 1;

        if (hasAppointment)
            return;

        await db.InsertAsync(new Appointment
        {
            TenantId = tenantId,
            PatientId = patient.Id,
            PatientName = patient.Name,
            PatientPhone = patient.Phone,
            DoctorUserId = admin.Id,
            DoctorName = admin.Name,
            Department = hospital.Departments[0],
            ScheduledAt = DateTime.UtcNow.AddDays(2),
            Status = AppointmentStatus.Scheduled,
            Notes = $"E2E appointment for {hospital.Slug}",
        }, ignoreTenant: true, ct: ct);
    }

    private static async Task EnsureUserRoleAssignmentAsync(ICureFlowDbSession db, User user, CancellationToken ct)
    {
        var exists = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "UserRoleAssignments"
            WHERE "UserId" = @userId AND "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { userId = user.Id, tenantId = user.TenantId },
            ignoreTenant: true,
            ct: ct) == 1;
        if (exists) return;

        var roleId = await db.QueryFirstOrDefaultAsync<Guid>(
            """
            SELECT "Id" FROM "Roles"
            WHERE "Name" = @roleName AND "IsDeleted" = false
            LIMIT 1
            """,
            new { roleName = CureFlowPermissions.MapLegacyRole(user.Role) },
            ignoreTenant: true,
            ct: ct);
        if (roleId == Guid.Empty) return;

        await db.InsertAsync(new UserRoleAssignment
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            RoleId = roleId,
        }, ignoreTenant: true, ct: ct);
    }

    public sealed record HospitalSeed(
        string Slug,
        string Name,
        string AdminEmail,
        string ExclusivePatientName,
        string[] ExtraPatientNames,
        string PrimaryPhone,
        string[] Departments);
}
