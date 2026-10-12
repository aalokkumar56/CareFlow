START TRANSACTION;
ALTER TABLE "Patients" ADD "IdentityStatus" character varying(24) NOT NULL DEFAULT 'unreviewed';

ALTER TABLE "PatientDemographicVersions" ADD CONSTRAINT "AK_PatientDemographicVersions_TenantId_PatientId_Id" UNIQUE ("TenantId", "PatientId", "Id");

CREATE TABLE "PatientIdentityVerifications" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "DemographicVersionId" uuid NOT NULL,
    "ProofReference" character varying(200) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "PatientTag" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentityVerifications" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentityVerifications_PatientDemographicVersions_Ten~" FOREIGN KEY ("TenantId", "PatientId", "DemographicVersionId") REFERENCES "PatientDemographicVersions" ("TenantId", "PatientId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "TemporaryPatientIdentities" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "Value" character varying(40) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "SourceReference" character varying(200),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_TemporaryPatientIdentities" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TemporaryPatientIdentities_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_TemporaryPatientIdentities_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_PatientIdentityVerifications_TenantId_PatientId_CreatedAt" ON "PatientIdentityVerifications" ("TenantId", "PatientId", "CreatedAt");

CREATE INDEX "IX_PatientIdentityVerifications_TenantId_PatientId_Demographic~" ON "PatientIdentityVerifications" ("TenantId", "PatientId", "DemographicVersionId");

CREATE UNIQUE INDEX "IX_TemporaryPatientIdentities_TenantId_FacilityId_Value" ON "TemporaryPatientIdentities" ("TenantId", "FacilityId", "Value");

CREATE UNIQUE INDEX "IX_TemporaryPatientIdentities_TenantId_PatientId" ON "TemporaryPatientIdentities" ("TenantId", "PatientId");

ALTER TABLE "TemporaryPatientIdentities" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "TemporaryPatientIdentities" FORCE ROW LEVEL SECURITY;
ALTER TABLE "PatientIdentityVerifications" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientIdentityVerifications" FORCE ROW LEVEL SECURITY;
CREATE POLICY temporary_patient_tenant ON "TemporaryPatientIdentities" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_verification_tenant ON "PatientIdentityVerifications" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "Patients" ADD CONSTRAINT patient_identity_status_valid CHECK ("IdentityStatus" IN ('unknown','unreviewed','verified_local'));
ALTER TABLE "TemporaryPatientIdentities" ADD CONSTRAINT temporary_patient_valid CHECK (
  "Value"='TEMP-'||replace("PatientId"::text,'-','') AND length(btrim("Reason"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "PatientIdentityVerifications" ADD CONSTRAINT patient_verification_valid CHECK (
  length(btrim("ProofReference"))>0 AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0
  AND length("PatientTag") BETWEEN 80 AND 128 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
CREATE FUNCTION validate_temporary_patient_intake() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted")
    OR NOT EXISTS(SELECT 1 FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId"
      AND "IdentityStatus"='unknown' AND "BirthDatePrecision"='unknown' AND "Phone"='' AND "DateOfBirth" IS NULL AND "Age" IS NULL
      AND "IsActive" AND NOT "IsDeleted") THEN
    RAISE EXCEPTION 'Unknown intake requires active local facility/patient without guessed demographics' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER temporary_patient_insert BEFORE INSERT ON "TemporaryPatientIdentities" FOR EACH ROW EXECUTE FUNCTION validate_temporary_patient_intake();
CREATE FUNCTION validate_patient_local_verification() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE current_version uuid;
BEGIN
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId"
    AND "IdentityStatus"='verified_local' AND "UpdatedBy"=NEW."CreatedBy" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Current local patient/reviewer attribution required' USING ERRCODE='23514'; END IF;
  SELECT "Id" INTO current_version FROM "PatientDemographicVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId"
    ORDER BY "Revision" DESC LIMIT 1;
  IF current_version IS DISTINCT FROM NEW."DemographicVersionId" THEN
    RAISE EXCEPTION 'Current demographic source evidence required' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_verification_insert BEFORE INSERT ON "PatientIdentityVerifications" FOR EACH ROW EXECUTE FUNCTION validate_patient_local_verification();
CREATE FUNCTION prevent_patient_identity_evidence_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Temporary identity and verification evidence are retained' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER temporary_patient_history BEFORE UPDATE OR DELETE ON "TemporaryPatientIdentities"
  FOR EACH ROW EXECUTE FUNCTION prevent_patient_identity_evidence_rewrite();
CREATE TRIGGER patient_verification_history BEFORE UPDATE OR DELETE ON "PatientIdentityVerifications"
  FOR EACH ROW EXECUTE FUNCTION prevent_patient_identity_evidence_rewrite();
CREATE FUNCTION validate_patient_identity_transition() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF TG_OP='INSERT' THEN
    IF NEW."IdentityStatus"='verified_local' THEN RAISE EXCEPTION 'New registrations begin unreviewed' USING ERRCODE='23514'; END IF;
  ELSIF NEW."IdentityStatus" IS DISTINCT FROM OLD."IdentityStatus" THEN
    IF NEW."IdentityStatus"<>'verified_local' OR OLD."IdentityStatus" NOT IN ('unknown','unreviewed') OR NEW."UpdatedBy" IS NULL THEN
      RAISE EXCEPTION 'Identity workflow transition requires reviewed verification' USING ERRCODE='23514';
    END IF;
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_identity_transition BEFORE INSERT OR UPDATE ON "Patients" FOR EACH ROW EXECUTE FUNCTION validate_patient_identity_transition();
CREATE FUNCTION require_patient_identity_verification() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM "PatientIdentityVerifications" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."Id"
    AND "CreatedBy"=NEW."UpdatedBy" AND "CreatedAt">=NEW."UpdatedAt") THEN
    RAISE EXCEPTION 'Local verification requires evidence in the same transaction' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE CONSTRAINT TRIGGER patient_identity_verification_required AFTER UPDATE ON "Patients"
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
  WHEN (NEW."IdentityStatus"='verified_local' AND OLD."IdentityStatus" IS DISTINCT FROM NEW."IdentityStatus")
  EXECUTE FUNCTION require_patient_identity_verification();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('0f79b2ee-67d6-4f55-9b70-f02b3ac4b723','PatientIdentity.Verify','Review local patient identity evidence','Configured accountable local review; no portal/sharing authority',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "TemporaryPatientIdentities","PatientIdentityVerifications" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "TemporaryPatientIdentities","PatientIdentityVerifications" FROM cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007155908_AddUnknownPatientIdentityVerification', '10.0.0');

COMMIT;

