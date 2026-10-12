DO $rls$ DECLARE tbl text; BEGIN
 FOREACH tbl IN ARRAY ARRAY['ClinicalFollowUpWorks','ClinicalFollowUpVersions'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',tbl);
  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',tbl);
  EXECUTE format('CREATE POLICY tenant_scope ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',tbl);
  EXECUTE format('CREATE POLICY patient_scope ON %I AS RESTRICTIVE FOR SELECT USING(public.cureflow_patient_read_allowed("TenantId","PatientId"))',tbl);
 END LOOP;
END $rls$;
INSERT INTO "Permissions"("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 SELECT p.id::uuid,p.code,p.code,'Explicitly reviewed clinical follow-up capability; no automatic grants',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false
 FROM(VALUES('54fa1070-d201-41ce-9000-000000000001','FollowUp.View'),('54fa1070-d201-41ce-9000-000000000002','FollowUp.Manage'),
 ('54fa1070-d201-41ce-9000-000000000003','FollowUp.Complete'),('54fa1070-d201-41ce-9000-000000000004','FollowUp.Escalate'))p(id,code)
 WHERE NOT EXISTS(SELECT 1 FROM "Permissions" e WHERE e."Code"=p.code);
CREATE FUNCTION public.cureflow_follow_up_user_allowed(scope_tenant uuid,scope_user uuid,scope_patient uuid,capability text)
 RETURNS boolean LANGUAGE sql STABLE AS $authority$
 SELECT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=scope_tenant AND u."Id"=scope_user AND u."IsActive" AND NOT u."IsDeleted")
 AND NOT EXISTS(SELECT required.code FROM(VALUES(capability),('FollowUp.View'),('Clinical.View'),('Patient.View'))required(code)
  WHERE NOT EXISTS(SELECT 1 FROM public."UserRoleAssignments" a
   JOIN public."Roles" r ON r."Id"=a."RoleId" AND r."IsActive" AND NOT r."IsDeleted"
   JOIN public."RolePermissions" rp ON rp."RoleId"=r."Id" AND rp."IsActive" AND NOT rp."IsDeleted"
   JOIN public."Permissions" p ON p."Id"=rp."PermissionId" AND p."IsActive" AND NOT p."IsDeleted"
   WHERE a."TenantId"=scope_tenant AND a."UserId"=scope_user AND a."IsActive" AND NOT a."IsDeleted" AND p."Code"=required.code))
 AND (NOT EXISTS(SELECT 1 FROM public."PatientConfidentialityStates" c WHERE c."TenantId"=scope_tenant AND c."PatientId"=scope_patient AND c."Restricted")
 OR EXISTS(SELECT 1 FROM public."UserRoleAssignments" a JOIN public."Roles" r ON r."Id"=a."RoleId" AND r."IsActive" AND NOT r."IsDeleted"
  JOIN public."RolePermissions" rp ON rp."RoleId"=r."Id" AND rp."IsActive" AND NOT rp."IsDeleted"
  JOIN public."Permissions" p ON p."Id"=rp."PermissionId" AND p."IsActive" AND NOT p."IsDeleted"
  WHERE a."TenantId"=scope_tenant AND a."UserId"=scope_user AND a."IsActive" AND NOT a."IsDeleted" AND p."Code"='PatientRestricted.View'));
$authority$;
ALTER TABLE "ClinicalFollowUpWorks" ADD CONSTRAINT named_follow_up_shape CHECK(COALESCE(
 "IsActive" AND NOT "IsDeleted" AND "SourceType"='appointment' AND "SourceVersion"~'^[1-9][0-9]*$' AND "Revision">0
 AND (("State"='open' AND "AcknowledgedAtUtc" IS NULL AND "CompletedAtUtc" IS NULL)
 OR("State"='acknowledged' AND "AssignedUserId" IS NOT NULL AND "AcknowledgedAtUtc" IS NOT NULL AND "CompletedAtUtc" IS NULL)
 OR("State"='completed' AND "AssignedUserId" IS NOT NULL AND "AcknowledgedAtUtc" IS NOT NULL AND "CompletedAtUtc">="AcknowledgedAtUtc")),false));
ALTER TABLE "ClinicalFollowUpVersions" ADD CONSTRAINT named_follow_up_version_shape CHECK(COALESCE(
 "IsActive" AND NOT "IsDeleted" AND "Revision">0 AND length(btrim("Reason")) BETWEEN 3 AND 1000
 AND "Action" IN('create','acknowledge','reassign','complete') AND ("Action"='complete')=("EvidenceNoteId" IS NOT NULL AND "EvidenceNoteRevision">0),false));
CREATE FUNCTION public.guard_named_follow_up() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE actor uuid; cap text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Clinical follow-up work is retained' USING ERRCODE='23514'; END IF;
 actor=NULLIF(current_setting('app.current_user_id',true),'')::uuid;
 IF actor IS NULL OR NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId") THEN
  RAISE EXCEPTION 'Current clinical chart authority required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  cap='FollowUp.Manage';
  IF NEW."CreatedBy" IS DISTINCT FROM actor OR NEW."Revision"<>1 OR NEW."State"<>'open' OR NOT EXISTS(
   SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."SourceId"
    AND a."PatientId"=NEW."PatientId" AND a."Revision"::text=NEW."SourceVersion" AND a."IsActive" AND NOT a."IsDeleted") THEN
   RAISE EXCEPTION 'Follow-up requires exact current appointment source and actor' USING ERRCODE='23514'; END IF;
 ELSE
  IF (NEW."TenantId",NEW."Id",NEW."PatientId",NEW."SourceType",NEW."SourceId",NEW."SourceVersion",NEW."CreatedAt",NEW."CreatedBy",NEW."IsActive",NEW."IsDeleted")
    IS DISTINCT FROM(OLD."TenantId",OLD."Id",OLD."PatientId",OLD."SourceType",OLD."SourceId",OLD."SourceVersion",OLD."CreatedAt",OLD."CreatedBy",OLD."IsActive",OLD."IsDeleted")
   OR NEW."Revision"<>OLD."Revision"+1 OR NEW."UpdatedBy" IS DISTINCT FROM actor OR OLD."State"='completed' THEN
   RAISE EXCEPTION 'Follow-up source is retained and mutations need exact next version' USING ERRCODE='23514'; END IF;
  IF NEW."State"='open' THEN cap='FollowUp.Manage';
  ELSE
   cap='FollowUp.Complete';
   IF OLD."AssignedUserId" IS DISTINCT FROM actor
    OR(NEW."AssignedUserId",NEW."EscalationOwnerUserId",NEW."DueAtUtc") IS DISTINCT FROM(OLD."AssignedUserId",OLD."EscalationOwnerUserId",OLD."DueAtUtc")
    OR(NEW."State"='acknowledged' AND (OLD."State"<>'open' OR NEW."AcknowledgedAtUtc" IS NULL))
    OR(NEW."State"='completed' AND (OLD."State"<>'acknowledged' OR NEW."AcknowledgedAtUtc" IS DISTINCT FROM OLD."AcknowledgedAtUtc")) THEN
    RAISE EXCEPTION 'Acknowledgment/completion requires current named assignee and allowed transition' USING ERRCODE='23514'; END IF;
  END IF;
 END IF;
 IF NOT public.cureflow_follow_up_user_allowed(NEW."TenantId",actor,NEW."PatientId",cap) THEN
  RAISE EXCEPTION 'Current follow-up capability required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' OR NEW."State"='open' THEN
  IF NEW."DueAtUtc"<=clock_timestamp() OR NEW."DueAtUtc">clock_timestamp()+interval '365 days'
   OR NOT public.cureflow_follow_up_user_allowed(NEW."TenantId",NEW."EscalationOwnerUserId",NEW."PatientId",'FollowUp.Escalate')
   OR(NEW."AssignedUserId" IS NOT NULL AND NOT public.cureflow_follow_up_user_allowed(NEW."TenantId",NEW."AssignedUserId",NEW."PatientId",'FollowUp.Complete')) THEN
   RAISE EXCEPTION 'Current named owner capabilities and bounded future due time required' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER named_follow_up_guard BEFORE INSERT OR UPDATE OR DELETE ON "ClinicalFollowUpWorks" FOR EACH ROW EXECUTE FUNCTION public.guard_named_follow_up();
CREATE FUNCTION public.guard_named_follow_up_version() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE w public."ClinicalFollowUpWorks"%ROWTYPE;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Clinical follow-up version evidence is immutable' USING ERRCODE='23514'; END IF;
 SELECT * INTO w FROM public."ClinicalFollowUpWorks" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."WorkId" FOR SHARE;
 IF NOT FOUND OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
  OR(NEW."PatientId",NEW."Revision",NEW."AssignedUserId",NEW."EscalationOwnerUserId",NEW."DueAtUtc",NEW."State",NEW."AcknowledgedAtUtc",NEW."CompletedAtUtc")
    IS DISTINCT FROM(w."PatientId",w."Revision",w."AssignedUserId",w."EscalationOwnerUserId",w."DueAtUtc",w."State",w."AcknowledgedAtUtc",w."CompletedAtUtc")
  OR NOT((NEW."Revision"=1 AND NEW."Action"='create') OR(NEW."Revision">1 AND NEW."Action"='reassign' AND w."State"='open')
    OR(NEW."Action"='acknowledge' AND w."State"='acknowledged') OR(NEW."Action"='complete' AND w."State"='completed')) THEN
  RAISE EXCEPTION 'Follow-up evidence must match exact attributable work version/action' USING ERRCODE='23514'; END IF;
 IF NEW."Action"='complete' AND NOT EXISTS(SELECT 1 FROM public."ClinicalNotes" n WHERE n."TenantId"=NEW."TenantId" AND n."Id"=NEW."EvidenceNoteId"
   AND n."PatientId"=w."PatientId" AND n."AppointmentId"=w."SourceId" AND n."Revision"=NEW."EvidenceNoteRevision"
   AND n."FinalizedAt" IS NOT NULL AND n."IsActive" AND NOT n."IsDeleted") THEN
  RAISE EXCEPTION 'Completion requires exact finalized clinical note evidence' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER named_follow_up_version_guard BEFORE INSERT OR UPDATE OR DELETE ON "ClinicalFollowUpVersions" FOR EACH ROW EXECUTE FUNCTION public.guard_named_follow_up_version();
CREATE FUNCTION public.require_named_follow_up_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public."ClinicalFollowUpVersions" v WHERE v."TenantId"=NEW."TenantId" AND v."WorkId"=NEW."Id" AND v."Revision"=NEW."Revision"
  AND(v."PatientId",v."AssignedUserId",v."EscalationOwnerUserId",v."DueAtUtc",v."State",v."AcknowledgedAtUtc",v."CompletedAtUtc")
   IS NOT DISTINCT FROM(NEW."PatientId",NEW."AssignedUserId",NEW."EscalationOwnerUserId",NEW."DueAtUtc",NEW."State",NEW."AcknowledgedAtUtc",NEW."CompletedAtUtc")) THEN
  RAISE EXCEPTION 'Follow-up state and attributable version must commit together' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER named_follow_up_commit AFTER INSERT OR UPDATE ON "ClinicalFollowUpWorks" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_named_follow_up_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "ClinicalFollowUpWorks","ClinicalFollowUpVersions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "ClinicalFollowUpWorks","ClinicalFollowUpVersions" FROM cureflow_tenant;
 GRANT UPDATE("AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","Revision","AcknowledgedAtUtc","CompletedAtUtc","UpdatedAt","UpdatedBy") ON "ClinicalFollowUpWorks" TO cureflow_tenant;
END IF; END $grant$;
