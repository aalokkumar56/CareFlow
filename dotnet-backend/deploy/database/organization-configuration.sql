START TRANSACTION;
ALTER TABLE "Facilities" ADD CONSTRAINT "AK_Facilities_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "FacilityLocations" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ParentId" uuid,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "Kind" character varying(32) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_FacilityLocations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FacilityLocations_TenantId_FacilityId_Id" UNIQUE ("TenantId", "FacilityId", "Id"),
    CONSTRAINT "FK_FacilityLocations_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FacilityLocations_FacilityLocations_TenantId_FacilityId_Par~" FOREIGN KEY ("TenantId", "FacilityId", "ParentId") REFERENCES "FacilityLocations" ("TenantId", "FacilityId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FacilityOperatingSettings" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "Language" character varying(35) NOT NULL,
    "DateFormat" character varying(20) NOT NULL,
    "TimeFormat" character varying(20) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_FacilityOperatingSettings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FacilityOperatingSettings_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OrganizationUnits" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ParentId" uuid,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "Kind" character varying(32) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_OrganizationUnits" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OrganizationUnits_TenantId_FacilityId_Id" UNIQUE ("TenantId", "FacilityId", "Id"),
    CONSTRAINT "FK_OrganizationUnits_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OrganizationUnits_OrganizationUnits_TenantId_FacilityId_Par~" FOREIGN KEY ("TenantId", "FacilityId", "ParentId") REFERENCES "OrganizationUnits" ("TenantId", "FacilityId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_FacilityLocations_TenantId_FacilityId_Code" ON "FacilityLocations" ("TenantId", "FacilityId", "Code");

CREATE INDEX "IX_FacilityLocations_TenantId_FacilityId_ParentId" ON "FacilityLocations" ("TenantId", "FacilityId", "ParentId");

CREATE UNIQUE INDEX "IX_FacilityOperatingSettings_TenantId_FacilityId_Revision" ON "FacilityOperatingSettings" ("TenantId", "FacilityId", "Revision");

CREATE UNIQUE INDEX "IX_OrganizationUnits_TenantId_FacilityId_Code" ON "OrganizationUnits" ("TenantId", "FacilityId", "Code");

CREATE INDEX "IX_OrganizationUnits_TenantId_FacilityId_ParentId" ON "OrganizationUnits" ("TenantId", "FacilityId", "ParentId");

DO $guard$ DECLARE t text; BEGIN
  FOREACH t IN ARRAY ARRAY['OrganizationUnits','FacilityLocations','FacilityOperatingSettings'] LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',t);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',t);
    EXECUTE format('CREATE POLICY organization_tenant ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',t);
  END LOOP;
END $guard$;
CREATE FUNCTION enforce_organization_node() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE ancestor_depth int; descendant_depth int; invalid_path boolean; parent_active boolean; has_child boolean;
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Organization history cannot be deleted' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('organization-db:'||NEW."TenantId"::text||':'||NEW."FacilityId"::text,0));
  PERFORM "Id" FROM "Facilities" WHERE "Id"=NEW."FacilityId" AND "TenantId"=NEW."TenantId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  IF NEW."IsDeleted" OR NEW."Revision"<1 OR NEW."Code" !~ '^[A-Z0-9_-]{1,64}$' OR length(btrim(NEW."Name"))=0
    OR (TG_TABLE_NAME='OrganizationUnits' AND NEW."Kind" NOT IN ('department','unit'))
    OR (TG_TABLE_NAME='FacilityLocations' AND NEW."Kind"<>'location') THEN
    RAISE EXCEPTION 'Invalid retained organization node' USING ERRCODE='23514'; END IF;
  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId"
    OR NEW."Code"<>OLD."Code" OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy"
    OR NEW."Revision"<>OLD."Revision"+1) THEN
    RAISE EXCEPTION 'Organization identity/provenance is immutable; revision must advance once' USING ERRCODE='23514'; END IF;
  IF TG_OP='INSERT' AND NEW."Revision"<>1 THEN RAISE EXCEPTION 'Initial revision must be one' USING ERRCODE='23514'; END IF;
  ancestor_depth:=0;
  IF NEW."ParentId" IS NOT NULL THEN
    EXECUTE format('SELECT "IsActive" AND NOT "IsDeleted" FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "Id"=$3',TG_TABLE_NAME)
      INTO parent_active USING NEW."TenantId",NEW."FacilityId",NEW."ParentId";
    IF parent_active IS DISTINCT FROM true THEN RAISE EXCEPTION 'Parent must be active in the same facility' USING ERRCODE='23514'; END IF;
    EXECUTE format('WITH RECURSIVE a AS (
      SELECT "Id","ParentId",1 depth,ARRAY["Id"] path,false cycle FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "Id"=$3
      UNION ALL SELECT p."Id",p."ParentId",a.depth+1,a.path||p."Id",p."Id"=ANY(a.path)
      FROM %I p JOIN a ON p."Id"=a."ParentId" WHERE p."TenantId"=$1 AND p."FacilityId"=$2 AND a.depth<32 AND NOT a.cycle)
      SELECT COALESCE(max(depth),0),COALESCE(bool_or(cycle OR "Id"=$4 OR depth>=32),false) FROM a',TG_TABLE_NAME,TG_TABLE_NAME)
      INTO ancestor_depth,invalid_path USING NEW."TenantId",NEW."FacilityId",NEW."ParentId",NEW."Id";
    IF invalid_path THEN RAISE EXCEPTION 'Organization cycle or depth limit' USING ERRCODE='23514'; END IF;
  END IF;
  EXECUTE format('WITH RECURSIVE d AS (
    SELECT "Id",1 depth,ARRAY["Id"] path,false cycle FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "ParentId"=$3
    UNION ALL SELECT c."Id",d.depth+1,d.path||c."Id",c."Id"=ANY(d.path)
    FROM %I c JOIN d ON c."ParentId"=d."Id" WHERE c."TenantId"=$1 AND c."FacilityId"=$2 AND d.depth<32 AND NOT d.cycle)
    SELECT COALESCE(max(depth),0),COALESCE(bool_or(cycle OR "Id"=$3 OR depth>=32),false) FROM d',TG_TABLE_NAME,TG_TABLE_NAME)
    INTO descendant_depth,invalid_path USING NEW."TenantId",NEW."FacilityId",NEW."Id";
  IF invalid_path OR ancestor_depth+1+descendant_depth>32 THEN
    RAISE EXCEPTION 'Organization subtree exceeds maximum depth' USING ERRCODE='23514'; END IF;
  IF NOT NEW."IsActive" THEN
    EXECUTE format('SELECT EXISTS(SELECT 1 FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "ParentId"=$3 AND "IsActive" AND NOT "IsDeleted")',TG_TABLE_NAME)
      INTO has_child USING NEW."TenantId",NEW."FacilityId",NEW."Id";
    IF has_child THEN RAISE EXCEPTION 'Active children prevent deactivation' USING ERRCODE='23514'; END IF;
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER organization_node_guard BEFORE INSERT OR UPDATE OR DELETE ON "OrganizationUnits" FOR EACH ROW EXECUTE FUNCTION enforce_organization_node();
CREATE TRIGGER location_node_guard BEFORE INSERT OR UPDATE OR DELETE ON "FacilityLocations" FOR EACH ROW EXECUTE FUNCTION enforce_organization_node();
ALTER TABLE "FacilityOperatingSettings" ADD CONSTRAINT operating_revision_positive CHECK ("Revision">0 AND length(btrim("Reason"))>0);
CREATE FUNCTION validate_operating_settings_append() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE previous_revision bigint;
BEGIN
  PERFORM pg_advisory_xact_lock(734291);
  PERFORM "Id" FROM "Facilities" WHERE "Id"=NEW."FacilityId" AND "TenantId"=NEW."TenantId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(max("Revision"),0) INTO previous_revision FROM "FacilityOperatingSettings"
    WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId";
  IF NEW."Revision"<>previous_revision+1 OR NEW."IsDeleted" OR NOT NEW."IsActive" THEN
    RAISE EXCEPTION 'Operating settings require a consecutive retained revision' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER operating_settings_append BEFORE INSERT ON "FacilityOperatingSettings" FOR EACH ROW EXECUTE FUNCTION validate_operating_settings_append();
CREATE FUNCTION prevent_operating_settings_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Operating settings history is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER operating_settings_history BEFORE UPDATE OR DELETE ON "FacilityOperatingSettings" FOR EACH ROW EXECUTE FUNCTION prevent_operating_settings_rewrite();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('4c50f496-c9f0-447a-b53f-f7fd5a9b79b1','Organization.View','View organization structure','Facility scoped bounded structure',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('9d4eb872-c88c-48b9-90e4-9e0d3b3258c2','Organization.Manage','Manage organization structure','Versioned retained facility structure',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN
  IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
    GRANT SELECT,INSERT,UPDATE ON "OrganizationUnits","FacilityLocations" TO cureflow_tenant;
    REVOKE DELETE ON "OrganizationUnits","FacilityLocations" FROM cureflow_tenant;
    GRANT SELECT,INSERT ON "FacilityOperatingSettings" TO cureflow_tenant;
    REVOKE UPDATE,DELETE ON "FacilityOperatingSettings" FROM cureflow_tenant;
  END IF;
END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007130052_AddOrganizationConfiguration', '10.0.0');

COMMIT;

