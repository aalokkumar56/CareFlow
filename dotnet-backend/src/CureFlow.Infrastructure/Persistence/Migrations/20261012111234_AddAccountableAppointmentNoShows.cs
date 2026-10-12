using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountableAppointmentNoShows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentNoShows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowUpWorkId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalAppointmentRevision = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentRevision = table.Column<long>(type: "bigint", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    CutoffMinutesAfterStart = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_AppointmentNoShows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentNoShows_AppointmentReservations_TenantId_Reserva~",
                        columns: x => new { x.TenantId, x.ReservationId },
                        principalTable: "AppointmentReservations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentNoShows_Appointments_TenantId_AppointmentId",
                        columns: x => new { x.TenantId, x.AppointmentId },
                        principalTable: "Appointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentNoShows_ClinicalFollowUpWorks_TenantId_FollowUpW~",
                        columns: x => new { x.TenantId, x.FollowUpWorkId },
                        principalTable: "ClinicalFollowUpWorks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentNoShows_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentNoShows_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentNoShows_TenantId_AppointmentId",
                table: "AppointmentNoShows",
                columns: new[] { "TenantId", "AppointmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentNoShows_TenantId_FacilityId_RecordedAtUtc",
                table: "AppointmentNoShows",
                columns: new[] { "TenantId", "FacilityId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentNoShows_TenantId_FollowUpWorkId",
                table: "AppointmentNoShows",
                columns: new[] { "TenantId", "FollowUpWorkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentNoShows_TenantId_PatientId",
                table: "AppointmentNoShows",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentNoShows_TenantId_ReservationId",
                table: "AppointmentNoShows",
                columns: new[] { "TenantId", "ReservationId" });
            migrationBuilder.Sql("""
ALTER TABLE "AppointmentNoShows" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentNoShows" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "AppointmentNoShows" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_confidentiality ON "AppointmentNoShows" AS RESTRICTIVE FOR SELECT
 USING(public.cureflow_patient_read_allowed("TenantId","PatientId"));
ALTER TABLE "AppointmentNoShows" ADD CONSTRAINT no_show_shape CHECK(COALESCE(
 "OriginalAppointmentRevision">0 AND "AppointmentRevision"="OriginalAppointmentRevision"+1
 AND "CutoffMinutesAfterStart" BETWEEN 0 AND 10080 AND "DurationMinutes" BETWEEN 1 AND 480
 AND "RecordedAtUtc">="ScheduledAtUtc"+make_interval(mins=>"CutoffMinutesAfterStart")
 AND length("Reason") BETWEEN 3 AND 500 AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "IsActive" AND NOT "IsDeleted" AND "CreatedBy" IS NOT NULL,false));
CREATE FUNCTION public.guard_appointment_no_show() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source public."Appointments"%ROWTYPE;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'No-show evidence is retained and immutable' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 SELECT * INTO source FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" FOR UPDATE;
 IF NOT FOUND OR source."PatientId"<>NEW."PatientId" OR source."FacilityId" IS DISTINCT FROM NEW."FacilityId"
  OR source."ReservationId" IS DISTINCT FROM NEW."ReservationId" OR source."Revision"<>NEW."OriginalAppointmentRevision"
  OR source."ScheduledAt"<>NEW."ScheduledAtUtc" OR source."DurationMinutes"<>NEW."DurationMinutes"
  OR source."Status" NOT IN(0,1) OR source."CheckedInAt" IS NOT NULL OR source."CompletedAt" IS NOT NULL
  OR source."IsDeleted" OR NOT source."IsActive" OR NEW."RecordedAtUtc">clock_timestamp()
  OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
  OR NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId")
  OR NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."CreatedBy" AND u."IsActive" AND NOT u."IsDeleted")
  OR NOT EXISTS(SELECT 1 FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."ReservationId"
   AND h."State"='confirmed' AND h."AppointmentId"=NEW."AppointmentId")
  OR NOT EXISTS(SELECT 1 FROM public."ClinicalFollowUpWorks" w WHERE w."TenantId"=NEW."TenantId" AND w."Id"=NEW."FollowUpWorkId"
   AND w."PatientId"=NEW."PatientId" AND w."SourceType"='appointment' AND w."SourceId"=NEW."AppointmentId"
   AND w."SourceVersion"=NEW."OriginalAppointmentRevision"::text AND w."AssignedUserId" IS NOT NULL
   AND w."EscalationOwnerUserId" IS NOT NULL AND w."DueAtUtc">NEW."RecordedAtUtc" AND w."State"='open' AND w."Revision"=1
   AND w."CreatedBy"=NEW."CreatedBy" AND w."IsActive" AND NOT w."IsDeleted") THEN
  RAISE EXCEPTION 'No-show requires exact pending source, elapsed cutoff and newly accountable follow-up' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_no_show_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentNoShows"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_no_show();
CREATE FUNCTION public.require_appointment_no_show_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" h
  ON h."TenantId"=a."TenantId" AND h."Id"=a."ReservationId"
  JOIN public."ClinicalFollowUpWorks" w ON w."TenantId"=a."TenantId" AND w."Id"=NEW."FollowUpWorkId"
  WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" AND a."PatientId"=NEW."PatientId"
   AND a."Status"=3 AND a."Revision"=NEW."AppointmentRevision" AND a."CheckedInAt" IS NULL
   AND a."ReservationId"=NEW."ReservationId" AND h."State"='released' AND h."AppointmentId"=a."Id"
   AND w."SourceType"='appointment' AND w."SourceId"=a."Id" AND w."PatientId"=a."PatientId"
   AND w."SourceVersion"=NEW."OriginalAppointmentRevision"::text AND w."AssignedUserId" IS NOT NULL)
  OR EXISTS(SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId"
   AND s."ReservationId"=NEW."ReservationId" AND s."State"<>'released') THEN
  RAISE EXCEPTION 'No-show, released capacity and named follow-up must commit atomically' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER appointment_no_show_commit AFTER INSERT ON "AppointmentNoShows" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_appointment_no_show_commit();
DO $extend$ DECLARE definition text; needle text;
BEGIN
 definition=pg_get_functiondef('public.guard_explicit_appointment_booking()'::regprocedure);
 needle=$needle$ IF TG_OP='UPDATE' THEN$needle$;
 IF position(needle IN definition)=0 OR position('AppointmentNoShows' IN definition)>0 THEN RAISE EXCEPTION 'Reviewed booking guard required'; END IF;
 EXECUTE replace(definition,needle,$branch$
 IF TG_OP='UPDATE' AND OLD."ReservationId" IS NOT NULL AND NEW."Status"=3 AND EXISTS(
  SELECT 1 FROM public."AppointmentNoShows" n WHERE n."TenantId"=NEW."TenantId" AND n."AppointmentId"=NEW."Id"
   AND n."PatientId"=OLD."PatientId" AND n."ReservationId"=OLD."ReservationId"
   AND n."OriginalAppointmentRevision"=OLD."Revision" AND n."AppointmentRevision"=NEW."Revision"
   AND n."CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid) THEN
  IF OLD."Status" NOT IN(0,1) OR OLD."CheckedInAt" IS NOT NULL OR OLD."CompletedAt" IS NOT NULL
   OR NEW."Revision"<>OLD."Revision"+1 OR NEW."UpdatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
   OR (to_jsonb(OLD)-ARRAY['Status','Revision','UpdatedAt','UpdatedBy']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['Status','Revision','UpdatedAt','UpdatedBy']) THEN
   RAISE EXCEPTION 'No-show cannot alter source or arrived booking' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
$branch$||needle);
 definition=pg_get_functiondef('public.guard_appointment_reservation()'::regprocedure);
 needle=$needle$IF NOT((OLD."State"='held' AND NEW."State" IN('confirmed','released','expired'))$needle$;
 IF position(needle IN definition)=0 THEN RAISE EXCEPTION 'Reviewed reservation guard required'; END IF;
 EXECUTE replace(definition,needle,needle||$branch$
 OR(OLD."State"='confirmed' AND NEW."State"='released' AND NEW."AppointmentId"=OLD."AppointmentId"
  AND EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentNoShows" n ON n."TenantId"=a."TenantId" AND n."AppointmentId"=a."Id"
   WHERE a."TenantId"=NEW."TenantId" AND a."Id"=OLD."AppointmentId" AND a."ReservationId"=OLD."Id"
    AND a."Status" IN(0,1) AND a."CheckedInAt" IS NULL AND a."CompletedAt" IS NULL
    AND n."ReservationId"=OLD."Id" AND n."OriginalAppointmentRevision"=a."Revision"
    AND n."CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid))
$branch$);
 definition=pg_get_functiondef('public.require_appointment_reservation_binding()'::regprocedure);
 needle=$needle$(a."Status"=4 AND h."State"='released')$needle$;
 IF position(needle IN definition)=0 THEN RAISE EXCEPTION 'Reviewed reciprocal booking guard required'; END IF;
 EXECUTE replace(definition,needle,needle||$branch$
 OR(a."Status"=3 AND h."State"='released' AND EXISTS(SELECT 1 FROM public."AppointmentNoShows" n
  WHERE n."TenantId"=a."TenantId" AND n."AppointmentId"=a."Id" AND n."AppointmentRevision"=a."Revision" AND n."ReservationId"=h."Id"))
$branch$);
END $extend$;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentNoShows" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentNoShows" FROM cureflow_tenant;
END IF; END $grant$;

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Retained no-show/follow-up evidence requires reviewed forward repair or protected restore.");
        }
    }
}