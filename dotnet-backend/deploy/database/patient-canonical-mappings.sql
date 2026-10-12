START TRANSACTION;
ALTER TABLE "PatientIdentityReviewVersions" ADD CONSTRAINT "AK_PatientIdentityReviewVersions_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "PatientMerges" (
    "Id" uuid NOT NULL,
    "SourcePatientId" uuid NOT NULL,
    "SurvivorPatientId" uuid NOT NULL,
    "ReviewVersionId" uuid NOT NULL,
    "SourcePatientTag" character varying(128) NOT NULL,
    "SurvivorPatientTag" character varying(128) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "MergedAt" timestamp with time zone NOT NULL,
    "Revision" bigint NOT NULL,
    "ReversedAt" timestamp with time zone,
    "ReversalReason" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientMerges" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_PatientMerges_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_PatientMerges_PatientIdentityReviewVersions_TenantId_Review~" FOREIGN KEY ("TenantId", "ReviewVersionId") REFERENCES "PatientIdentityReviewVersions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientMerges_Patients_TenantId_SourcePatientId" FOREIGN KEY ("TenantId", "SourcePatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientMerges_Patients_TenantId_SurvivorPatientId" FOREIGN KEY ("TenantId", "SurvivorPatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "PatientLinkJournals" (
    "Id" uuid NOT NULL,
    "MergeId" uuid NOT NULL,
    "Operation" character varying(16) NOT NULL,
    "EntityType" character varying(80) NOT NULL,
    "EntityId" uuid NOT NULL,
    "BeforeValue" character varying(1000) NOT NULL,
    "AfterValue" character varying(1000) NOT NULL,
    "AppliedAt" timestamp with time zone NOT NULL,
    "PublicationHold" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientLinkJournals" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientLinkJournals_PatientMerges_TenantId_MergeId" FOREIGN KEY ("TenantId", "MergeId") REFERENCES "PatientMerges" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientLinkJournals_TenantId_MergeId_Operation" ON "PatientLinkJournals" ("TenantId", "MergeId", "Operation");

CREATE INDEX "IX_PatientMerges_TenantId_ReviewVersionId" ON "PatientMerges" ("TenantId", "ReviewVersionId");

CREATE UNIQUE INDEX "IX_PatientMerges_TenantId_SourcePatientId" ON "PatientMerges" ("TenantId", "SourcePatientId") WHERE "ReversedAt" IS NULL;

CREATE INDEX "IX_PatientMerges_TenantId_SurvivorPatientId" ON "PatientMerges" ("TenantId", "SurvivorPatientId");

ALTER TABLE "PatientMerges" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientMerges" FORCE ROW LEVEL SECURITY;
ALTER TABLE "PatientLinkJournals" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientLinkJournals" FORCE ROW LEVEL SECURITY;
CREATE POLICY patient_merge_tenant ON "PatientMerges" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_link_journal_tenant ON "PatientLinkJournals" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientMerges" ADD CONSTRAINT patient_merge_valid CHECK (
  "SourcePatientId"<>"SurvivorPatientId" AND "Revision"=1 AND "ReversedAt" IS NULL AND "ReversalReason" IS NULL
  AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "PatientLinkJournals" ADD CONSTRAINT patient_link_valid CHECK (
  "Operation"='merge' AND "EntityType"='PatientIdentityMapping' AND NOT "PublicationHold"
  AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
CREATE FUNCTION validate_patient_merge_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE patient_count bigint; evidence record; ancestor_depth integer; first_id uuid; second_id uuid;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-mapping:'||NEW."TenantId"::text,0));
  first_id=LEAST(NEW."SourcePatientId",NEW."SurvivorPatientId");
  second_id=GREATEST(NEW."SourcePatientId",NEW."SurvivorPatientId");
  PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||first_id::text||':'||second_id::text,0));
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id" IN (first_id,second_id)
    AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE;
  GET DIAGNOSTICS patient_count=ROW_COUNT;
  IF patient_count<>2 THEN RAISE EXCEPTION 'Two active scoped identities required' USING ERRCODE='23514'; END IF;
  SELECT * INTO evidence FROM "PatientIdentityReviewVersions"
    WHERE "TenantId"=NEW."TenantId" AND "FirstPatientId"=first_id AND "SecondPatientId"=second_id
    ORDER BY "Revision" DESC LIMIT 1 FOR SHARE;
  IF evidence."Id" IS DISTINCT FROM NEW."ReviewVersionId" OR evidence."Decision"<>'potential_same'
    OR evidence."CreatedBy" IS NULL OR evidence."CreatedBy"=NEW."CreatedBy"
    OR evidence."FirstPatientTag" IS DISTINCT FROM (CASE WHEN first_id=NEW."SourcePatientId" THEN NEW."SourcePatientTag" ELSE NEW."SurvivorPatientTag" END)
    OR evidence."SecondPatientTag" IS DISTINCT FROM (CASE WHEN second_id=NEW."SourcePatientId" THEN NEW."SourcePatientTag" ELSE NEW."SurvivorPatientTag" END) THEN
    RAISE EXCEPTION 'Current distinct review evidence required' USING ERRCODE='23514';
  END IF;
  IF EXISTS(SELECT 1 FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId"
    AND "SourcePatientId" IN (first_id,second_id) AND "ReversedAt" IS NULL) THEN
    RAISE EXCEPTION 'Explicit canonical endpoints required' USING ERRCODE='23514';
  END IF;
  WITH RECURSIVE ancestors(id,depth) AS (
    SELECT NEW."SourcePatientId",0 UNION ALL SELECT m."SourcePatientId",a.depth+1 FROM "PatientMerges" m JOIN ancestors a ON m."SurvivorPatientId"=a.id
    WHERE m."TenantId"=NEW."TenantId" AND m."ReversedAt" IS NULL AND a.depth<32)
    SELECT COALESCE(MAX(depth),0) INTO ancestor_depth FROM ancestors;
  IF ancestor_depth>=31 THEN RAISE EXCEPTION 'Mapping depth requires review' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_merge_insert BEFORE INSERT ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION validate_patient_merge_insert();
CREATE FUNCTION prevent_patient_mapping_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Patient mapping evidence is retained' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER patient_merge_history BEFORE UPDATE OR DELETE ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION prevent_patient_mapping_rewrite();
CREATE TRIGGER patient_link_history BEFORE UPDATE OR DELETE ON "PatientLinkJournals" FOR EACH ROW EXECUTE FUNCTION prevent_patient_mapping_rewrite();
CREATE FUNCTION validate_patient_link_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE mapping record;
BEGIN
  SELECT * INTO mapping FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."MergeId";
  IF mapping."Id" IS NULL OR NEW."EntityId"<>mapping."SourcePatientId" OR NEW."CreatedBy"<>mapping."CreatedBy"
     OR NEW."BeforeValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SourcePatientId"::text
     OR NEW."AfterValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SurvivorPatientId"::text THEN
    RAISE EXCEPTION 'Exact retained mapping journal required' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_link_insert BEFORE INSERT ON "PatientLinkJournals" FOR EACH ROW EXECUTE FUNCTION validate_patient_link_insert();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('d06f1860-b8fb-4138-b8be-b807fa51e21d','IdentityMerge.View','Resolve explicit local identity mappings','No automatic chart aggregation',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('d4f147f7-6835-4f59-b94f-3fcf3816941d','IdentityMerge.Manage','Apply reviewed local identity mappings','Distinct review and retained source IDs',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientMerges","PatientLinkJournals" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientMerges","PatientLinkJournals" FROM cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007153620_AddPatientCanonicalMappings', '10.0.0');

COMMIT;

