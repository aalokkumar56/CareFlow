START TRANSACTION;
ALTER TABLE "Users" ADD CONSTRAINT "AK_Users_TenantId_Id" UNIQUE ("TenantId", "Id");

ALTER TABLE "Appointments" ADD CONSTRAINT "AK_Appointments_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "AppointmentReservations" (
    "Id" uuid NOT NULL,
    "OwnerUserId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PractitionerUserId" uuid NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "HoldExpiresAtUtc" timestamp with time zone NOT NULL,
    "State" character varying(20) NOT NULL,
    "Revision" bigint NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "AppointmentId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentReservations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_AppointmentReservations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_AppointmentReservations_Appointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentReservations_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentReservations_Users_TenantId_OwnerUserId" FOREIGN KEY ("TenantId", "OwnerUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentReservations_Users_TenantId_PractitionerUserId" FOREIGN KEY ("TenantId", "PractitionerUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "AppointmentReservationSeats" (
    "Id" uuid NOT NULL,
    "ReservationId" uuid NOT NULL,
    "TargetType" character varying(20) NOT NULL,
    "TargetId" uuid NOT NULL,
    "Seat" integer NOT NULL,
    "CapacitySnapshot" integer NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "State" character varying(20) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentReservationSeats" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentReservationSeats_AppointmentReservations_TenantI~" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_AppointmentReservations_TenantId_AppointmentId" ON "AppointmentReservations" ("TenantId", "AppointmentId") WHERE "AppointmentId" IS NOT NULL;

CREATE INDEX "IX_AppointmentReservations_TenantId_FacilityId" ON "AppointmentReservations" ("TenantId", "FacilityId");

CREATE INDEX "IX_AppointmentReservations_TenantId_OwnerUserId_State_HoldExpi~" ON "AppointmentReservations" ("TenantId", "OwnerUserId", "State", "HoldExpiresAtUtc");

CREATE INDEX "IX_AppointmentReservations_TenantId_PractitionerUserId" ON "AppointmentReservations" ("TenantId", "PractitionerUserId");

CREATE UNIQUE INDEX "IX_AppointmentReservationSeats_TenantId_ReservationId_TargetTy~" ON "AppointmentReservationSeats" ("TenantId", "ReservationId", "TargetType", "TargetId");

CREATE INDEX "IX_AppointmentReservationSeats_TenantId_TargetType_TargetId_St~" ON "AppointmentReservationSeats" ("TenantId", "TargetType", "TargetId", "StartsAtUtc", "EndsAtUtc");

CREATE EXTENSION IF NOT EXISTS btree_gist;
ALTER TABLE "AppointmentReservations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentReservations" FORCE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentReservationSeats" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentReservationSeats" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "AppointmentReservations" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY tenant_scope ON "AppointmentReservationSeats" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY confirmed_patient_confidentiality ON "AppointmentReservations" AS RESTRICTIVE FOR SELECT USING ("AppointmentId" IS NULL OR EXISTS(
 SELECT 1 FROM public."Appointments" a WHERE a."TenantId"="AppointmentReservations"."TenantId" AND a."Id"="AppointmentReservations"."AppointmentId" AND NOT a."IsDeleted"));
ALTER TABLE "AppointmentReservations" ADD CONSTRAINT reservation_shape CHECK (COALESCE(
 "StartsAtUtc"<"EndsAtUtc" AND "EndsAtUtc"<="StartsAtUtc"+interval '8 hours' AND "HoldExpiresAtUtc">"CreatedAt"
 AND "HoldExpiresAtUtc"<="CreatedAt"+interval '30 minutes' AND "HoldExpiresAtUtc"<="EndsAtUtc"
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "CreatedBy"="OwnerUserId" AND "IsActive" AND NOT "IsDeleted"
 AND (("State"='held' AND "Revision"=1 AND "AppointmentId" IS NULL)
  OR ("State" IN ('expired','released') AND "Revision"=2 AND "AppointmentId" IS NULL)
  OR ("State"='confirmed' AND "Revision"=2 AND "AppointmentId" IS NOT NULL)),false));
ALTER TABLE "AppointmentReservationSeats" ADD CONSTRAINT reservation_seat_shape CHECK (
 "TargetType" IN ('practitioner','resource') AND "CapacitySnapshot" BETWEEN 1 AND 100000 AND "Seat" BETWEEN 1 AND "CapacitySnapshot"
 AND ("TargetType"<>'practitioner' OR ("Seat"=1 AND "CapacitySnapshot"=1))
 AND "StartsAtUtc"<"EndsAtUtc" AND "State" IN ('held','confirmed','released','expired') AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "AppointmentReservationSeats" ADD CONSTRAINT reservation_seat_no_overlap EXCLUDE USING gist
 ("TenantId" WITH =,"TargetType" WITH =,"TargetId" WITH =,"Seat" WITH =,tstzrange("StartsAtUtc","EndsAtUtc",'[)') WITH &&)
 WHERE ("State" IN ('held','confirmed'));
CREATE FUNCTION public.guard_appointment_reservation() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation history is retained' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."State"<>'held' OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
   OR NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."OwnerUserId" AND u."IsActive" AND NOT u."IsDeleted") THEN
   RAISE EXCEPTION 'Hold requires current owner and active facility' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'held' OR NEW."State" NOT IN ('confirmed','released','expired') OR NEW."Revision"<>OLD."Revision"+1
   OR (to_jsonb(OLD)-ARRAY['State','Revision','AppointmentId','UpdatedAt'])
      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) THEN
   RAISE EXCEPTION 'Reservation source is immutable; transition is terminal' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='expired' THEN
   IF OLD."HoldExpiresAtUtc">clock_timestamp() THEN RAISE EXCEPTION 'Live hold cannot expire' USING ERRCODE='23514'; END IF;
  ELSIF NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
   RAISE EXCEPTION 'Reservation owner is required' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='confirmed' AND (OLD."HoldExpiresAtUtc"<=clock_timestamp() OR NOT EXISTS(
   SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId"
    AND a."CreatedAt">=OLD."CreatedAt" AND a."DoctorUserId"=NEW."PractitionerUserId" AND a."ScheduledAt"=NEW."StartsAtUtc"
    AND a."DurationMinutes"=EXTRACT(EPOCH FROM (NEW."EndsAtUtc"-NEW."StartsAtUtc"))/60 AND a."Status" IN (0,1) AND NOT a."IsDeleted")) THEN
   RAISE EXCEPTION 'Expired hold or mismatched new appointment cannot confirm' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_reservation_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentReservations" FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_reservation();
CREATE FUNCTION public.guard_appointment_reservation_seat() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE reservation public."AppointmentReservations"%ROWTYPE; capacity integer;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation occupancy history is retained' USING ERRCODE='23514'; END IF;
 SELECT * INTO reservation FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReservationId";
 IF NOT FOUND OR NEW."StartsAtUtc"<>reservation."StartsAtUtc" OR NEW."EndsAtUtc"<>reservation."EndsAtUtc" OR NEW."State"<>reservation."State" THEN
  RAISE EXCEPTION 'Occupancy must match exact source reservation' USING ERRCODE='23514';
 END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."State"<>'held' THEN RAISE EXCEPTION 'Occupancy starts held' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(734291);
  PERFORM pg_advisory_xact_lock(hashtextextended('appointment-seat:'||NEW."TenantId"::text||':'||NEW."TargetType"||':'||NEW."TargetId"::text,0));
  IF NEW."TargetType"='practitioner' THEN
   capacity=1;
   IF NEW."TargetId"<>reservation."PractitionerUserId" OR NOT EXISTS(
    SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
     WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."TargetId" AND u."IsActive" AND NOT u."IsDeleted"
      AND p."IsActive" AND NOT p."IsDeleted" AND p."IsAvailable") THEN RAISE EXCEPTION 'Practitioner scope unavailable' USING ERRCODE='23514'; END IF;
  ELSE
   SELECT r."Capacity" INTO capacity FROM public."FacilityResources" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."TargetId"
    AND r."FacilityId"=reservation."FacilityId" AND r."ResourceType" IN ('room','equipment') AND r."IsActive" AND NOT r."IsDeleted"
    AND r."OperationalStatus"='enabled' AND r."EffectiveFrom"<=NEW."StartsAtUtc" AND (r."EffectiveUntil" IS NULL OR r."EffectiveUntil">=NEW."EndsAtUtc") FOR SHARE;
  END IF;
  IF capacity IS NULL OR NEW."CapacitySnapshot"<>capacity OR NEW."Seat">capacity THEN
   RAISE EXCEPTION 'Seat is outside current resource capacity' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'held' OR NEW."State" NOT IN ('confirmed','released','expired')
   OR (to_jsonb(OLD)-ARRAY['State','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','UpdatedAt']) THEN
   RAISE EXCEPTION 'Occupancy identity/range cannot change; transition is terminal' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_reservation_seat_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentReservationSeats" FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_reservation_seat();
CREATE FUNCTION public.require_reservation_occupancy_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE reservation_id uuid; scope_tenant uuid; current_state text; practitioner uuid;
BEGIN
 IF TG_TABLE_NAME='AppointmentReservations' THEN reservation_id=NEW."Id"; ELSE reservation_id=NEW."ReservationId"; END IF;
 scope_tenant=NEW."TenantId";
 SELECT r."State",r."PractitionerUserId" INTO current_state,practitioner FROM public."AppointmentReservations" r WHERE r."TenantId"=scope_tenant AND r."Id"=reservation_id;
 IF current_state IS NULL OR NOT EXISTS(SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=scope_tenant AND s."ReservationId"=reservation_id AND s."TargetType"='practitioner' AND s."TargetId"=practitioner)
  OR EXISTS(SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=scope_tenant AND s."ReservationId"=reservation_id AND s."State"<>current_state)
  OR (SELECT count(*) FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=scope_tenant AND s."ReservationId"=reservation_id)>6 THEN
   RAISE EXCEPTION 'Reservation and all bounded occupancy seats must commit together' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER reservation_occupancy_commit AFTER INSERT OR UPDATE ON "AppointmentReservations" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_reservation_occupancy_commit();
CREATE CONSTRAINT TRIGGER reservation_seat_commit AFTER INSERT OR UPDATE ON "AppointmentReservationSeats" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_reservation_occupancy_commit();
CREATE FUNCTION public.guard_reserved_resource_capacity() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NEW."Capacity"<OLD."Capacity" AND EXISTS(SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId"
  AND s."TargetType"='resource' AND s."TargetId"=NEW."Id" AND s."State" IN ('held','confirmed') AND s."EndsAtUtc">clock_timestamp() AND s."Seat">NEW."Capacity") THEN
  RAISE EXCEPTION 'Capacity reduction requires reviewed reservation reconciliation' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER reserved_resource_capacity BEFORE UPDATE ON "FacilityResources" FOR EACH ROW EXECUTE FUNCTION public.guard_reserved_resource_capacity();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentReservations","AppointmentReservationSeats" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentReservations","AppointmentReservationSeats" FROM cureflow_tenant;
 GRANT UPDATE ("State","Revision","AppointmentId","UpdatedAt") ON "AppointmentReservations" TO cureflow_tenant;
 GRANT UPDATE ("State","UpdatedAt") ON "AppointmentReservationSeats" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008184110_AddAtomicAppointmentReservations', '10.0.0');

COMMIT;

