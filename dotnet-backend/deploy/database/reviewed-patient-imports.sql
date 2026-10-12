START TRANSACTION;
CREATE TABLE "PatientImportBatches" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "FileSha256" character varying(64) NOT NULL,
    "Format" character varying(8) NOT NULL,
    "FileLength" bigint NOT NULL,
    "SourceObjectKey" character varying(500) NOT NULL,
    "SourceObjectVersion" uuid NOT NULL,
    "MappingJson" text NOT NULL,
    "MappingFingerprint" character varying(64) NOT NULL,
    "ScanEngine" character varying(100) NOT NULL,
    "ScanEngineVersion" character varying(200) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "ParseErrorCode" character varying(100),
    "RowCount" integer NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientImportBatches" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_PatientImportBatches_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_PatientImportBatches_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "PatientImportRows" (
    "Id" uuid NOT NULL,
    "BatchId" uuid NOT NULL,
    "RowNumber" integer NOT NULL,
    "RawValuesJson" text NOT NULL,
    "DraftJson" text,
    "ErrorsJson" text NOT NULL,
    "RepeatedFileRow" boolean NOT NULL,
    "SourceHash" character varying(64) NOT NULL,
    "Decision" character varying(20) NOT NULL,
    "PatientId" uuid,
    "ReviewReason" character varying(500),
    "ReviewedBy" uuid,
    "ReviewedAt" timestamp with time zone,
    "ReviewPolicyVersion" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientImportRows" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientImportRows_PatientImportBatches_TenantId_BatchId" FOREIGN KEY ("TenantId", "BatchId") REFERENCES "PatientImportBatches" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientImportRows_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientImportBatches_TenantId_FacilityId_FileSha256_Mapping~" ON "PatientImportBatches" ("TenantId", "FacilityId", "FileSha256", "MappingFingerprint");

CREATE UNIQUE INDEX "IX_PatientImportRows_TenantId_BatchId_RowNumber" ON "PatientImportRows" ("TenantId", "BatchId", "RowNumber");

CREATE INDEX "IX_PatientImportRows_TenantId_PatientId" ON "PatientImportRows" ("TenantId", "PatientId");

ALTER TABLE "PatientImportBatches" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientImportBatches" FORCE ROW LEVEL SECURITY;
ALTER TABLE "PatientImportRows" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientImportRows" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "PatientImportBatches" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY tenant_scope ON "PatientImportRows" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY import_owner ON "PatientImportBatches" AS RESTRICTIVE FOR ALL USING ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid) WITH CHECK ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid);
CREATE POLICY import_owner ON "PatientImportRows" AS RESTRICTIVE FOR ALL USING ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid) WITH CHECK ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid);
CREATE POLICY patient_confidentiality_read ON "PatientImportRows" AS RESTRICTIVE FOR SELECT USING ("PatientId" IS NULL OR public.cureflow_patient_read_allowed("TenantId","PatientId"));
ALTER TABLE "PatientImportBatches" ADD CONSTRAINT import_source_shape CHECK (
 "Format" IN ('csv','xls','xlsx') AND "FileLength" BETWEEN 1 AND 8388608 AND "RowCount" BETWEEN 1 AND 5000
 AND "FileSha256" ~ '^[0-9A-F]{64}$' AND "MappingFingerprint" ~ '^[0-9A-F]{64}$'
 AND length("SourceObjectKey") BETWEEN 1 AND 500 AND length("PolicyVersion") BETWEEN 1 AND 100
 AND length("ScanEngine") BETWEEN 1 AND 100 AND length("ScanEngineVersion") BETWEEN 1 AND 200
 AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '87600 hours'
 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "PatientImportRows" ADD CONSTRAINT import_row_shape CHECK (
 "RowNumber" BETWEEN 2 AND 5005 AND "SourceHash" ~ '^[0-9A-F]{64}$' AND length("RawValuesJson")<=1048576
 AND jsonb_typeof("RawValuesJson"::jsonb)='array' AND jsonb_typeof("ErrorsJson"::jsonb)='array'
 AND ("DraftJson" IS NULL OR jsonb_typeof("DraftJson"::jsonb)='object') AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted"
 AND (("Decision"='pending' AND "PatientId" IS NULL AND "ReviewReason" IS NULL AND "ReviewedBy" IS NULL AND "ReviewedAt" IS NULL AND "ReviewPolicyVersion" IS NULL)
 OR ("Decision" IN ('skip','create_new') AND ("Decision"='create_new')=("PatientId" IS NOT NULL)
 AND length(btrim("ReviewReason")) BETWEEN 1 AND 500 AND "ReviewedBy" IS NOT NULL AND "ReviewedAt" IS NOT NULL AND length("ReviewPolicyVersion") BETWEEN 1 AND 100)));
CREATE FUNCTION public.guard_patient_import_batch() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Original import source evidence is immutable' USING ERRCODE='23514'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public."Facilities" f WHERE f."TenantId"=NEW."TenantId" AND f."Id"=NEW."FacilityId" AND f."IsActive" AND NOT f."IsDeleted") THEN
  RAISE EXCEPTION 'Import requires current active facility' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER patient_import_batch_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientImportBatches" FOR EACH ROW EXECUTE FUNCTION public.guard_patient_import_batch();
CREATE FUNCTION public.guard_patient_import_row() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE batch public."PatientImportBatches"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Original import row evidence is retained' USING ERRCODE='23514'; END IF;
 SELECT * INTO batch FROM public."PatientImportBatches" b WHERE b."TenantId"=NEW."TenantId" AND b."Id"=NEW."BatchId";
 IF NOT FOUND OR batch."CreatedBy" IS DISTINCT FROM NEW."CreatedBy" OR batch."ExpiresAt"<=clock_timestamp() THEN
  RAISE EXCEPTION 'Import batch scope or review window invalid' USING ERRCODE='23514';
 END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."Decision"<>'pending' THEN RAISE EXCEPTION 'Import rows start pending' USING ERRCODE='23514'; END IF;
  IF (SELECT count(*) FROM public."PatientImportRows" r WHERE r."TenantId"=NEW."TenantId" AND r."BatchId"=NEW."BatchId")>=batch."RowCount" THEN
   RAISE EXCEPTION 'Import row count exceeds original source' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."Decision"<>'pending' OR NEW."Decision" NOT IN ('create_new','skip')
    OR (to_jsonb(OLD)-ARRAY['Decision','PatientId','ReviewReason','ReviewedBy','ReviewedAt','ReviewPolicyVersion','UpdatedAt','UpdatedBy'])
       IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['Decision','PatientId','ReviewReason','ReviewedBy','ReviewedAt','ReviewPolicyVersion','UpdatedAt','UpdatedBy'])
    OR NEW."ReviewedBy" IS DISTINCT FROM NEW."CreatedBy" OR NEW."UpdatedBy" IS DISTINCT FROM NEW."CreatedBy" THEN
   RAISE EXCEPTION 'Import decision is terminal; original row evidence cannot change' USING ERRCODE='23514';
  END IF;
  IF NEW."Decision"='create_new' AND (NEW."DraftJson" IS NULL OR jsonb_array_length(NEW."ErrorsJson"::jsonb)>0 OR NOT EXISTS(
   SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=NEW."PatientId" AND p."IsActive" AND NOT p."IsDeleted")) THEN
   RAISE EXCEPTION 'Create requires valid retained row and current patient scope' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER patient_import_row_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientImportRows" FOR EACH ROW EXECUTE FUNCTION public.guard_patient_import_row();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "PatientImportBatches","PatientImportRows" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientImportBatches","PatientImportRows" FROM cureflow_tenant;
 GRANT UPDATE ("Decision","PatientId","ReviewReason","ReviewedBy","ReviewedAt","ReviewPolicyVersion","UpdatedAt","UpdatedBy") ON "PatientImportRows" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008175020_AddReviewedPatientImports', '10.0.0');

COMMIT;

