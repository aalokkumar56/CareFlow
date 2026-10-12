START TRANSACTION;
CREATE TABLE "OrganizationStructureVersions" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "NodeId" uuid NOT NULL,
    "NodeCategory" character varying(16) NOT NULL,
    "Revision" bigint NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "ParentId" uuid,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "Kind" character varying(32) NOT NULL,
    "NodeIsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_OrganizationStructureVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OrganizationStructureVersions_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_OrganizationStructureVersions_TenantId_FacilityId_NodeCate~1" ON "OrganizationStructureVersions" ("TenantId", "FacilityId", "NodeCategory", "NodeId", "Revision");

CREATE INDEX "IX_OrganizationStructureVersions_TenantId_FacilityId_NodeCateg~" ON "OrganizationStructureVersions" ("TenantId", "FacilityId", "NodeCategory", "NodeId", "EffectiveFrom");

ALTER TABLE "OrganizationStructureVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "OrganizationStructureVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY structure_history_tenant ON "OrganizationStructureVersions"
  USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "OrganizationStructureVersions" ADD CONSTRAINT structure_version_valid CHECK ("Revision">0 AND "NodeCategory" IN ('units','locations'));
CREATE FUNCTION retain_organization_structure_version() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp AS $body$
BEGIN
  INSERT INTO public."OrganizationStructureVersions" ("Id","TenantId","FacilityId","NodeId","NodeCategory","Revision","EffectiveFrom","ParentId","Name","Code","Kind","NodeIsActive","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
    VALUES (gen_random_uuid(),NEW."TenantId",NEW."FacilityId",NEW."Id",CASE WHEN TG_TABLE_NAME='OrganizationUnits' THEN 'units' ELSE 'locations' END,
      NEW."Revision",clock_timestamp(),NEW."ParentId",NEW."Name",NEW."Code",NEW."Kind",NEW."IsActive",clock_timestamp(),clock_timestamp(),COALESCE(NEW."UpdatedBy",NEW."CreatedBy"),NULL,true,false);
  RETURN NEW;
END $body$;
REVOKE ALL ON FUNCTION retain_organization_structure_version() FROM PUBLIC;
CREATE TRIGGER retain_unit_structure AFTER INSERT OR UPDATE ON "OrganizationUnits" FOR EACH ROW EXECUTE FUNCTION retain_organization_structure_version();
CREATE TRIGGER retain_location_structure AFTER INSERT OR UPDATE ON "FacilityLocations" FOR EACH ROW EXECUTE FUNCTION retain_organization_structure_version();
CREATE FUNCTION prevent_structure_history_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Organization structure history is immutable' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER structure_history_immutable BEFORE UPDATE OR DELETE ON "OrganizationStructureVersions" FOR EACH ROW EXECUTE FUNCTION prevent_structure_history_rewrite();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT ON "OrganizationStructureVersions" TO cureflow_tenant;
  REVOKE INSERT,UPDATE,DELETE ON "OrganizationStructureVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- No pre-rollout history backfill: effective identities must not be invented from current rows.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007132515_AddOrganizationStructureHistory', '10.0.0');

COMMIT;

