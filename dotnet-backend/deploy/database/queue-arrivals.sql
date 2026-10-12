START TRANSACTION;
CREATE TABLE "QueueCounters" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ArrivalDate" date NOT NULL,
    "LastToken" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_QueueCounters" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QueueCounters_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "QueueEntries" (
    "Id" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ReservationId" uuid NOT NULL,
    "OperatingSettingId" uuid NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "ArrivalDate" date NOT NULL,
    "ArrivedAtUtc" timestamp with time zone NOT NULL,
    "Token" bigint NOT NULL,
    "AppointmentRevision" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_QueueEntries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QueueEntries_AppointmentReservations_TenantId_ReservationId" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_Appointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_FacilityOperatingSettings_OperatingSettingId" FOREIGN KEY ("OperatingSettingId") REFERENCES "FacilityOperatingSettings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_QueueCounters_TenantId_FacilityId_ArrivalDate" ON "QueueCounters" ("TenantId", "FacilityId", "ArrivalDate");

CREATE INDEX "IX_QueueEntries_OperatingSettingId" ON "QueueEntries" ("OperatingSettingId");

CREATE UNIQUE INDEX "IX_QueueEntries_TenantId_AppointmentId" ON "QueueEntries" ("TenantId", "AppointmentId");

CREATE UNIQUE INDEX "IX_QueueEntries_TenantId_FacilityId_ArrivalDate_Token" ON "QueueEntries" ("TenantId", "FacilityId", "ArrivalDate", "Token");

CREATE INDEX "IX_QueueEntries_TenantId_PatientId" ON "QueueEntries" ("TenantId", "PatientId");

CREATE INDEX "IX_QueueEntries_TenantId_ReservationId" ON "QueueEntries" ("TenantId", "ReservationId");

ALTER TABLE "QueueEntries" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "QueueEntries" FORCE ROW LEVEL SECURITY;
ALTER TABLE "QueueCounters" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "QueueCounters" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "QueueEntries" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_scope ON "QueueEntries" AS RESTRICTIVE FOR SELECT USING (public.cureflow_patient_read_allowed("TenantId","PatientId"));
CREATE POLICY tenant_scope ON "QueueCounters" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "QueueEntries" ADD CONSTRAINT queue_arrival_shape CHECK ("Token" BETWEEN 1 AND 1000000000
 AND "AppointmentRevision">1 AND length("Timezone") BETWEEN 1 AND 100 AND "IsActive" AND NOT "IsDeleted"
 AND "CreatedBy" IS NOT NULL);
ALTER TABLE "QueueCounters" ADD CONSTRAINT queue_counter_shape CHECK ("LastToken" BETWEEN 1 AND 1000000000 AND "IsActive" AND NOT "IsDeleted");
DO $extend$ DECLARE definition text; original_condition text; replacement_condition text;
BEGIN
 definition=pg_get_functiondef('public.guard_explicit_appointment_booking()'::regprocedure);
 original_condition=$old$IF (to_jsonb(OLD)-ARRAY['UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt']) THEN$old$;
 replacement_condition=$new$IF (to_jsonb(OLD)-ARRAY['UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt'])
 AND NOT (OLD."CheckedInAt" IS NULL AND NEW."CheckedInAt" IS NOT NULL AND NEW."Revision"=OLD."Revision"+1
 AND NEW."Status" IN (0,1) AND (to_jsonb(OLD)-ARRAY['CheckedInAt','Revision','UpdatedAt','UpdatedBy'])
 IS NOT DISTINCT FROM (to_jsonb(NEW)-ARRAY['CheckedInAt','Revision','UpdatedAt','UpdatedBy'])) THEN$new$;
 IF position(original_condition IN definition)=0 THEN RAISE EXCEPTION 'Reviewed booking guard version is required'; END IF;
 EXECUTE replace(definition,original_condition,replacement_condition);
END $extend$;
CREATE FUNCTION public.guard_queue_arrival() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Arrival evidence is retained and immutable' USING ERRCODE='23514'; END IF;
 IF NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
 OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
 OR NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" r
 ON r."TenantId"=a."TenantId" AND r."Id"=a."ReservationId"
 WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" AND a."PatientId"=NEW."PatientId"
 AND a."FacilityId"=NEW."FacilityId" AND a."ReservationId"=NEW."ReservationId"
 AND a."Status" IN (0,1) AND a."CheckedInAt"=NEW."ArrivedAtUtc" AND a."Revision"=NEW."AppointmentRevision"
 AND a."IsActive" AND NOT a."IsDeleted" AND r."State"='confirmed' AND r."AppointmentId"=a."Id")
 OR NOT EXISTS(SELECT 1 FROM public."FacilityOperatingSettings" s WHERE s."TenantId"=NEW."TenantId"
 AND s."Id"=NEW."OperatingSettingId" AND s."FacilityId"=NEW."FacilityId" AND s."Timezone"=NEW."Timezone"
 AND NOT EXISTS(SELECT 1 FROM public."FacilityOperatingSettings" newer WHERE newer."TenantId"=s."TenantId"
 AND newer."FacilityId"=s."FacilityId" AND newer."Revision">s."Revision"))
 OR NEW."ArrivalDate"<>(NEW."ArrivedAtUtc" AT TIME ZONE NEW."Timezone")::date THEN
 RAISE EXCEPTION 'Arrival requires exact current facility appointment and timezone evidence' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER queue_arrival_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueueEntries" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_arrival();
CREATE FUNCTION public.guard_queue_counter() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Queue token allocation is retained' USING ERRCODE='23514'; END IF;
 IF public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
 OR (TG_OP='INSERT' AND NEW."LastToken"<>1)
 OR (TG_OP='UPDATE' AND (NEW."LastToken"<>OLD."LastToken"+1
 OR (to_jsonb(OLD)-ARRAY['LastToken','UpdatedAt','UpdatedBy']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['LastToken','UpdatedAt','UpdatedBy']))) THEN
 RAISE EXCEPTION 'Queue tokens allocate once in retained facility/date scope' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER queue_counter_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueueCounters" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_counter();
CREATE FUNCTION public.require_queue_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE last_token bigint;
BEGIN
 SELECT c."LastToken" INTO last_token FROM public."QueueCounters" c WHERE c."TenantId"=NEW."TenantId"
 AND c."FacilityId"=NEW."FacilityId" AND c."ArrivalDate"=NEW."ArrivalDate";
 IF last_token IS NULL OR NOT EXISTS(SELECT 1 FROM public."QueueEntries" q WHERE q."TenantId"=NEW."TenantId"
 AND q."FacilityId"=NEW."FacilityId" AND q."ArrivalDate"=NEW."ArrivalDate" AND q."Token"=last_token) THEN
 RAISE EXCEPTION 'Token and exact arrival must commit together' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER queue_counter_commit AFTER INSERT OR UPDATE ON "QueueCounters" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_queue_commit();
CREATE CONSTRAINT TRIGGER queue_entry_commit AFTER INSERT ON "QueueEntries" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_queue_commit();
CREATE FUNCTION public.retain_appointment_arrival() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF OLD."CheckedInAt" IS NOT NULL AND (NEW."CheckedInAt" IS DISTINCT FROM OLD."CheckedInAt"
 OR NEW."PatientId"<>OLD."PatientId" OR NEW."FacilityId" IS DISTINCT FROM OLD."FacilityId"
 OR NEW."ReservationId" IS DISTINCT FROM OLD."ReservationId" OR NEW."ScheduledAt"<>OLD."ScheduledAt"
 OR NEW."DurationMinutes"<>OLD."DurationMinutes" OR NEW."Status" IN (3,4) OR NEW."IsDeleted") THEN
 RAISE EXCEPTION 'Checked-in source requires a reviewed queue transition' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_arrival_retained BEFORE UPDATE ON "Appointments" FOR EACH ROW EXECUTE FUNCTION public.retain_appointment_arrival();
CREATE FUNCTION public.require_appointment_arrival_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF OLD."CheckedInAt" IS NULL AND NEW."CheckedInAt" IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM public."QueueEntries" q WHERE q."TenantId"=NEW."TenantId" AND q."AppointmentId"=NEW."Id"
 AND q."PatientId"=NEW."PatientId" AND q."FacilityId"=NEW."FacilityId" AND q."ReservationId"=NEW."ReservationId"
 AND q."ArrivedAtUtc"=NEW."CheckedInAt" AND q."AppointmentRevision"=NEW."Revision") THEN
 RAISE EXCEPTION 'New check-in and exact queue evidence must commit together' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER appointment_arrival_commit AFTER UPDATE ON "Appointments" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_appointment_arrival_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "QueueEntries","QueueCounters" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "QueueEntries","QueueCounters" FROM cureflow_tenant;
 GRANT UPDATE ("LastToken","UpdatedAt","UpdatedBy") ON "QueueCounters" TO cureflow_tenant;
END IF; END $grant$;
CREATE OR REPLACE FUNCTION public.enforce_reschedule_work() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE exception_row public."ScheduleExceptions"; appointment_row public."Appointments";
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Original booking impact is immutable' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NEW."IsDeleted" OR NOT NEW."IsActive" OR NEW."State"<>'needs_review' THEN
 RAISE EXCEPTION 'Invalid initial booking impact evidence' USING ERRCODE='23514'; END IF;
 SELECT * INTO exception_row FROM public."ScheduleExceptions" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."ExceptionId" AND "IsActive";
 IF NOT FOUND THEN RAISE EXCEPTION 'Current same-tenant exception required' USING ERRCODE='23514'; END IF;
 SELECT * INTO appointment_row FROM public."Appointments" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."AppointmentId"
 AND "IsActive" AND NOT "IsDeleted" AND "Status" IN (0,1) FOR SHARE;
 IF NOT FOUND OR appointment_row."PatientId"<>NEW."PatientId" OR appointment_row."ScheduledAt"<>NEW."OriginalScheduledAtUtc"
 OR appointment_row."DurationMinutes"<>NEW."OriginalDurationMinutes" OR NEW."OriginalDurationMinutes" NOT BETWEEN 1 AND 1440
 OR (exception_row."PractitionerUserId" IS NOT NULL AND appointment_row."DoctorUserId" IS DISTINCT FROM exception_row."PractitionerUserId")
 OR NEW."FacilityAssociationNeedsReview" IS DISTINCT FROM (appointment_row."FacilityId" IS NULL)
 OR (appointment_row."FacilityId" IS NOT NULL AND appointment_row."FacilityId"<>exception_row."FacilityId")
 OR (appointment_row."FacilityId" IS NULL AND exception_row."ResourceId" IS NOT NULL AND exception_row."PractitionerUserId" IS NULL)
 OR (appointment_row."FacilityId" IS NOT NULL AND exception_row."ResourceId" IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId"
 AND s."ReservationId"=appointment_row."ReservationId" AND s."TargetType"='resource' AND s."TargetId"=exception_row."ResourceId" AND s."State"='confirmed'))
 OR appointment_row."ScheduledAt">=exception_row."EndsAtUtc"
 OR appointment_row."ScheduledAt"+make_interval(mins=>appointment_row."DurationMinutes")<=exception_row."StartsAtUtc" THEN
 RAISE EXCEPTION 'Impact must retain the exact overlapping source booking' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $body$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009100817_AddFacilityQueueArrivals', '10.0.0');

COMMIT;

