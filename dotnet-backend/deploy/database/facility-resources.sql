START TRANSACTION;
CREATE TABLE "FacilityResources" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "LocationId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "ResourceType" character varying(32) NOT NULL,
    "Capacity" integer NOT NULL,
    "OperationalStatus" character varying(32) NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveUntil" timestamp with time zone,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_FacilityResources" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FacilityResources_FacilityLocations_TenantId_FacilityId_Loc~" FOREIGN KEY ("TenantId", "FacilityId", "LocationId") REFERENCES "FacilityLocations" ("TenantId", "FacilityId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_FacilityResources_TenantId_FacilityId_Code" ON "FacilityResources" ("TenantId", "FacilityId", "Code");

CREATE INDEX "IX_FacilityResources_TenantId_FacilityId_LocationId" ON "FacilityResources" ("TenantId", "FacilityId", "LocationId");

ALTER TABLE "FacilityResources" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "FacilityResources" FORCE ROW LEVEL SECURITY;
CREATE POLICY resource_tenant ON "FacilityResources" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "FacilityResources" ADD CONSTRAINT resource_configuration_valid CHECK (
  "Capacity" BETWEEN 1 AND 100000 AND "Revision">0 AND "Code" ~ '^[A-Z0-9_-]{1,64}$'
  AND "ResourceType" ~ '^[a-z0-9_-]{1,32}$' AND length(btrim("Name"))>0
  AND "OperationalStatus" IN ('enabled','disabled','maintenance')
  AND ("EffectiveUntil" IS NULL OR "EffectiveUntil">"EffectiveFrom"));
CREATE FUNCTION enforce_facility_resource() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Retained resources cannot be deleted' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('organization-db:'||NEW."TenantId"::text||':'||NEW."FacilityId"::text,0));
  IF NEW."IsDeleted" OR (TG_OP='INSERT' AND NEW."Revision"<>1) THEN
    RAISE EXCEPTION 'Invalid retained resource revision' USING ERRCODE='23514'; END IF;
  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId" OR NEW."Code"<>OLD."Code"
    OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR NEW."Revision"<>OLD."Revision"+1) THEN
    RAISE EXCEPTION 'Resource identity/provenance must be retained' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "FacilityLocations" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Id"=NEW."LocationId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active same-facility location required' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER resource_guard BEFORE INSERT OR UPDATE OR DELETE ON "FacilityResources" FOR EACH ROW EXECUTE FUNCTION enforce_facility_resource();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT,UPDATE ON "FacilityResources" TO cureflow_tenant;
  REVOKE DELETE ON "FacilityResources" FROM cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007131452_AddFacilityResources', '10.0.0');

COMMIT;

