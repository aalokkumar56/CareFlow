START TRANSACTION;
CREATE TABLE "ScheduleRules" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PractitionerUserId" uuid,
    "ResourceId" uuid,
    "OperatingSettingId" uuid NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveThrough" date NOT NULL,
    "WeekdayMask" integer NOT NULL,
    "StartMinute" integer NOT NULL,
    "EndMinute" integer NOT NULL,
    "AmbiguousTimePolicy" character varying(16) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ScheduleRules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ScheduleRules_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleRules_FacilityOperatingSettings_OperatingSettingId" FOREIGN KEY ("OperatingSettingId") REFERENCES "FacilityOperatingSettings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleRules_FacilityResources_ResourceId" FOREIGN KEY ("ResourceId") REFERENCES "FacilityResources" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleRules_Users_PractitionerUserId" FOREIGN KEY ("PractitionerUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ScheduleRules_OperatingSettingId" ON "ScheduleRules" ("OperatingSettingId");

CREATE INDEX "IX_ScheduleRules_PractitionerUserId" ON "ScheduleRules" ("PractitionerUserId");

CREATE INDEX "IX_ScheduleRules_ResourceId" ON "ScheduleRules" ("ResourceId");

CREATE INDEX "IX_ScheduleRules_TenantId_FacilityId_Id" ON "ScheduleRules" ("TenantId", "FacilityId", "Id");

CREATE INDEX "IX_ScheduleRules_TenantId_PractitionerUserId_EffectiveFrom_Eff~" ON "ScheduleRules" ("TenantId", "PractitionerUserId", "EffectiveFrom", "EffectiveThrough");

CREATE INDEX "IX_ScheduleRules_TenantId_ResourceId_EffectiveFrom_EffectiveTh~" ON "ScheduleRules" ("TenantId", "ResourceId", "EffectiveFrom", "EffectiveThrough");

ALTER TABLE "ScheduleRules" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ScheduleRules" FORCE ROW LEVEL SECURITY;
CREATE POLICY schedule_rule_tenant ON "ScheduleRules"
 USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "ScheduleRules" ADD CONSTRAINT schedule_rule_bounds CHECK (
 ("PractitionerUserId" IS NOT NULL OR "ResourceId" IS NOT NULL) AND "Revision">0 AND NOT "IsDeleted"
 AND "EffectiveThrough">="EffectiveFrom" AND "EffectiveThrough"-"EffectiveFrom"<=365
 AND "WeekdayMask" BETWEEN 1 AND 127 AND "StartMinute" BETWEEN 0 AND 1439
 AND "EndMinute">"StartMinute" AND "EndMinute"<=1440
 AND "AmbiguousTimePolicy" IN ('reject','earlier','later'));
CREATE FUNCTION enforce_schedule_rule() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Retained schedule rules cannot be deleted' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF TG_OP='UPDATE' THEN
  IF (to_jsonb(NEW)-ARRAY['IsActive','Revision','UpdatedAt','UpdatedBy']) IS DISTINCT FROM
     (to_jsonb(OLD)-ARRAY['IsActive','Revision','UpdatedAt','UpdatedBy'])
     OR NOT OLD."IsActive" OR NEW."IsActive" OR NEW."Revision"<>OLD."Revision"+1 THEN
   RAISE EXCEPTION 'Schedule rules are retained; only a versioned disable is permitted' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
 IF NEW."Revision"<>1 OR NOT NEW."IsActive" THEN RAISE EXCEPTION 'Invalid initial schedule revision' USING ERRCODE='23514'; END IF;
 PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Active same-tenant facility required' USING ERRCODE='23514'; END IF;
 PERFORM "Id" FROM "FacilityOperatingSettings" s WHERE s."TenantId"=NEW."TenantId" AND s."FacilityId"=NEW."FacilityId"
  AND s."Id"=NEW."OperatingSettingId" AND s."Timezone"=NEW."Timezone"
  AND s."Revision"=(SELECT max("Revision") FROM "FacilityOperatingSettings" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId");
 IF NOT FOUND THEN RAISE EXCEPTION 'Current explicit facility timezone required' USING ERRCODE='23514'; END IF;
 IF NEW."PractitionerUserId" IS NOT NULL THEN
  PERFORM u."Id" FROM "Users" u JOIN "StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
   WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."PractitionerUserId" AND u."Role" IN (1,4,6)
   AND u."IsActive" AND NOT u."IsDeleted" AND p."IsActive" AND p."IsAvailable" AND NOT p."IsDeleted" FOR SHARE OF u,p;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active eligible same-tenant practitioner required' USING ERRCODE='23514'; END IF;
 END IF;
 IF NEW."ResourceId" IS NOT NULL THEN
  PERFORM "Id" FROM "FacilityResources" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Id"=NEW."ResourceId"
   AND "IsActive" AND NOT "IsDeleted" AND "OperationalStatus"='enabled' FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Enabled same-facility resource required' USING ERRCODE='23514'; END IF;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM generate_series(0,least(6,NEW."EffectiveThrough"-NEW."EffectiveFrom")) d
    WHERE (NEW."WeekdayMask" & (1 << extract(dow FROM NEW."EffectiveFrom"+d)::int))<>0) THEN
  RAISE EXCEPTION 'The rule must occur in its effective interval' USING ERRCODE='23514';
 END IF;
 IF EXISTS(SELECT 1 FROM "ScheduleRules" r WHERE r."TenantId"=NEW."TenantId" AND r."IsActive" AND NOT r."IsDeleted"
    AND (r."PractitionerUserId"=NEW."PractitionerUserId" OR r."ResourceId"=NEW."ResourceId")
    AND r."EffectiveFrom"<=NEW."EffectiveThrough" AND r."EffectiveThrough">=NEW."EffectiveFrom"
    AND (r."Timezone"<>NEW."Timezone" OR (r."StartMinute"<NEW."EndMinute" AND r."EndMinute">NEW."StartMinute"
      AND EXISTS(SELECT 1 FROM generate_series(0,least(6,least(r."EffectiveThrough",NEW."EffectiveThrough")-greatest(r."EffectiveFrom",NEW."EffectiveFrom"))) d
       WHERE ((r."WeekdayMask" & NEW."WeekdayMask") & (1 << extract(dow FROM greatest(r."EffectiveFrom",NEW."EffectiveFrom")+d)::int))<>0)))) THEN
  RAISE EXCEPTION 'Overlapping practitioner or resource rules must be resolved explicitly' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $body$;
CREATE TRIGGER schedule_rule_guard BEFORE INSERT OR UPDATE OR DELETE ON "ScheduleRules" FOR EACH ROW EXECUTE FUNCTION enforce_schedule_rule();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "ScheduleRules" TO cureflow_tenant;
 GRANT UPDATE ("IsActive","Revision","UpdatedAt","UpdatedBy") ON "ScheduleRules" TO cureflow_tenant;
 REVOKE DELETE ON "ScheduleRules" FROM cureflow_tenant;
END IF; END $grant$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008175428_AddFacilityScheduleRules', '10.0.0');

COMMIT;

