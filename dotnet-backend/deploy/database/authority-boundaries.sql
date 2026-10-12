-- Operator preparation ONLY. Review against a backed-up rehearsal database first.
-- Run with the migration owner; never run this from application startup.
-- Creates NOLOGIN grant groups, not passwords/login accounts. Grant each group to
-- exactly one separately provisioned NOSUPERUSER NOBYPASSRLS INHERIT login role.
BEGIN;
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN CREATE ROLE cureflow_tenant NOLOGIN NOSUPERUSER NOBYPASSRLS; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_identity') THEN CREATE ROLE cureflow_identity NOLOGIN NOSUPERUSER NOBYPASSRLS; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_catalogue') THEN CREATE ROLE cureflow_catalogue NOLOGIN NOSUPERUSER NOBYPASSRLS; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_platform_identity') THEN CREATE ROLE cureflow_platform_identity NOLOGIN NOSUPERUSER NOBYPASSRLS; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_control_plane') THEN CREATE ROLE cureflow_control_plane NOLOGIN NOSUPERUSER NOBYPASSRLS; END IF;
END $$;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM cureflow_tenant,cureflow_identity,cureflow_catalogue,cureflow_control_plane,cureflow_platform_identity;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM cureflow_tenant,cureflow_identity,cureflow_catalogue,cureflow_control_plane,cureflow_platform_identity;
GRANT USAGE ON SCHEMA public TO cureflow_tenant,cureflow_identity,cureflow_catalogue,cureflow_control_plane,cureflow_platform_identity;
-- Fail if PUBLIC grants supply alternate access: operator must review/revoke those
-- independently (this script never revokes rights from unrelated users).
DO $$ DECLARE t record; BEGIN
 FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname='public' AND c.relkind IN ('r','p') AND c.relrowsecurity AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='TenantId' AND NOT a.attisdropped)
 LOOP
   EXECUTE format('GRANT SELECT,INSERT,UPDATE,DELETE ON public.%I TO cureflow_tenant',t.relname);
   -- Replace the historical mutable-setting-only policy with a role-gated policy.
   -- Restrictive guard also protects tables having additional permissive policies.
   EXECUTE format('DROP POLICY IF EXISTS cureflow_authority_guard ON public.%I',t.relname);
   EXECUTE format($policy$CREATE POLICY cureflow_authority_guard ON public.%I AS RESTRICTIVE FOR ALL
     USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
       OR (current_setting('app.platform_admin',true)='true' AND
          ((pg_has_role(current_user,'cureflow_identity','MEMBER') AND %L=ANY(ARRAY['Users','UserRoleAssignments','HospitalProfiles','TenantOnboardingStates','TemplatePlaceholders']))
          OR (pg_has_role(current_user,'cureflow_control_plane','MEMBER') AND %L=ANY(ARRAY['Facilities','TenantEntitlements','TenantApiActivities','TenantOnboardingStates'])))))
     WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
       OR (current_setting('app.platform_admin',true)='true' AND
          ((pg_has_role(current_user,'cureflow_identity','MEMBER') AND %L=ANY(ARRAY['Users','UserRoleAssignments','HospitalProfiles','TenantOnboardingStates','TemplatePlaceholders']))
          OR (pg_has_role(current_user,'cureflow_control_plane','MEMBER') AND %L=ANY(ARRAY['Facilities','TenantEntitlements','TenantApiActivities','TenantOnboardingStates'])))))$policy$,
       t.relname,t.relname,t.relname,t.relname,t.relname);
 END LOOP;
END $$;
GRANT SELECT ON "Tenants" TO cureflow_catalogue,cureflow_tenant;
GRANT UPDATE ("OnboardingComplete","Timezone","UpdatedAt") ON "Tenants" TO cureflow_tenant;
-- Tenant identity table also receives its own Id-based scope; catalogue sees public
-- branding and worker identity only; identity/control-plane use explicit roles.
ALTER TABLE "Tenants" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Tenants" FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS cureflow_tenant_identity ON "Tenants";
DROP POLICY IF EXISTS cureflow_tenant_identity_allow ON "Tenants";
CREATE POLICY cureflow_tenant_identity_allow ON "Tenants" FOR ALL USING (true) WITH CHECK (true);
CREATE POLICY cureflow_tenant_identity ON "Tenants" AS RESTRICTIVE FOR ALL
 USING ("Id"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
   OR pg_has_role(current_user,'cureflow_catalogue','MEMBER')
   OR (current_setting('app.platform_admin',true)='true' AND (pg_has_role(current_user,'cureflow_identity','MEMBER') OR pg_has_role(current_user,'cureflow_control_plane','MEMBER'))))
 WITH CHECK ("Id"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
   OR (current_setting('app.platform_admin',true)='true' AND (pg_has_role(current_user,'cureflow_identity','MEMBER') OR pg_has_role(current_user,'cureflow_control_plane','MEMBER'))));
GRANT SELECT ON "Roles","Permissions","RolePermissions","PlatformModules","PlanModules" TO cureflow_tenant,cureflow_identity;
GRANT SELECT,INSERT,UPDATE ON "Users","UserRoleAssignments","HospitalProfiles","TenantOnboardingStates","TemplatePlaceholders","RefreshSessions" TO cureflow_identity;
GRANT SELECT,INSERT ON "Tenants" TO cureflow_identity;
GRANT SELECT,INSERT,UPDATE ON "Tenants","PlatformUsers","PlatformRoles","PlatformCommands","PlatformModules","PlanModules","TenantEntitlements","Facilities","PlatformOperationJobs","PlatformSupportSessions","TenantOnboardingStates" TO cureflow_control_plane;
GRANT SELECT,INSERT ON "PlatformAuditLogs" TO cureflow_control_plane;
GRANT SELECT ON "TenantApiActivities" TO cureflow_control_plane;
-- Bounded support aggregate: no chart fields, arbitrary SQL or row identifiers.
CREATE OR REPLACE FUNCTION public.cureflow_platform_usage(tenant_id uuid)
RETURNS TABLE ("Users" bigint,"Patients" bigint)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT (SELECT count(*) FROM public."Users" WHERE "TenantId"=tenant_id AND NOT "IsDeleted"),
        (SELECT count(*) FROM public."Patients" WHERE "TenantId"=tenant_id AND NOT "IsDeleted");
$$;
REVOKE ALL ON FUNCTION public.cureflow_platform_usage(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.cureflow_platform_usage(uuid) TO cureflow_control_plane;
-- Newly created tables receive NO authority grants automatically; review each migration.
COMMIT;
-- Append these only after reviewing the provider routing identifier contract.
-- Unknown, missing or ambiguous phone identifiers never select the first tenant.
BEGIN;
CREATE OR REPLACE FUNCTION public.cureflow_webhook_tenant(phone_id text)
RETURNS uuid LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT CASE WHEN count(*)=1 THEN (array_agg(s."TenantId"))[1] ELSE NULL END
 FROM public."WhatsAppSettings" s JOIN public."Tenants" t ON t."Id"=s."TenantId"
 WHERE s."PhoneNumberId"=phone_id AND NOT s."IsDeleted" AND t."IsActive" AND NOT t."IsDeleted";
$$;
REVOKE ALL ON FUNCTION public.cureflow_webhook_tenant(text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.cureflow_webhook_tenant(text) TO cureflow_catalogue;
COMMIT;
-- Apply after PlatformSessions migration; login credential cannot manage tenant configuration.
BEGIN;
GRANT SELECT,INSERT ON "PlatformUsers","PlatformSessions" TO cureflow_platform_identity;
GRANT INSERT ON "PlatformAuditLogs" TO cureflow_platform_identity;
-- Operators may check/revoke sessions, never mint them or browse future secret columns.
REVOKE ALL ON "PlatformSessions" FROM cureflow_control_plane;
-- Also remove previous column grants before installing the reviewed metadata boundary.
DO $$ DECLARE session_column record; BEGIN
 FOR session_column IN SELECT attname FROM pg_attribute
  WHERE attrelid='public."PlatformSessions"'::regclass AND attnum>0 AND NOT attisdropped LOOP
  EXECUTE format('REVOKE SELECT (%1$I), INSERT (%1$I), UPDATE (%1$I), REFERENCES (%1$I) ON public."PlatformSessions" FROM cureflow_control_plane',session_column.attname);
 END LOOP;
END $$;
GRANT SELECT ("Id","PlatformUserId","ExpiresAt","RevokedAt","SecurityRevision","IsActive","IsDeleted")
 ON "PlatformSessions" TO cureflow_control_plane;
GRANT UPDATE ("RevokedAt","UpdatedAt","SecurityRevision") ON "PlatformSessions" TO cureflow_control_plane;
COMMIT;
-- Retained versions are append-only even when the generic tenant-table grant loop runs.
BEGIN;
DO $$ BEGIN
 IF to_regclass('public."PrivateDocumentVersions"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "PrivateDocumentVersions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PrivateDocumentVersions" FROM cureflow_tenant;
 END IF;
END $$;
COMMIT;
-- Normal runtime can append protected clinical evidence, never browse/rewrite it.
BEGIN;
DO $$ BEGIN
 IF to_regclass('public."ClinicalAuditEvents"') IS NOT NULL THEN
  GRANT INSERT ON "ClinicalAuditEvents" TO cureflow_tenant;
  REVOKE SELECT,UPDATE,DELETE ON "ClinicalAuditEvents" FROM cureflow_tenant;
 END IF;
END $$;
COMMIT;

-- Reapply immutable command/event grants after the generic tenant-table loop.
BEGIN;
DO $$ BEGIN
 IF to_regclass('public."IdempotencyRecords"') IS NOT NULL THEN
  GRANT SELECT,INSERT,UPDATE ON "IdempotencyRecords" TO cureflow_tenant;
  REVOKE DELETE ON "IdempotencyRecords" FROM cureflow_tenant;
 END IF;
 IF to_regclass('public."OutboxEvents"') IS NOT NULL THEN
  GRANT SELECT,INSERT,UPDATE ON "OutboxEvents" TO cureflow_tenant;
  REVOKE DELETE ON "OutboxEvents" FROM cureflow_tenant;
 END IF;
 IF to_regclass('public."OutboxConsumerReceipts"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "OutboxConsumerReceipts" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "OutboxConsumerReceipts" FROM cureflow_tenant;
 END IF;
END $$;
COMMIT;

BEGIN;
DO $$ BEGIN
 IF to_regclass('public."DocumentScanEvidence"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "DocumentScanEvidence" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "DocumentScanEvidence" FROM cureflow_tenant;
 END IF;
END $$;
COMMIT;

BEGIN;
DO $$ BEGIN
 IF to_regclass('public."TenantProcessingPolicies"') IS NOT NULL THEN
  GRANT SELECT ON "TenantProcessingPolicies" TO cureflow_tenant;
  REVOKE INSERT,UPDATE,DELETE ON "TenantProcessingPolicies" FROM cureflow_tenant;
 END IF;
END $$;
COMMIT;


-- Retained organization histories: runtime never hard-deletes nodes or rewrites settings versions.
BEGIN;
DO $$ BEGIN
 IF to_regclass('public."OrganizationUnits"') IS NOT NULL THEN
  GRANT SELECT,INSERT,UPDATE ON "OrganizationUnits","FacilityLocations" TO cureflow_tenant;
  REVOKE DELETE ON "OrganizationUnits","FacilityLocations" FROM cureflow_tenant;
 END IF;
 IF to_regclass('public."FacilityOperatingSettings"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "FacilityOperatingSettings" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "FacilityOperatingSettings" FROM cureflow_tenant;
 END IF;
END $$;
COMMIT;
BEGIN;
DO $$ BEGIN IF to_regclass('public."FacilityResources"') IS NOT NULL THEN
 GRANT SELECT,INSERT,UPDATE ON "FacilityResources" TO cureflow_tenant;
 REVOKE DELETE ON "FacilityResources" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
BEGIN;
DO $$ BEGIN IF to_regclass('public."OrganizationStructureVersions"') IS NOT NULL THEN
 GRANT SELECT ON "OrganizationStructureVersions" TO cureflow_tenant;
 REVOKE INSERT,UPDATE,DELETE ON "OrganizationStructureVersions" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientIdentifiers"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "PatientIdentifiers" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientIdentifiers" FROM cureflow_tenant;
 GRANT SELECT,INSERT,UPDATE ON "PatientIdentifierCounters" TO cureflow_tenant;
 REVOKE DELETE ON "PatientIdentifierCounters" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientContactVersions"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "PatientContactVersions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientContactVersions" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientIdentityReviewVersions"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "PatientIdentityReviewVersions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientIdentityReviewVersions" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
-- Apply after canonical mapping migration; evidence has no destructive runtime grant.
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientMerges"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "PatientMerges","PatientLinkJournals" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientMerges","PatientLinkJournals" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
-- Apply after mapping reversal migration, restoring only constrained reversal columns.
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientMerges"') IS NOT NULL
 AND EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid=to_regclass('public."PatientMerges"') AND tgname='patient_mapping_journal_required') THEN
 REVOKE UPDATE ON "PatientMerges" FROM cureflow_tenant;
 GRANT UPDATE ("ReversedAt","ReversalReason","Revision","UpdatedAt","UpdatedBy") ON "PatientMerges" TO cureflow_tenant;
END IF; END $$;
COMMIT;

-- Database-triggered demographic provenance; runtime may read, never insert/rewrite evidence.
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientDemographicVersions"') IS NOT NULL THEN
 GRANT SELECT ON "PatientDemographicVersions" TO cureflow_tenant;
 REVOKE INSERT,UPDATE,DELETE ON "PatientDemographicVersions" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
-- Retained unknown identity and accountable verification evidence.
BEGIN;
DO $$ BEGIN IF to_regclass('public."TemporaryPatientIdentities"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "TemporaryPatientIdentities","PatientIdentityVerifications" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "TemporaryPatientIdentities","PatientIdentityVerifications" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
-- Explicit confidential-chart predicate and retained effective scope state.
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientConfidentialityStates"') IS NOT NULL THEN
 GRANT SELECT,INSERT,UPDATE ON "PatientConfidentialityStates" TO cureflow_tenant;
 REVOKE DELETE ON "PatientConfidentialityStates" FROM cureflow_tenant;
 GRANT SELECT,INSERT ON "PatientAlertVersions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientAlertVersions" FROM cureflow_tenant;
 IF to_regprocedure('public.cureflow_patient_read_allowed(uuid,uuid)') IS NOT NULL THEN
  GRANT EXECUTE ON FUNCTION public.cureflow_patient_read_allowed(uuid,uuid) TO cureflow_tenant;
 END IF;
END IF; END $$;
COMMIT;
-- Reviewed source imports are retained; only terminal decision metadata is writable.
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientImportBatches"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "PatientImportBatches","PatientImportRows" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientImportBatches","PatientImportRows" FROM cureflow_tenant;
 GRANT UPDATE ("Decision","PatientId","ReviewReason","ReviewedBy","ReviewedAt","ReviewPolicyVersion","UpdatedAt","UpdatedBy") ON "PatientImportRows" TO cureflow_tenant;
END IF; END $$;
COMMIT;
-- Explicit retained schedule rules; runtime can only disable a current version.
BEGIN;
DO $$ BEGIN IF to_regclass('public."ScheduleRules"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "ScheduleRules" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "ScheduleRules" FROM cureflow_tenant;
 GRANT UPDATE ("IsActive","Revision","UpdatedAt","UpdatedBy") ON "ScheduleRules" TO cureflow_tenant;
END IF; END $$;
COMMIT;
-- Local directory export artifact commit only; source/provenance cannot be rewritten.
BEGIN;
DO $$ BEGIN IF to_regclass('public."PatientExportJobs"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "PatientExportJobs" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientExportJobs" FROM cureflow_tenant;
 GRANT UPDATE ("State","Revision","ObjectKey","ObjectVersion","Sha256","Length","UpdatedAt") ON "PatientExportJobs" TO cureflow_tenant;
END IF; END $$;
COMMIT;
-- Scoped facility metadata reads and read-lock helper never authorize tenant mutations.
BEGIN;
REVOKE ALL ON public."Facilities" FROM cureflow_tenant;
DO $$ BEGIN IF to_regprocedure('public.cureflow_lock_active_facility(uuid,uuid)') IS NOT NULL
 AND EXISTS(SELECT 1 FROM pg_class WHERE oid=to_regclass('public."Facilities"') AND relrowsecurity AND relforcerowsecurity) THEN
 GRANT SELECT ON public."Facilities" TO cureflow_tenant;
 GRANT EXECUTE ON FUNCTION public.cureflow_lock_active_facility(uuid,uuid) TO cureflow_tenant;
END IF; END $$;
COMMIT;
-- Reviewed lead source/identity/referral attribution is append-only.
BEGIN;
DO $$ BEGIN IF to_regclass('public."LeadConversions"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "LeadConversions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "LeadConversions" FROM cureflow_tenant;
END IF; END $$;
COMMIT;
-- Retained leave/holiday/block definitions and immutable potential booking impact.
BEGIN;
DO $$ BEGIN IF to_regclass('public."ScheduleExceptions"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "ScheduleExceptions","AppointmentRescheduleWorks" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "ScheduleExceptions","AppointmentRescheduleWorks" FROM cureflow_tenant;
 GRANT UPDATE ("IsActive","Revision","DisabledAt","UpdatedAt","UpdatedBy") ON "ScheduleExceptions" TO cureflow_tenant;
END IF; END $$;
COMMIT;
-- Operational occupancy identity/range are immutable; only terminal state/link columns change.
BEGIN;
DO $$ BEGIN IF to_regclass('public."AppointmentReservations"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "AppointmentReservations","AppointmentReservationSeats" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentReservations","AppointmentReservationSeats" FROM cureflow_tenant;
 GRANT UPDATE ("State","Revision","AppointmentId","UpdatedAt") ON "AppointmentReservations" TO cureflow_tenant;
 GRANT UPDATE ("State","UpdatedAt") ON "AppointmentReservationSeats" TO cureflow_tenant;
END IF; END $$;
COMMIT;-- Arrival evidence is immutable; daily token counters allow monotone allocation only.
BEGIN;
DO $$ BEGIN IF to_regclass('public."QueueEntries"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "QueueEntries","QueueCounters" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "QueueEntries","QueueCounters" FROM cureflow_tenant;
 GRANT UPDATE ("LastToken","UpdatedAt","UpdatedBy") ON "QueueCounters" TO cureflow_tenant;
END IF; END $$;
COMMIT;

-- Optional booking change phase after AddRetainedAppointmentChanges.
BEGIN;
DO $grants$ BEGIN
 IF to_regclass('public."AppointmentBookingChanges"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "AppointmentBookingChanges" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "AppointmentBookingChanges" FROM cureflow_tenant;
 END IF;
END $grants$;
COMMIT;-- Priority and dispatch decisions are retained once; no runtime rewriting or deletion.
BEGIN;
DO $$ BEGIN IF to_regclass('public."QueueStarts"') IS NOT NULL THEN
 GRANT SELECT,INSERT ON "QueuePriorityOverrides","QueueStarts" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "QueuePriorityOverrides","QueueStarts" FROM cureflow_tenant;
END IF; END $$;
COMMIT;

-- Optional waitlist phase after AddReleasedCapacityWaitlists.
BEGIN;
DO $grants$ BEGIN
 IF to_regclass('public."AppointmentWaitlistEntries"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" FROM cureflow_tenant;
  GRANT UPDATE("State","Revision","CurrentOfferId","UpdatedAt") ON "AppointmentWaitlistEntries" TO cureflow_tenant;
  GRANT UPDATE("State","Revision","AppointmentId","UpdatedAt") ON "AppointmentWaitlistOffers" TO cureflow_tenant;
 END IF;
END $grants$;
COMMIT;
DO $no_shows$ BEGIN
 IF to_regclass('public."AppointmentNoShows"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "AppointmentNoShows" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "AppointmentNoShows" FROM cureflow_tenant;
 END IF;
END $no_shows$;
DO $encounters$ BEGIN
 IF to_regclass('public."EncounterTransitions"') IS NOT NULL THEN
  GRANT SELECT,INSERT ON "Visits","EncounterTransitions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "Visits","EncounterTransitions" FROM cureflow_tenant;
  GRANT UPDATE("Symptoms","Diagnosis","FollowUpAdvice","FollowUpDate","Status","EndedAt","Revision","UpdatedAt","UpdatedBy") ON "Visits" TO cureflow_tenant;
 END IF;
END $encounters$;