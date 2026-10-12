DO $rls$ DECLARE tbl text;
BEGIN
 FOREACH tbl IN ARRAY ARRAY['AppointmentWaitlistEntries','AppointmentWaitlistOffers','AppointmentWaitlistDecisions'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',tbl);
  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',tbl);
  EXECUTE format('CREATE POLICY tenant_scope ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',tbl);
  EXECUTE format('CREATE POLICY patient_scope ON %I AS RESTRICTIVE FOR SELECT USING(public.cureflow_patient_read_allowed("TenantId","PatientId"))',tbl);
 END LOOP;
END $rls$;
ALTER TABLE "AppointmentWaitlistEntries" ADD CONSTRAINT waitlist_entry_shape CHECK(COALESCE(
 "Revision">0 AND "IsActive" AND NOT "IsDeleted" AND "DurationMinutes" BETWEEN 1 AND 480
 AND "EarliestStartsAtUtc"<"LatestEndsAtUtc" AND "LatestEndsAtUtc"-"EarliestStartsAtUtc">=make_interval(mins=>"DurationMinutes")
 AND "ExpiresAtUtc"="LatestEndsAtUtc" AND "LatestEndsAtUtc"<="CreatedAt"+interval '365 days'
 AND jsonb_typeof("ResourceIdsJson"::jsonb)='array' AND jsonb_array_length("ResourceIdsJson"::jsonb)<=5
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "BookingKind" IN('routine','walk_in','follow_up')
 AND ("BookingKind"='follow_up')=("FollowUpAppointmentId" IS NOT NULL)
 AND (("State"='waiting' AND "CurrentOfferId" IS NULL) OR ("State" IN('offered','accepted') AND "CurrentOfferId" IS NOT NULL)
 OR "State" IN('withdrawn','expired')),false));
ALTER TABLE "AppointmentWaitlistOffers" ADD CONSTRAINT waitlist_offer_shape CHECK(COALESCE(
 "IsActive" AND NOT "IsDeleted" AND "CreatedBy"="OwnerUserId" AND "ExpiresAtUtc">"CreatedAt"
 AND "ExpiresAtUtc"<="CreatedAt"+interval '30 minutes' AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "ReservationId"<>"ReleasedReservationId" AND (("State"='offered' AND "Revision"=1 AND "AppointmentId" IS NULL)
 OR("State"='accepted' AND "Revision"=2 AND "AppointmentId" IS NOT NULL)
 OR("State" IN('expired','withdrawn') AND "Revision"=2 AND "AppointmentId" IS NULL)),false));
ALTER TABLE "AppointmentWaitlistDecisions" ADD CONSTRAINT waitlist_decision_shape CHECK(
 "EntryRevision">0 AND "IsActive" AND NOT "IsDeleted" AND "CreatedBy" IS NOT NULL
 AND "Action" IN('add','offer','accept','expire_offer','withdrawn','expired') AND length("Reason") BETWEEN 3 AND 1000);
CREATE FUNCTION public.guard_appointment_waitlist_entry() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Waitlist request history is retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId")
  OR NULLIF(current_setting('app.current_user_id',true),'')::uuid IS NULL THEN RAISE EXCEPTION 'Current patient actor scope required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."FollowUpAppointmentId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM public."Appointments" a
   WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."FollowUpAppointmentId" AND a."PatientId"=NEW."PatientId"
    AND a."ScheduledAt"<NEW."EarliestStartsAtUtc" AND a."Status" NOT IN(3,4) AND a."IsActive" AND NOT a."IsDeleted") THEN
   RAISE EXCEPTION 'Waitlist follow-up requires exact earlier same-patient source' USING ERRCODE='23514';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
    WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."PractitionerUserId" AND u."Role" IN(1,4,6)
     AND u."IsActive" AND NOT u."IsDeleted" AND p."IsActive" AND NOT p."IsDeleted") THEN
   RAISE EXCEPTION 'Waitlist requires active eligible staff' USING ERRCODE='23514';
  END IF;
  IF NEW."Revision"<>1 OR NEW."State"<>'waiting' OR NEW."EarliestStartsAtUtc"<=clock_timestamp()
   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
   OR NOT EXISTS(SELECT 1 FROM public."PatientIdentifiers" i WHERE i."TenantId"=NEW."TenantId" AND i."PatientId"=NEW."PatientId"
    AND i."FacilityId"=NEW."FacilityId" AND i."Namespace"='local-mrn') THEN
   RAISE EXCEPTION 'Waitlist starts with an explicit current patient/facility request' USING ERRCODE='23514';
  END IF;
 ELSE
  IF NEW."Revision"<>OLD."Revision"+1 OR OLD."State" NOT IN('waiting','offered')
   OR (to_jsonb(OLD)-ARRAY['State','Revision','CurrentOfferId','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','CurrentOfferId','UpdatedAt'])
   OR NOT((OLD."State"='waiting' AND NEW."State" IN('offered','withdrawn','expired'))
    OR(OLD."State"='offered' AND NEW."State" IN('waiting','accepted','withdrawn','expired'))) THEN
   RAISE EXCEPTION 'Waitlist intent is immutable; only reviewed terminal/offer transitions are allowed' USING ERRCODE='23514';
  END IF;
  IF NEW."State" IN('offered','accepted') AND (NEW."ExpiresAtUtc"<=clock_timestamp() OR NOT EXISTS(
   SELECT 1 FROM public."AppointmentWaitlistOffers" o WHERE o."TenantId"=NEW."TenantId" AND o."Id"=NEW."CurrentOfferId"
    AND o."EntryId"=NEW."Id" AND o."PatientId"=NEW."PatientId" AND o."State"=CASE WHEN NEW."State"='offered' THEN 'offered' ELSE 'accepted' END)) THEN
   RAISE EXCEPTION 'Entry and exact current offer state must agree' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='expired' AND NEW."ExpiresAtUtc">clock_timestamp() THEN RAISE EXCEPTION 'Live request cannot expire' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_entry_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistEntries"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_entry();
CREATE FUNCTION public.guard_appointment_waitlist_offer() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry_row public."AppointmentWaitlistEntries"%ROWTYPE; held public."AppointmentReservations"%ROWTYPE; released public."AppointmentReservations"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Waitlist offer history is retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 SELECT * INTO entry_row FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=NEW."TenantId" AND e."Id"=NEW."EntryId" FOR UPDATE;
 IF NOT FOUND OR entry_row."PatientId"<>NEW."PatientId" OR NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId") THEN
  RAISE EXCEPTION 'Offer requires exact current patient request' USING ERRCODE='23514';
 END IF;
 SELECT * INTO held FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReservationId" FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Offer requires exact reservation' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  SELECT * INTO released FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReleasedReservationId";
  IF NOT FOUND OR released."State"<>'released' OR held."State"<>'held' OR held."OwnerUserId"<>NEW."OwnerUserId"
   OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
   OR entry_row."State"<>'waiting' OR entry_row."ExpiresAtUtc"<=clock_timestamp() OR NEW."State"<>'offered' OR NEW."Revision"<>1
   OR NEW."ExpiresAtUtc"<>held."HoldExpiresAtUtc" OR held."HoldExpiresAtUtc"<=clock_timestamp() OR held."StartsAtUtc"<=clock_timestamp()
   OR held."FacilityId"<>entry_row."FacilityId" OR held."PractitionerUserId"<>entry_row."PractitionerUserId"
   OR (held."FacilityId",held."PractitionerUserId",held."StartsAtUtc",held."EndsAtUtc")
     IS DISTINCT FROM (released."FacilityId",released."PractitionerUserId",released."StartsAtUtc",released."EndsAtUtc")
   OR held."StartsAtUtc"<entry_row."EarliestStartsAtUtc" OR held."EndsAtUtc">entry_row."LatestEndsAtUtc"
   OR EXTRACT(EPOCH FROM(held."EndsAtUtc"-held."StartsAtUtc"))/60<>entry_row."DurationMinutes"
   OR EXISTS(SELECT 1 FROM jsonb_array_elements_text((entry_row."ResourceIdsJson"::jsonb)) ids(value) WHERE NOT EXISTS(
    SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id"
     AND s."TargetType"='resource' AND s."TargetId"=ids.value::uuid))
   OR (SELECT count(*) FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id"
     AND s."TargetType"='resource')<>jsonb_array_length((entry_row."ResourceIdsJson"::jsonb))
   OR EXISTS((SELECT s."TargetType",s."TargetId" FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id")
     EXCEPT (SELECT s."TargetType",s."TargetId" FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=released."Id")) THEN
   RAISE EXCEPTION 'Offer must bind exact released capacity and live owned hold' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'offered' OR NEW."State" NOT IN('accepted','expired','withdrawn') OR NEW."Revision"<>2
   OR (to_jsonb(OLD)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) THEN
   RAISE EXCEPTION 'Offer evidence is immutable; decisions are terminal' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='accepted' THEN
   IF OLD."ExpiresAtUtc"<=clock_timestamp() OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
    OR held."State"<>'confirmed' OR held."AppointmentId"<>NEW."AppointmentId"
    OR NOT EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId"
     AND a."PatientId"=NEW."PatientId" AND a."ReservationId"=held."Id" AND a."FacilityId"=entry_row."FacilityId") THEN
    RAISE EXCEPTION 'Expired/changed offer cannot accept or bind another patient' USING ERRCODE='23514';
   END IF;
  ELSIF held."State" NOT IN('expired','released') OR (NEW."State"='expired' AND OLD."ExpiresAtUtc">clock_timestamp()) THEN
   RAISE EXCEPTION 'Offer closure requires exact hold release/expiry' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_offer_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistOffers"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_offer();
CREATE FUNCTION public.guard_appointment_waitlist_decision() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Waitlist decision evidence is immutable' USING ERRCODE='23514'; END IF;
 IF NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid OR NOT EXISTS(
  SELECT 1 FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=NEW."TenantId" AND e."Id"=NEW."EntryId"
   AND e."PatientId"=NEW."PatientId" AND e."Revision"=NEW."EntryRevision") THEN
  RAISE EXCEPTION 'Decision requires exact current patient/request version and actor' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_decision_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistDecisions"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_decision();
CREATE FUNCTION public.require_waitlist_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry_id uuid; scope_tenant uuid; entry_row public."AppointmentWaitlistEntries"%ROWTYPE;
BEGIN
 IF TG_TABLE_NAME='AppointmentWaitlistEntries' THEN entry_id=NEW."Id"; ELSE entry_id=NEW."EntryId"; END IF;
 scope_tenant=NEW."TenantId";
 SELECT * INTO entry_row FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=scope_tenant AND e."Id"=entry_id;
 IF NOT FOUND OR NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistDecisions" d WHERE d."TenantId"=scope_tenant AND d."EntryId"=entry_id
    AND d."EntryRevision"=entry_row."Revision"
    AND ((entry_row."State"='waiting' AND d."Action" IN('add','expire_offer')) OR(entry_row."State"='offered' AND d."Action"='offer')
      OR(entry_row."State"='accepted' AND d."Action"='accept') OR(entry_row."State" IN('expired','withdrawn') AND d."Action"=entry_row."State")))
  OR EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o WHERE o."TenantId"=scope_tenant AND o."EntryId"=entry_id AND o."State"='offered'
    AND (entry_row."State"<>'offered' OR entry_row."CurrentOfferId"<>o."Id"))
  OR (entry_row."State"='offered' AND NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o
    WHERE o."TenantId"=scope_tenant AND o."Id"=entry_row."CurrentOfferId" AND o."EntryId"=entry_id AND o."State"='offered'))
  OR (entry_row."State"='accepted' AND NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o
    JOIN public."Appointments" a ON a."TenantId"=o."TenantId" AND a."Id"=o."AppointmentId"
    WHERE o."TenantId"=scope_tenant AND o."Id"=entry_row."CurrentOfferId" AND o."EntryId"=entry_id AND o."State"='accepted'
     AND a."PatientId"=entry_row."PatientId" AND a."ReservationId"=o."ReservationId")) THEN
  RAISE EXCEPTION 'Waitlist offer, acceptance booking and accountable decision must commit together' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER waitlist_entry_commit AFTER INSERT OR UPDATE ON "AppointmentWaitlistEntries" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_waitlist_commit();
CREATE CONSTRAINT TRIGGER waitlist_offer_commit AFTER INSERT OR UPDATE ON "AppointmentWaitlistOffers" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_waitlist_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" FROM cureflow_tenant;
 GRANT UPDATE("State","Revision","CurrentOfferId","UpdatedAt") ON "AppointmentWaitlistEntries" TO cureflow_tenant;
 GRANT UPDATE("State","Revision","AppointmentId","UpdatedAt") ON "AppointmentWaitlistOffers" TO cureflow_tenant;
END IF; END $grant$;
