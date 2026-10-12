START TRANSACTION;
CREATE TABLE "ScheduleExceptions" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PractitionerUserId" uuid,
    "ResourceId" uuid,
    "OperatingSettingId" uuid NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "Kind" character varying(16) NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "DisabledAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ScheduleExceptions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ScheduleExceptions_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleExceptions_FacilityOperatingSettings_OperatingSetti~" FOREIGN KEY ("OperatingSettingId") REFERENCES "FacilityOperatingSettings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleExceptions_FacilityResources_ResourceId" FOREIGN KEY ("ResourceId") REFERENCES "FacilityResources" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleExceptions_Users_PractitionerUserId" FOREIGN KEY ("PractitionerUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "AppointmentRescheduleWorks" (
    "Id" uuid NOT NULL,
    "ExceptionId" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "OriginalScheduledAtUtc" timestamp with time zone NOT NULL,
    "OriginalDurationMinutes" integer NOT NULL,
    "FacilityAssociationNeedsReview" boolean NOT NULL,
    "State" character varying(32) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentRescheduleWorks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentRescheduleWorks_Appointments_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "Appointments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentRescheduleWorks_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentRescheduleWorks_ScheduleExceptions_ExceptionId" FOREIGN KEY ("ExceptionId") REFERENCES "ScheduleExceptions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_AppointmentRescheduleWorks_AppointmentId" ON "AppointmentRescheduleWorks" ("AppointmentId");

CREATE INDEX "IX_AppointmentRescheduleWorks_ExceptionId" ON "AppointmentRescheduleWorks" ("ExceptionId");

CREATE UNIQUE INDEX "IX_AppointmentRescheduleWorks_TenantId_ExceptionId_Appointment~" ON "AppointmentRescheduleWorks" ("TenantId", "ExceptionId", "AppointmentId");

CREATE INDEX "IX_AppointmentRescheduleWorks_TenantId_ExceptionId_Id" ON "AppointmentRescheduleWorks" ("TenantId", "ExceptionId", "Id");

CREATE INDEX "IX_AppointmentRescheduleWorks_TenantId_PatientId" ON "AppointmentRescheduleWorks" ("TenantId", "PatientId");

CREATE INDEX "IX_ScheduleExceptions_OperatingSettingId" ON "ScheduleExceptions" ("OperatingSettingId");

CREATE INDEX "IX_ScheduleExceptions_PractitionerUserId" ON "ScheduleExceptions" ("PractitionerUserId");

CREATE INDEX "IX_ScheduleExceptions_ResourceId" ON "ScheduleExceptions" ("ResourceId");

CREATE INDEX "IX_ScheduleExceptions_TenantId_FacilityId_Id" ON "ScheduleExceptions" ("TenantId", "FacilityId", "Id");

CREATE INDEX "IX_ScheduleExceptions_TenantId_FacilityId_StartsAtUtc_EndsAtUtc" ON "ScheduleExceptions" ("TenantId", "FacilityId", "StartsAtUtc", "EndsAtUtc");

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


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008182413_AddScheduleExceptions', '10.0.0');

COMMIT;

