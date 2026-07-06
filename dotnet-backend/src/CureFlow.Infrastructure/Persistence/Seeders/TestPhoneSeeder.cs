using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Persistence.Seeders;

/// <summary>
/// Points all CRM phone fields at the shared WhatsApp test handset so inbound
/// webhooks from 7600174070 match patients and referral doctors.
/// </summary>
public static class TestPhoneSeeder
{
    public static async Task SeedAsync(ICureFlowDbSession db, ILogger logger, CancellationToken ct = default)
    {
        var phone = WhatsappPhoneHelper.TestPhoneNormalized;

        var patients = await db.ExecuteAsync(
            """
            UPDATE "Patients"
            SET "Phone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false AND "Phone" IS DISTINCT FROM @phone
            """,
            new { phone },
            ignoreTenant: true,
            ct: ct);

        var doctors = await db.ExecuteAsync(
            """
            UPDATE "ReferringDoctors"
            SET "Phone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false
              AND ("Phone" IS NULL OR "Phone" IS DISTINCT FROM @phone)
            """,
            new { phone },
            ignoreTenant: true,
            ct: ct);

        var users = await db.ExecuteAsync(
            """
            UPDATE "Users"
            SET "Phone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false
              AND ("Phone" IS NULL OR "Phone" IS DISTINCT FROM @phone)
            """,
            new { phone },
            ignoreTenant: true,
            ct: ct);

        var appointments = await db.ExecuteAsync(
            """
            UPDATE "Appointments"
            SET "PatientPhone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false AND "PatientPhone" IS DISTINCT FROM @phone
            """,
            new { phone },
            ignoreTenant: true,
            ct: ct);

        var conversations = await db.ExecuteAsync(
            """
            UPDATE "Conversations"
            SET "WaPhone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false AND "WaPhone" IS DISTINCT FROM @phone
            """,
            new { phone },
            ignoreTenant: true,
            ct: ct);

        logger.LogInformation(
            "Test phone {Phone} applied — patients:{Patients} doctors:{Doctors} users:{Users} appointments:{Appointments} conversations:{Conversations}",
            phone, patients, doctors, users, appointments, conversations);
    }
}
