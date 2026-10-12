START TRANSACTION;
CREATE TABLE "PatientIdentityReviewVersions" (
    "Id" uuid NOT NULL,
    "ReviewId" uuid NOT NULL,
    "FirstPatientId" uuid NOT NULL,
    "SecondPatientId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "Decision" character varying(32) NOT NULL,
    "EvidenceReference" character varying(200) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "FirstPatientTag" character varying(128) NOT NULL,
    "SecondPatientTag" character varying(128) NOT NULL,
    "ExactNameSignal" boolean NOT NULL,
    "SharedPhoneSignal" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentityReviewVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentityReviewVersions_Patients_TenantId_FirstPatien~" FOREIGN KEY ("TenantId", "FirstPatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientIdentityReviewVersions_Patients_TenantId_SecondPatie~" FOREIGN KEY ("TenantId", "SecondPatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientIdentityReviewVersions_TenantId_FirstPatientId_Secon~" ON "PatientIdentityReviewVersions" ("TenantId", "FirstPatientId", "SecondPatientId", "Revision");

CREATE UNIQUE INDEX "IX_PatientIdentityReviewVersions_TenantId_ReviewId_Revision" ON "PatientIdentityReviewVersions" ("TenantId", "ReviewId", "Revision");

CREATE INDEX "IX_PatientIdentityReviewVersions_TenantId_SecondPatientId" ON "PatientIdentityReviewVersions" ("TenantId", "SecondPatientId");

ALTER TABLE "PatientIdentityReviewVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientIdentityReviewVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY identity_review_tenant ON "PatientIdentityReviewVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientIdentityReviewVersions" ADD CONSTRAINT identity_review_valid CHECK (
  "FirstPatientId"<"SecondPatientId" AND "Revision">0 AND "Decision" IN ('open','different','potential_same','inconclusive')
  AND length(btrim("EvidenceReference"))>0 AND length(btrim("Reason"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
CREATE FUNCTION validate_identity_review_append() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE previous_revision bigint; previous_review uuid; patient_count bigint;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||NEW."FirstPatientId"::text||':'||NEW."SecondPatientId"::text,0));
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id" IN (NEW."FirstPatientId",NEW."SecondPatientId") AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE;
  GET DIAGNOSTICS patient_count=ROW_COUNT;
  IF patient_count<>2 THEN RAISE EXCEPTION 'Two active scoped identities required' USING ERRCODE='23514'; END IF;
  SELECT "Revision","ReviewId" INTO previous_revision,previous_review FROM "PatientIdentityReviewVersions"
    WHERE "TenantId"=NEW."TenantId" AND "FirstPatientId"=NEW."FirstPatientId" AND "SecondPatientId"=NEW."SecondPatientId" ORDER BY "Revision" DESC LIMIT 1;
  IF previous_revision IS NULL THEN
    IF NEW."Revision"<>1 OR NEW."Decision"<>'open' THEN RAISE EXCEPTION 'New pair reviews begin open' USING ERRCODE='23514'; END IF;
  ELSIF NEW."Revision"<>previous_revision+1 OR NEW."ReviewId"<>previous_review THEN
    RAISE EXCEPTION 'Review identity and consecutive evidence must be retained' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER identity_review_append BEFORE INSERT ON "PatientIdentityReviewVersions" FOR EACH ROW EXECUTE FUNCTION validate_identity_review_append();
CREATE FUNCTION prevent_identity_review_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Identity review evidence is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER identity_review_history BEFORE UPDATE OR DELETE ON "PatientIdentityReviewVersions" FOR EACH ROW EXECUTE FUNCTION prevent_identity_review_rewrite();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('849fbf4f-cae0-470d-9011-2c4956a36d39','IdentityReview.View','Read local identity reviews','Minimum fields and protected review evidence',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('d7114e87-3a89-43be-98e4-b20bfd182c87','IdentityReview.Manage','Manage local identity reviews','Explicit candidate review without merge authority',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientIdentityReviewVersions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientIdentityReviewVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- Catalogue permissions only: no reviewer role assignments or identity merge authority are invented.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007145424_AddPatientIdentityReviews', '10.0.0');

COMMIT;

