[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SchemaSql,
    [Parameter(Mandatory)][string]$AuthoritySql,
    [Parameter(Mandatory)][string]$WorkRoot,
    [string]$PostgreSqlBin = 'C:\Program Files\PostgreSQL\18\bin'
)

# Empty, synthetic destinations only. Never reads application connection settings.
# This operational rehearsal does not back up or certify an existing hospital.
$ErrorActionPreference = 'Stop'
$schemaPath = (Resolve-Path -LiteralPath $SchemaSql).Path
$authorityPath = (Resolve-Path -LiteralPath $AuthoritySql).Path
foreach ($toolName in @('initdb', 'pg_ctl', 'psql', 'pg_dump', 'pg_restore')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PostgreSqlBin ($toolName + '.exe')) -PathType Leaf)) {
        throw "Required PostgreSQL tool is unavailable: $toolName"
    }
}
New-Item -ItemType Directory -Path $WorkRoot -Force | Out-Null
$workPath = (Resolve-Path -LiteralPath $WorkRoot).Path
$runPath = Join-Path $workPath ('synthetic-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runPath | Out-Null
$schemaSnapshot = Join-Path $runPath 'schema.sql'
$authoritySnapshot = Join-Path $runPath 'authority.sql'
Copy-Item -LiteralPath $schemaPath -Destination $schemaSnapshot
Copy-Item -LiteralPath $authorityPath -Destination $authoritySnapshot
$schemaPath = $schemaSnapshot
$authorityPath = $authoritySnapshot
$initialSchemaPath=$schemaPath
$forwardSchemaPath=$null
$schemaText=[IO.File]::ReadAllText($schemaPath)
$bookingMarker=$schemaText.IndexOf('20261008185741_AddExplicitAppointmentBookings',[StringComparison]::Ordinal)
if($bookingMarker -ge 0){
    $forwardStart=$schemaText.LastIndexOf('START TRANSACTION;',$bookingMarker,[StringComparison]::Ordinal)
    if($forwardStart -le 0){throw 'The reviewed explicit-booking migration boundary was not found.'}
    $initialSchemaPath=Join-Path $runPath 'baseline-before-explicit-bookings.sql'
    $forwardSchemaPath=Join-Path $runPath 'forward-explicit-bookings.sql'
    [IO.File]::WriteAllText($initialSchemaPath,$schemaText.Substring(0,$forwardStart))
    [IO.File]::WriteAllText($forwardSchemaPath,$schemaText.Substring($forwardStart))
}
$clusterPath = Join-Path $runPath 'cluster'
$passwordPath = Join-Path $runPath 'initialize-password.txt'
$passfilePath = Join-Path $runPath 'pgpass.conf'
$originalPassfile = $env:PGPASSFILE
$started = $false
$password = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$utf8 = [Text.UTF8Encoding]::new($false)

function Invoke-PgSql([string]$database, [string]$statement) {
    $result = & (Join-Path $PostgreSqlBin 'psql.exe') -X -h 127.0.0.1 -p $port -U rehearsal_owner -d $database -v ON_ERROR_STOP=1 -At -c $statement 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Synthetic SQL failed: $result" }
    return $result
}
function Apply-Sql([string]$path) {
    $result = & (Join-Path $PostgreSqlBin 'psql.exe') -X -h 127.0.0.1 -p $port -U rehearsal_owner -d cureflow_rehearsal -v ON_ERROR_STOP=1 -f $path 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Synthetic schema/grant application failed: $result" }
}

try {
    [IO.File]::WriteAllText($passwordPath, $password, $utf8)
    $initializeOutput = & (Join-Path $PostgreSqlBin 'initdb.exe') -D $clusterPath -U rehearsal_owner --auth=scram-sha-256 --pwfile=$passwordPath --encoding=UTF8 --locale=C 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Synthetic cluster initialization failed: $initializeOutput" }
    Remove-Item -LiteralPath $passwordPath
    [IO.File]::WriteAllText($passfilePath, "127.0.0.1:${port}:*:rehearsal_owner:$password", $utf8)
    $env:PGPASSFILE = $passfilePath
    $launcher = Start-Process -FilePath (Join-Path $PostgreSqlBin 'pg_ctl.exe') -ArgumentList @(
        '-D', ('"' + $clusterPath + '"'), '-l', ('"' + (Join-Path $runPath 'server.log') + '"'),
        '-o', ('"-h 127.0.0.1 -p ' + $port + '"'), '-w', 'start'
    ) -WindowStyle Hidden -PassThru
    $started = $true
    # Start-Process -Wait also waits for the persistent server descendant on Windows.
    if (-not $launcher.WaitForExit(20000) -or $launcher.ExitCode -ne 0) {
        throw 'Synthetic server launch failed; inspect its retained server.log.'
    }
    Invoke-PgSql postgres 'CREATE DATABASE cureflow_rehearsal' | Out-Null
    Apply-Sql $initialSchemaPath
    Apply-Sql $authorityPath
    # Repeat deployment grants to verify their replay behavior.
    Apply-Sql $authorityPath
    $grantEvidence = Invoke-PgSql cureflow_rehearsal @'
DO $$ BEGIN
 IF has_table_privilege('cureflow_control_plane','"Patients"','SELECT')
 OR has_table_privilege('cureflow_control_plane','"PlatformSessions"','SELECT')
 OR has_column_privilege('cureflow_control_plane','"PlatformSessions"','SecurityRevision','INSERT')
 OR NOT has_column_privilege('cureflow_control_plane','"PlatformSessions"','RevokedAt','UPDATE')
 THEN RAISE EXCEPTION 'Restricted authority grants do not match the contract'; END IF;
END $$;
SET ROLE cureflow_control_plane;
DO $$ BEGIN
 BEGIN PERFORM 1 FROM public."Patients" LIMIT 1;
 EXCEPTION WHEN insufficient_privilege THEN RETURN; END;
 RAISE EXCEPTION 'Control-plane clinical access unexpectedly succeeded';
END $$;
RESET ROLE;
BEGIN;
INSERT INTO public."Tenants" ("Id","Slug","Name","Plan","SubscriptionStatus","SeatLimit","PatientLimit","MessagesQuotaMonthly","CreatedAt","UpdatedAt","IsActive","IsDeleted")
VALUES ('10000000-0000-4000-8000-000000000001','synthetic-a','Synthetic A',0,0,1,10,0,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
       ('10000000-0000-4000-8000-000000000002','synthetic-b','Synthetic B',0,0,1,10,0,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO public."Patients" ("Id","TenantId","Name","Phone","EmailNotificationsEnabled","Gender","BloodGroup","InquirySource","Status","Tags","CreatedAt","UpdatedAt","IsActive","IsDeleted")
VALUES ('20000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','Synthetic A','',false,0,0,'synthetic',0,'[]',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
       ('20000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000002','Synthetic B','',false,0,0,'synthetic',0,'[]',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
COMMIT;
DO $$ BEGIN
 IF to_regclass('public."PatientDemographicVersions"') IS NOT NULL THEN
  IF (SELECT count(*) FROM public."PatientDemographicVersions" WHERE "PatientId" IN
      ('20000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000002') AND "VerificationStatus"='unverified') <> 2
   OR has_table_privilege('cureflow_tenant','public."PatientDemographicVersions"','INSERT,UPDATE,DELETE')
  THEN RAISE EXCEPTION 'Demographic provenance capture or immutable grants failed'; END IF;
 END IF;
END $$;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','',true);
DO $$ BEGIN IF EXISTS (SELECT 1 FROM public."Patients") THEN RAISE EXCEPTION 'Missing tenant exposes clinical rows'; END IF; END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.platform_admin','true',true);
DO $$ BEGIN
 IF (SELECT count(*) FROM public."Patients") <> 1
 OR EXISTS (SELECT 1 FROM public."Patients" WHERE "TenantId" <> '10000000-0000-4000-8000-000000000001')
 THEN RAISE EXCEPTION 'Cross-tenant clinical rows exposed'; END IF;
 BEGIN
  UPDATE public."Patients" SET "TenantId"='10000000-0000-4000-8000-000000000002' WHERE "Id"='20000000-0000-4000-8000-000000000001';
 EXCEPTION WHEN insufficient_privilege THEN RETURN; END;
 RAISE EXCEPTION 'Cross-tenant reassignment unexpectedly succeeded';
END $$;
ROLLBACK;
SET ROLE cureflow_tenant;
DO $$ BEGIN IF EXISTS (SELECT 1 FROM public."Patients") THEN RAISE EXCEPTION 'Transaction tenant context survived rollback'; END IF; END $$;
RESET ROLE;
CREATE SCHEMA rehearsal;
CREATE TABLE rehearsal.evidence(id uuid PRIMARY KEY, value text NOT NULL);
INSERT INTO rehearsal.evidence VALUES ('4a711069-4dcb-47df-8943-cad1b80273ac','synthetic-preserved-marker');
'@
    $migrationCount = Invoke-PgSql cureflow_rehearsal 'SELECT count(*) FROM "__EFMigrationsHistory"'
    $privacyVerified = (Invoke-PgSql cureflow_rehearsal "SELECT to_regprocedure('public.cureflow_patient_read_allowed(uuid,uuid)') IS NOT NULL") -eq 't'
    if ($privacyVerified) {
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
INSERT INTO public."PatientDocuments" ("Id","TenantId","PatientId","DocumentType","Title","OriginalFileName","ContentType","FileSizeBytes","FileUrl","CapturedAt","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('90000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001','paper_note','Synthetic metadata only','synthetic.txt','text/plain',1,'',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO public."PrivateDocumentVersions" ("Id","TenantId","DocumentId","DocumentKind","ObjectKey","Sha256","Length","ContentType","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('90000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','90000000-0000-4000-8000-000000000001','patient_document','10000000000040008000000000000001/90000000000040008000000000000002',repeat('A',64),1,'text/plain',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO public."Users" ("Id","TenantId","Name","Email","PasswordHash","Role","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('30000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','Synthetic reviewer','synthetic@example.invalid','not-a-password-hash',0,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO public."Roles" ("Id","Name","IsSystem","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('40000000-0000-4000-8000-000000000001','Synthetic restricted reviewer',false,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO public."RolePermissions" ("Id","RoleId","PermissionId","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 SELECT '50000000-0000-4000-8000-000000000001','40000000-0000-4000-8000-000000000001',"Id",CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false
 FROM public."Permissions" WHERE "Code"='PatientRestricted.View';
INSERT INTO public."UserRoleAssignments" ("Id","TenantId","UserId","RoleId","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('60000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000001','40000000-0000-4000-8000-000000000001',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO public."PatientAlertVersions" ("Id","TenantId","AlertId","PatientId","Key","Category","Revision","Title","Severity","AlertIsActive","SourceType","Reason","EffectiveFrom","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 VALUES ('70000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','70000000-0000-4000-8000-000000000002','20000000-0000-4000-8000-000000000001','chart','confidentiality',1,'Synthetic restricted chart','info',true,'staff_observed','Synthetic scope rehearsal',CURRENT_TIMESTAMP,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false);
INSERT INTO public."PatientConfidentialityStates" ("Id","TenantId","PatientId","Restricted","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 VALUES ('80000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',true,1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false);
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','',true);
SELECT set_config('app.restricted_patient_read','true',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM public."Patients") THEN RAISE EXCEPTION 'Worker or clearance flag bypasses confidentiality'; END IF; END $$;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM public."PatientDocuments") OR EXISTS(SELECT 1 FROM public."PrivateDocumentVersions")
 THEN RAISE EXCEPTION 'Restricted child/file metadata bypasses confidentiality'; END IF; END $$;
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
DO $$ BEGIN IF (SELECT count(*) FROM public."Patients")<>1 THEN RAISE EXCEPTION 'Current restricted reviewer cannot read scoped patient'; END IF; END $$;
DO $$ BEGIN IF (SELECT count(*) FROM public."PatientDocuments")<>1 OR (SELECT count(*) FROM public."PrivateDocumentVersions")<>1
 THEN RAISE EXCEPTION 'Authorized reviewer cannot resolve retained file associations'; END IF; END $$;
RESET ROLE;
UPDATE public."RolePermissions" SET "IsActive"=false WHERE "Id"='50000000-0000-4000-8000-000000000001';
SET LOCAL ROLE cureflow_tenant;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM public."Patients") THEN RAISE EXCEPTION 'Revoked persisted capability still opens restricted chart'; END IF; END $$;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM public."PatientDocuments") OR EXISTS(SELECT 1 FROM public."PrivateDocumentVersions")
 THEN RAISE EXCEPTION 'Revoked capability still opens protected file metadata'; END IF; END $$;
ROLLBACK;
'@ | Out-Null
    }
    $scheduleVerified = (Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."ScheduleRules"'') IS NOT NULL') -eq 't'
    if ($scheduleVerified) {
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
INSERT INTO "Facilities" ("Id","TenantId","Name","Code","IsPrimary","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('a0000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','Synthetic facility','SYN',true,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO "Users" ("Id","TenantId","Name","Email","PasswordHash","Role","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('a0000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','Synthetic practitioner','practitioner@example.invalid','not-a-password-hash',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO "StaffProfiles" ("Id","TenantId","UserId","EmploymentType","IsAvailable","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('a0000000-0000-4000-8000-000000000003','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002',0,true,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO "FacilityOperatingSettings" ("Id","TenantId","FacilityId","Revision","EffectiveFrom","Timezone","Language","DateFormat","TimeFormat","Reason","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('a0000000-0000-4000-8000-000000000004','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001',1,CURRENT_TIMESTAMP,'Asia/Kolkata','en-IN','yyyy-MM-dd','HH:mm','Synthetic-only operating setting',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
DO $$ BEGIN
 IF has_table_privilege('cureflow_tenant','public."Facilities"','INSERT,UPDATE,DELETE')
    OR (SELECT count(*) FROM public."Facilities")<>1
    OR public.cureflow_lock_active_facility('10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001') IS NULL THEN
  RAISE EXCEPTION 'Scoped facility read-lock authority does not match the narrow contract';
 END IF;
END $$;
INSERT INTO "ScheduleRules" ("Id","TenantId","FacilityId","PractitionerUserId","OperatingSettingId","Timezone","EffectiveFrom","EffectiveThrough","WeekdayMask","StartMinute","EndMinute","AmbiguousTimePolicy","Revision","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('a0000000-0000-4000-8000-000000000005','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000004','Asia/Kolkata','2026-10-01','2026-10-31',127,540,600,'reject',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN
 INSERT INTO "ScheduleRules" ("Id","TenantId","FacilityId","PractitionerUserId","OperatingSettingId","Timezone","EffectiveFrom","EffectiveThrough","WeekdayMask","StartMinute","EndMinute","AmbiguousTimePolicy","Revision","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('a0000000-0000-4000-8000-000000000006','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000004','Asia/Kolkata','2026-10-01','2026-10-31',127,570,630,'reject',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Overlapping practitioner rule accepted'; END IF;
END $$;
INSERT INTO "ScheduleRules" ("Id","TenantId","FacilityId","PractitionerUserId","OperatingSettingId","Timezone","EffectiveFrom","EffectiveThrough","WeekdayMask","StartMinute","EndMinute","AmbiguousTimePolicy","Revision","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('a0000000-0000-4000-8000-000000000007','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000004','Asia/Kolkata','2026-10-01','2026-10-31',127,600,660,'reject',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN DELETE FROM "ScheduleRules" WHERE "Id"='a0000000-0000-4000-8000-000000000005';
 EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Retained rule deletion accepted'; END IF;
END $$;
UPDATE "ScheduleRules" SET "IsActive"=false,"Revision"=2,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='a0000000-0000-4000-8000-000000000005';
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN UPDATE "ScheduleRules" SET "IsActive"=true,"Revision"=3 WHERE "Id"='a0000000-0000-4000-8000-000000000005';
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Retained rule reactivation accepted'; END IF;
END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000002',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "ScheduleRules") THEN RAISE EXCEPTION 'Cross-tenant schedule read accepted'; END IF; END $$;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "Facilities") OR public.cureflow_lock_active_facility('10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001') IS NOT NULL
 THEN RAISE EXCEPTION 'Cross-tenant facility read/lock accepted'; END IF; END $$;
COMMIT;
'@ | Out-Null
    }
    $exceptionVerified = (Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."ScheduleExceptions"'') IS NOT NULL') -eq 't'
    if ($exceptionVerified) {
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
INSERT INTO "Appointments" ("Id","TenantId","PatientId","PatientName","PatientPhone","DoctorUserId","DoctorName","Department","ScheduledAt","DurationMinutes","Status","ReminderSent","FollowUpSent","IsPaid","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('b0000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001','Synthetic A','','a0000000-0000-4000-8000-000000000002','Synthetic practitioner','Synthetic','2026-10-08T10:00:00Z',15,0,false,false,false,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
INSERT INTO "ScheduleExceptions" ("Id","TenantId","FacilityId","PractitionerUserId","OperatingSettingId","Timezone","Kind","StartsAtUtc","EndsAtUtc","Reason","Revision","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('b0000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000004','Asia/Kolkata','leave','2026-10-08T10:00:00Z','2026-10-08T10:30:00Z','Synthetic leave only',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO "AppointmentRescheduleWorks" ("Id","TenantId","ExceptionId","AppointmentId","PatientId","OriginalScheduledAtUtc","OriginalDurationMinutes","FacilityAssociationNeedsReview","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('b0000000-0000-4000-8000-000000000003','10000000-0000-4000-8000-000000000001','b0000000-0000-4000-8000-000000000002','b0000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001','2026-10-08T10:00:00Z',15,true,'needs_review',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN INSERT INTO "AppointmentRescheduleWorks" ("Id","TenantId","ExceptionId","AppointmentId","PatientId","OriginalScheduledAtUtc","OriginalDurationMinutes","FacilityAssociationNeedsReview","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES ('b0000000-0000-4000-8000-000000000004','10000000-0000-4000-8000-000000000001','b0000000-0000-4000-8000-000000000002','b0000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000002','2026-10-08T10:00:00Z',15,true,'needs_review',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 EXCEPTION WHEN check_violation OR foreign_key_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Wrong-patient booking impact accepted'; END IF;
END $$;
SELECT set_config('app.current_user_id','',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "AppointmentRescheduleWorks") THEN RAISE EXCEPTION 'Actorless worker reads confidential booking impact'; END IF; END $$;
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
DO $$ BEGIN IF (SELECT count(*) FROM "AppointmentRescheduleWorks")<>1 THEN RAISE EXCEPTION 'Authorized booking impact is unavailable'; END IF; END $$;
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN UPDATE "AppointmentRescheduleWorks" SET "State"='resolved'; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Original booking impact rewrite accepted'; END IF;
END $$;
UPDATE "ScheduleExceptions" SET "IsActive"=false,"Revision"=2,"DisabledAt"=CURRENT_TIMESTAMP,"UpdatedAt"=CURRENT_TIMESTAMP
 WHERE "Id"='b0000000-0000-4000-8000-000000000002';
DO $$ BEGIN IF (SELECT count(*) FROM "AppointmentRescheduleWorks")<>1
 OR NOT EXISTS(SELECT 1 FROM "Appointments" WHERE "Id"='b0000000-0000-4000-8000-000000000001' AND "Status"=0 AND "ScheduledAt"='2026-10-08T10:00:00Z' AND "DurationMinutes"=15)
 THEN RAISE EXCEPTION 'Exception disable altered or silently resolved the source booking'; END IF; END $$;
COMMIT;
'@ | Out-Null
    }
    $reservationVerified = (Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."AppointmentReservations"'') IS NOT NULL') -eq 't'
    if ($reservationVerified) {
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
DO $$ DECLARE hold_id uuid=gen_random_uuid(); other_id uuid=gen_random_uuid(); denied boolean=false;
 start_at timestamptz=date_trunc('minute',clock_timestamp())+interval '1 hour';
BEGIN
 INSERT INTO "AppointmentReservations" ("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 VALUES (hold_id,'10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002',start_at,start_at+interval '15 minutes',clock_timestamp()+interval '5 minutes','held',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
 INSERT INTO "AppointmentReservationSeats" ("Id","TenantId","ReservationId","TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES(gen_random_uuid(),'10000000-0000-4000-8000-000000000001',hold_id,'practitioner','a0000000-0000-4000-8000-000000000002',1,1,start_at,start_at+interval '15 minutes','held',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 SET CONSTRAINTS ALL IMMEDIATE;
 SET CONSTRAINTS ALL DEFERRED;
 BEGIN
  INSERT INTO "AppointmentReservations" ("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
  VALUES (other_id,'10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002',start_at,start_at+interval '15 minutes',clock_timestamp()+interval '5 minutes','held',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
  INSERT INTO "AppointmentReservationSeats" ("Id","TenantId","ReservationId","TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES(gen_random_uuid(),'10000000-0000-4000-8000-000000000001',other_id,'practitioner','a0000000-0000-4000-8000-000000000002',1,1,start_at,start_at+interval '15 minutes','held',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 EXCEPTION WHEN exclusion_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Concurrent active seat overlap accepted'; END IF;
 denied=false;
 BEGIN
  INSERT INTO "AppointmentReservations" ("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
  VALUES (other_id,'10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002',start_at+interval '15 minutes',start_at+interval '30 minutes',clock_timestamp()+interval '5 minutes','held',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
  SET CONSTRAINTS ALL IMMEDIATE;
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Header without occupancy committed'; END IF;
 SET CONSTRAINTS ALL DEFERRED;
 denied=false;
 BEGIN UPDATE "AppointmentReservations" SET "State"='expired',"Revision"=2,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"=hold_id;
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Live hold prematurely expired'; END IF;
 UPDATE "AppointmentReservations" SET "State"='released',"Revision"=2,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"=hold_id;
 UPDATE "AppointmentReservationSeats" SET "State"='released',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"=hold_id;
 SET CONSTRAINTS ALL IMMEDIATE;
 denied=false;
 BEGIN UPDATE "AppointmentReservations" SET "State"='held',"Revision"=3,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"=hold_id;
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Terminal reservation reactivated'; END IF;
 denied=false;
 BEGIN DELETE FROM "AppointmentReservationSeats" WHERE "ReservationId"=hold_id;
 EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Retained occupancy deleted'; END IF;
END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000002',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "AppointmentReservations") OR EXISTS(SELECT 1 FROM "AppointmentReservationSeats")
 THEN RAISE EXCEPTION 'Cross-tenant reservation read accepted'; END IF; END $$;
COMMIT;
'@ | Out-Null
    }

    if($reservationVerified) {
        $raceInsert = @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
INSERT INTO "AppointmentReservations" ("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'RACE-ID'::uuid,"TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc",clock_timestamp()+interval '5 minutes','held',1,'synthetic-race',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"CreatedBy",true,false
FROM "AppointmentReservations" WHERE "State"='released' ORDER BY "Id" LIMIT 1;
INSERT INTO "AppointmentReservationSeats" ("Id","TenantId","ReservationId","TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId",'RACE-ID'::uuid,"TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc",'held',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false
FROM "AppointmentReservationSeats" WHERE "State"='released' ORDER BY "Id" LIMIT 1;
'@
        $invokeRace = {
            param($bin,$racePort,$passFile,$applicationName,$sql)
            $env:PGPASSFILE=$passFile
            $env:PGAPPNAME=$applicationName
            $output=& (Join-Path $bin 'psql.exe') -X -h 127.0.0.1 -p $racePort -U rehearsal_owner -d cureflow_rehearsal -v ON_ERROR_STOP=1 -v VERBOSITY=sqlstate -At -c $sql 2>&1
            [pscustomobject]@{ExitCode=$LASTEXITCODE;Output=($output -join "`n")}
        }
        $firstSql=$raceInsert.Replace('RACE-ID','c0000000-0000-4000-8000-000000000001')+"`nSELECT pg_sleep(4); COMMIT;"
        $secondSql=$raceInsert.Replace('RACE-ID','c0000000-0000-4000-8000-000000000002')+"`nCOMMIT;"
        $firstJob=Start-Job -ScriptBlock $invokeRace -ArgumentList $PostgreSqlBin,$port,$passfilePath,'synthetic-reservation-first',$firstSql
        try {
            $waiting=$false
            for($attempt=0;$attempt -lt 50;$attempt++) {
                if((Invoke-PgSql cureflow_rehearsal "SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name='synthetic-reservation-first' AND wait_event='PgSleep')") -eq 't'){$waiting=$true;break}
                Start-Sleep -Milliseconds 100
            }
            if(-not $waiting){throw 'First synthetic reservation did not reach the concurrent commit window.'}
            $secondJob=Start-Job -ScriptBlock $invokeRace -ArgumentList $PostgreSqlBin,$port,$passfilePath,'synthetic-reservation-second',$secondSql
            $null=Wait-Job -Job $firstJob,$secondJob -Timeout 30
            if($firstJob.State -ne 'Completed' -or $secondJob.State -ne 'Completed'){throw 'Synthetic reservation race exceeded its bounded wait.'}
            $firstResult=Receive-Job $firstJob
            $secondResult=Receive-Job $secondJob
            if($firstResult.ExitCode -ne 0 -or $secondResult.ExitCode -eq 0 -or $secondResult.Output -notmatch '23P01'){
                throw "Expected one committed seat and one exclusion conflict: first=$($firstResult.ExitCode), second=$($secondResult.ExitCode), detail=$($secondResult.Output)"
            }
            if((Invoke-PgSql cureflow_rehearsal 'SELECT count(*) FROM "AppointmentReservations" WHERE "Id" IN (''c0000000-0000-4000-8000-000000000001'',''c0000000-0000-4000-8000-000000000002'')') -ne '1'){
                throw 'Concurrent reservation count differs from one.'
            }
        }
        finally {
            if($firstJob.State -eq 'Running'){Stop-Job $firstJob}
            Remove-Job $firstJob
            if($null -ne $secondJob){if($secondJob.State -eq 'Running'){Stop-Job $secondJob};Remove-Job $secondJob}
        }

    }
    if($null -ne $forwardSchemaPath){
        # Legacy fixtures are deliberately inserted before strict new-booking guards.
        # Forward upgrade preserves them; no runtime bypass or inferred venue is used.
        Apply-Sql $forwardSchemaPath
        Apply-Sql $authorityPath
        Apply-Sql $authorityPath
        Invoke-PgSql cureflow_rehearsal @'
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM "Appointments" WHERE "Id"='b0000000-0000-4000-8000-000000000001'
 AND "FacilityId" IS NULL AND "ReservationId" IS NULL AND "Revision"=1
 AND "ScheduledAt"='2026-10-08T10:00:00Z' AND "DurationMinutes"=15 AND "Status"=0)
 THEN RAISE EXCEPTION 'Forward explicit-booking migration changed retained legacy source'; END IF; END $$;
'@ | Out-Null
        $migrationCount=Invoke-PgSql cureflow_rehearsal 'SELECT count(*) FROM "__EFMigrationsHistory"'
    }
    $queueVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."QueueEntries"'') IS NOT NULL') -eq 't'
    if($queueVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
DO $$ DECLARE arrived timestamptz=date_trunc('milliseconds',clock_timestamp());
 starts timestamptz=date_trunc('minute',clock_timestamp())-interval '1 minute';
 arrival_date date=(clock_timestamp() AT TIME ZONE 'Asia/Kolkata')::date; denied boolean=false;
BEGIN
 INSERT INTO "Patients" ("Id","TenantId","Name","Phone","EmailNotificationsEnabled","Gender","BloodGroup","InquirySource","Status","Tags","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','Synthetic arrival','','false',0,0,'synthetic',0,'[]',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 INSERT INTO "PatientIdentifierCounters" ("Id","TenantId","FacilityId","Namespace","CurrentValue","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','local-mrn',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 INSERT INTO "PatientIdentifiers" ("Id","TenantId","FacilityId","PatientId","Namespace","Value","Sequence","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000003','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001','local-mrn','MRN-0000000001',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 INSERT INTO "AppointmentReservations" ("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000004','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002',starts,starts+interval '15 minutes',clock_timestamp()+interval '5 minutes','held',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
 INSERT INTO "AppointmentReservationSeats" ("Id","TenantId","ReservationId","TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000005','10000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000004','practitioner','a0000000-0000-4000-8000-000000000002',1,1,starts,starts+interval '15 minutes','held',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
 INSERT INTO "Appointments" ("Id","TenantId","PatientId","PatientName","PatientPhone","DoctorUserId","DoctorName","Department","ScheduledAt","DurationMinutes","Status","ReminderSent","FollowUpSent","IsPaid","FacilityId","ReservationId","BookingKind","BookingPolicyVersion","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000006','10000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001','Synthetic arrival','','a0000000-0000-4000-8000-000000000002','Synthetic practitioner','Synthetic',starts,15,0,false,false,false,'a0000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000004','walk_in','synthetic-only',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
 UPDATE "AppointmentReservations" SET "State"='confirmed',"Revision"=2,"AppointmentId"='d0000000-0000-4000-8000-000000000006',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='d0000000-0000-4000-8000-000000000004';
 UPDATE "AppointmentReservationSeats" SET "State"='confirmed',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='d0000000-0000-4000-8000-000000000004';
 SET CONSTRAINTS ALL IMMEDIATE;
 SET CONSTRAINTS ALL DEFERRED;
 UPDATE "Appointments" SET "CheckedInAt"=arrived,"Revision"=2,"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"='a0000000-0000-4000-8000-000000000002' WHERE "Id"='d0000000-0000-4000-8000-000000000006';
 INSERT INTO "QueueCounters" ("Id","TenantId","FacilityId","ArrivalDate","LastToken","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000007','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001',arrival_date,1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000002',true,false);
 INSERT INTO "QueueEntries" ("Id","TenantId","AppointmentId","PatientId","FacilityId","ReservationId","OperatingSettingId","Timezone","ArrivalDate","ArrivedAtUtc","Token","AppointmentRevision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 VALUES('d0000000-0000-4000-8000-000000000008','10000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000006','d0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000004','a0000000-0000-4000-8000-000000000004','Asia/Kolkata',arrival_date,arrived,1,2,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
 SET CONSTRAINTS ALL IMMEDIATE;
 BEGIN
  INSERT INTO "QueueEntries" ("Id","AppointmentId","PatientId","FacilityId","ReservationId","OperatingSettingId","Timezone","ArrivalDate","ArrivedAtUtc","Token","AppointmentRevision","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted","TenantId") SELECT gen_random_uuid(),"AppointmentId","PatientId","FacilityId","ReservationId","OperatingSettingId","Timezone","ArrivalDate","ArrivedAtUtc","Token","AppointmentRevision","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted","TenantId" FROM "QueueEntries" WHERE "Id"='d0000000-0000-4000-8000-000000000008';
 EXCEPTION WHEN unique_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Repeated arrival allocated a second queue entry'; END IF;
 denied=false;
 BEGIN UPDATE "QueueCounters" SET "LastToken"=2,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='d0000000-0000-4000-8000-000000000007';
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Counter allocation without arrival committed'; END IF;
 denied=false;
 BEGIN UPDATE "Appointments" SET "CheckedInAt"=NULL,"Revision"=3 WHERE "Id"='d0000000-0000-4000-8000-000000000006';
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Checked-in evidence was removed'; END IF;
 denied=false;
 BEGIN DELETE FROM "QueueEntries"; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Retained queue evidence was deleted'; END IF;
END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000002',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "QueueEntries") OR EXISTS(SELECT 1 FROM "QueueCounters")
 THEN RAISE EXCEPTION 'Cross-tenant queue read accepted'; END IF; END $$;
COMMIT;
'@ | Out-Null
    }

    $dispatchVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."QueueStarts"'') IS NOT NULL') -eq 't'
    if($dispatchVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN INSERT INTO "QueuePriorityOverrides" ("Id","TenantId","QueueEntryId","PatientId","FacilityId","ArrivalDate","Revision","SortRank","Reason","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 SELECT gen_random_uuid(),"TenantId","Id","PatientId","FacilityId","ArrivalDate",2,0,'Synthetic denied override','synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false
 FROM "QueueEntries" WHERE "Id"='d0000000-0000-4000-8000-000000000008';
 EXCEPTION WHEN insufficient_privilege THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Incomplete restricted scope was treated as complete queue authority'; END IF;
END $$;
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
INSERT INTO "QueuePriorityOverrides" ("Id","TenantId","QueueEntryId","PatientId","FacilityId","ArrivalDate","Revision","SortRank","Reason","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'e0000000-0000-4000-8000-000000000001',"TenantId","Id","PatientId","FacilityId","ArrivalDate",2,0,'Synthetic recorded override','synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false
FROM "QueueEntries" WHERE "Id"='d0000000-0000-4000-8000-000000000008';
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN INSERT INTO "QueuePriorityOverrides" ("Id","TenantId","QueueEntryId","PatientId","FacilityId","ArrivalDate","Revision","SortRank","Reason","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 SELECT gen_random_uuid(),"TenantId","QueueEntryId","PatientId","FacilityId","ArrivalDate",2,10,'Synthetic stale override','synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"CreatedBy",true,false FROM "QueuePriorityOverrides";
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Stale queue revision accepted'; END IF;
 denied=false;
 BEGIN UPDATE "QueuePriorityOverrides" SET "Reason"='Rewritten'; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Attributable priority history was rewritten'; END IF;
END $$;
RESET ROLE;
-- Explicit synthetic fixture authority for the second competing staff actor only.
INSERT INTO "UserRoleAssignments" ("Id","TenantId","UserId","RoleId","CreatedAt","UpdatedAt","IsActive","IsDeleted")
VALUES('e0000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000002','40000000-0000-4000-8000-000000000001',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
COMMIT;
'@ | Out-Null
        $startInsert=@'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','STAFF-ID',true);
INSERT INTO "QueueStarts" ("Id","TenantId","QueueEntryId","PatientId","FacilityId","ArrivalDate","Revision","SortRank","DefaultRank","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'START-ID'::uuid,"TenantId","Id","PatientId","FacilityId","ArrivalDate",3,0,10,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'STAFF-ID'::uuid,true,false
FROM "QueueEntries" WHERE "Id"='d0000000-0000-4000-8000-000000000008';
'@
        $firstSql=$startInsert.Replace('STAFF-ID','30000000-0000-4000-8000-000000000001').Replace('START-ID','e0000000-0000-4000-8000-000000000003')+"`nSELECT pg_sleep(4); COMMIT;"
        $secondSql=$startInsert.Replace('STAFF-ID','a0000000-0000-4000-8000-000000000002').Replace('START-ID','e0000000-0000-4000-8000-000000000004')+"`nCOMMIT;"
        $secondJob=$null
        $firstJob=Start-Job -ScriptBlock $invokeRace -ArgumentList $PostgreSqlBin,$port,$passfilePath,'synthetic-queue-first',$firstSql
        try{
            $waiting=$false
            for($attempt=0;$attempt -lt 50;$attempt++){
                if((Invoke-PgSql cureflow_rehearsal "SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name='synthetic-queue-first' AND wait_event='PgSleep')") -eq 't'){$waiting=$true;break}
                Start-Sleep -Milliseconds 100
            }
            if(-not $waiting){throw 'First queue claimant did not reach the concurrent commit window.'}
            $secondJob=Start-Job -ScriptBlock $invokeRace -ArgumentList $PostgreSqlBin,$port,$passfilePath,'synthetic-queue-second',$secondSql
            $null=Wait-Job -Job $firstJob,$secondJob -Timeout 30
            if($firstJob.State -ne 'Completed' -or $secondJob.State -ne 'Completed'){throw 'Synthetic queue claim race exceeded its bounded wait.'}
            $firstResult=Receive-Job $firstJob
            $secondResult=Receive-Job $secondJob
            if($firstResult.ExitCode -ne 0 -or $secondResult.ExitCode -eq 0 -or $secondResult.Output -notmatch '23514'){
                throw "Expected one queue start and one denied competing claim: first=$($firstResult.ExitCode), second=$($secondResult.ExitCode), detail=$($secondResult.Output)"
            }
            if((Invoke-PgSql cureflow_rehearsal 'SELECT count(*) FROM "QueueStarts"') -ne '1'){throw 'More than one queue start committed.'}
        }
        finally{
            if($firstJob.State -eq 'Running'){Stop-Job $firstJob}
            Remove-Job $firstJob
            if($null -ne $secondJob){if($secondJob.State -eq 'Running'){Stop-Job $secondJob};Remove-Job $secondJob}
        }
    }

    $bookingChangesVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."AppointmentBookingChanges"'') IS NOT NULL') -eq 't'
    if($bookingChangesVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
INSERT INTO "Appointments" ("Id","TenantId","PatientId","PatientName","PatientPhone","DoctorUserId","DoctorName","Department","ScheduledAt","DurationMinutes","Status","ReminderSent","FollowUpSent","IsPaid","FacilityId","ReservationId","BookingKind","BookingPolicyVersion","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'f0000000-0000-4000-8000-000000000001',"TenantId",'d0000000-0000-4000-8000-000000000001','Synthetic future booking','',"PractitionerUserId",'Synthetic practitioner','Synthetic',"StartsAtUtc",15,0,false,false,false,"FacilityId","Id",'routine','synthetic-only',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"OwnerUserId",true,false
FROM "AppointmentReservations" WHERE "Id"='c0000000-0000-4000-8000-000000000001';
UPDATE "AppointmentReservations" SET "State"='confirmed',"Revision"=2,"AppointmentId"='f0000000-0000-4000-8000-000000000001',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='c0000000-0000-4000-8000-000000000001';
UPDATE "AppointmentReservationSeats" SET "State"='confirmed',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='c0000000-0000-4000-8000-000000000001';
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
INSERT INTO "AppointmentReservations" ("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'f0000000-0000-4000-8000-000000000002',"TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc"+interval '1 hour',"EndsAtUtc"+interval '1 hour',clock_timestamp()+interval '5 minutes','held',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"CreatedBy",true,false
FROM "AppointmentReservations" WHERE "Id"='c0000000-0000-4000-8000-000000000001';
INSERT INTO "AppointmentReservationSeats" ("Id","TenantId","ReservationId","TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id",'practitioner',"PractitionerUserId",1,1,"StartsAtUtc","EndsAtUtc",'held',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false FROM "AppointmentReservations" WHERE "Id"='f0000000-0000-4000-8000-000000000002';
INSERT INTO "AppointmentBookingChanges" ("Id","TenantId","AppointmentId","PatientId","AppointmentRevision","Action","PreviousReservationId","ReplacementReservationId","PreviousStartsAtUtc","PreviousEndsAtUtc","ReplacementStartsAtUtc","ReplacementEndsAtUtc","Reason","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'f0000000-0000-4000-8000-000000000003',a."TenantId",a."Id",a."PatientId",2,'reschedule',a."ReservationId",h."Id",a."ScheduledAt",a."ScheduledAt"+interval '15 minutes',h."StartsAtUtc",h."EndsAtUtc",'Synthetic explicit reschedule','synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false
FROM "Appointments" a JOIN "AppointmentReservations" h ON h."Id"='f0000000-0000-4000-8000-000000000002' AND h."TenantId"=a."TenantId" WHERE a."Id"='f0000000-0000-4000-8000-000000000001';
UPDATE "AppointmentReservations" SET "State"='released',"Revision"=3,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='c0000000-0000-4000-8000-000000000001';
UPDATE "AppointmentReservationSeats" SET "State"='released',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='c0000000-0000-4000-8000-000000000001';
UPDATE "Appointments" a SET "ReservationId"=h."Id","ScheduledAt"=h."StartsAtUtc","Revision"=2,"UpdatedAt"=CURRENT_TIMESTAMP
FROM "AppointmentReservations" h WHERE a."Id"='f0000000-0000-4000-8000-000000000001' AND h."Id"='f0000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservations" SET "State"='confirmed',"Revision"=2,"AppointmentId"='f0000000-0000-4000-8000-000000000001',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='f0000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservationSeats" SET "State"='confirmed',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='f0000000-0000-4000-8000-000000000002';
SET CONSTRAINTS ALL IMMEDIATE;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM "Appointments" a JOIN "AppointmentReservations" previous ON previous."Id"='c0000000-0000-4000-8000-000000000001'
 WHERE a."Id"='f0000000-0000-4000-8000-000000000001' AND a."Revision"=2 AND a."ReservationId"='f0000000-0000-4000-8000-000000000002'
 AND previous."AppointmentId"=a."Id" AND previous."State"='released' AND a."ScheduledAt"=previous."StartsAtUtc"+interval '1 hour')
 THEN RAISE EXCEPTION 'Reschedule did not preserve ID and released original allocation'; END IF; END $$;
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
INSERT INTO "AppointmentBookingChanges" ("Id","TenantId","AppointmentId","PatientId","AppointmentRevision","Action","PreviousReservationId","PreviousStartsAtUtc","PreviousEndsAtUtc","Reason","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'f0000000-0000-4000-8000-000000000004',"TenantId","Id","PatientId",3,'cancel',"ReservationId","ScheduledAt","ScheduledAt"+interval '15 minutes','Synthetic explicit cancellation','synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false
FROM "Appointments" WHERE "Id"='f0000000-0000-4000-8000-000000000001';
UPDATE "AppointmentReservations" SET "State"='released',"Revision"=3,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='f0000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservationSeats" SET "State"='released',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='f0000000-0000-4000-8000-000000000002';
UPDATE "Appointments" SET "Status"=4,"Revision"=3,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='f0000000-0000-4000-8000-000000000001';
SET CONSTRAINTS ALL IMMEDIATE;
DO $$ DECLARE denied boolean=false; BEGIN
 IF NOT EXISTS(SELECT 1 FROM "Appointments" WHERE "Id"='f0000000-0000-4000-8000-000000000001' AND "Status"=4 AND "Revision"=3)
 OR (SELECT count(*) FROM "AppointmentBookingChanges" WHERE "AppointmentId"='f0000000-0000-4000-8000-000000000001')<>2
 OR EXISTS(SELECT 1 FROM "AppointmentReservationSeats" WHERE "ReservationId" IN('c0000000-0000-4000-8000-000000000001','f0000000-0000-4000-8000-000000000002') AND "State"<>'released') THEN
 RAISE EXCEPTION 'Cancellation lost retained history or leaked active allocation'; END IF;
 BEGIN UPDATE "Appointments" SET "Status"=0,"Revision"=4 WHERE "Id"='f0000000-0000-4000-8000-000000000001'; EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Cancellation was silently reactivated'; END IF;
 denied=false;
 BEGIN DELETE FROM "AppointmentBookingChanges"; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Attributable booking change history was deleted'; END IF;
END $$;
COMMIT;
'@ | Out-Null
    }

    $waitlistVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."AppointmentWaitlistEntries"'') IS NOT NULL') -eq 't'
    if($waitlistVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
INSERT INTO "AppointmentWaitlistEntries" ("Id","TenantId","PatientId","FacilityId","PractitionerUserId","ResourceIdsJson","EarliestStartsAtUtc","LatestEndsAtUtc","DurationMinutes","ExpiresAtUtc","BookingKind","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a1000000-0000-4000-8000-000000000001',"TenantId",'d0000000-0000-4000-8000-000000000001',"FacilityId","PractitionerUserId",'[]',"StartsAtUtc","EndsAtUtc",15,"EndsAtUtc",'routine','waiting',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false FROM "AppointmentReservations" WHERE "Id"='c0000000-0000-4000-8000-000000000001';
INSERT INTO "AppointmentWaitlistDecisions" ("Id","TenantId","EntryId","PatientId","EntryRevision","Action","Reason","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
VALUES('a1000000-0000-4000-8000-000000000005','10000000-0000-4000-8000-000000000001','a1000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001',1,'add','Synthetic explicit request',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
INSERT INTO "AppointmentReservations" ("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a1000000-0000-4000-8000-000000000002',"TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc",clock_timestamp()+interval '5 minutes','held',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"OwnerUserId",true,false FROM "AppointmentReservations" WHERE "Id"='c0000000-0000-4000-8000-000000000001';
INSERT INTO "AppointmentReservationSeats" ("Id","TenantId","ReservationId","TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id",'practitioner',"PractitionerUserId",1,1,"StartsAtUtc","EndsAtUtc",'held',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false FROM "AppointmentReservations" WHERE "Id"='a1000000-0000-4000-8000-000000000002';
INSERT INTO "AppointmentWaitlistOffers" ("Id","TenantId","EntryId","PatientId","ReleasedReservationId","ReservationId","OwnerUserId","ExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a1000000-0000-4000-8000-000000000003',"TenantId",'a1000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001','c0000000-0000-4000-8000-000000000001',"Id","OwnerUserId","HoldExpiresAtUtc",'offered',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"OwnerUserId",true,false FROM "AppointmentReservations" WHERE "Id"='a1000000-0000-4000-8000-000000000002';
UPDATE "AppointmentWaitlistEntries" SET "State"='offered',"Revision"=2,"CurrentOfferId"='a1000000-0000-4000-8000-000000000003',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='a1000000-0000-4000-8000-000000000001';
INSERT INTO "AppointmentWaitlistDecisions" ("Id","TenantId","EntryId","PatientId","EntryRevision","OfferId","Action","Reason","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
VALUES('a1000000-0000-4000-8000-000000000006','10000000-0000-4000-8000-000000000001','a1000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001',2,'a1000000-0000-4000-8000-000000000003','offer','Synthetic exact released-capacity offer',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
INSERT INTO "Appointments" ("Id","TenantId","PatientId","PatientName","PatientPhone","DoctorUserId","DoctorName","Department","ScheduledAt","DurationMinutes","Status","ReminderSent","FollowUpSent","IsPaid","FacilityId","ReservationId","BookingKind","BookingPolicyVersion","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a1000000-0000-4000-8000-000000000004',"TenantId",'d0000000-0000-4000-8000-000000000001','Synthetic waitlist booking','',"PractitionerUserId",'Synthetic practitioner','Synthetic',"StartsAtUtc",15,0,false,false,false,"FacilityId","Id",'routine','synthetic-only',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"OwnerUserId",true,false FROM "AppointmentReservations" WHERE "Id"='a1000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservations" SET "State"='confirmed',"Revision"=2,"AppointmentId"='a1000000-0000-4000-8000-000000000004',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='a1000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservationSeats" SET "State"='confirmed',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='a1000000-0000-4000-8000-000000000002';
UPDATE "AppointmentWaitlistOffers" SET "State"='accepted',"Revision"=2,"AppointmentId"='a1000000-0000-4000-8000-000000000004',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='a1000000-0000-4000-8000-000000000003';
UPDATE "AppointmentWaitlistEntries" SET "State"='accepted',"Revision"=3,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='a1000000-0000-4000-8000-000000000001';
INSERT INTO "AppointmentWaitlistDecisions" ("Id","TenantId","EntryId","PatientId","EntryRevision","OfferId","Action","Reason","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
VALUES('a1000000-0000-4000-8000-000000000007','10000000-0000-4000-8000-000000000001','a1000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001',3,'a1000000-0000-4000-8000-000000000003','accept','Synthetic explicit acceptance',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
SET CONSTRAINTS ALL IMMEDIATE;
DO $$ DECLARE denied boolean=false; BEGIN
 IF NOT EXISTS(SELECT 1 FROM "AppointmentWaitlistEntries" e JOIN "AppointmentWaitlistOffers" o ON o."Id"=e."CurrentOfferId" JOIN "Appointments" a ON a."Id"=o."AppointmentId"
 WHERE e."Id"='a1000000-0000-4000-8000-000000000001' AND e."State"='accepted' AND o."State"='accepted' AND a."PatientId"=e."PatientId" AND a."ReservationId"=o."ReservationId")
 OR (SELECT count(*) FROM "AppointmentWaitlistDecisions" WHERE "EntryId"='a1000000-0000-4000-8000-000000000001')<>3
 THEN RAISE EXCEPTION 'Waitlist acceptance lost exact patient/hold or retained decisions'; END IF;
 BEGIN UPDATE "AppointmentWaitlistOffers" SET "State"='accepted',"Revision"=3 WHERE "Id"='a1000000-0000-4000-8000-000000000003'; EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'An offer accepted twice'; END IF;
 denied=false;
 BEGIN DELETE FROM "AppointmentWaitlistDecisions"; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Waitlist decision history was deleted'; END IF;
END $$;
COMMIT;
'@ | Out-Null
    }

# Insert this PowerShell fragment after the waitlistVerified block, immediately before $archivePath.
# Evidence records named follow-up and no-show checks separately.
# Synthetic operational SQL rehearsal only; no application API/test harness or actual-data acceptance.
    $followUpVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."ClinicalFollowUpWorks"'') IS NOT NULL') -eq 't'
    if($followUpVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
-- Explicit synthetic fixture grants only; migration/catalogue must not assign them implicitly.
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id"=rp."PermissionId"
 WHERE p."Code" LIKE 'FollowUp.%') THEN RAISE EXCEPTION 'Reserved follow-up capabilities were implicitly assigned'; END IF; END $$;
INSERT INTO "Permissions"("Id","Code","Name","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),code,code,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false FROM(VALUES('Clinical.View'),('Patient.View'))p(code)
WHERE NOT EXISTS(SELECT 1 FROM "Permissions" e WHERE e."Code"=p.code);
INSERT INTO "RolePermissions"("Id","RoleId","PermissionId","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),'40000000-0000-4000-8000-000000000001',p."Id",CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false
FROM "Permissions" p WHERE p."Code" IN('FollowUp.View','FollowUp.Manage','FollowUp.Complete','FollowUp.Escalate','Clinical.View','Patient.View')
AND NOT EXISTS(SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId"='40000000-0000-4000-8000-000000000001' AND rp."PermissionId"=p."Id");
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
INSERT INTO "ClinicalFollowUpWorks"("Id","TenantId","PatientId","SourceType","SourceId","SourceVersion","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a2000000-0000-4000-8000-000000000001',"TenantId","PatientId",'appointment',"Id","Revision"::text,
'30000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000001',CURRENT_TIMESTAMP+interval '1 day','open',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false
FROM "Appointments" WHERE "Id"='b0000000-0000-4000-8000-000000000001';
INSERT INTO "ClinicalFollowUpVersions"("Id","TenantId","WorkId","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc","Action","Reason","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc",'create','Synthetic accountable creation',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false FROM "ClinicalFollowUpWorks" WHERE "Id"='a2000000-0000-4000-8000-000000000001';
UPDATE "ClinicalFollowUpWorks" SET "State"='acknowledged',"Revision"=2,"AcknowledgedAtUtc"=CURRENT_TIMESTAMP,"UpdatedBy"='30000000-0000-4000-8000-000000000001' WHERE "Id"='a2000000-0000-4000-8000-000000000001';
INSERT INTO "ClinicalFollowUpVersions"("Id","TenantId","WorkId","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc","Action","Reason","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc",'acknowledge','Synthetic named acknowledgement',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false FROM "ClinicalFollowUpWorks" WHERE "Id"='a2000000-0000-4000-8000-000000000001';
-- A completion without a matching finalized locked clinical note is rolled back in its savepoint.
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN
  UPDATE "ClinicalFollowUpWorks" SET "State"='completed',"Revision"=3,"CompletedAtUtc"=CURRENT_TIMESTAMP,"UpdatedBy"='30000000-0000-4000-8000-000000000001' WHERE "Id"='a2000000-0000-4000-8000-000000000001';
  INSERT INTO "ClinicalFollowUpVersions"("Id","TenantId","WorkId","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc","Action","Reason","EvidenceNoteId","EvidenceNoteRevision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
  SELECT gen_random_uuid(),"TenantId","Id","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc",'complete','Synthetic unsupported evidence',gen_random_uuid(),1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false FROM "ClinicalFollowUpWorks" WHERE "Id"='a2000000-0000-4000-8000-000000000001';
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Completion without authorized exact clinical evidence succeeded'; END IF;
 denied=false;
 BEGIN UPDATE "ClinicalFollowUpVersions" SET "Reason"='Rewrite'; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Immutable follow-up history was rewritten'; END IF;
 denied=false;
 BEGIN DELETE FROM "ClinicalFollowUpWorks"; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Retained work was deleted'; END IF;
 denied=false;
 BEGIN UPDATE "ClinicalFollowUpWorks" SET "Revision"=2 WHERE "Id"='a2000000-0000-4000-8000-000000000001'; EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Stale follow-up revision was accepted'; END IF;
END $$;
UPDATE "ClinicalFollowUpWorks" SET "AssignedUserId"=NULL,"State"='open',"AcknowledgedAtUtc"=NULL,"Revision"=3,"UpdatedBy"='30000000-0000-4000-8000-000000000001' WHERE "Id"='a2000000-0000-4000-8000-000000000001';
INSERT INTO "ClinicalFollowUpVersions"("Id","TenantId","WorkId","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc","Action","Reason","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","AcknowledgedAtUtc","CompletedAtUtc",'reassign','Synthetic explicit unassigned escalation',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false FROM "ClinicalFollowUpWorks" WHERE "Id"='a2000000-0000-4000-8000-000000000001';
DO $$ DECLARE denied boolean=false; BEGIN
 IF NOT EXISTS(SELECT 1 FROM "ClinicalFollowUpWorks" WHERE "Id"='a2000000-0000-4000-8000-000000000001' AND "AssignedUserId" IS NULL AND "State"='open' AND "Revision"=3)
 OR(SELECT count(*) FROM "ClinicalFollowUpVersions" WHERE "WorkId"='a2000000-0000-4000-8000-000000000001')<>3 THEN RAISE EXCEPTION 'Reassignment lost unresolved work or attributable history'; END IF;
 BEGIN
  UPDATE "ClinicalFollowUpWorks" SET "Revision"=4,"UpdatedBy"='30000000-0000-4000-8000-000000000001' WHERE "Id"='a2000000-0000-4000-8000-000000000001';
  SET CONSTRAINTS ALL IMMEDIATE;
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Follow-up mutation without retained version committed'; END IF;
END $$;
SET CONSTRAINTS ALL IMMEDIATE;
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000002',true);
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "ClinicalFollowUpWorks") OR EXISTS(SELECT 1 FROM "ClinicalFollowUpVersions") THEN RAISE EXCEPTION 'Cross-tenant follow-up/history exposed'; END IF; END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "ClinicalFollowUpWorks") OR EXISTS(SELECT 1 FROM "ClinicalFollowUpVersions") THEN RAISE EXCEPTION 'Restricted chart follow-up/history exposed without current actor'; END IF; END $$;
ROLLBACK;
'@ | Out-Null
    }

    $noShowVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."AppointmentNoShows"'') IS NOT NULL') -eq 't'
    if($noShowVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
INSERT INTO "StaffProfiles"("Id","TenantId","UserId","EmploymentType","IsAvailable","CreatedAt","UpdatedAt","IsActive","IsDeleted")
VALUES('a3000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000001',0,true,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false);
INSERT INTO "AppointmentReservations"("Id","TenantId","OwnerUserId","FacilityId","PractitionerUserId","StartsAtUtc","EndsAtUtc","HoldExpiresAtUtc","State","Revision","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
VALUES('a3000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000001',date_trunc('minute',clock_timestamp())-interval '5 minutes',date_trunc('minute',clock_timestamp())+interval '10 minutes',clock_timestamp()+interval '3 minutes','held',1,'synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false);
INSERT INTO "AppointmentReservationSeats"("Id","TenantId","ReservationId","TargetType","TargetId","Seat","CapacitySnapshot","StartsAtUtc","EndsAtUtc","State","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id",'practitioner',"PractitionerUserId",1,1,"StartsAtUtc","EndsAtUtc",'held',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false FROM "AppointmentReservations" WHERE "Id"='a3000000-0000-4000-8000-000000000002';
INSERT INTO "Appointments"("Id","TenantId","PatientId","PatientName","PatientPhone","DoctorUserId","DoctorName","Department","ScheduledAt","DurationMinutes","Status","ReminderSent","FollowUpSent","IsPaid","FacilityId","ReservationId","BookingKind","BookingPolicyVersion","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a3000000-0000-4000-8000-000000000003',"TenantId",'d0000000-0000-4000-8000-000000000001','Synthetic no-show','',"PractitionerUserId",'Synthetic owner','Synthetic',"StartsAtUtc",15,0,false,false,false,"FacilityId","Id",'walk_in','synthetic-only',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"OwnerUserId",true,false FROM "AppointmentReservations" WHERE "Id"='a3000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservations" SET "State"='confirmed',"Revision"=2,"AppointmentId"='a3000000-0000-4000-8000-000000000003',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='a3000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservationSeats" SET "State"='confirmed',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='a3000000-0000-4000-8000-000000000002';
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
INSERT INTO "ClinicalFollowUpWorks"("Id","TenantId","PatientId","SourceType","SourceId","SourceVersion","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a3000000-0000-4000-8000-000000000004',"TenantId","PatientId",'appointment',"Id","Revision"::text,'30000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000001',CURRENT_TIMESTAMP+interval '1 day','open',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false FROM "Appointments" WHERE "Id"='a3000000-0000-4000-8000-000000000003';
INSERT INTO "ClinicalFollowUpVersions"("Id","TenantId","WorkId","PatientId","Revision","AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","Action","Reason","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id","PatientId",1,"AssignedUserId","EscalationOwnerUserId","DueAtUtc",'open','create','Synthetic named no-show follow-up',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false FROM "ClinicalFollowUpWorks" WHERE "Id"='a3000000-0000-4000-8000-000000000004';
INSERT INTO "AppointmentNoShows"("Id","TenantId","AppointmentId","PatientId","FacilityId","ReservationId","FollowUpWorkId","OriginalAppointmentRevision","AppointmentRevision","ScheduledAtUtc","DurationMinutes","CutoffMinutesAfterStart","RecordedAtUtc","Reason","PolicyVersion","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT 'a3000000-0000-4000-8000-000000000005',"TenantId","Id","PatientId","FacilityId","ReservationId",'a3000000-0000-4000-8000-000000000004',"Revision","Revision"+1,"ScheduledAt","DurationMinutes",0,clock_timestamp(),'Synthetic confirmed absence','synthetic-only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'30000000-0000-4000-8000-000000000001',true,false FROM "Appointments" WHERE "Id"='a3000000-0000-4000-8000-000000000003';
UPDATE "AppointmentReservations" SET "State"='released',"Revision"=3,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"='a3000000-0000-4000-8000-000000000002';
UPDATE "AppointmentReservationSeats" SET "State"='released',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "ReservationId"='a3000000-0000-4000-8000-000000000002';
UPDATE "Appointments" SET "Status"=3,"Revision"=2,"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"='30000000-0000-4000-8000-000000000001' WHERE "Id"='a3000000-0000-4000-8000-000000000003';
SET CONSTRAINTS ALL IMMEDIATE;
DO $$ DECLARE denied boolean=false; BEGIN
 IF NOT EXISTS(SELECT 1 FROM "Appointments" a JOIN "AppointmentNoShows" n ON n."AppointmentId"=a."Id"
 JOIN "ClinicalFollowUpWorks" w ON w."Id"=n."FollowUpWorkId" WHERE a."Id"='a3000000-0000-4000-8000-000000000003'
 AND a."Status"=3 AND a."Revision"=2 AND w."SourceVersion"='1' AND w."AssignedUserId" IS NOT NULL)
 THEN RAISE EXCEPTION 'No-show lost accountable exact-source follow-up'; END IF;
 BEGIN UPDATE "Appointments" SET "CheckedInAt"=clock_timestamp(),"Revision"=3,"UpdatedBy"='30000000-0000-4000-8000-000000000001' WHERE "Id"='a3000000-0000-4000-8000-000000000003'; EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Late arrival silently replaced no-show'; END IF;
 denied=false;
 BEGIN UPDATE "AppointmentNoShows" SET "Reason"='Rewritten'; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'No-show history rewritten'; END IF;
 denied=false;
 BEGIN DELETE FROM "AppointmentNoShows"; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'No-show history deleted'; END IF;
END $$;
COMMIT;
'@ | Out-Null
    }

# Insert after all follow-up/no-show fixture blocks, immediately before $archivePath.
# Evidence property: PatientSpecificAppointmentQuerySqlVerified=$patientAppointmentQueryVerified.
# Uses actual retained appointment rows from queue/reschedule/waitlist rehearsals. No application/API tests.
    $patientAppointmentQueryVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."Appointments"'') IS NOT NULL') -eq 't'
    if($patientAppointmentQueryVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','30000000-0000-4000-8000-000000000001',true);
DO $$
DECLARE first_id uuid; second_id uuid; first_at timestamptz; next_id uuid; zone text; local_time timestamp; cancelled_count integer;
BEGIN
 -- Actual list predicate, half-open window and stable tuple keyset; same-name/phone labels have no role in matching.
 SELECT a."Id",a."ScheduledAt" INTO first_id,first_at FROM "Appointments" a
 WHERE a."TenantId"='10000000-0000-4000-8000-000000000001' AND a."PatientId"='d0000000-0000-4000-8000-000000000001'
  AND a."IsActive" AND NOT a."IsDeleted" AND a."ScheduledAt">=CURRENT_TIMESTAMP-interval '1 day' AND a."ScheduledAt"<CURRENT_TIMESTAMP+interval '30 days'
 ORDER BY a."ScheduledAt",a."Id" LIMIT 1;
 IF first_id IS NULL THEN RAISE EXCEPTION 'Patient-specific actual fixture list is empty'; END IF;
 SELECT a."Id" INTO second_id FROM "Appointments" a
 WHERE a."TenantId"='10000000-0000-4000-8000-000000000001' AND a."PatientId"='d0000000-0000-4000-8000-000000000001'
  AND a."IsActive" AND NOT a."IsDeleted" AND a."ScheduledAt">=CURRENT_TIMESTAMP-interval '1 day' AND a."ScheduledAt"<CURRENT_TIMESTAMP+interval '30 days'
  AND(a."ScheduledAt",a."Id")>(first_at,first_id)
 ORDER BY a."ScheduledAt",a."Id" LIMIT 1;
 IF second_id IS NULL OR second_id=first_id THEN RAISE EXCEPTION 'Patient appointment keyset repeated or lost the following row'; END IF;
 SELECT count(*) INTO cancelled_count FROM "Appointments" a
 WHERE a."TenantId"='10000000-0000-4000-8000-000000000001' AND a."PatientId"='d0000000-0000-4000-8000-000000000001'
  AND a."IsActive" AND NOT a."IsDeleted" AND a."Status"=4 AND a."FacilityId"='a0000000-0000-4000-8000-000000000001';
 IF cancelled_count<>1 THEN RAISE EXCEPTION 'Exact facility/status list did not retain the cancellation'; END IF;
 -- Actual next-projection SQL and effective facility-timezone lateral join.
 SELECT a."Id",setting."Timezone",a."ScheduledAt" AT TIME ZONE setting."Timezone" INTO next_id,zone,local_time
 FROM "Appointments" a LEFT JOIN LATERAL(SELECT s."Timezone" FROM "FacilityOperatingSettings" s
  WHERE s."TenantId"=a."TenantId" AND s."FacilityId"=a."FacilityId" AND s."EffectiveFrom"<=a."ScheduledAt"
  ORDER BY s."EffectiveFrom" DESC,s."Revision" DESC LIMIT 1)setting ON true
 WHERE a."TenantId"='10000000-0000-4000-8000-000000000001' AND a."PatientId"='d0000000-0000-4000-8000-000000000001'
  AND a."IsActive" AND NOT a."IsDeleted" AND a."Status" IN(0,1) AND a."ScheduledAt">=CURRENT_TIMESTAMP
 ORDER BY a."ScheduledAt",a."Id" LIMIT 1;
 IF next_id IS DISTINCT FROM 'a1000000-0000-4000-8000-000000000004'::uuid OR zone IS DISTINCT FROM 'Asia/Kolkata'
  OR local_time IS DISTINCT FROM(SELECT "ScheduledAt" AT TIME ZONE 'Asia/Kolkata' FROM "Appointments" WHERE "Id"=next_id)
 THEN RAISE EXCEPTION 'Next visit selected cancelled/past/other patient work or wrong facility timezone'; END IF;
 IF EXISTS(SELECT 1 FROM "Appointments" WHERE "PatientId"='20000000-0000-4000-8000-000000000001' AND "Id" IN(first_id,second_id,next_id))
 THEN RAISE EXCEPTION 'Explicit patient query mixed another patient'; END IF;
 IF EXISTS(SELECT 1 FROM "Appointments" WHERE "PatientId"='d0000000-0000-4000-8000-000000000001'
  AND "FacilityId"='a0000000-0000-4000-8000-000000000001' AND "ScheduledAt">=first_at AND "ScheduledAt"<first_at)
 THEN RAISE EXCEPTION 'Half-open empty boundary returned a row'; END IF;
 -- Supplemental local midnight conversion boundary; actual next eligibility remains an absolute instant comparison.
 IF('2026-10-12T18:30:00Z'::timestamptz AT TIME ZONE 'Asia/Kolkata')::date<>'2026-10-13'::date
 THEN RAISE EXCEPTION 'Facility midnight conversion is incorrect'; END IF;
END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000002',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "Appointments" WHERE "PatientId"='d0000000-0000-4000-8000-000000000001') THEN RAISE EXCEPTION 'Tenant scope exposed patient appointments'; END IF; END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','',true);
DO $$ BEGIN IF EXISTS(SELECT 1 FROM "Appointments" WHERE "PatientId"='20000000-0000-4000-8000-000000000001') THEN RAISE EXCEPTION 'Restricted patient appointment rows exposed without actor'; END IF; END $$;
ROLLBACK;
'@ | Out-Null
    }

    $encounterVerified=(Invoke-PgSql cureflow_rehearsal 'SELECT to_regclass(''public."EncounterTransitions"'') IS NOT NULL') -eq 't'
    if($encounterVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
-- Explicit synthetic fixture permissions only, never production defaults.
INSERT INTO "Permissions"("Id","Code","Name","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),code,code,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false FROM(VALUES('Clinical.Edit'),('Appointment.View'))p(code)
WHERE NOT EXISTS(SELECT 1 FROM "Permissions" e WHERE e."Code"=p.code);
INSERT INTO "RolePermissions"("Id","RoleId","PermissionId","CreatedAt","UpdatedAt","IsActive","IsDeleted")
SELECT gen_random_uuid(),'40000000-0000-4000-8000-000000000001',p."Id",CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false
FROM "Permissions" p WHERE p."Code" IN('Clinical.Edit','Appointment.View')
AND NOT EXISTS(SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId"='40000000-0000-4000-8000-000000000001' AND rp."PermissionId"=p."Id");
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
INSERT INTO "Visits"("Id","TenantId","PatientId","AppointmentId","DoctorUserId","DoctorName","VisitDate","Status","VisitType","AttachmentsJson","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
VALUES('a4000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000006','a0000000-0000-4000-8000-000000000002','Synthetic assigned doctor',clock_timestamp(),1,'outpatient','[]',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false);
INSERT INTO "EncounterTransitions"("Id","TenantId","VisitId","PatientId","AppointmentId","Revision","Status","StartedAtUtc","Action","Reason","PolicyVersion","SourceAppointmentRevision","ContentSnapshotJson","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id","PatientId","AppointmentId",1,1,"VisitDate",'start','Synthetic exact arrived source','synthetic-only',2,'{}',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false FROM "Visits" WHERE "Id"='a4000000-0000-4000-8000-000000000001';
SET CONSTRAINTS ALL IMMEDIATE;
SET CONSTRAINTS ALL DEFERRED;
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN INSERT INTO "Visits"("Id","TenantId","PatientId","AppointmentId","DoctorUserId","DoctorName","VisitDate","Status","VisitType","AttachmentsJson","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 SELECT gen_random_uuid(),"TenantId","PatientId","AppointmentId","DoctorUserId","DoctorName",clock_timestamp(),1,'outpatient','[]',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"CreatedBy",true,false FROM "Visits" WHERE "Id"='a4000000-0000-4000-8000-000000000001';
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Duplicate encounter start accepted'; END IF;
 denied=false;
 BEGIN INSERT INTO "Visits"("Id","TenantId","PatientId","AppointmentId","DoctorUserId","DoctorName","VisitDate","Status","VisitType","AttachmentsJson","Revision","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
 SELECT gen_random_uuid(),"TenantId",'20000000-0000-4000-8000-000000000001',"AppointmentId","DoctorUserId","DoctorName",clock_timestamp(),1,'outpatient','[]',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,"CreatedBy",true,false FROM "Visits" WHERE "Id"='a4000000-0000-4000-8000-000000000001';
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Other-patient source encounter accepted'; END IF;
END $$;
COMMIT;
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
UPDATE "Visits" SET "Symptoms"='Synthetic review',"Revision"=2,"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"='a0000000-0000-4000-8000-000000000002' WHERE "Id"='a4000000-0000-4000-8000-000000000001';
INSERT INTO "EncounterTransitions"("Id","TenantId","VisitId","PatientId","AppointmentId","Revision","PreviousStatus","Status","StartedAtUtc","Action","Reason","PolicyVersion","ContentSnapshotJson","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id","PatientId","AppointmentId",2,1,1,"VisitDate",'update','Synthetic retained clinical change','synthetic-only','{"Symptoms":"Synthetic review"}',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false FROM "Visits" WHERE "Id"='a4000000-0000-4000-8000-000000000001';
SET CONSTRAINTS ALL IMMEDIATE;
SET CONSTRAINTS ALL DEFERRED;
DO $$ DECLARE denied boolean=false; BEGIN
 BEGIN UPDATE "Visits" SET "Revision"=3,"UpdatedBy"='a0000000-0000-4000-8000-000000000002' WHERE "Id"='a4000000-0000-4000-8000-000000000001'; SET CONSTRAINTS ALL IMMEDIATE;
 EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Unrecorded encounter version committed'; END IF;
END $$;
UPDATE "Visits" SET "Status"=2,"EndedAt"=clock_timestamp(),"Revision"=3,"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"='a0000000-0000-4000-8000-000000000002' WHERE "Id"='a4000000-0000-4000-8000-000000000001';
INSERT INTO "EncounterTransitions"("Id","TenantId","VisitId","PatientId","AppointmentId","Revision","PreviousStatus","Status","StartedAtUtc","EndedAtUtc","Action","Reason","PolicyVersion","ContentSnapshotJson","CreatedAt","UpdatedAt","CreatedBy","IsActive","IsDeleted")
SELECT gen_random_uuid(),"TenantId","Id","PatientId","AppointmentId",3,1,2,"VisitDate","EndedAt",'complete','Synthetic explicit completion','synthetic-only','{"Symptoms":"Synthetic review"}',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'a0000000-0000-4000-8000-000000000002',true,false FROM "Visits" WHERE "Id"='a4000000-0000-4000-8000-000000000001';
SET CONSTRAINTS ALL IMMEDIATE;
DO $$ DECLARE denied boolean=false; BEGIN
 IF(SELECT count(*) FROM "Visits" WHERE "AppointmentId"='d0000000-0000-4000-8000-000000000006')<>1
 OR(SELECT count(*) FROM "EncounterTransitions" WHERE "VisitId"='a4000000-0000-4000-8000-000000000001')<>3 THEN RAISE EXCEPTION 'Encounter identity/history was not retained'; END IF;
 BEGIN UPDATE "Visits" SET "Status"=1,"EndedAt"=NULL,"Revision"=4,"UpdatedBy"='a0000000-0000-4000-8000-000000000002' WHERE "Id"='a4000000-0000-4000-8000-000000000001'; EXCEPTION WHEN check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Completed encounter reopened'; END IF;
 denied=false;
 BEGIN DELETE FROM "EncounterTransitions"; EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Encounter history deleted'; END IF;
END $$;
COMMIT;
'@ | Out-Null
    }

    $worklistVerified=$encounterVerified
    if($worklistVerified){
        Invoke-PgSql cureflow_rehearsal @'
BEGIN;
SET LOCAL ROLE cureflow_tenant;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000001',true);
SELECT set_config('app.current_user_id','a0000000-0000-4000-8000-000000000002',true);
CREATE TEMP TABLE synthetic_worklist ON COMMIT DROP AS SELECT a."Id" AS "AppointmentId",a."PatientId",a."Revision" AS "AppointmentRevision",a."ScheduledAt" AS "ScheduledAtUtc",
     q."Id" AS "QueueEntryId",q."Token" AS "ArrivalToken",q."ArrivedAtUtc",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Id" END AS "EncounterId",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Revision" END AS "EncounterRevision",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Status" END AS "EncounterStatus",
     (encounter.total>1 OR encounter.total<>encounter.matches) AS "RequiresReconciliation"
    FROM "Appointments" a
    LEFT JOIN "QueueEntries" q ON q."TenantId"=a."TenantId" AND q."AppointmentId"=a."Id" AND q."PatientId"=a."PatientId" AND q."FacilityId"=a."FacilityId"
    LEFT JOIN LATERAL(SELECT count(*) AS total,count(*) FILTER(WHERE e."PatientId"=a."PatientId" AND e."DoctorUserId"='a0000000-0000-4000-8000-000000000002'::uuid) AS matches
     FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id" AND e."IsActive" AND NOT e."IsDeleted")encounter ON true
    LEFT JOIN LATERAL(SELECT e."Id",e."Revision",e."Status" FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id"
     AND e."PatientId"=a."PatientId" AND e."DoctorUserId"='a0000000-0000-4000-8000-000000000002'::uuid AND e."IsActive" AND NOT e."IsDeleted"
     ORDER BY e."Id" LIMIT 1)v ON true
    WHERE a."TenantId"='10000000-0000-4000-8000-000000000001'::uuid AND a."FacilityId"='a0000000-0000-4000-8000-000000000001'::uuid AND a."DoctorUserId"='a0000000-0000-4000-8000-000000000002'::uuid AND a."IsActive" AND NOT a."IsDeleted"
     AND a."Status" IN(0,1,2) AND a."ScheduledAt">=((SELECT "ScheduledAt" AT TIME ZONE 'Asia/Kolkata' FROM "Appointments" WHERE "Id"='d0000000-0000-4000-8000-000000000006')::date::timestamp AT TIME ZONE 'Asia/Kolkata') AND a."ScheduledAt"<(((SELECT "ScheduledAt" AT TIME ZONE 'Asia/Kolkata' FROM "Appointments" WHERE "Id"='d0000000-0000-4000-8000-000000000006')::date+1)::timestamp AT TIME ZONE 'Asia/Kolkata')
     AND(q."Id" IS NOT NULL OR encounter.matches>0)
     AND(NULL::timestamptz IS NULL OR(a."ScheduledAt",a."Id")>(NULL,NULL::uuid))
     AND public.cureflow_patient_read_allowed(a."TenantId",a."PatientId")
    ORDER BY a."ScheduledAt",a."Id" LIMIT 101;
DO $$ BEGIN
 IF(SELECT count(*) FROM synthetic_worklist)<>1 OR NOT EXISTS(SELECT 1 FROM synthetic_worklist
 WHERE "AppointmentId"='d0000000-0000-4000-8000-000000000006' AND "PatientId"='d0000000-0000-4000-8000-000000000001'
 AND "QueueEntryId"='d0000000-0000-4000-8000-000000000008' AND "EncounterId"='a4000000-0000-4000-8000-000000000001'
 AND "EncounterRevision"=3 AND "EncounterStatus"=2 AND NOT "RequiresReconciliation") THEN
 RAISE EXCEPTION 'Assigned worklist did not preserve exact arrival/encounter/patient source'; END IF;
END $$;
CREATE TEMP TABLE synthetic_other_doctor ON COMMIT DROP AS SELECT a."Id" AS "AppointmentId",a."PatientId",a."Revision" AS "AppointmentRevision",a."ScheduledAt" AS "ScheduledAtUtc",
     q."Id" AS "QueueEntryId",q."Token" AS "ArrivalToken",q."ArrivedAtUtc",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Id" END AS "EncounterId",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Revision" END AS "EncounterRevision",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Status" END AS "EncounterStatus",
     (encounter.total>1 OR encounter.total<>encounter.matches) AS "RequiresReconciliation"
    FROM "Appointments" a
    LEFT JOIN "QueueEntries" q ON q."TenantId"=a."TenantId" AND q."AppointmentId"=a."Id" AND q."PatientId"=a."PatientId" AND q."FacilityId"=a."FacilityId"
    LEFT JOIN LATERAL(SELECT count(*) AS total,count(*) FILTER(WHERE e."PatientId"=a."PatientId" AND e."DoctorUserId"='30000000-0000-4000-8000-000000000001'::uuid) AS matches
     FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id" AND e."IsActive" AND NOT e."IsDeleted")encounter ON true
    LEFT JOIN LATERAL(SELECT e."Id",e."Revision",e."Status" FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id"
     AND e."PatientId"=a."PatientId" AND e."DoctorUserId"='30000000-0000-4000-8000-000000000001'::uuid AND e."IsActive" AND NOT e."IsDeleted"
     ORDER BY e."Id" LIMIT 1)v ON true
    WHERE a."TenantId"='10000000-0000-4000-8000-000000000001'::uuid AND a."FacilityId"='a0000000-0000-4000-8000-000000000001'::uuid AND a."DoctorUserId"='30000000-0000-4000-8000-000000000001'::uuid AND a."IsActive" AND NOT a."IsDeleted"
     AND a."Status" IN(0,1,2) AND a."ScheduledAt">=((SELECT "ScheduledAt" AT TIME ZONE 'Asia/Kolkata' FROM "Appointments" WHERE "Id"='d0000000-0000-4000-8000-000000000006')::date::timestamp AT TIME ZONE 'Asia/Kolkata') AND a."ScheduledAt"<(((SELECT "ScheduledAt" AT TIME ZONE 'Asia/Kolkata' FROM "Appointments" WHERE "Id"='d0000000-0000-4000-8000-000000000006')::date+1)::timestamp AT TIME ZONE 'Asia/Kolkata')
     AND(q."Id" IS NOT NULL OR encounter.matches>0)
     AND(NULL::timestamptz IS NULL OR(a."ScheduledAt",a."Id")>(NULL,NULL::uuid))
     AND public.cureflow_patient_read_allowed(a."TenantId",a."PatientId")
    ORDER BY a."ScheduledAt",a."Id" LIMIT 101;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM synthetic_other_doctor) THEN RAISE EXCEPTION 'Another clinician worklist borrowed assigned cases'; END IF; END $$;
SELECT set_config('app.current_tenant_id','10000000-0000-4000-8000-000000000002',true);
CREATE TEMP TABLE synthetic_other_tenant ON COMMIT DROP AS SELECT a."Id" AS "AppointmentId",a."PatientId",a."Revision" AS "AppointmentRevision",a."ScheduledAt" AS "ScheduledAtUtc",
     q."Id" AS "QueueEntryId",q."Token" AS "ArrivalToken",q."ArrivedAtUtc",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Id" END AS "EncounterId",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Revision" END AS "EncounterRevision",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Status" END AS "EncounterStatus",
     (encounter.total>1 OR encounter.total<>encounter.matches) AS "RequiresReconciliation"
    FROM "Appointments" a
    LEFT JOIN "QueueEntries" q ON q."TenantId"=a."TenantId" AND q."AppointmentId"=a."Id" AND q."PatientId"=a."PatientId" AND q."FacilityId"=a."FacilityId"
    LEFT JOIN LATERAL(SELECT count(*) AS total,count(*) FILTER(WHERE e."PatientId"=a."PatientId" AND e."DoctorUserId"='a0000000-0000-4000-8000-000000000002'::uuid) AS matches
     FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id" AND e."IsActive" AND NOT e."IsDeleted")encounter ON true
    LEFT JOIN LATERAL(SELECT e."Id",e."Revision",e."Status" FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id"
     AND e."PatientId"=a."PatientId" AND e."DoctorUserId"='a0000000-0000-4000-8000-000000000002'::uuid AND e."IsActive" AND NOT e."IsDeleted"
     ORDER BY e."Id" LIMIT 1)v ON true
    WHERE a."TenantId"='10000000-0000-4000-8000-000000000001'::uuid AND a."FacilityId"='a0000000-0000-4000-8000-000000000001'::uuid AND a."DoctorUserId"='a0000000-0000-4000-8000-000000000002'::uuid AND a."IsActive" AND NOT a."IsDeleted"
     AND a."Status" IN(0,1,2) AND a."ScheduledAt">=((SELECT "ScheduledAt" AT TIME ZONE 'Asia/Kolkata' FROM "Appointments" WHERE "Id"='d0000000-0000-4000-8000-000000000006')::date::timestamp AT TIME ZONE 'Asia/Kolkata') AND a."ScheduledAt"<(((SELECT "ScheduledAt" AT TIME ZONE 'Asia/Kolkata' FROM "Appointments" WHERE "Id"='d0000000-0000-4000-8000-000000000006')::date+1)::timestamp AT TIME ZONE 'Asia/Kolkata')
     AND(q."Id" IS NOT NULL OR encounter.matches>0)
     AND(NULL::timestamptz IS NULL OR(a."ScheduledAt",a."Id")>(NULL,NULL::uuid))
     AND public.cureflow_patient_read_allowed(a."TenantId",a."PatientId")
    ORDER BY a."ScheduledAt",a."Id" LIMIT 101;
DO $$ BEGIN IF EXISTS(SELECT 1 FROM synthetic_other_tenant) THEN RAISE EXCEPTION 'Cross-tenant assigned worklist exposed'; END IF; END $$;
ROLLBACK;
'@ | Out-Null
    }
    $archivePath = Join-Path $runPath 'synthetic-only.dump'
    $dumpOutput = & (Join-Path $PostgreSqlBin 'pg_dump.exe') -h 127.0.0.1 -p $port -U rehearsal_owner -d cureflow_rehearsal -Fc -f $archivePath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Synthetic archive creation failed: $dumpOutput" }
    Invoke-PgSql postgres 'CREATE DATABASE cureflow_restored' | Out-Null
    $restoreOutput = & (Join-Path $PostgreSqlBin 'pg_restore.exe') -h 127.0.0.1 -p $port -U rehearsal_owner -d cureflow_restored --exit-on-error $archivePath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Synthetic restore failed: $restoreOutput" }
    $restoredCount = Invoke-PgSql cureflow_restored 'SELECT count(*) FROM "__EFMigrationsHistory"'
    $marker = Invoke-PgSql cureflow_restored "SELECT value FROM rehearsal.evidence WHERE id='4a711069-4dcb-47df-8943-cad1b80273ac'"
    $restoredPatientCount = Invoke-PgSql cureflow_restored 'SELECT count(*) FROM public."Patients" WHERE "Id" IN (''20000000-0000-4000-8000-000000000001'',''20000000-0000-4000-8000-000000000002'')'
    if ($migrationCount -ne $restoredCount -or $marker -ne 'synthetic-preserved-marker' -or $restoredPatientCount -ne '2') {
        throw "Synthetic restore reconciliation failed: migrations=$migrationCount restored=$restoredCount patients=$restoredPatientCount marker=$marker"
    }
    $evidence = [ordered]@{
        Kind = 'Empty synthetic database rehearsal; not existing-data acceptance'
        CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        MigrationCount = [int]$migrationCount
        RestoredMigrationCount = [int]$restoredCount
        ControlPlaneClinicalReadDenied = $true
        NarrowSessionGrants = $true
        TenantRowsIsolated = $true
        MissingTenantDenied = $true
        PlatformFlagCannotBypassRls = $true
        CrossTenantReassignmentDenied = $true
        TransactionContextClearedAfterRollback = $true
        RestrictedPatientScopeVerified = $privacyVerified
        RetainedScheduleScopeVerified = $scheduleVerified
        ScheduleExceptionBookingImpactVerified = $exceptionVerified
        AtomicReservationSeatAndTerminalGuardsVerified = $reservationVerified
        ConcurrentLastPractitionerSeatVerified = $reservationVerified
        LegacyBookingForwardUpgradePreserved = ($null -ne $forwardSchemaPath)
        ExactQueueArrivalAndRetainedTokenVerified = $queueVerified
        AttributablePriorityAndCompetingStaffClaimVerified = $dispatchVerified
        RetainedRescheduleAndCancellationVerified = $bookingChangesVerified
        ReleasedCapacityWaitlistAcceptanceVerified = $waitlistVerified
        NamedFollowUpOwnershipAndRetainedEvidenceVerified = $followUpVerified
        NoShowAccountableFollowUpAndLateArrivalGuardVerified = $noShowVerified
        PatientSpecificAppointmentQuerySqlVerified = $patientAppointmentQueryVerified
        RetainedEncounterLifecycleVerified = $encounterVerified
        AssignedClinicianWorklistSqlVerified = $worklistVerified
        AuthorityReplaySucceeded = $true
        SyntheticMarkerPreserved = $true
        SchemaSha256 = (Get-FileHash -LiteralPath $schemaPath -Algorithm SHA256).Hash
        AuthoritySha256 = (Get-FileHash -LiteralPath $authorityPath -Algorithm SHA256).Hash
        SyntheticArchiveSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    }
    $evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runPath 'evidence.json') -Encoding utf8
    Write-Output "Synthetic schema/grant/restore rehearsal passed: $runPath"
}
finally {
    if ($started) {
        $stopOutput = & (Join-Path $PostgreSqlBin 'pg_ctl.exe') -D $clusterPath -m fast -w stop 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Synthetic helper did not stop; inspect its retained server.log.' }
    }
    $env:PGPASSFILE = $originalPassfile
    foreach ($secretPath in @($passwordPath, $passfilePath)) {
        if (Test-Path -LiteralPath $secretPath) { Remove-Item -LiteralPath $secretPath }
    }
}
