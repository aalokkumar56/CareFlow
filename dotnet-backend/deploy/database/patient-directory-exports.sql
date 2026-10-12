START TRANSACTION;
CREATE TABLE "PatientExportJobs" (
    "Id" uuid NOT NULL,
    "RequesterId" uuid NOT NULL,
    "PatientIdsJson" text NOT NULL,
    "PatientTagsJson" text NOT NULL,
    "FieldsJson" text NOT NULL,
    "Purpose" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "State" character varying(20) NOT NULL,
    "Revision" bigint NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "ObjectKey" character varying(500),
    "ObjectVersion" uuid,
    "Sha256" character varying(64),
    "Length" bigint,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientExportJobs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientExportJobs_Users_RequesterId" FOREIGN KEY ("RequesterId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_PatientExportJobs_RequesterId" ON "PatientExportJobs" ("RequesterId");

CREATE INDEX "IX_PatientExportJobs_TenantId_RequesterId_CreatedAt" ON "PatientExportJobs" ("TenantId", "RequesterId", "CreatedAt");

ALTER TABLE "PatientExportJobs" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientExportJobs" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "PatientExportJobs" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY export_owner ON "PatientExportJobs" AS RESTRICTIVE FOR SELECT USING (
 NULLIF(current_setting('app.current_user_id',true),'') IS NULL OR "RequesterId"=NULLIF(current_setting('app.current_user_id',true),'')::uuid);
CREATE POLICY patient_confidentiality_read ON "PatientExportJobs" AS RESTRICTIVE FOR SELECT USING (
 NOT EXISTS(SELECT 1 FROM jsonb_array_elements_text("PatientIdsJson"::jsonb) id WHERE NOT public.cureflow_patient_read_allowed("TenantId",id.value::uuid)));
ALTER TABLE "PatientExportJobs" ADD CONSTRAINT export_shape CHECK (COALESCE(
 jsonb_typeof("PatientIdsJson"::jsonb)='array' AND jsonb_array_length("PatientIdsJson"::jsonb) BETWEEN 1 AND 200
 AND jsonb_typeof("PatientTagsJson"::jsonb)='object' AND jsonb_typeof("FieldsJson"::jsonb)='array' AND jsonb_array_length("FieldsJson"::jsonb) BETWEEN 1 AND 6
 AND length("PatientIdsJson")<=10000 AND length("PatientTagsJson")<=40000 AND length("FieldsJson")<=200
 AND length(btrim("Purpose")) BETWEEN 1 AND 500 AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "CreatedBy"="RequesterId" AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '24 hours' AND "IsActive" AND NOT "IsDeleted"
 AND (("State"='pending' AND "Revision"=1 AND "ObjectKey" IS NULL AND "ObjectVersion" IS NULL AND "Sha256" IS NULL AND "Length" IS NULL)
  OR ("State"='ready' AND "Revision"=2 AND length("ObjectKey") BETWEEN 1 AND 500 AND "ObjectVersion" IS NOT NULL AND "Sha256" ~ '^[0-9A-F]{64}$' AND "Length" BETWEEN 1 AND 2097152)),false));
CREATE FUNCTION public.guard_patient_export_job() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE selected uuid; field text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Export receipt/provenance is retained' USING ERRCODE='23514'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."RequesterId" AND u."IsActive" AND NOT u."IsDeleted")
  OR NEW."ExpiresAt"<=clock_timestamp() THEN RAISE EXCEPTION 'Export requester/review window invalid' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."State"<>'pending' OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
   RAISE EXCEPTION 'Export requires authenticated requester and pending state' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'pending' OR NEW."State"<>'ready' OR NEW."Revision"<>2
   OR (to_jsonb(OLD)-ARRAY['State','Revision','ObjectKey','ObjectVersion','Sha256','Length','UpdatedAt'])
      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','ObjectKey','ObjectVersion','Sha256','Length','UpdatedAt']) THEN
   RAISE EXCEPTION 'Export source is immutable and artifact commit is terminal' USING ERRCODE='23514';
  END IF;
 END IF;
 IF (SELECT count(*) FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb))<>
    (SELECT count(DISTINCT value) FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb)) THEN
  RAISE EXCEPTION 'Export scope cannot repeat patients' USING ERRCODE='23514';
 END IF;
 FOR selected IN SELECT value::uuid FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb) LOOP
  IF NOT EXISTS(SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=selected AND p."IsActive" AND NOT p."IsDeleted"
    AND NOT EXISTS(SELECT 1 FROM public."PatientConfidentialityStates" s WHERE s."TenantId"=p."TenantId" AND s."PatientId"=p."Id" AND s."Restricted"))
    OR NOT (NEW."PatientTagsJson"::jsonb ? selected::text) THEN
   RAISE EXCEPTION 'Export patient scope is not current unrestricted scope' USING ERRCODE='23514';
  END IF;
 END LOOP;
 FOR field IN SELECT value FROM jsonb_array_elements_text(NEW."FieldsJson"::jsonb) LOOP
  IF field NOT IN ('patient_id','name','phone','email','age','gender') THEN RAISE EXCEPTION 'Unsupported directory export field' USING ERRCODE='23514'; END IF;
 END LOOP;
 RETURN NEW;
END $guard$;
CREATE TRIGGER patient_export_job_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientExportJobs" FOR EACH ROW EXECUTE FUNCTION public.guard_patient_export_job();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "PatientExportJobs" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientExportJobs" FROM cureflow_tenant;
 GRANT UPDATE ("State","Revision","ObjectKey","ObjectVersion","Sha256","Length","UpdatedAt") ON "PatientExportJobs" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008180009_AddPatientDirectoryExports', '10.0.0');

COMMIT;

