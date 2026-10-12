START TRANSACTION;
ALTER TABLE "Appointments" ADD "BookingKind" character varying(20);

ALTER TABLE "Appointments" ADD "BookingPolicyVersion" character varying(100);

ALTER TABLE "Appointments" ADD "FacilityId" uuid;

ALTER TABLE "Appointments" ADD "FollowUpAppointmentId" uuid;

ALTER TABLE "Appointments" ADD "ReservationId" uuid;

ALTER TABLE "Appointments" ADD "Revision" bigint NOT NULL DEFAULT 1;

CREATE INDEX "IX_Appointments_TenantId_FacilityId" ON "Appointments" ("TenantId", "FacilityId");

CREATE INDEX "IX_Appointments_TenantId_FollowUpAppointmentId" ON "Appointments" ("TenantId", "FollowUpAppointmentId");

CREATE UNIQUE INDEX "IX_Appointments_TenantId_ReservationId" ON "Appointments" ("TenantId", "ReservationId") WHERE "ReservationId" IS NOT NULL;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_AppointmentReservations_TenantId_ReservationId" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_Appointments_TenantId_FollowUpAppointmentId" FOREIGN KEY ("TenantId", "FollowUpAppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT booking_source_shape CHECK (
 "Revision">0 AND (("ReservationId" IS NULL AND "FacilityId" IS NULL AND "BookingKind" IS NULL AND "FollowUpAppointmentId" IS NULL AND "BookingPolicyVersion" IS NULL)
 OR COALESCE("ReservationId" IS NOT NULL AND "FacilityId" IS NOT NULL AND "DoctorUserId" IS NOT NULL
  AND "BookingKind" IN ('routine','walk_in','follow_up') AND ("BookingKind"='follow_up')=("FollowUpAppointmentId" IS NOT NULL)
  AND length("BookingPolicyVersion") BETWEEN 1 AND 100 AND "DurationMinutes" BETWEEN 1 AND 480,FALSE)));
CREATE FUNCTION public.guard_explicit_appointment_booking() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE held public."AppointmentReservations"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN
  IF OLD."ReservationId" IS NOT NULL THEN RAISE EXCEPTION 'Booked source history is retained' USING ERRCODE='23514'; END IF;
  RETURN OLD;
 END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF TG_OP='UPDATE' THEN
  IF OLD."ReservationId" IS NULL THEN
   IF NEW."ReservationId" IS NOT NULL OR NEW."FacilityId" IS NOT NULL OR NEW."BookingKind" IS NOT NULL THEN
    RAISE EXCEPTION 'Historical appointments require reviewed venue migration, never implicit binding' USING ERRCODE='23514';
   END IF;
   IF NEW."Status" IN (0,1) AND NOT NEW."IsDeleted"
    AND (NEW."DoctorUserId",NEW."ScheduledAt",NEW."DurationMinutes",NEW."Status",NEW."IsDeleted")
       IS DISTINCT FROM (OLD."DoctorUserId",OLD."ScheduledAt",OLD."DurationMinutes",OLD."Status",OLD."IsDeleted")
    AND (NEW."DurationMinutes" NOT BETWEEN 1 AND 480 OR EXISTS(
      SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId"
       AND s."TargetType"='practitioner' AND s."TargetId"=NEW."DoctorUserId" AND s."State" IN ('held','confirmed')
       AND s."StartsAtUtc"<NEW."ScheduledAt"+make_interval(mins=>LEAST(GREATEST(NEW."DurationMinutes",1),480))
       AND s."EndsAtUtc">NEW."ScheduledAt")) THEN
    RAISE EXCEPTION 'Legacy practitioner move conflicts with retained reservations or requires duration review' USING ERRCODE='23P01';
   END IF;
   RETURN NEW;
  END IF;
  IF (to_jsonb(OLD)-ARRAY['UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt']) THEN
   RAISE EXCEPTION 'Reserved source changes require reviewed revisioned transition' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
 IF NEW."ReservationId" IS NULL OR NEW."Revision"<>1 OR NEW."Status"<>0 OR NEW."CheckedInAt" IS NOT NULL OR NEW."CompletedAt" IS NOT NULL
  OR NOT NEW."IsActive" OR NEW."IsDeleted" THEN RAISE EXCEPTION 'New booking requires explicit held source and initial state' USING ERRCODE='23514'; END IF;
 SELECT * INTO held FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."ReservationId" FOR UPDATE;
 IF NOT FOUND OR held."State"<>'held' OR held."HoldExpiresAtUtc"<=clock_timestamp()
  OR held."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
  OR NEW."CreatedBy" IS DISTINCT FROM held."OwnerUserId" OR held."FacilityId"<>NEW."FacilityId" OR held."PractitionerUserId"<>NEW."DoctorUserId"
  OR held."StartsAtUtc"<>NEW."ScheduledAt" OR NEW."DurationMinutes"<>EXTRACT(EPOCH FROM(held."EndsAtUtc"-held."StartsAtUtc"))/60 THEN
  RAISE EXCEPTION 'New appointment must match exact live owned hold' USING ERRCODE='23514';
 END IF;
 IF public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
  OR NOT EXISTS(SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=NEW."PatientId" AND p."IsActive" AND NOT p."IsDeleted")
  OR NOT EXISTS(SELECT 1 FROM public."PatientIdentifiers" i WHERE i."TenantId"=NEW."TenantId" AND i."FacilityId"=NEW."FacilityId"
    AND i."PatientId"=NEW."PatientId" AND i."Namespace"='local-mrn') THEN
  RAISE EXCEPTION 'Booking requires current scoped patient/facility identifier' USING ERRCODE='23514';
 END IF;
 IF NEW."BookingKind"='follow_up' AND NOT EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId"
  AND a."Id"=NEW."FollowUpAppointmentId" AND a."PatientId"=NEW."PatientId" AND a."ScheduledAt"<NEW."ScheduledAt"
  AND a."Status" NOT IN (3,4) AND a."IsActive" AND NOT a."IsDeleted") THEN
  RAISE EXCEPTION 'Follow-up must preserve exact same patient source' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER explicit_appointment_booking_guard BEFORE INSERT OR UPDATE OR DELETE ON "Appointments"
 FOR EACH ROW EXECUTE FUNCTION public.guard_explicit_appointment_booking();
CREATE FUNCTION public.require_appointment_reservation_binding() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source_id uuid; scope_tenant uuid;
BEGIN
 IF TG_TABLE_NAME='Appointments' THEN source_id=NEW."Id"; scope_tenant=NEW."TenantId";
 ELSE source_id=NEW."AppointmentId"; scope_tenant=NEW."TenantId"; END IF;
 IF source_id IS NOT NULL AND EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND a."ReservationId" IS NOT NULL)
  AND NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" h
   ON h."TenantId"=a."TenantId" AND h."Id"=a."ReservationId"
   WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND h."AppointmentId"=a."Id" AND h."State"='confirmed'
    AND a."FacilityId"=h."FacilityId" AND a."DoctorUserId"=h."PractitionerUserId" AND a."ScheduledAt"=h."StartsAtUtc"
    AND a."DurationMinutes"=EXTRACT(EPOCH FROM(h."EndsAtUtc"-h."StartsAtUtc"))/60) THEN
  RAISE EXCEPTION 'Appointment and exact confirmed hold must commit atomically' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER appointment_reservation_binding_commit AFTER INSERT OR UPDATE ON "Appointments"
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_reservation_binding();
CREATE CONSTRAINT TRIGGER reservation_appointment_binding_commit AFTER INSERT OR UPDATE ON "AppointmentReservations"
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_reservation_binding();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008185741_AddExplicitAppointmentBookings', '10.0.0');

COMMIT;

