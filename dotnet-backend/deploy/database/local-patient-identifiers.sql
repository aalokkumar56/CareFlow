START TRANSACTION;
ALTER TABLE "Patients" ADD CONSTRAINT "AK_Patients_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "PatientIdentifierCounters" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "Namespace" character varying(64) NOT NULL,
    "CurrentValue" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentifierCounters" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentifierCounters_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "PatientIdentifiers" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Namespace" character varying(64) NOT NULL,
    "Value" character varying(64) NOT NULL,
    "Sequence" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentifiers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentifiers_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientIdentifiers_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientIdentifierCounters_TenantId_FacilityId_Namespace" ON "PatientIdentifierCounters" ("TenantId", "FacilityId", "Namespace");

CREATE UNIQUE INDEX "IX_PatientIdentifiers_TenantId_FacilityId_Namespace_PatientId" ON "PatientIdentifiers" ("TenantId", "FacilityId", "Namespace", "PatientId");

CREATE UNIQUE INDEX "IX_PatientIdentifiers_TenantId_FacilityId_Namespace_Value" ON "PatientIdentifiers" ("TenantId", "FacilityId", "Namespace", "Value");

CREATE INDEX "IX_PatientIdentifiers_TenantId_PatientId" ON "PatientIdentifiers" ("TenantId", "PatientId");

DO $rls$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['PatientIdentifiers','PatientIdentifierCounters'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',t);
  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',t);
  EXECUTE format('CREATE POLICY identifier_tenant ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',t);
END LOOP; END $rls$;
ALTER TABLE "PatientIdentifierCounters" ADD CONSTRAINT local_counter_valid CHECK ("Namespace"='local-mrn' AND "CurrentValue">0 AND NOT "IsDeleted");
ALTER TABLE "PatientIdentifiers" ADD CONSTRAINT local_identifier_valid CHECK ("Namespace"='local-mrn' AND "Sequence">0 AND NOT "IsDeleted"
  AND "Value"='MRN-'||CASE WHEN length("Sequence"::text)<10 THEN lpad("Sequence"::text,10,'0') ELSE "Sequence"::text END);
CREATE FUNCTION enforce_identifier_counter() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Identifier counters cannot be deleted' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required for namespace counter' USING ERRCODE='23514'; END IF;
  IF NOT NEW."IsActive" THEN RAISE EXCEPTION 'Namespace counter cannot be disabled/reset' USING ERRCODE='23514'; END IF;
  IF TG_OP='INSERT' AND NEW."CurrentValue"<>1 THEN RAISE EXCEPTION 'Counter must begin at one' USING ERRCODE='23514'; END IF;
  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId" OR NEW."Namespace"<>OLD."Namespace"
    OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR NEW."CurrentValue"<>OLD."CurrentValue"+1) THEN
    RAISE EXCEPTION 'Identifier counter cannot reset or change namespace' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER identifier_counter_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientIdentifierCounters" FOR EACH ROW EXECUTE FUNCTION enforce_identifier_counter();
CREATE FUNCTION prevent_identifier_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Issued identifiers are immutable and cannot be reused' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER identifier_immutable BEFORE UPDATE OR DELETE ON "PatientIdentifiers" FOR EACH ROW EXECUTE FUNCTION prevent_identifier_rewrite();
CREATE FUNCTION validate_identifier_allocation() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE current_value bigint;
BEGIN
  SELECT "CurrentValue" INTO current_value FROM "PatientIdentifierCounters" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Namespace"=NEW."Namespace" FOR SHARE;
  IF current_value IS DISTINCT FROM NEW."Sequence" THEN RAISE EXCEPTION 'Identifier requires current allocated counter value' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active patient required' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER identifier_allocation_guard BEFORE INSERT ON "PatientIdentifiers" FOR EACH ROW EXECUTE FUNCTION validate_identifier_allocation();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientIdentifiers" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientIdentifiers" FROM cureflow_tenant;
  GRANT SELECT,INSERT,UPDATE ON "PatientIdentifierCounters" TO cureflow_tenant;
  REVOKE DELETE ON "PatientIdentifierCounters" FROM cureflow_tenant;
END IF; END $grant$;
-- No retroactive MRNs, source identifier copying, resets or clinical-row identity replacement.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007133330_AddLocalPatientIdentifiers', '10.0.0');

COMMIT;

