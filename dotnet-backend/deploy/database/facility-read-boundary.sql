-- Additive correction: hospital metadata reads must not confer facility mutation rights.
ALTER TABLE public."Facilities" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."Facilities" FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS facilities_read_scope ON public."Facilities";
DROP POLICY IF EXISTS facilities_write_scope ON public."Facilities";
CREATE POLICY facilities_read_scope ON public."Facilities" FOR SELECT USING (
 "TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
 OR (current_setting('app.platform_admin',true)='true' AND pg_has_role(current_user,'cureflow_control_plane','MEMBER')));
CREATE POLICY facilities_write_scope ON public."Facilities" FOR ALL USING (
 current_setting('app.platform_admin',true)='true' AND pg_has_role(current_user,'cureflow_control_plane','MEMBER'))
 WITH CHECK (current_setting('app.platform_admin',true)='true' AND pg_has_role(current_user,'cureflow_control_plane','MEMBER'));
CREATE OR REPLACE FUNCTION public.cureflow_lock_active_facility(scope_tenant uuid,scope_facility uuid)
 RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp AS $body$
DECLARE found_id uuid;
BEGIN
 IF scope_tenant IS DISTINCT FROM NULLIF(current_setting('app.current_tenant_id',true),'')::uuid THEN RETURN NULL; END IF;
 SELECT "Id" INTO found_id FROM public."Facilities" WHERE "TenantId"=scope_tenant AND "Id"=scope_facility
   AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
 RETURN found_id;
END $body$;
REVOKE ALL ON FUNCTION public.cureflow_lock_active_facility(uuid,uuid) FROM PUBLIC;
-- Preserve trigger definitions and their other checks; replace only the reviewed facility lock statement.
DO $repair$ DECLARE function_row record; definition text; repaired text; BEGIN
 FOR function_row IN SELECT p.oid FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
  WHERE n.nspname='public' AND p.proname=ANY(ARRAY['enforce_organization_node','validate_operating_settings_append',
    'enforce_facility_resource','enforce_identifier_counter','validate_identifier_allocation','enforce_schedule_rule'])
 LOOP
  definition=pg_get_functiondef(function_row.oid);
  repaired=regexp_replace(definition,'(PERFORM "Id" FROM "Facilities"[^;]*)( FOR SHARE);',
   '\1 AND public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId")=NEW."FacilityId";','g');
  IF repaired=definition THEN RAISE EXCEPTION 'Expected facility guard lock statement was not found'; END IF;
  EXECUTE repaired;
 END LOOP;
END $repair$;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT ON public."Facilities" TO cureflow_tenant;
 REVOKE INSERT,UPDATE,DELETE ON public."Facilities" FROM cureflow_tenant;
 GRANT EXECUTE ON FUNCTION public.cureflow_lock_active_facility(uuid,uuid) TO cureflow_tenant;
END IF; END $grant$;
