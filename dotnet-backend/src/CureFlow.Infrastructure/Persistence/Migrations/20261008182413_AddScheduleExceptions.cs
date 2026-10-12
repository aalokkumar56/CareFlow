using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PractitionerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperatingSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    DisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleExceptions_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExceptions_FacilityOperatingSettings_OperatingSetti~",
                        column: x => x.OperatingSettingId,
                        principalTable: "FacilityOperatingSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExceptions_FacilityResources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "FacilityResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExceptions_Users_PractitionerUserId",
                        column: x => x.PractitionerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentRescheduleWorks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExceptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OriginalDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    FacilityAssociationNeedsReview = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentRescheduleWorks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentRescheduleWorks_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentRescheduleWorks_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentRescheduleWorks_ScheduleExceptions_ExceptionId",
                        column: x => x.ExceptionId,
                        principalTable: "ScheduleExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRescheduleWorks_AppointmentId",
                table: "AppointmentRescheduleWorks",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRescheduleWorks_ExceptionId",
                table: "AppointmentRescheduleWorks",
                column: "ExceptionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRescheduleWorks_TenantId_ExceptionId_Appointment~",
                table: "AppointmentRescheduleWorks",
                columns: new[] { "TenantId", "ExceptionId", "AppointmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRescheduleWorks_TenantId_ExceptionId_Id",
                table: "AppointmentRescheduleWorks",
                columns: new[] { "TenantId", "ExceptionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRescheduleWorks_TenantId_PatientId",
                table: "AppointmentRescheduleWorks",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExceptions_OperatingSettingId",
                table: "ScheduleExceptions",
                column: "OperatingSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExceptions_PractitionerUserId",
                table: "ScheduleExceptions",
                column: "PractitionerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExceptions_ResourceId",
                table: "ScheduleExceptions",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExceptions_TenantId_FacilityId_Id",
                table: "ScheduleExceptions",
                columns: new[] { "TenantId", "FacilityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExceptions_TenantId_FacilityId_StartsAtUtc_EndsAtUtc",
                table: "ScheduleExceptions",
                columns: new[] { "TenantId", "FacilityId", "StartsAtUtc", "EndsAtUtc" });
            migrationBuilder.Sql("""
                ALTER TABLE "ScheduleExceptions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "ScheduleExceptions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY schedule_exception_tenant ON "ScheduleExceptions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                ALTER TABLE "ScheduleExceptions" ADD CONSTRAINT schedule_exception_bounds CHECK (
                 "Revision">0 AND NOT "IsDeleted" AND "Kind" IN ('leave','holiday','block')
                 AND ("Kind"<>'leave' OR "PractitionerUserId" IS NOT NULL)
                 AND ("Kind"<>'holiday' OR ("PractitionerUserId" IS NULL AND "ResourceId" IS NULL))
                 AND "EndsAtUtc">"StartsAtUtc" AND "EndsAtUtc"-"StartsAtUtc"<=interval '31 days'
                 AND length(btrim("Reason")) BETWEEN 3 AND 500
                 AND (("IsActive" AND "DisabledAt" IS NULL) OR (NOT "IsActive" AND "DisabledAt" IS NOT NULL)));
                CREATE FUNCTION enforce_schedule_exception() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Schedule exceptions are retained' USING ERRCODE='23514'; END IF;
                 PERFORM pg_advisory_xact_lock(734291);
                 IF TG_OP='UPDATE' THEN
                  IF (to_jsonb(NEW)-ARRAY['IsActive','Revision','DisabledAt','UpdatedAt','UpdatedBy']) IS DISTINCT FROM
                     (to_jsonb(OLD)-ARRAY['IsActive','Revision','DisabledAt','UpdatedAt','UpdatedBy'])
                   OR NOT OLD."IsActive" OR NEW."IsActive" OR NEW."Revision"<>OLD."Revision"+1 OR NEW."DisabledAt" IS NULL THEN
                   RAISE EXCEPTION 'Only a versioned retained exception disable is permitted' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                 END IF;
                 IF NEW."Revision"<>1 OR NOT NEW."IsActive" OR NEW."DisabledAt" IS NOT NULL
                   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL THEN
                  RAISE EXCEPTION 'Active same-tenant facility and initial exception version required' USING ERRCODE='23514';
                 END IF;
                 PERFORM "Id" FROM "FacilityOperatingSettings" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId"
                  AND "Id"=NEW."OperatingSettingId" AND "Timezone"=NEW."Timezone"
                  AND "Revision"=(SELECT max("Revision") FROM "FacilityOperatingSettings" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId");
                 IF NOT FOUND THEN RAISE EXCEPTION 'Current explicit facility timezone required' USING ERRCODE='23514'; END IF;
                 IF NEW."PractitionerUserId" IS NOT NULL THEN
                  PERFORM u."Id" FROM "Users" u JOIN "StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
                   WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."PractitionerUserId" AND u."Role" IN (1,4,6)
                   AND NOT u."IsDeleted" AND NOT p."IsDeleted" FOR SHARE OF u,p;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Same-tenant practitioner required' USING ERRCODE='23514'; END IF;
                 END IF;
                 IF NEW."ResourceId" IS NOT NULL THEN
                  PERFORM "Id" FROM "FacilityResources" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Id"=NEW."ResourceId" AND NOT "IsDeleted" FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Same-facility resource required' USING ERRCODE='23514'; END IF;
                 END IF;
                 RETURN NEW;
                END $body$;
                CREATE TRIGGER schedule_exception_guard BEFORE INSERT OR UPDATE OR DELETE ON "ScheduleExceptions" FOR EACH ROW EXECUTE FUNCTION enforce_schedule_exception();
                ALTER TABLE "AppointmentRescheduleWorks" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "AppointmentRescheduleWorks" FORCE ROW LEVEL SECURITY;
                CREATE POLICY reschedule_work_tenant ON "AppointmentRescheduleWorks" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
                 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
                CREATE POLICY reschedule_work_patient_scope ON "AppointmentRescheduleWorks" AS RESTRICTIVE FOR SELECT
                 USING (public.cureflow_patient_read_allowed("TenantId","PatientId"));
                CREATE FUNCTION enforce_reschedule_work() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE exception_row "ScheduleExceptions"; appointment_row "Appointments";
                BEGIN
                 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Original potential booking impact is immutable' USING ERRCODE='23514'; END IF;
                 PERFORM pg_advisory_xact_lock(734291);
                 IF NEW."IsDeleted" OR NOT NEW."IsActive" OR NEW."State"<>'needs_review' OR NOT NEW."FacilityAssociationNeedsReview" THEN
                  RAISE EXCEPTION 'Invalid initial booking impact evidence' USING ERRCODE='23514'; END IF;
                 SELECT * INTO exception_row FROM "ScheduleExceptions" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."ExceptionId" AND "IsActive";
                 IF NOT FOUND THEN RAISE EXCEPTION 'Current same-tenant exception required' USING ERRCODE='23514'; END IF;
                 SELECT * INTO appointment_row FROM "Appointments" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."AppointmentId"
                  AND "IsActive" AND NOT "IsDeleted" AND "Status" IN (0,1) FOR SHARE;
                 IF NOT FOUND OR appointment_row."PatientId"<>NEW."PatientId" OR appointment_row."ScheduledAt"<>NEW."OriginalScheduledAtUtc"
                  OR appointment_row."DurationMinutes"<>NEW."OriginalDurationMinutes" OR NEW."OriginalDurationMinutes" NOT BETWEEN 1 AND 1440
                  OR (exception_row."PractitionerUserId" IS NOT NULL AND appointment_row."DoctorUserId" IS DISTINCT FROM exception_row."PractitionerUserId")
                  OR (exception_row."ResourceId" IS NOT NULL AND exception_row."PractitionerUserId" IS NULL)
                  OR appointment_row."ScheduledAt">=exception_row."EndsAtUtc"
                  OR appointment_row."ScheduledAt"+make_interval(mins=>appointment_row."DurationMinutes")<=exception_row."StartsAtUtc" THEN
                  RAISE EXCEPTION 'Impact must retain the exact overlapping source booking' USING ERRCODE='23514';
                 END IF;
                 RETURN NEW;
                END $body$;
                CREATE TRIGGER reschedule_work_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentRescheduleWorks" FOR EACH ROW EXECUTE FUNCTION enforce_reschedule_work();
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                 GRANT SELECT,INSERT ON "ScheduleExceptions","AppointmentRescheduleWorks" TO cureflow_tenant;
                 REVOKE UPDATE,DELETE ON "ScheduleExceptions","AppointmentRescheduleWorks" FROM cureflow_tenant;
                 GRANT UPDATE ("IsActive","Revision","DisabledAt","UpdatedAt","UpdatedBy") ON "ScheduleExceptions" TO cureflow_tenant;
                END IF; END $grant$;
                
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Use a reviewed forward migration to preserve schedule exceptions and booking impact evidence.");
        }
    }
}
