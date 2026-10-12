using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CureFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientMappingReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "PatientMerges" DROP CONSTRAINT patient_merge_valid;
                ALTER TABLE "PatientMerges" ADD CONSTRAINT patient_merge_valid CHECK (
                  "SourcePatientId"<>"SurvivorPatientId" AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0
                  AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted"
                  AND (("Revision"=1 AND "ReversedAt" IS NULL AND "ReversalReason" IS NULL)
                    OR ("Revision"=2 AND "ReversedAt">="MergedAt" AND length(btrim("ReversalReason"))>0 AND "UpdatedBy" IS NOT NULL)));
                ALTER TABLE "PatientLinkJournals" DROP CONSTRAINT patient_link_valid;
                ALTER TABLE "PatientLinkJournals" ADD CONSTRAINT patient_link_valid CHECK (
                  "Operation" IN ('merge','reverse') AND "EntityType"='PatientIdentityMapping'
                  AND "PublicationHold"=("Operation"='reverse') AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
                DROP TRIGGER patient_merge_history ON "PatientMerges";
                CREATE FUNCTION validate_patient_mapping_change() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Mappings cannot be deleted' USING ERRCODE='23514'; END IF;
                  PERFORM pg_advisory_xact_lock(hashtextextended('patient-mapping:'||NEW."TenantId"::text,0));
                  IF TG_OP='INSERT' THEN
                    IF NEW."Revision"<>1 OR NEW."ReversedAt" IS NOT NULL THEN RAISE EXCEPTION 'New mapping must be active' USING ERRCODE='23514'; END IF;
                    IF EXISTS(SELECT 1 FROM "PatientLinkJournals" j JOIN "PatientMerges" m ON m."TenantId"=j."TenantId" AND m."Id"=j."MergeId"
                      WHERE m."TenantId"=NEW."TenantId" AND (m."SourcePatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId")
                        OR m."SurvivorPatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId")) AND j."PublicationHold") THEN
                      RAISE EXCEPTION 'Correction publication hold requires review' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                  END IF;
                  IF OLD."ReversedAt" IS NOT NULL OR NEW."ReversedAt" IS NULL OR NEW."Revision"<>OLD."Revision"+1
                    OR (to_jsonb(NEW)-ARRAY['ReversedAt','ReversalReason','Revision','UpdatedAt','UpdatedBy'])
                       IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['ReversedAt','ReversalReason','Revision','UpdatedAt','UpdatedBy']) THEN
                    RAISE EXCEPTION 'Only a retained single reversal is permitted' USING ERRCODE='23514';
                  END IF;
                  IF EXISTS(SELECT 1 FROM "PatientMerges" m WHERE m."TenantId"=NEW."TenantId" AND m."Id"<>NEW."Id" AND m."ReversedAt" IS NULL
                    AND (m."SourcePatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId") OR m."SurvivorPatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId"))) THEN
                    RAISE EXCEPTION 'Dependent mappings require reconciliation review' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE TRIGGER patient_merge_history BEFORE INSERT OR UPDATE OR DELETE ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION validate_patient_mapping_change();
                CREATE OR REPLACE FUNCTION validate_patient_link_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE mapping record; evidence record; first_id uuid; second_id uuid;
                BEGIN
                  SELECT * INTO mapping FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."MergeId";
                  IF mapping."Id" IS NULL OR NEW."EntityId"<>mapping."SourcePatientId" THEN
                    RAISE EXCEPTION 'Exact retained mapping journal required' USING ERRCODE='23514';
                  END IF;
                  IF NEW."Operation"='merge' THEN
                    IF NEW."CreatedBy"<>mapping."CreatedBy"
                       OR NEW."BeforeValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SourcePatientId"::text
                       OR NEW."AfterValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SurvivorPatientId"::text THEN
                      RAISE EXCEPTION 'Exact merge journal required' USING ERRCODE='23514';
                    END IF;
                  ELSE
                    first_id=LEAST(mapping."SourcePatientId",mapping."SurvivorPatientId");
                    second_id=GREATEST(mapping."SourcePatientId",mapping."SurvivorPatientId");
                    PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||first_id::text||':'||second_id::text,0));
                    SELECT * INTO evidence FROM "PatientIdentityReviewVersions" WHERE "TenantId"=NEW."TenantId"
                      AND "FirstPatientId"=first_id AND "SecondPatientId"=second_id ORDER BY "Revision" DESC LIMIT 1 FOR SHARE;
                    IF mapping."ReversedAt" IS NULL OR NEW."CreatedBy" IS DISTINCT FROM mapping."UpdatedBy"
                       OR NEW."BeforeValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SurvivorPatientId"::text
                       OR NEW."AfterValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SourcePatientId"::text
                       OR evidence."Id" IS NULL OR evidence."Decision"<>'different' OR evidence."CreatedAt"<=mapping."MergedAt"
                       OR evidence."CreatedBy" IS NULL OR evidence."CreatedBy"=NEW."CreatedBy"
                       OR NEW."AfterValue"::jsonb->>'reviewVersionId' IS DISTINCT FROM evidence."Id"::text
                       OR NEW."AfterValue"::jsonb->>'reviewRevision' IS DISTINCT FROM evidence."Revision"::text
                       OR NOT NEW."PublicationHold" THEN
                      RAISE EXCEPTION 'Subsequent distinct correction review and publication hold required' USING ERRCODE='23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE FUNCTION require_patient_mapping_journal() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                  IF NOT EXISTS(SELECT 1 FROM "PatientLinkJournals" WHERE "TenantId"=NEW."TenantId" AND "MergeId"=NEW."Id" AND "Operation"='merge')
                    OR (NEW."ReversedAt" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "PatientLinkJournals"
                      WHERE "TenantId"=NEW."TenantId" AND "MergeId"=NEW."Id" AND "Operation"='reverse' AND "PublicationHold")) THEN
                    RAISE EXCEPTION 'Mapping change requires retained journal in the same transaction' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $body$;
                CREATE CONSTRAINT TRIGGER patient_mapping_journal_required AFTER INSERT OR UPDATE ON "PatientMerges"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_patient_mapping_journal();
                INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
                  VALUES ('f91dd62c-86de-480c-952d-fd21e2645eb0','IdentityMerge.Reverse','Reverse reviewed local identity mappings','Preserved correction journal and publication hold',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
                  ON CONFLICT ("Code") DO NOTHING;
                DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
                  REVOKE UPDATE ON "PatientMerges" FROM cureflow_tenant;
                  GRANT UPDATE ("ReversedAt","ReversalReason","Revision","UpdatedAt","UpdatedBy") ON "PatientMerges" TO cureflow_tenant;
                END IF; END $grant$;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Reviewed mapping reversal history cannot be downgraded automatically; use a protected restore or reviewed forward repair.");


        }
    }
}
