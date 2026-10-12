START TRANSACTION;
CREATE TABLE "PatientContactVersions" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "ContactType" character varying(16) NOT NULL,
    "Usage" character varying(16) NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "Value" character varying(254),
    "Relationship" character varying(100),
    "SourceReference" character varying(200),
    "VerificationStatus" character varying(16) NOT NULL,
    "Preferred" boolean NOT NULL,
    "ContactIsActive" boolean NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveUntil" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientContactVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientContactVersions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientContactVersions_TenantId_PatientId_ContactId_Revision" ON "PatientContactVersions" ("TenantId", "PatientId", "ContactId", "Revision");

ALTER TABLE "PatientContactVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientContactVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY contact_tenant ON "PatientContactVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientContactVersions" ADD CONSTRAINT local_contact_valid CHECK (
  "Revision">0 AND "ContactType" IN ('phone','email','person') AND "Usage" IN ('normal','emergency','guardian')
  AND "VerificationStatus"='unverified' AND NOT "IsDeleted" AND "IsActive" AND length(btrim("DisplayName"))>0
  AND ("EffectiveUntil" IS NULL OR "EffectiveUntil">"EffectiveFrom")
  AND ("Usage"<>'guardian' OR COALESCE(length(btrim("Relationship")),0)>0)
  AND ("ContactType"<>'phone' OR COALESCE("Value" ~ '^\+?[0-9]{5,20}$' AND length("Value")<=20,false))
  AND ("ContactType"<>'email' OR COALESCE(length(btrim("Value")),0)>0)
  AND ("ContactType"<>'person' OR COALESCE(length(btrim("Value")),0)=0));
CREATE FUNCTION validate_local_contact_append() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE previous_revision bigint;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-contacts:'||NEW."TenantId"::text||':'||NEW."PatientId"::text,0));
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active scoped patient required for contact update' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(max("Revision"),0) INTO previous_revision FROM "PatientContactVersions"
    WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "ContactId"=NEW."ContactId";
  IF NEW."Revision"<>previous_revision+1 THEN RAISE EXCEPTION 'Contact history requires a consecutive revision' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER local_contact_append BEFORE INSERT ON "PatientContactVersions" FOR EACH ROW EXECUTE FUNCTION validate_local_contact_append();
CREATE FUNCTION prevent_local_contact_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Local contact history is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER local_contact_history BEFORE UPDATE OR DELETE ON "PatientContactVersions" FOR EACH ROW EXECUTE FUNCTION prevent_local_contact_rewrite();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientContactVersions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientContactVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- No backfill, identity verification, portal proxy or global guardian authority.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007144416_AddLocalPatientContactVersions', '10.0.0');

COMMIT;

