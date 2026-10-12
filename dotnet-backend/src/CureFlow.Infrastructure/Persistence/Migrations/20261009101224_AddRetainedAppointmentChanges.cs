using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetainedAppointmentChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentReservations_TenantId_AppointmentId",
                table: "AppointmentReservations");

            migrationBuilder.CreateTable(
                name: "AppointmentBookingChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentRevision = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviousReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacementReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousStartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreviousEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplacementStartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacementEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
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
                    table.PrimaryKey("PK_AppointmentBookingChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentBookingChanges_AppointmentReservations_TenantId_~",
                        columns: x => new { x.TenantId, x.PreviousReservationId },
                        principalTable: "AppointmentReservations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentBookingChanges_AppointmentReservations_TenantId~1",
                        columns: x => new { x.TenantId, x.ReplacementReservationId },
                        principalTable: "AppointmentReservations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentBookingChanges_Appointments_TenantId_Appointment~",
                        columns: x => new { x.TenantId, x.AppointmentId },
                        principalTable: "Appointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentBookingChanges_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReservations_TenantId_AppointmentId",
                table: "AppointmentReservations",
                columns: new[] { "TenantId", "AppointmentId" },
                unique: true,
                filter: "\"AppointmentId\" IS NOT NULL AND \"State\" = 'confirmed'");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBookingChanges_TenantId_AppointmentId_Appointmen~",
                table: "AppointmentBookingChanges",
                columns: new[] { "TenantId", "AppointmentId", "AppointmentRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBookingChanges_TenantId_PatientId",
                table: "AppointmentBookingChanges",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBookingChanges_TenantId_PreviousReservationId",
                table: "AppointmentBookingChanges",
                columns: new[] { "TenantId", "PreviousReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBookingChanges_TenantId_ReplacementReservationId",
                table: "AppointmentBookingChanges",
                columns: new[] { "TenantId", "ReplacementReservationId" });
            migrationBuilder.Sql("""
ALTER TABLE "AppointmentBookingChanges" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentBookingChanges" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "AppointmentBookingChanges" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_confidentiality ON "AppointmentBookingChanges" AS RESTRICTIVE FOR SELECT
 USING(public.cureflow_patient_read_allowed("TenantId","PatientId"));
ALTER TABLE "AppointmentBookingChanges" ADD CONSTRAINT booking_change_shape CHECK(COALESCE(
 "AppointmentRevision">1 AND length("Reason") BETWEEN 3 AND 1000 AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "IsActive" AND NOT "IsDeleted" AND "PreviousStartsAtUtc"<"PreviousEndsAtUtc"
 AND (("Action"='cancel' AND "ReplacementReservationId" IS NULL AND "ReplacementStartsAtUtc" IS NULL AND "ReplacementEndsAtUtc" IS NULL)
 OR ("Action"='reschedule' AND "ReplacementReservationId" IS NOT NULL AND "ReplacementReservationId"<>"PreviousReservationId"
 AND "ReplacementStartsAtUtc"<"ReplacementEndsAtUtc")),false));
CREATE FUNCTION public.guard_appointment_booking_change() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source public."Appointments"%ROWTYPE; previous public."AppointmentReservations"%ROWTYPE; next_hold public."AppointmentReservations"%ROWTYPE;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Booking change evidence is immutable' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 SELECT * INTO source FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" FOR UPDATE;
 IF NOT FOUND OR source."PatientId"<>NEW."PatientId" OR source."Revision"+1<>NEW."AppointmentRevision"
  OR source."ReservationId"<>NEW."PreviousReservationId" OR source."ScheduledAt"<>NEW."PreviousStartsAtUtc"
  OR source."ScheduledAt"+make_interval(mins=>source."DurationMinutes")<>NEW."PreviousEndsAtUtc"
  OR source."CheckedInAt" IS NOT NULL OR source."CompletedAt" IS NOT NULL OR source."Status" NOT IN(0,1)
  OR source."ScheduledAt"<=clock_timestamp() OR source."IsDeleted"
  OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
  RAISE EXCEPTION 'Change must record exact future unchecked-in patient booking source' USING ERRCODE='23514';
 END IF;
 SELECT * INTO previous FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."PreviousReservationId" FOR UPDATE;
 IF NOT FOUND OR previous."State"<>'confirmed' OR previous."AppointmentId"<>source."Id" THEN
  RAISE EXCEPTION 'Change requires exact prior confirmed allocation' USING ERRCODE='23514';
 END IF;
 IF NEW."Action"='reschedule' THEN
  SELECT * INTO next_hold FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."ReplacementReservationId" FOR UPDATE;
  IF NOT FOUND OR next_hold."State"<>'held' OR next_hold."HoldExpiresAtUtc"<=clock_timestamp() OR next_hold."StartsAtUtc"<=clock_timestamp()
   OR next_hold."OwnerUserId" IS DISTINCT FROM NEW."CreatedBy" OR NEW."ReplacementStartsAtUtc"<>next_hold."StartsAtUtc"
   OR NEW."ReplacementEndsAtUtc"<>next_hold."EndsAtUtc" THEN
   RAISE EXCEPTION 'Replacement evidence must match current exact owned hold' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_booking_change_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentBookingChanges"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_booking_change();
CREATE FUNCTION public.require_booking_change_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" previous
   ON previous."TenantId"=a."TenantId" AND previous."Id"=NEW."PreviousReservationId"
  WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" AND a."PatientId"=NEW."PatientId" AND a."Revision"=NEW."AppointmentRevision"
   AND previous."State"='released' AND previous."AppointmentId"=a."Id" AND a."CheckedInAt" IS NULL
   AND ((NEW."Action"='cancel' AND a."Status"=4 AND a."ReservationId"=previous."Id")
    OR (NEW."Action"='reschedule' AND a."Status" IN(0,1) AND a."ReservationId"=NEW."ReplacementReservationId"
     AND a."ScheduledAt"=NEW."ReplacementStartsAtUtc" AND a."ScheduledAt"+make_interval(mins=>a."DurationMinutes")=NEW."ReplacementEndsAtUtc"))) THEN
  RAISE EXCEPTION 'Booking, prior release and replacement/cancellation must commit atomically' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER booking_change_commit AFTER INSERT ON "AppointmentBookingChanges" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_booking_change_commit();
ALTER TABLE "AppointmentReservations" DROP CONSTRAINT reservation_shape;
ALTER TABLE "AppointmentReservations" ADD CONSTRAINT reservation_shape CHECK(COALESCE(
 "StartsAtUtc"<"EndsAtUtc" AND "EndsAtUtc"<="StartsAtUtc"+interval '8 hours'
 AND "HoldExpiresAtUtc">"CreatedAt" AND "HoldExpiresAtUtc"<="CreatedAt"+interval '30 minutes' AND "HoldExpiresAtUtc"<="EndsAtUtc"
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "CreatedBy"="OwnerUserId" AND "IsActive" AND NOT "IsDeleted"
 AND (("State"='held' AND "Revision"=1 AND "AppointmentId" IS NULL)
 OR ("State" IN('released','expired') AND "Revision"=2 AND "AppointmentId" IS NULL)
 OR ("State"='confirmed' AND "Revision"=2 AND "AppointmentId" IS NOT NULL)
 OR ("State"='released' AND "Revision"=3 AND "AppointmentId" IS NOT NULL)),false));
CREATE OR REPLACE FUNCTION public.require_appointment_reservation_binding() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source_id uuid; scope_tenant uuid;
BEGIN
 IF TG_TABLE_NAME='Appointments' THEN source_id=NEW."Id"; scope_tenant=NEW."TenantId";
 ELSE source_id=NEW."AppointmentId"; scope_tenant=NEW."TenantId"; END IF;
 IF source_id IS NOT NULL AND EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND a."ReservationId" IS NOT NULL)
  AND NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" h ON h."TenantId"=a."TenantId" AND h."Id"=a."ReservationId"
   WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND h."AppointmentId"=a."Id"
    AND ((a."Status" IN(0,1) AND h."State"='confirmed') OR(a."Status"=4 AND h."State"='released'))
    AND a."FacilityId"=h."FacilityId" AND a."DoctorUserId"=h."PractitionerUserId" AND a."ScheduledAt"=h."StartsAtUtc"
    AND a."DurationMinutes"=EXTRACT(EPOCH FROM(h."EndsAtUtc"-h."StartsAtUtc"))/60) THEN
  RAISE EXCEPTION 'Appointment and exact current confirmed/released allocation must commit atomically' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentBookingChanges" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentBookingChanges" FROM cureflow_tenant;
END IF; END $grant$;
CREATE OR REPLACE FUNCTION public.guard_appointment_reservation() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation history is retained' USING ERRCODE='23514'; END IF;
                 IF TG_OP='INSERT' THEN
                  IF NEW."State"<>'held' OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
                   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
                   OR NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."OwnerUserId" AND u."IsActive" AND NOT u."IsDeleted") THEN
                   RAISE EXCEPTION 'Hold requires current owner and active facility' USING ERRCODE='23514';
                  END IF;
                 ELSE
                  IF NOT((OLD."State"='held' AND NEW."State" IN('confirmed','released','expired'))
 OR(OLD."State"='confirmed' AND NEW."State"='released' AND NEW."AppointmentId"=OLD."AppointmentId"
 AND EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentBookingChanges" c ON c."TenantId"=a."TenantId" AND c."AppointmentId"=a."Id"
  WHERE a."TenantId"=NEW."TenantId" AND a."Id"=OLD."AppointmentId" AND a."ReservationId"=OLD."Id"
   AND a."ScheduledAt">clock_timestamp() AND a."CheckedInAt" IS NULL AND a."CompletedAt" IS NULL AND a."Status" IN(0,1)
   AND c."PreviousReservationId"=OLD."Id" AND c."AppointmentRevision"=a."Revision"+1
   AND c."CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid))) OR NEW."Revision"<>OLD."Revision"+1
                   OR (to_jsonb(OLD)-ARRAY['State','Revision','AppointmentId','UpdatedAt'])
                      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) THEN
                   RAISE EXCEPTION 'Reservation source is immutable; transition is terminal' USING ERRCODE='23514';
                  END IF;
                  IF NEW."State"='expired' THEN
                   IF OLD."HoldExpiresAtUtc">clock_timestamp() THEN RAISE EXCEPTION 'Live hold cannot expire' USING ERRCODE='23514'; END IF;
                  ELSIF OLD."State"='held' AND NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
                   RAISE EXCEPTION 'Reservation owner is required' USING ERRCODE='23514';
                  END IF;
                  IF NEW."State"='confirmed' AND (OLD."HoldExpiresAtUtc"<=clock_timestamp() OR NOT EXISTS(
                   SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId"
                    AND (a."CreatedAt">=OLD."CreatedAt" OR EXISTS(SELECT 1 FROM public."AppointmentBookingChanges" c
 WHERE c."TenantId"=a."TenantId" AND c."AppointmentId"=a."Id" AND c."AppointmentRevision"=a."Revision"
 AND c."Action"='reschedule' AND c."ReplacementReservationId"=NEW."Id")) AND a."FacilityId"=NEW."FacilityId" AND a."ReservationId"=NEW."Id" AND a."DoctorUserId"=NEW."PractitionerUserId" AND a."ScheduledAt"=NEW."StartsAtUtc"
                    AND a."DurationMinutes"=EXTRACT(EPOCH FROM (NEW."EndsAtUtc"-NEW."StartsAtUtc"))/60 AND a."Status" IN (0,1) AND NOT a."IsDeleted")) THEN
                   RAISE EXCEPTION 'Expired hold or mismatched new appointment cannot confirm' USING ERRCODE='23514';
                  END IF;
                 END IF;
                 RETURN NEW;
                END $guard$;
CREATE OR REPLACE FUNCTION public.guard_appointment_reservation_seat() RETURNS trigger LANGUAGE plpgsql AS $guard$
                DECLARE reservation public."AppointmentReservations"%ROWTYPE; capacity integer;
                BEGIN
                 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation occupancy history is retained' USING ERRCODE='23514'; END IF;
                 SELECT * INTO reservation FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReservationId";
                 IF NOT FOUND OR NEW."StartsAtUtc"<>reservation."StartsAtUtc" OR NEW."EndsAtUtc"<>reservation."EndsAtUtc" OR NEW."State"<>reservation."State" THEN
                  RAISE EXCEPTION 'Occupancy must match exact source reservation' USING ERRCODE='23514';
                 END IF;
                 IF TG_OP='INSERT' THEN
                  IF NEW."State"<>'held' THEN RAISE EXCEPTION 'Occupancy starts held' USING ERRCODE='23514'; END IF;
                  PERFORM pg_advisory_xact_lock(734291);
                  PERFORM pg_advisory_xact_lock(hashtextextended('appointment-seat:'||NEW."TenantId"::text||':'||NEW."TargetType"||':'||NEW."TargetId"::text,0));
                  IF NEW."TargetType"='practitioner' THEN
                   capacity=1;
                   IF NEW."TargetId"<>reservation."PractitionerUserId" OR NOT EXISTS(
                    SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
                     WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."TargetId" AND u."IsActive" AND NOT u."IsDeleted"
                      AND p."IsActive" AND NOT p."IsDeleted" AND p."IsAvailable") THEN RAISE EXCEPTION 'Practitioner scope unavailable' USING ERRCODE='23514'; END IF;
                  ELSE
                   SELECT r."Capacity" INTO capacity FROM public."FacilityResources" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."TargetId"
                    AND r."FacilityId"=reservation."FacilityId" AND r."ResourceType" IN ('room','equipment') AND r."IsActive" AND NOT r."IsDeleted"
                    AND r."OperationalStatus"='enabled' AND r."EffectiveFrom"<=NEW."StartsAtUtc" AND (r."EffectiveUntil" IS NULL OR r."EffectiveUntil">=NEW."EndsAtUtc") FOR SHARE;
                  END IF;
                  IF capacity IS NULL OR NEW."CapacitySnapshot"<>capacity OR NEW."Seat">capacity THEN
                   RAISE EXCEPTION 'Seat is outside current resource capacity' USING ERRCODE='23514';
                  END IF;
                 ELSE
                  IF NOT((OLD."State"='held' AND NEW."State" IN('confirmed','released','expired')) OR(OLD."State"='confirmed' AND NEW."State"='released'))
                   OR (to_jsonb(OLD)-ARRAY['State','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','UpdatedAt']) THEN
                   RAISE EXCEPTION 'Occupancy identity/range cannot change; transition is terminal' USING ERRCODE='23514';
                  END IF;
                 END IF;
                 RETURN NEW;
                END $guard$;
DO $extend$ DECLARE definition text; insertion_point text;
BEGIN
 definition=pg_get_functiondef('public.guard_explicit_appointment_booking()'::regprocedure);
 insertion_point=$needle$ IF TG_OP='UPDATE' THEN$needle$;
 IF position(insertion_point IN definition)=0 OR position('AppointmentBookingChanges' IN definition)>0 THEN
  RAISE EXCEPTION 'Reviewed booking/arrival guard version is required';
 END IF;
 EXECUTE replace(definition,insertion_point,$branch$
 IF TG_OP='UPDATE' AND (NEW."PatientId",NEW."ScheduledAt",NEW."Status")
   IS DISTINCT FROM (OLD."PatientId",OLD."ScheduledAt",OLD."Status")
  AND EXISTS(SELECT 1 FROM public."Appointments" child WHERE child."TenantId"=OLD."TenantId" AND child."FollowUpAppointmentId"=OLD."Id"
   AND child."Status" IN(0,1) AND NOT child."IsDeleted" AND child."IsActive"
   AND (NEW."PatientId"<>child."PatientId" OR NEW."Status" IN(3,4) OR NEW."ScheduledAt">=child."ScheduledAt")) THEN
  RAISE EXCEPTION 'Active follow-up references require reviewed reconciliation before prior source changes' USING ERRCODE='23514';
 END IF;
 IF TG_OP='UPDATE' AND OLD."ReservationId" IS NOT NULL AND EXISTS(
   SELECT 1 FROM public."AppointmentBookingChanges" c WHERE c."TenantId"=NEW."TenantId" AND c."AppointmentId"=NEW."Id"
    AND c."AppointmentRevision"=NEW."Revision") THEN
  IF OLD."CheckedInAt" IS NOT NULL OR OLD."CompletedAt" IS NOT NULL OR OLD."Status" NOT IN(0,1) OR OLD."ScheduledAt"<=clock_timestamp()
   OR NEW."Revision"<>OLD."Revision"+1
   OR (to_jsonb(OLD)-ARRAY['FacilityId','ReservationId','DoctorUserId','DoctorName','Department','ScheduledAt','DurationMinutes','Status','Revision','UpdatedAt'])
      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['FacilityId','ReservationId','DoctorUserId','DoctorName','Department','ScheduledAt','DurationMinutes','Status','Revision','UpdatedAt'])
   OR NOT EXISTS(SELECT 1 FROM public."AppointmentBookingChanges" c WHERE c."TenantId"=NEW."TenantId" AND c."AppointmentId"=NEW."Id"
    AND c."PatientId"=OLD."PatientId" AND c."PreviousReservationId"=OLD."ReservationId" AND c."AppointmentRevision"=NEW."Revision"
    AND c."CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid
    AND ((c."Action"='cancel' AND NEW."Status"=4
      AND (to_jsonb(OLD)-ARRAY['Status','Revision','UpdatedAt']) IS NOT DISTINCT FROM (to_jsonb(NEW)-ARRAY['Status','Revision','UpdatedAt']))
     OR(c."Action"='reschedule' AND NEW."Status"=OLD."Status" AND c."ReplacementReservationId"=NEW."ReservationId"
      AND EXISTS(SELECT 1 FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."ReservationId"
       AND h."State"='held' AND h."HoldExpiresAtUtc">clock_timestamp() AND h."FacilityId"=NEW."FacilityId"
       AND h."PractitionerUserId"=NEW."DoctorUserId" AND h."StartsAtUtc"=NEW."ScheduledAt"
       AND NEW."DurationMinutes"=EXTRACT(EPOCH FROM(h."EndsAtUtc"-h."StartsAtUtc"))/60)))) THEN
   RAISE EXCEPTION 'Reserved transition requires exact immutable change evidence' USING ERRCODE='23514';
  END IF;
  IF NEW."Status"<>4 AND NEW."FollowUpAppointmentId" IS NOT NULL AND NOT EXISTS(
    SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."FollowUpAppointmentId"
     AND a."PatientId"=NEW."PatientId" AND a."ScheduledAt"<NEW."ScheduledAt" AND a."Status" NOT IN(3,4) AND NOT a."IsDeleted") THEN
   RAISE EXCEPTION 'Rescheduled follow-up must retain valid earlier same-patient source' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
$branch$||insertion_point);
END $extend$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Retained booking change/notice/allocation history requires reviewed forward repair or protected restore.");
        }
    }
}