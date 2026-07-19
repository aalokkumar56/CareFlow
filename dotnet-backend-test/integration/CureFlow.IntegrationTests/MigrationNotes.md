# Migration notes: unit DB suites → integration

Move PostgreSQL-backed suites out of `unit/CureFlow.UnitTests` into this project.
Gate on `CUREFLOW_TEST_CONNECTION` (and use `[Collection("Database")]` / `DatabaseCollection` for serial DB mutation).

## Move as-is (DB-only)

| Unit file | Notes |
|---|---|
| `TenantRlsIsolationTests.cs` | RLS at DB layer; first stub is `TenantRlsIntegrationTests.cs` |
| `TenantApiIsolationTests.cs` | Service-level tenant scoping against live DB |
| `AuditLogIsolationTests.cs` | Uses `[Collection("DatabaseIntegration")]` today |
| `MultiTenantAuthTests` (class in `MultiTenantAuthTests.cs`) | Register/login JWT + tenant; keep `TenantSlugHelperTests` in unit |

## Split before move (mixed unit + DB)

| Unit file | Keep in unit | Move to integration |
|---|---|---|
| `TenantIsolationTests.cs` | `WhereActive_*` SQL-fragment facts | `CrossTenant_GetById_ReturnsNull_WhenPatientBelongsToOtherTenant` |
| `MultiTenantIsolationTests.cs` | `MultiHospitalE2eSeeder_DefinesThreeDistinctHospitals` | `TenantA_CannotGetTenantBPatient_ById` and other DB facts |

## Shared helpers to relocate (or duplicate lightly)

| Unit file | Integration destination |
|---|---|
| `DatabaseIntegrationCollection.cs` | `DatabaseCollection.cs` (collection name `"Database"`) |
| `TestDbConnection.cs` / `TestDbHelper.cs` | Shared helper under integration (env `CUREFLOW_TEST_CONNECTION`; optional appsettings fallback) |

## Stay in unit

Pure unit / mocked suites (no live PostgreSQL), including but not limited to:

`SmokeTests`, `RoleNameRulesTests`, `TenantTimeHelperTests`, `AnyArrayParameterTests`, `CreateUserRequestBindingTests`, `SafeRemoteUrlTests`, WhatsApp/Email/Notification service tests that use mocks, `TenantSlugHelperTests`.

## Suggested order

1. Helpers + `DatabaseCollection`
2. `TenantRlsIsolationTests` → flesh out `TenantRlsIntegrationTests`
3. `TenantApiIsolationTests`, `AuditLogIsolationTests`
4. Split mixed files; move DB facts
5. `MultiTenantAuthTests` (mutates auth data — keep serial via `Database` collection)
6. Delete moved code from unit; drop unit `DatabaseIntegration` collection when unused
