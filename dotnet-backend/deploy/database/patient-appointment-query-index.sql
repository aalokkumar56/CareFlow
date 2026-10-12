START TRANSACTION;
CREATE INDEX "IX_Appointments_TenantId_PatientId_ScheduledAt_Id" ON "Appointments" ("TenantId", "PatientId", "ScheduledAt", "Id");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261012111801_AddPatientAppointmentQueryIndex', '10.0.0');

COMMIT;

