START TRANSACTION;
DROP INDEX "IX_Appointments_TenantId_FacilityId";

ALTER TABLE "Visits" ADD "Revision" bigint NOT NULL DEFAULT 1;

ALTER TABLE "Visits" ADD CONSTRAINT "AK_Visits_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "EncounterTransitions" (
    "Id" uuid NOT NULL,
    "VisitId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "AppointmentId" uuid,
    "Revision" bigint NOT NULL,
    "PreviousStatus" integer,
    "Status" integer NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "EndedAtUtc" timestamp with time zone,
    "Action" character varying(20) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "SourceAppointmentRevision" bigint,
    "ContentSnapshotJson" character varying(90000) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_EncounterTransitions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EncounterTransitions_Appointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EncounterTransitions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EncounterTransitions_Visits_TenantId_VisitId" FOREIGN KEY ("TenantId", "VisitId") REFERENCES "Visits" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Visits_TenantId_AppointmentId" ON "Visits" ("TenantId", "AppointmentId");

CREATE INDEX "IX_Appointments_TenantId_FacilityId_DoctorUserId_ScheduledAt_Id" ON "Appointments" ("TenantId", "FacilityId", "DoctorUserId", "ScheduledAt", "Id");

CREATE INDEX "IX_EncounterTransitions_TenantId_AppointmentId" ON "EncounterTransitions" ("TenantId", "AppointmentId");

CREATE INDEX "IX_EncounterTransitions_TenantId_PatientId_CreatedAt" ON "EncounterTransitions" ("TenantId", "PatientId", "CreatedAt");

CREATE UNIQUE INDEX "IX_EncounterTransitions_TenantId_VisitId_Revision" ON "EncounterTransitions" ("TenantId", "VisitId", "Revision");

ALTER TABLE "EncounterTransitions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "EncounterTransitions" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "EncounterTransitions" USING("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_scope ON "EncounterTransitions" AS RESTRICTIVE FOR SELECT USING(public.cureflow_patient_read_allowed("TenantId","PatientId"));
CREATE FUNCTION public.cureflow_encounter_actor_allowed(scope_tenant uuid,scope_user uuid,scope_patient uuid)
 RETURNS boolean LANGUAGE sql STABLE AS $authority$
 SELECT EXISTS(SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" s ON s."TenantId"=u."TenantId" AND s."UserId"=u."Id"
  WHERE u."TenantId"=scope_tenant AND u."Id"=scope_user AND u."Role"=1 AND u."IsActive" AND NOT u."IsDeleted" AND s."IsActive" AND NOT s."IsDeleted")
 AND public.cureflow_patient_read_allowed(scope_tenant,scope_patient)
 AND NOT EXISTS(SELECT required.code FROM(VALUES('Clinical.Edit'),('Clinical.View'),('Patient.View'),('Appointment.View'))required(code)
  WHERE NOT EXISTS(SELECT 1 FROM public."UserRoleAssignments" a JOIN public."Roles" r ON r."Id"=a."RoleId" AND r."IsActive" AND NOT r."IsDeleted"
   JOIN public."RolePermissions" rp ON rp."RoleId"=r."Id" AND rp."IsActive" AND NOT rp."IsDeleted"
   JOIN public."Permissions" p ON p."Id"=rp."PermissionId" AND p."IsActive" AND NOT p."IsDeleted"
   WHERE a."TenantId"=scope_tenant AND a."UserId"=scope_user AND a."IsActive" AND NOT a."IsDeleted" AND p."Code"=required.code));
$authority$;
ALTER TABLE "EncounterTransitions" ADD CONSTRAINT encounter_transition_shape CHECK(COALESCE(
 "Revision">0 AND "IsActive" AND NOT "IsDeleted" AND "CreatedBy" IS NOT NULL AND length(btrim("Reason")) BETWEEN 3 AND 1000
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND jsonb_typeof("ContentSnapshotJson"::jsonb)='object'
 AND (("Action"='start' AND "Revision"=1 AND "PreviousStatus" IS NULL AND "Status"=1 AND "EndedAtUtc" IS NULL AND "SourceAppointmentRevision">0)
 OR("Action"='update' AND "Revision">1 AND "PreviousStatus"=1 AND "Status"=1 AND "EndedAtUtc" IS NULL AND "SourceAppointmentRevision" IS NULL)
 OR("Action"='complete' AND "Revision">1 AND "PreviousStatus"=1 AND "Status"=2 AND "EndedAtUtc">="StartedAtUtc" AND "SourceAppointmentRevision" IS NULL)),false));
CREATE FUNCTION public.guard_encounter_lifecycle() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE actor uuid; source public."Appointments"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Encounter records and links are retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 actor=NULLIF(current_setting('app.current_user_id',true),'')::uuid;
 IF actor IS NULL OR NEW."DoctorUserId"<>actor OR NOT public.cureflow_encounter_actor_allowed(NEW."TenantId",actor,NEW."PatientId")
  OR NOT NEW."IsActive" OR NEW."IsDeleted" THEN RAISE EXCEPTION 'Current assigned clinician and chart authority required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  SELECT * INTO source FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" FOR UPDATE;
  IF NOT FOUND OR source."PatientId"<>NEW."PatientId" OR source."DoctorUserId"<>actor OR source."Status" NOT IN(0,1)
   OR source."CheckedInAt" IS NULL OR source."CompletedAt" IS NOT NULL OR source."ReservationId" IS NULL OR source."FacilityId" IS NULL
   OR source."IsDeleted" OR NOT source."IsActive" OR public.cureflow_lock_active_facility(NEW."TenantId",source."FacilityId") IS NULL
   OR NEW."Revision"<>1 OR NEW."Status"<>1 OR NEW."EndedAt" IS NOT NULL OR NEW."CreatedBy" IS DISTINCT FROM actor
   OR NEW."VisitType"<>'outpatient' OR NEW."DoctorNotes" IS NOT NULL OR NEW."VisitDate">clock_timestamp()
   OR EXISTS(SELECT 1 FROM public."Visits" v WHERE v."TenantId"=NEW."TenantId" AND v."AppointmentId"=NEW."AppointmentId" AND v."IsActive" AND NOT v."IsDeleted") THEN
   RAISE EXCEPTION 'Encounter start requires one exact assigned arrived appointment; existing encounters must resume or reconcile' USING ERRCODE='23514'; END IF;
 ELSE
  IF OLD."Status"<>1 OR OLD."EndedAt" IS NOT NULL OR NEW."Revision"<>OLD."Revision"+1 OR NEW."UpdatedBy" IS DISTINCT FROM actor
   OR NEW."Status" NOT IN(1,2) OR(NEW."Status"=1 AND NEW."EndedAt" IS NOT NULL)
   OR(NEW."Status"=2 AND(NEW."EndedAt" IS NULL OR NEW."EndedAt"<NEW."VisitDate" OR NEW."EndedAt">clock_timestamp()))
   OR(to_jsonb(OLD)-ARRAY['Symptoms','Diagnosis','FollowUpAdvice','FollowUpDate','Status','EndedAt','Revision','UpdatedAt','UpdatedBy'])
     IS DISTINCT FROM(to_jsonb(NEW)-ARRAY['Symptoms','Diagnosis','FollowUpAdvice','FollowUpDate','Status','EndedAt','Revision','UpdatedAt','UpdatedBy']) THEN
   RAISE EXCEPTION 'Encounter changes need exact next version, open state and retained source identity' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER encounter_lifecycle_guard BEFORE INSERT OR UPDATE OR DELETE ON "Visits" FOR EACH ROW EXECUTE FUNCTION public.guard_encounter_lifecycle();
CREATE FUNCTION public.guard_encounter_transition() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE visit public."Visits"%ROWTYPE; actor uuid;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Encounter transition evidence is immutable' USING ERRCODE='23514'; END IF;
 actor=NULLIF(current_setting('app.current_user_id',true),'')::uuid;
 SELECT * INTO visit FROM public."Visits" v WHERE v."TenantId"=NEW."TenantId" AND v."Id"=NEW."VisitId";
 IF NOT FOUND OR NEW."CreatedBy" IS DISTINCT FROM actor OR visit."DoctorUserId" IS DISTINCT FROM actor
  OR NOT public.cureflow_encounter_actor_allowed(NEW."TenantId",actor,NEW."PatientId")
  OR(NEW."PatientId",NEW."AppointmentId",NEW."Revision",NEW."Status",NEW."StartedAtUtc",NEW."EndedAtUtc")
   IS DISTINCT FROM(visit."PatientId",visit."AppointmentId",visit."Revision",visit."Status",visit."VisitDate",visit."EndedAt")
  OR(NEW."ContentSnapshotJson"::jsonb->>'Symptoms') IS DISTINCT FROM visit."Symptoms"
  OR(NEW."ContentSnapshotJson"::jsonb->>'Diagnosis') IS DISTINCT FROM visit."Diagnosis"
  OR(NEW."ContentSnapshotJson"::jsonb->>'FollowUpAdvice') IS DISTINCT FROM visit."FollowUpAdvice"
  OR(NEW."ContentSnapshotJson"::jsonb->>'FollowUpDate')::timestamptz IS DISTINCT FROM visit."FollowUpDate" THEN
  RAISE EXCEPTION 'Encounter evidence must match exact attributable state and content revision' USING ERRCODE='23514'; END IF;
 IF NEW."Action"='start' AND(NOT EXISTS(SELECT 1 FROM public."Visits" fresh WHERE fresh."TenantId"=visit."TenantId" AND fresh."Id"=visit."Id"
   AND fresh.xmin::text::bigint=(txid_current()%4294967296)) OR NOT EXISTS(
  SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=visit."TenantId" AND a."Id"=visit."AppointmentId" AND a."PatientId"=visit."PatientId"
   AND a."DoctorUserId"=visit."DoctorUserId" AND a."Revision"=NEW."SourceAppointmentRevision" AND a."CheckedInAt" IS NOT NULL AND a."Status" IN(0,1))) THEN
  RAISE EXCEPTION 'Start evidence pins the exact current arrived source; legacy start history cannot be fabricated' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER encounter_transition_guard BEFORE INSERT OR UPDATE OR DELETE ON "EncounterTransitions" FOR EACH ROW EXECUTE FUNCTION public.guard_encounter_transition();
CREATE FUNCTION public.require_encounter_transition_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public."EncounterTransitions" t WHERE t."TenantId"=NEW."TenantId" AND t."VisitId"=NEW."Id" AND t."Revision"=NEW."Revision"
  AND(t."PatientId",t."AppointmentId",t."Status",t."StartedAtUtc",t."EndedAtUtc") IS NOT DISTINCT FROM(NEW."PatientId",NEW."AppointmentId",NEW."Status",NEW."VisitDate",NEW."EndedAt")) THEN
  RAISE EXCEPTION 'Encounter state and immutable transition must commit together' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER encounter_transition_commit AFTER INSERT OR UPDATE ON "Visits" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_encounter_transition_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "Visits","EncounterTransitions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "Visits","EncounterTransitions" FROM cureflow_tenant;
 GRANT UPDATE("Symptoms","Diagnosis","FollowUpAdvice","FollowUpDate","Status","EndedAt","Revision","UpdatedAt","UpdatedBy") ON "Visits" TO cureflow_tenant;
END IF; END $grant$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261012113647_AddRetainedEncounterLifecycle', '10.0.0');

COMMIT;

