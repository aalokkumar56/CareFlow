START TRANSACTION;
ALTER TABLE "Patients" ADD "BirthDateDay" integer;

ALTER TABLE "Patients" ADD "BirthDateMonth" integer;

ALTER TABLE "Patients" ADD "BirthDatePrecision" character varying(24) NOT NULL DEFAULT 'legacy_unspecified';

ALTER TABLE "Patients" ADD "BirthDateYear" integer;

CREATE TABLE "PatientDemographicVersions" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "SourceType" character varying(32) NOT NULL,
    "SourceReference" character varying(200),
    "Reason" character varying(500) NOT NULL,
    "VerificationStatus" character varying(24) NOT NULL,
    "BeforeValue" text,
    "AfterValue" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientDemographicVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientDemographicVersions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientDemographicVersions_TenantId_PatientId_Revision" ON "PatientDemographicVersions" ("TenantId", "PatientId", "Revision");

ALTER TABLE "PatientDemographicVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientDemographicVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY patient_demographic_tenant ON "PatientDemographicVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientDemographicVersions" ADD CONSTRAINT patient_demographic_valid CHECK (
  "Revision">0 AND "VerificationStatus"='unverified' AND "SourceType" IN ('local_application','patient_reported','guardian_reported','document_reported','staff_observed','import')
  AND length(btrim("Reason"))>0 AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "Patients" ADD CONSTRAINT patient_birth_precision_valid CHECK (COALESCE((
  ("BirthDatePrecision" IN ('legacy_unspecified','unknown') AND "BirthDateYear" IS NULL AND "BirthDateMonth" IS NULL AND "BirthDateDay" IS NULL)
  OR ("BirthDatePrecision" IN ('year','estimated_year') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" IS NULL AND "BirthDateDay" IS NULL)
  OR ("BirthDatePrecision" IN ('month','estimated_month') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" BETWEEN 1 AND 12 AND "BirthDateDay" IS NULL)
  OR ("BirthDatePrecision" IN ('day','estimated_day') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" BETWEEN 1 AND 12 AND "BirthDateDay" BETWEEN 1 AND 31)),false));
ALTER TABLE "Patients" ADD CONSTRAINT patient_calendar_birth_valid CHECK (
  CASE WHEN "BirthDatePrecision" IN ('day','estimated_day') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" BETWEEN 1 AND 12 THEN
    "BirthDateDay"<=EXTRACT(DAY FROM (make_date("BirthDateYear","BirthDateMonth",1)+INTERVAL '1 month'-INTERVAL '1 day'))
    ELSE "BirthDatePrecision" NOT IN ('day','estimated_day') END);
ALTER TABLE "Patients" ADD CONSTRAINT patient_birth_representations_valid CHECK ("BirthDatePrecision"='legacy_unspecified' OR "DateOfBirth" IS NULL);
CREATE FUNCTION capture_patient_demographic_change() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
  SET search_path=pg_catalog,public,pg_temp AS $body$
DECLARE before_value jsonb; after_value jsonb; excluded text[]; next_revision bigint; source_type text; source_ref text; reason text;
BEGIN
  excluded=ARRAY['IdentityStatus','TenantId','Id','CreatedAt','UpdatedAt','CreatedBy','UpdatedBy','IsActive','IsDeleted','SourceLeadId','EmailNotificationsEnabled',
    'Department','InquirySource','ReferralDoctor','ReferringDoctorId','AssignedStaffId','Status','Tags','Notes','LastContactAt','FollowUpDate','AiSummary','AiLeadScore'];
  after_value=to_jsonb(NEW)-excluded;
  IF TG_OP='UPDATE' THEN before_value=to_jsonb(OLD)-excluded; END IF;
  source_type=COALESCE(NULLIF(current_setting('app.demographic_source_type',true),''),'local_application');
  source_ref=NULLIF(current_setting('app.demographic_source_reference',true),'');
  reason=COALESCE(NULLIF(current_setting('app.demographic_reason',true),''),'Local application demographic write');
  -- Consume local metadata even when demographics did not change, avoiding source reuse by a later command.
  PERFORM set_config('app.demographic_source_type','',true);
  PERFORM set_config('app.demographic_source_reference','',true);
  PERFORM set_config('app.demographic_reason','',true);
  IF TG_OP='UPDATE' AND before_value=after_value THEN RETURN NEW; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-demographics:'||NEW."TenantId"::text||':'||NEW."Id"::text,0));
  SELECT COALESCE(MAX("Revision"),0)+1 INTO next_revision FROM public."PatientDemographicVersions"
    WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."Id";
  INSERT INTO public."PatientDemographicVersions" ("Id","TenantId","PatientId","Revision","SourceType","SourceReference","Reason","VerificationStatus",
    "BeforeValue","AfterValue","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
    VALUES (gen_random_uuid(),NEW."TenantId",NEW."Id",next_revision,source_type,source_ref,reason,'unverified',before_value::text,after_value::text,
      CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CASE WHEN TG_OP='INSERT' THEN NEW."CreatedBy" ELSE NEW."UpdatedBy" END,NULL,true,false);
  RETURN NEW;
END $body$;
REVOKE ALL ON FUNCTION capture_patient_demographic_change() FROM PUBLIC;
CREATE TRIGGER patient_demographic_capture AFTER INSERT OR UPDATE ON "Patients" FOR EACH ROW EXECUTE FUNCTION capture_patient_demographic_change();
CREATE FUNCTION prevent_patient_demographic_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Demographic evidence is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER patient_demographic_history BEFORE UPDATE OR DELETE ON "PatientDemographicVersions"
  FOR EACH ROW EXECUTE FUNCTION prevent_patient_demographic_rewrite();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT ON "PatientDemographicVersions" TO cureflow_tenant;
  REVOKE INSERT,UPDATE,DELETE ON "PatientDemographicVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- Existing DOB timestamps, patient IDs and source associations are unchanged; no historical evidence is fabricated.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007154947_AddPatientDemographicProvenance', '10.0.0');

COMMIT;

