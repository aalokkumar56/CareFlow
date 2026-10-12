START TRANSACTION;
CREATE TABLE "AppointmentNoShows" (
    "Id" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ReservationId" uuid NOT NULL,
    "FollowUpWorkId" uuid NOT NULL,
    "OriginalAppointmentRevision" bigint NOT NULL,
    "AppointmentRevision" bigint NOT NULL,
    "ScheduledAtUtc" timestamp with time zone NOT NULL,
    "DurationMinutes" integer NOT NULL,
    "CutoffMinutesAfterStart" integer NOT NULL,
    "RecordedAtUtc" timestamp with time zone NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentNoShows" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentNoShows_AppointmentReservations_TenantId_Reserva~" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentNoShows_Appointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentNoShows_ClinicalFollowUpWorks_TenantId_FollowUpW~" FOREIGN KEY ("TenantId", "FollowUpWorkId") REFERENCES "ClinicalFollowUpWorks" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentNoShows_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentNoShows_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_AppointmentNoShows_TenantId_AppointmentId" ON "AppointmentNoShows" ("TenantId", "AppointmentId");

CREATE INDEX "IX_AppointmentNoShows_TenantId_FacilityId_RecordedAtUtc" ON "AppointmentNoShows" ("TenantId", "FacilityId", "RecordedAtUtc");

CREATE UNIQUE INDEX "IX_AppointmentNoShows_TenantId_FollowUpWorkId" ON "AppointmentNoShows" ("TenantId", "FollowUpWorkId");

CREATE INDEX "IX_AppointmentNoShows_TenantId_PatientId" ON "AppointmentNoShows" ("TenantId", "PatientId");

CREATE INDEX "IX_AppointmentNoShows_TenantId_ReservationId" ON "AppointmentNoShows" ("TenantId", "ReservationId");

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


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261012111234_AddAccountableAppointmentNoShows', '10.0.0');

COMMIT;

