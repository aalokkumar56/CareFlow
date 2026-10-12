using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReleasedCapacityWaitlists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentWaitlistDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryRevision = table.Column<long>(type: "bigint", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
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
                    table.PrimaryKey("PK_AppointmentWaitlistDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistDecisions_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentWaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PractitionerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceIdsJson = table.Column<string>(type: "text", nullable: false),
                    EarliestStartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BookingKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FollowUpAppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CurrentOfferId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppointmentWaitlistEntries", x => x.Id);
                    table.UniqueConstraint("AK_AppointmentWaitlistEntries_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistEntries_Appointments_TenantId_FollowUpAp~",
                        columns: x => new { x.TenantId, x.FollowUpAppointmentId },
                        principalTable: "Appointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistEntries_Facilities_TenantId_FacilityId",
                        columns: x => new { x.TenantId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistEntries_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistEntries_Users_TenantId_PractitionerUserId",
                        columns: x => new { x.TenantId, x.PractitionerUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentWaitlistOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReleasedReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppointmentWaitlistOffers", x => x.Id);
                    table.UniqueConstraint("AK_AppointmentWaitlistOffers_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistOffers_AppointmentReservations_TenantId_~",
                        columns: x => new { x.TenantId, x.ReleasedReservationId },
                        principalTable: "AppointmentReservations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistOffers_AppointmentReservations_TenantId~1",
                        columns: x => new { x.TenantId, x.ReservationId },
                        principalTable: "AppointmentReservations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistOffers_AppointmentWaitlistEntries_Tenant~",
                        columns: x => new { x.TenantId, x.EntryId },
                        principalTable: "AppointmentWaitlistEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistOffers_Appointments_TenantId_Appointment~",
                        columns: x => new { x.TenantId, x.AppointmentId },
                        principalTable: "Appointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistOffers_Patients_TenantId_PatientId",
                        columns: x => new { x.TenantId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentWaitlistOffers_Users_TenantId_OwnerUserId",
                        columns: x => new { x.TenantId, x.OwnerUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistDecisions_TenantId_EntryId_EntryRevision",
                table: "AppointmentWaitlistDecisions",
                columns: new[] { "TenantId", "EntryId", "EntryRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistDecisions_TenantId_OfferId",
                table: "AppointmentWaitlistDecisions",
                columns: new[] { "TenantId", "OfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistDecisions_TenantId_PatientId",
                table: "AppointmentWaitlistDecisions",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistEntries_TenantId_CurrentOfferId",
                table: "AppointmentWaitlistEntries",
                columns: new[] { "TenantId", "CurrentOfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistEntries_TenantId_FacilityId_State_Create~",
                table: "AppointmentWaitlistEntries",
                columns: new[] { "TenantId", "FacilityId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistEntries_TenantId_FollowUpAppointmentId",
                table: "AppointmentWaitlistEntries",
                columns: new[] { "TenantId", "FollowUpAppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistEntries_TenantId_PatientId_State_Expires~",
                table: "AppointmentWaitlistEntries",
                columns: new[] { "TenantId", "PatientId", "State", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistEntries_TenantId_PractitionerUserId",
                table: "AppointmentWaitlistEntries",
                columns: new[] { "TenantId", "PractitionerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistOffers_TenantId_AppointmentId",
                table: "AppointmentWaitlistOffers",
                columns: new[] { "TenantId", "AppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistOffers_TenantId_EntryId",
                table: "AppointmentWaitlistOffers",
                columns: new[] { "TenantId", "EntryId" },
                unique: true,
                filter: "\"State\" = 'offered'");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistOffers_TenantId_OwnerUserId",
                table: "AppointmentWaitlistOffers",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistOffers_TenantId_PatientId",
                table: "AppointmentWaitlistOffers",
                columns: new[] { "TenantId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistOffers_TenantId_ReleasedReservationId",
                table: "AppointmentWaitlistOffers",
                columns: new[] { "TenantId", "ReleasedReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentWaitlistOffers_TenantId_ReservationId",
                table: "AppointmentWaitlistOffers",
                columns: new[] { "TenantId", "ReservationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentWaitlistDecisions_AppointmentWaitlistEntries_Ten~",
                table: "AppointmentWaitlistDecisions",
                columns: new[] { "TenantId", "EntryId" },
                principalTable: "AppointmentWaitlistEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentWaitlistDecisions_AppointmentWaitlistOffers_Tena~",
                table: "AppointmentWaitlistDecisions",
                columns: new[] { "TenantId", "OfferId" },
                principalTable: "AppointmentWaitlistOffers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentWaitlistEntries_AppointmentWaitlistOffers_Tenant~",
                table: "AppointmentWaitlistEntries",
                columns: new[] { "TenantId", "CurrentOfferId" },
                principalTable: "AppointmentWaitlistOffers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
DO $rls$ DECLARE tbl text;
BEGIN
 FOREACH tbl IN ARRAY ARRAY['AppointmentWaitlistEntries','AppointmentWaitlistOffers','AppointmentWaitlistDecisions'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',tbl);
  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',tbl);
  EXECUTE format('CREATE POLICY tenant_scope ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',tbl);
  EXECUTE format('CREATE POLICY patient_scope ON %I AS RESTRICTIVE FOR SELECT USING(public.cureflow_patient_read_allowed("TenantId","PatientId"))',tbl);
 END LOOP;
END $rls$;
ALTER TABLE "AppointmentWaitlistEntries" ADD CONSTRAINT waitlist_entry_shape CHECK(COALESCE(
 "Revision">0 AND "IsActive" AND NOT "IsDeleted" AND "DurationMinutes" BETWEEN 1 AND 480
 AND "EarliestStartsAtUtc"<"LatestEndsAtUtc" AND "LatestEndsAtUtc"-"EarliestStartsAtUtc">=make_interval(mins=>"DurationMinutes")
 AND "ExpiresAtUtc"="LatestEndsAtUtc" AND "LatestEndsAtUtc"<="CreatedAt"+interval '365 days'
 AND jsonb_typeof("ResourceIdsJson"::jsonb)='array' AND jsonb_array_length("ResourceIdsJson"::jsonb)<=5
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "BookingKind" IN('routine','walk_in','follow_up')
 AND ("BookingKind"='follow_up')=("FollowUpAppointmentId" IS NOT NULL)
 AND (("State"='waiting' AND "CurrentOfferId" IS NULL) OR ("State" IN('offered','accepted') AND "CurrentOfferId" IS NOT NULL)
 OR "State" IN('withdrawn','expired')),false));
ALTER TABLE "AppointmentWaitlistOffers" ADD CONSTRAINT waitlist_offer_shape CHECK(COALESCE(
 "IsActive" AND NOT "IsDeleted" AND "CreatedBy"="OwnerUserId" AND "ExpiresAtUtc">"CreatedAt"
 AND "ExpiresAtUtc"<="CreatedAt"+interval '30 minutes' AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "ReservationId"<>"ReleasedReservationId" AND (("State"='offered' AND "Revision"=1 AND "AppointmentId" IS NULL)
 OR("State"='accepted' AND "Revision"=2 AND "AppointmentId" IS NOT NULL)
 OR("State" IN('expired','withdrawn') AND "Revision"=2 AND "AppointmentId" IS NULL)),false));
ALTER TABLE "AppointmentWaitlistDecisions" ADD CONSTRAINT waitlist_decision_shape CHECK(
 "EntryRevision">0 AND "IsActive" AND NOT "IsDeleted" AND "CreatedBy" IS NOT NULL
 AND "Action" IN('add','offer','accept','expire_offer','withdrawn','expired') AND length("Reason") BETWEEN 3 AND 1000);
CREATE FUNCTION public.guard_appointment_waitlist_entry() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Waitlist request history is retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId")
  OR NULLIF(current_setting('app.current_user_id',true),'')::uuid IS NULL THEN RAISE EXCEPTION 'Current patient actor scope required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."FollowUpAppointmentId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM public."Appointments" a
   WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."FollowUpAppointmentId" AND a."PatientId"=NEW."PatientId"
    AND a."ScheduledAt"<NEW."EarliestStartsAtUtc" AND a."Status" NOT IN(3,4) AND a."IsActive" AND NOT a."IsDeleted") THEN
   RAISE EXCEPTION 'Waitlist follow-up requires exact earlier same-patient source' USING ERRCODE='23514';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
    WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."PractitionerUserId" AND u."Role" IN(1,4,6)
     AND u."IsActive" AND NOT u."IsDeleted" AND p."IsActive" AND NOT p."IsDeleted") THEN
   RAISE EXCEPTION 'Waitlist requires active eligible staff' USING ERRCODE='23514';
  END IF;
  IF NEW."Revision"<>1 OR NEW."State"<>'waiting' OR NEW."EarliestStartsAtUtc"<=clock_timestamp()
   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
   OR NOT EXISTS(SELECT 1 FROM public."PatientIdentifiers" i WHERE i."TenantId"=NEW."TenantId" AND i."PatientId"=NEW."PatientId"
    AND i."FacilityId"=NEW."FacilityId" AND i."Namespace"='local-mrn') THEN
   RAISE EXCEPTION 'Waitlist starts with an explicit current patient/facility request' USING ERRCODE='23514';
  END IF;
 ELSE
  IF NEW."Revision"<>OLD."Revision"+1 OR OLD."State" NOT IN('waiting','offered')
   OR (to_jsonb(OLD)-ARRAY['State','Revision','CurrentOfferId','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','CurrentOfferId','UpdatedAt'])
   OR NOT((OLD."State"='waiting' AND NEW."State" IN('offered','withdrawn','expired'))
    OR(OLD."State"='offered' AND NEW."State" IN('waiting','accepted','withdrawn','expired'))) THEN
   RAISE EXCEPTION 'Waitlist intent is immutable; only reviewed terminal/offer transitions are allowed' USING ERRCODE='23514';
  END IF;
  IF NEW."State" IN('offered','accepted') AND (NEW."ExpiresAtUtc"<=clock_timestamp() OR NOT EXISTS(
   SELECT 1 FROM public."AppointmentWaitlistOffers" o WHERE o."TenantId"=NEW."TenantId" AND o."Id"=NEW."CurrentOfferId"
    AND o."EntryId"=NEW."Id" AND o."PatientId"=NEW."PatientId" AND o."State"=CASE WHEN NEW."State"='offered' THEN 'offered' ELSE 'accepted' END)) THEN
   RAISE EXCEPTION 'Entry and exact current offer state must agree' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='expired' AND NEW."ExpiresAtUtc">clock_timestamp() THEN RAISE EXCEPTION 'Live request cannot expire' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_entry_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistEntries"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_entry();
CREATE FUNCTION public.guard_appointment_waitlist_offer() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry_row public."AppointmentWaitlistEntries"%ROWTYPE; held public."AppointmentReservations"%ROWTYPE; released public."AppointmentReservations"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Waitlist offer history is retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 SELECT * INTO entry_row FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=NEW."TenantId" AND e."Id"=NEW."EntryId" FOR UPDATE;
 IF NOT FOUND OR entry_row."PatientId"<>NEW."PatientId" OR NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId") THEN
  RAISE EXCEPTION 'Offer requires exact current patient request' USING ERRCODE='23514';
 END IF;
 SELECT * INTO held FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReservationId" FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Offer requires exact reservation' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  SELECT * INTO released FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReleasedReservationId";
  IF NOT FOUND OR released."State"<>'released' OR held."State"<>'held' OR held."OwnerUserId"<>NEW."OwnerUserId"
   OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
   OR entry_row."State"<>'waiting' OR entry_row."ExpiresAtUtc"<=clock_timestamp() OR NEW."State"<>'offered' OR NEW."Revision"<>1
   OR NEW."ExpiresAtUtc"<>held."HoldExpiresAtUtc" OR held."HoldExpiresAtUtc"<=clock_timestamp() OR held."StartsAtUtc"<=clock_timestamp()
   OR held."FacilityId"<>entry_row."FacilityId" OR held."PractitionerUserId"<>entry_row."PractitionerUserId"
   OR (held."FacilityId",held."PractitionerUserId",held."StartsAtUtc",held."EndsAtUtc")
     IS DISTINCT FROM (released."FacilityId",released."PractitionerUserId",released."StartsAtUtc",released."EndsAtUtc")
   OR held."StartsAtUtc"<entry_row."EarliestStartsAtUtc" OR held."EndsAtUtc">entry_row."LatestEndsAtUtc"
   OR EXTRACT(EPOCH FROM(held."EndsAtUtc"-held."StartsAtUtc"))/60<>entry_row."DurationMinutes"
   OR EXISTS(SELECT 1 FROM jsonb_array_elements_text((entry_row."ResourceIdsJson"::jsonb)) ids(value) WHERE NOT EXISTS(
    SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id"
     AND s."TargetType"='resource' AND s."TargetId"=ids.value::uuid))
   OR (SELECT count(*) FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id"
     AND s."TargetType"='resource')<>jsonb_array_length((entry_row."ResourceIdsJson"::jsonb))
   OR EXISTS((SELECT s."TargetType",s."TargetId" FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id")
     EXCEPT (SELECT s."TargetType",s."TargetId" FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=released."Id")) THEN
   RAISE EXCEPTION 'Offer must bind exact released capacity and live owned hold' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'offered' OR NEW."State" NOT IN('accepted','expired','withdrawn') OR NEW."Revision"<>2
   OR (to_jsonb(OLD)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) THEN
   RAISE EXCEPTION 'Offer evidence is immutable; decisions are terminal' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='accepted' THEN
   IF OLD."ExpiresAtUtc"<=clock_timestamp() OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
    OR held."State"<>'confirmed' OR held."AppointmentId"<>NEW."AppointmentId"
    OR NOT EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId"
     AND a."PatientId"=NEW."PatientId" AND a."ReservationId"=held."Id" AND a."FacilityId"=entry_row."FacilityId") THEN
    RAISE EXCEPTION 'Expired/changed offer cannot accept or bind another patient' USING ERRCODE='23514';
   END IF;
  ELSIF held."State" NOT IN('expired','released') OR (NEW."State"='expired' AND OLD."ExpiresAtUtc">clock_timestamp()) THEN
   RAISE EXCEPTION 'Offer closure requires exact hold release/expiry' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_offer_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistOffers"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_offer();
CREATE FUNCTION public.guard_appointment_waitlist_decision() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Waitlist decision evidence is immutable' USING ERRCODE='23514'; END IF;
 IF NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid OR NOT EXISTS(
  SELECT 1 FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=NEW."TenantId" AND e."Id"=NEW."EntryId"
   AND e."PatientId"=NEW."PatientId" AND e."Revision"=NEW."EntryRevision") THEN
  RAISE EXCEPTION 'Decision requires exact current patient/request version and actor' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_decision_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistDecisions"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_decision();
CREATE FUNCTION public.require_waitlist_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry_id uuid; scope_tenant uuid; entry_row public."AppointmentWaitlistEntries"%ROWTYPE;
BEGIN
 IF TG_TABLE_NAME='AppointmentWaitlistEntries' THEN entry_id=NEW."Id"; ELSE entry_id=NEW."EntryId"; END IF;
 scope_tenant=NEW."TenantId";
 SELECT * INTO entry_row FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=scope_tenant AND e."Id"=entry_id;
 IF NOT FOUND OR NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistDecisions" d WHERE d."TenantId"=scope_tenant AND d."EntryId"=entry_id
    AND d."EntryRevision"=entry_row."Revision"
    AND ((entry_row."State"='waiting' AND d."Action" IN('add','expire_offer')) OR(entry_row."State"='offered' AND d."Action"='offer')
      OR(entry_row."State"='accepted' AND d."Action"='accept') OR(entry_row."State" IN('expired','withdrawn') AND d."Action"=entry_row."State")))
  OR EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o WHERE o."TenantId"=scope_tenant AND o."EntryId"=entry_id AND o."State"='offered'
    AND (entry_row."State"<>'offered' OR entry_row."CurrentOfferId"<>o."Id"))
  OR (entry_row."State"='offered' AND NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o
    WHERE o."TenantId"=scope_tenant AND o."Id"=entry_row."CurrentOfferId" AND o."EntryId"=entry_id AND o."State"='offered'))
  OR (entry_row."State"='accepted' AND NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o
    JOIN public."Appointments" a ON a."TenantId"=o."TenantId" AND a."Id"=o."AppointmentId"
    WHERE o."TenantId"=scope_tenant AND o."Id"=entry_row."CurrentOfferId" AND o."EntryId"=entry_id AND o."State"='accepted'
     AND a."PatientId"=entry_row."PatientId" AND a."ReservationId"=o."ReservationId")) THEN
  RAISE EXCEPTION 'Waitlist offer, acceptance booking and accountable decision must commit together' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER waitlist_entry_commit AFTER INSERT OR UPDATE ON "AppointmentWaitlistEntries" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_waitlist_commit();
CREATE CONSTRAINT TRIGGER waitlist_offer_commit AFTER INSERT OR UPDATE ON "AppointmentWaitlistOffers" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_waitlist_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" FROM cureflow_tenant;
 GRANT UPDATE("State","Revision","CurrentOfferId","UpdatedAt") ON "AppointmentWaitlistEntries" TO cureflow_tenant;
 GRANT UPDATE("State","Revision","AppointmentId","UpdatedAt") ON "AppointmentWaitlistOffers" TO cureflow_tenant;
END IF; END $grant$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Retained waitlist/offers/decisions require reviewed forward repair or protected restore.");
        }
    }
}