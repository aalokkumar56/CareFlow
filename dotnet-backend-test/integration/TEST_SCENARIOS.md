# Cure-Flow Integration Test Scenario Catalog

> **Status:** Catalog only — no tests implemented yet.  
> **Target project:** `dotnet-backend-test/integration/CureFlow.IntegrationTests` (future)  
> **Target solution:** `dotnet-backend-test/CureFlow.IntegrationTest.sln` (future)  
> **Style:** Microsoft ASP.NET Core integration testing (`WebApplicationFactory<TEntryPoint>`, real PostgreSQL, Testcontainers optional)

This document enumerates **exhaustive** integration scenarios for Cure-Flow’s .NET API. Integration tests exercise **real HTTP + middleware + auth + DB (including RLS)** — not mocked service units.

---

## 1. Scope and definitions

### In scope (integration)

| Layer | Covered |
|-------|---------|
| HTTP pipeline | Routing, model binding, validation filters, exception → status codes |
| AuthN / AuthZ | JWT, permission policies, platform-user policy, rate limiting (behavior) |
| Middleware | Tenant resolution, tenant lifecycle gates, RLS session setup |
| Persistence | PostgreSQL via app DI; Dapper/EF paths; soft-delete; tenant filters |
| Cross-tenant | Isolation via app filters **and** Postgres RLS |
| External I/O | WhatsApp/Email/SMS/AI — **fakes or WireMock** inside factory; webhook body still hits real processor |

### Out of scope (remain unit)

| Suite / concern | Keep in `CureFlow.UnitTests` |
|-----------------|------------------------------|
| Pure helpers | `TenantSlugHelperTests`, `WhatsappPhoneHelperTests`, `TenantTimeHelperTests`, `SafeRemoteUrlTests`, `WhatsappMediaPolicyTests`, `WhatsappNotificationTypeTests`, `AppointmentMessageTimeTests`, `RoleNameRulesTests`, `AnyArrayParameterTests`, `CreateUserRequestBindingTests`, `SmokeTests` |
| Signature/math unit | `WhatsappWebhookProcessorTests` (HMAC extract/verify pure methods) |
| Moq’d service units without DB | `NotificationServiceTests`, `EmailServiceTests`, `WhatsappApiServiceTests`, `WhatsAppSettingsServiceTests` (unless rewritten as HTTP/DB) |
| SQL fragment string asserts | `TenantIsolationTests.WhereActive_*` (pure unit) |

### Test types used in this catalog

| Code | Meaning |
|------|---------|
| **HTTP** | `WebApplicationFactory` → `HttpClient` against `CureFlow.Api` |
| **DB** | Direct Npgsql/Dapper against test DB (RLS session, seed probes) |
| **HYBRID** | HTTP action + DB assertion of side effects |
| **HOSTED** | Optional: hosted service / scheduler tick under factory (lower priority) |

### Priority

| P | Meaning |
|---|---------|
| **P0** | Security / multi-tenant / lifecycle — must ship before relying on CI green |
| **P1** | Core CRM CRUD and auth happy paths |
| **P2** | Secondary modules, settings, edge cases |
| **P3** | Nice-to-have / demos / AI / performance soak |

---

## 2. Recommended harness (for implementers later)

```
dotnet-backend-test/
├── CureFlow.UnitTest.sln
├── CureFlow.IntegrationTest.sln          ← add later
├── unit/CureFlow.UnitTests/              ← pure unit (+ temporary DB suites to migrate)
└── integration/
    ├── TEST_SCENARIOS.md                 ← this file
    ├── README.md
    └── CureFlow.IntegrationTests/        ← add later
        ├── Fixtures/
        │   ├── CureFlowWebApplicationFactory.cs
        │   ├── PostgresFixture.cs          (Testcontainers or CUREFLOW_TEST_CONNECTION)
        │   └── AuthTokenFactory.cs
        ├── Helpers/
        │   ├── HttpAuthExtensions.cs
        │   ├── SeedHelpers.cs
        │   └── RlsSessionHelper.cs
        └── Scenarios/
            ├── Auth/
            ├── Platform/
            ├── Tenancy/
            ├── Patients/
            └── ...
```

**Conventions**

- Collection `IntegrationSerial` with `DisableParallelization = true` for mutating DB suites.
- Each scenario ID (`INT-xxx`) maps 1:1 to a `[Fact]` / `[Theory]` name prefix.
- Prefer unique emails/slugs per test (`Guid` suffix) over shared mutable seed where possible.
- Seed tenants `care-cure-althan` / `city-hospital-surat` (from `MultiHospitalE2eSeeder`) are valid for cross-tenant isolation fixtures.
- Env: `CUREFLOW_TEST_CONNECTION` (or Testcontainers connection injected into factory config).

**Factory overrides (typical)**

- Replace WhatsApp Meta HTTP client with stub returning demo success.
- Replace AI / scraper with no-op.
- Use test JWT signing key from configuration.
- Disable or shorten rate-limit windows for deterministic tests (or use unique IPs).

---

## 3. Existing DB-backed unit suites → migrate here

These live under `dotnet-backend-test/unit/CureFlow.UnitTests` and use `CUREFLOW_TEST_CONNECTION` / `DatabaseIntegration`. When `CureFlow.IntegrationTests` exists, **move or rewrite** them as HTTP/DB integration scenarios (mapping below).

| Current suite | Type today | Migrate? | Target INT ranges | Notes |
|---------------|------------|----------|-------------------|-------|
| `TenantRlsIsolationTests` | DB RLS | **Yes — full** | INT-100… | Keep as DB-layer suite inside integration project |
| `MultiTenantIsolationTests` | DB session | **Yes — full** | INT-110…, INT-200… | Rewrite patient isolation as HTTP where possible; keep registration collision as HYBRID |
| `TenantIsolationTests` (DB facts only) | DB session | **Yes — DB facts** | INT-110… | Leave `WhereActive_*` string tests in unit |
| `TenantApiIsolationTests` | Service + DB | **Yes — rewrite as HTTP** | INT-210… | Prefer `WebApplicationFactory` over constructing services |
| `AuditLogIsolationTests` | Service + DB | **Yes — rewrite as HTTP** | INT-220…, INT-700… | Assert via `GET /api/audit-logs` |
| `MultiTenantAuthTests` (DB facts) | AuthService + DB | **Yes — rewrite as HTTP** | INT-010…, INT-020… | `RegisterAndLogin_*`, three hospitals, reserved slug → HTTP |
| `TenantSlugHelperTests` | Pure unit | **No** | — | Stay in unit |
| `DatabaseIntegrationCollection` | Fixture | **Yes — rename** | — | Become `IntegrationSerial` collection |
| `TestDbConnection` / `TestDbHelper` | Infra | **Yes — share** | — | Move to integration helpers; unit keeps thin skip stubs if needed |

**Do not migrate (unit):** `SmokeTests`, binding/helper/WhatsApp processor pure tests, Moq service tests.

---

## 4. Scenario index (by area)

| Area | ID range | Count (approx.) |
|------|----------|----------------:|
| A. Host / health / factory smoke | INT-001 – INT-009 | 9 |
| B. Tenant auth (hospital JWT) | INT-010 – INT-039 | 30 |
| C. Platform auth & tenant lifecycle | INT-040 – INT-079 | 40 |
| D. Onboarding gates | INT-080 – INT-099 | 20 |
| E. DB + RLS isolation | INT-100 – INT-149 | 50 |
| F. Cross-tenant HTTP isolation | INT-200 – INT-249 | 50 |
| G. Patients HTTP CRUD | INT-300 – INT-349 | 50 |
| H. Appointments HTTP | INT-350 – INT-389 | 40 |
| I. Clinical / EHR HTTP | INT-400 – INT-459 | 60 |
| J. Users / roles / permissions | INT-500 – INT-549 | 50 |
| K. Notifications | INT-550 – INT-579 | 30 |
| L. Campaigns | INT-600 – INT-639 | 40 |
| M. WhatsApp webhook & messaging | INT-650 – INT-699 | 50 |
| N. Conversations / inbox | INT-700 – INT-729 | 30 |
| O. Staff / schedules | INT-730 – INT-749 | 20 |
| P. Referrals / referring doctors | INT-750 – INT-769 | 20 |
| Q. Tasks / dashboard / audit | INT-770 – INT-799 | 30 |
| R. Settings / integrations / templates | INT-800 – INT-849 | 50 |
| S. Public / tenant resolution | INT-850 – INT-869 | 20 |
| T. Negative / security / abuse | INT-900 – INT-949 | 50 |
| U. End-to-end SaaS journeys | INT-950 – INT-979 | 30 |
| | **Total catalogued** | **~800** |

IDs below are the authoritative scenario list. Gaps in numbering within a band are reserved for future scenarios.

---

## 5. Scenarios

### A. Host / health / factory smoke — INT-001…INT-009

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-001 | P0 | HTTP | Factory boots `CureFlow.Api` | Factory + test DB | `GET /api/health` | 200; `status=healthy` |
| INT-002 | P0 | HTTP | Swagger/OpenAPI available in Development | Dev env | `GET /swagger/v1/swagger.json` | 200; contains `api/auth/login` |
| INT-003 | P0 | HYBRID | Migrations applied on factory start (or explicit migrate) | Empty/test DB | Boot | Tables `Tenants`, `Patients`, RLS policies exist |
| INT-004 | P1 | HTTP | Unauthenticated protected route → 401 | No token | `GET /api/patients` | 401 |
| INT-005 | P1 | HTTP | Invalid JWT → 401 | Bad bearer | `GET /api/auth/me` | 401 |
| INT-006 | P1 | HTTP | Expired JWT → 401 | Expired token | `GET /api/auth/me` | 401 |
| INT-007 | P2 | HTTP | Content-Type JSON binding failure → 400 | Malformed JSON | `POST /api/auth/login` | 400 |
| INT-008 | P2 | HTTP | CORS preflight does not break API (if configured) | OPTIONS | `OPTIONS /api/health` | 204/200 per config |
| INT-009 | P3 | HTTP | Dev seed endpoint gated | Non-dev / unauthorized | `POST /api/dev/seed-multi-hospitals` | 404 or 401/403 |

---

### B. Tenant authentication (hospital JWT) — INT-010…INT-039

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-010 | P0 | HTTP | Register tenant happy path | Unique hospital+email | `POST /api/auth/register-tenant` | 200; tenant id/slug; lifecycle `PendingApproval` (or product default) |
| INT-011 | P0 | HTTP | Register then login returns JWT with `tenant_id` | From INT-010 | `POST /api/auth/login` | 200; JWT claim `tenant_id` matches response tenant |
| INT-012 | P0 | HTTP | Login wrong password | Known user | Login bad password | 401/400 per API contract |
| INT-013 | P0 | HTTP | Login unknown email | — | Login | 401/400 |
| INT-014 | P0 | HTTP | `GET /api/auth/me` with valid tenant JWT | Active tenant admin | Me | 200; email/role match |
| INT-015 | P0 | HTTP | `GET /api/auth/session` includes lifecycle + onboarding flags | Pending/Active/etc. | Session | DTO fields match DB |
| INT-016 | P1 | HTTP | Register rejects duplicate admin email | Existing email | Register-tenant | 409/400 |
| INT-017 | P1 | HTTP | Register rejects reserved slug base (`admin`, `api`, `platform`) | Name that slugifies reserved | Register-tenant | Error *(migrates `RegisterTenant_RejectsReservedSlug`)* |
| INT-018 | P1 | HTTP | Three hospitals → distinct slugs and tenant IDs | 3 registers | Register×3 | Unique ids/slugs; system placeholders seeded per tenant *(migrates `RegisterThreeHospitals_*`)* |
| INT-019 | P1 | HTTP | Register-tenant rate limit eventually 429 | Burst requests | Register spam | 429 after policy |
| INT-020 | P1 | HTTP | Login rate limit eventually 429 | Burst logins | Login spam | 429 |
| INT-021 | P1 | HTTP | JWT includes permission claims for role | Doctor user | Login → decode | Expected `permission` claims present |
| INT-022 | P1 | HTTP | Disabled user cannot login | User `is_active=false` | Login | Fail |
| INT-023 | P1 | HTTP | Soft-deleted user cannot login | User deleted | Login | Fail |
| INT-024 | P1 | HYBRID | Create user via `POST /api/auth/register` (User.Create) | Admin token | Create user | DB row tenant-scoped; login works |
| INT-025 | P1 | HTTP | Create user without User.Create → 403 | Reception token | `POST /api/auth/register` | 403 |
| INT-026 | P2 | HTTP | Login response user/tenant DTO shape | Active | Login | Required fields non-null |
| INT-027 | P2 | HTTP | Me after role assignment reflects new permissions | Assign role | Me / new JWT | Permissions updated (re-login if needed) |
| INT-028 | P2 | HTTP | Cross-tenant JWT cannot be used as another tenant by rewriting header only | Tenant A JWT + Host/slug B | Patients list | Still scoped to A (or 403) — no privilege escalation |
| INT-029 | P2 | HTTP | Platform JWT rejected on tenant CRM routes requiring tenant context | Platform token | `GET /api/patients` | 401/403 |
| INT-030 | P2 | HTTP | Tenant JWT rejected on platform routes | Tenant token | `GET /api/platform/tenants` | 401/403 |
| INT-031 | P2 | HTTP | Register-tenant validates required fields | Missing name/email/password | Register | 400 validation |
| INT-032 | P2 | HTTP | Weak password policy (if enforced) | Short password | Register | 400 |
| INT-033 | P2 | HTTP | Phone normalization on register | Local 10-digit | Register | Stored E.164 / +91 |
| INT-034 | P3 | HTTP | Concurrent register same email — one wins | Parallel | Register×2 | One success, one conflict |
| INT-035 | P3 | HTTP | Session for suspended tenant still readable on allow-list | Suspended | `GET /api/auth/session` | 200 with suspended status |
| INT-036 | P1 | HTTP | Register-tenant seeds RBAC roles/permissions for new tenant | New tenant | Query roles via admin APIs after approve+onboard | Default roles exist |
| INT-037 | P1 | HTTP | Register-tenant seeds system template placeholders | New tenant | DB or templates API | `IsSystem=true` placeholders for tenant |
| INT-038 | P2 | HTTP | Login with email case-insensitivity | Mixed case email | Login | Success if product normalizes |
| INT-039 | P2 | HTTP | Token lifetime / clock skew tolerance | Near-expiry token | Me | 200 within skew |

---

### C. Platform auth & tenant lifecycle — INT-040…INT-079

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-040 | P0 | HTTP | `GET /api/platform/auth/setup-status` when no owner | Empty PlatformUsers | Setup-status | `needs_bootstrap=true` (or equiv.) |
| INT-041 | P0 | HTTP | Bootstrap first platform owner | Setup needed | `POST /api/platform/auth/bootstrap` | 200; platform JWT with `platform_user` |
| INT-042 | P0 | HTTP | Bootstrap second time rejected | Owner exists | Bootstrap again | 400/409/403 |
| INT-043 | P0 | HTTP | Platform login happy path | Owner | `POST /api/platform/auth/login` | 200; platform token |
| INT-044 | P0 | HTTP | Platform login bad password | Owner | Login | Fail |
| INT-045 | P0 | HTTP | Bootstrap rate limited | Burst | Bootstrap spam | 429 |
| INT-046 | P0 | HTTP | List tenants as platform | Seeded tenants | `GET /api/platform/tenants` | 200; includes pending/active |
| INT-047 | P0 | HTTP | Filter tenants by status=pendingapproval | Mixed | List `?status=pendingapproval` | Only PendingApproval |
| INT-048 | P0 | HTTP | Filter tenants by status=active | Mixed | List | Only Active |
| INT-049 | P0 | HTTP | Filter tenants by status=suspended | Mixed | List | Only Suspended |
| INT-050 | P0 | HTTP | Filter tenants by status=rejected | Mixed | List | Only Rejected |
| INT-051 | P0 | HTTP | Approve pending tenant | Pending tenant | `PATCH .../approve` | Active; `ApprovedAt` set; platform audit |
| INT-052 | P0 | HTTP | Approve non-pending → 404 | Already active | Approve | 404 body |
| INT-053 | P0 | HTTP | Reject pending with reason | Pending | `PATCH .../reject` | Rejected; reason stored; inactive |
| INT-054 | P0 | HTTP | Reject non-pending → 404 | Active | Reject | 404 |
| INT-055 | P0 | HTTP | Suspend active tenant | Active | `PATCH .../suspend` | Suspended; `IsActive=false` |
| INT-056 | P0 | HTTP | Activate suspended tenant | Suspended | `PATCH .../activate` | Active; `IsActive=true` |
| INT-057 | P0 | HTTP | Activate rejected tenant | Rejected | Activate | Active (per current SQL — document behavior) |
| INT-058 | P0 | HYBRID | Platform actions write PlatformAuditLog | Approve/reject/suspend | DB query | Rows for `tenant.approve` etc. |
| INT-059 | P0 | HTTP | Hospital admin blocked while PendingApproval | Pending JWT | `GET /api/patients` | 403 `tenant_pending_approval` |
| INT-060 | P0 | HTTP | Pending tenant can still call `/api/auth/me` and `/session` | Pending JWT | Me/Session | 200 |
| INT-061 | P0 | HTTP | Rejected tenant CRM blocked | Rejected JWT | Patients | 403 `tenant_rejected` + reason |
| INT-062 | P0 | HTTP | Suspended tenant CRM blocked | Suspended JWT | Patients | 403 `tenant_suspended` |
| INT-063 | P0 | HTTP | After approve, CRM still blocked until onboarding complete | Active, onboarding incomplete | Patients | 403 `onboarding_incomplete` |
| INT-064 | P1 | HTTP | After approve+onboarding complete, CRM allowed | Fully ready tenant | Patients | 200 |
| INT-065 | P1 | HTTP | Platform user bypasses tenant lifecycle middleware | Platform JWT | Platform tenants list | 200 even if no tenant context |
| INT-066 | P1 | HTTP | Non-platform cannot approve | Tenant admin JWT | Approve | 401/403 |
| INT-067 | P1 | HTTP | Approve sets `ApprovedByPlatformUserId` | Platform user A | Approve | DB matches A |
| INT-068 | P2 | HTTP | Invalid status filter ignored/empty | `?status=nope` | List | All or empty per ParseLifecycleFilter |
| INT-069 | P2 | HTTP | Suspend then hospital login still issues JWT but APIs 403 | Suspended user | Login + Patients | Login ok; Patients 403 |
| INT-070 | P2 | HTTP | Reject clears approval fields appropriately | Reject | DB | RejectionReason set; ApprovedAt null/unchanged per design |
| INT-071 | P2 | HTTP | List tenants ordered by CreatedAt | Multiple | List | Ascending order |
| INT-072 | P2 | HTTP | Soft-deleted tenants excluded from platform list | Soft-delete | List | Absent |
| INT-073 | P3 | HTTP | Concurrent approve same tenant — single success | Parallel approve | One 200, one 404 |
| INT-074 | P3 | HTTP | Platform bootstrap password hashed (not stored plaintext) | Bootstrap | DB PlatformUsers | Hash ≠ plaintext |
| INT-075 | P1 | HTTP | Full lifecycle: register → pending block → approve → onboard → active CRM | New tenant | Journey | Each gate status code correct |
| INT-076 | P1 | HTTP | Full lifecycle: register → reject → CRM blocked → (optional) activate | New tenant | Journey | Rejected gate works |
| INT-077 | P1 | HTTP | Full lifecycle: active → suspend → CRM blocked → activate → CRM ok | Ready tenant | Journey | Round-trip |
| INT-078 | P2 | HTTP | Setup-status after bootstrap | Owner exists | Setup-status | `needs_bootstrap=false` |
| INT-079 | P2 | HTTP | Platform login rate limited | Burst | 429 |

---

### D. Onboarding gates — INT-080…INT-099

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-080 | P0 | HTTP | `GET /api/onboarding` state for new approved tenant | Active, incomplete | Get state | Steps incomplete |
| INT-081 | P0 | HTTP | Mark profile complete | — | `POST /api/onboarding/profile` | Flag set |
| INT-082 | P0 | HTTP | Mark WhatsApp connected | — | `POST /api/onboarding/whatsapp` | Flag set |
| INT-083 | P0 | HTTP | Mark team invited | — | `POST /api/onboarding/team` | Flag set |
| INT-084 | P0 | HTTP | Complete onboarding | All steps | `POST /api/onboarding/complete` | `OnboardingComplete=true` |
| INT-085 | P0 | HTTP | Incomplete onboarding allows hospital-profile | Incomplete | `GET/PUT /api/hospital-profile` | 200 |
| INT-086 | P0 | HTTP | Incomplete onboarding allows users management | Incomplete | `GET /api/users` | 200 (with User.View) |
| INT-087 | P0 | HTTP | Incomplete onboarding allows integrations/settings prefixes | Incomplete | WhatsApp settings GET | 200 |
| INT-088 | P0 | HTTP | Incomplete onboarding blocks patients/appointments/campaigns | Incomplete | Those GETs | 403 onboarding |
| INT-089 | P1 | HTTP | Complete without prior steps — allowed or rejected per product | Incomplete | Complete | Document actual rule |
| INT-090 | P1 | HTTP | Onboarding endpoints require auth | No token | Onboarding | 401 |
| INT-091 | P1 | HTTP | Onboarding is tenant-scoped (tenant A cannot complete B) | Two tenants | Cross tokens | No cross write |
| INT-092 | P2 | HTTP | Re-complete onboarding is idempotent | Already complete | Complete again | 200; still complete |
| INT-093 | P2 | HTTP | Pending tenant cannot use onboarding APIs (not in AuthPaths) | Pending | Onboarding GET | 403 pending |
| INT-094 | P2 | HTTP | After complete, session shows onboarding_complete | — | Session | Flag true |
| INT-095 | P2 | HTTP | Hospital profile import allowed during onboarding | Incomplete | `POST /api/hospital-profile/import` | 200/4xx from scraper stub — not lifecycle 403 |
| INT-096 | P3 | HTTP | Partial step order independence | Mark team before profile | State | Accepts any order |
| INT-097 | P1 | HYBRID | Completing onboarding flips DB `Tenants.OnboardingComplete` | — | Complete | DB true |
| INT-098 | P2 | HTTP | Auth register user allowed during onboarding | Incomplete + User.Create | `POST /api/auth/register` | 200 |
| INT-099 | P2 | HTTP | Dashboard blocked during onboarding | Incomplete | `GET /api/dashboard/overview` | 403 |

---

### E. Database + RLS isolation — INT-100…INT-149

> Migrates / expands `TenantRlsIsolationTests`, DB parts of `TenantIsolationTests` / `MultiTenantIsolationTests`.

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-100 | P0 | DB | RLS enabled + FORCE on all tenant tables | Migration applied | `pg_policies` / `relrowsecurity` | Policy `tenant_isolation` present |
| INT-101 | P0 | DB | Unscoped session: `SELECT COUNT(*) FROM Patients` → 0 | No `app.current_tenant_id` | Count | 0 *(migrates RawSql_WithoutTenantContext)* |
| INT-102 | P0 | DB | Scoped to Althan: sees exclusive patient, not Surat | Set tenant GUCs | Counts by name | Own >0, other =0 *(migrates RawSql_WithTenantContext)* |
| INT-103 | P0 | DB | Cross-tenant GetById under RLS returns null | Alpha patient id, beta GUC | Select by id | Null/0 rows *(migrates CrossTenant_GetById_UnderRls)* |
| INT-104 | P0 | DB | Platform bypass GUC sees all tenants’ patients | `app.platform_admin=true` | Count | ≥ both exclusive patients |
| INT-105 | P0 | DB | INSERT without tenant GUC rejected / invisible | Unscoped | Insert attempt | Fail or not visible |
| INT-106 | P0 | DB | INSERT with tenant A GUC cannot set TenantId=B | Scoped A | Insert B | WITH CHECK violation |
| INT-107 | P0 | DB | UPDATE other tenant row blocked | Scoped B, row A | Update | 0 rows |
| INT-108 | P0 | DB | DELETE other tenant row blocked | Scoped B | Delete | 0 rows |
| INT-109 | P0 | DB | RLS on Appointments | Seed cross | Select | Isolated |
| INT-110 | P0 | DB | RLS on Conversations + Messages | Seed | Select | Isolated |
| INT-111 | P0 | DB | RLS on Campaigns + CampaignRecipients | Seed | Select | Isolated |
| INT-112 | P0 | DB | RLS on Clinical tables (Vitals, Notes, Allergies, Rx, Labs, Visits, Lifestyle) | Seed | Select | Isolated |
| INT-113 | P0 | DB | RLS on Users / UserRoleAssignments | Seed | Select | Isolated |
| INT-114 | P0 | DB | RLS on AuditLogs | Marker row | Select | Isolated *(supports AuditLogIsolation)* |
| INT-115 | P0 | DB | RLS on WhatsAppSettings / SmsSettings / EmailSettings | Seed | Select | Isolated |
| INT-116 | P0 | DB | RLS on NotificationEvents / Receipts / Preferences | Seed | Select | Isolated |
| INT-117 | P0 | DB | RLS on StaffProfiles / DoctorSchedules | Seed | Select | Isolated |
| INT-118 | P0 | DB | RLS on Templates / TemplatePlaceholders | Seed | Select | Isolated |
| INT-119 | P0 | DB | RLS on PatientDocuments | Seed | Select | Isolated |
| INT-120 | P0 | DB | RLS on Tasks / ReferringDoctors / Referrals | Seed | Select | Isolated |
| INT-121 | P0 | DB | RLS on HospitalProfiles | Seed | Select | Isolated |
| INT-122 | P0 | DB | `CureFlowDbSession` GetById patient cross-tenant null | Session B | GetById A | Null *(migrates MultiTenantIsolation / TenantIsolation)* |
| INT-123 | P0 | DB | `CureFlowDbSession` GetAll patients only own tenant | Session B | GetAll | Only B; no exclusive A name |
| INT-124 | P1 | DB | Soft-deleted rows excluded by app session helpers | Soft-delete | GetById | Null |
| INT-125 | P1 | DB | Tenants table itself not RLS-leaking hospital data | Hospital role | Raw tenants select | Per policy design |
| INT-126 | P1 | DB | Role bypass detection skips tests when superuser | Superuser conn | Probe | Skip or assert bypass documented |
| INT-127 | P1 | DB | Session GUCs reset between pooled connections | Pool | Open×2 | No tenant bleed |
| INT-128 | P1 | DB | Concurrent scoped connections different tenants | Parallel | Select | No cross rows |
| INT-129 | P2 | DB | IntegrationEvents tenant isolation | Seed | Select | Isolated |
| INT-130 | P2 | DB | EmailMessages tenant isolation | Seed | Select | Isolated |
| INT-131 | P2 | DB | MarketingCalendarEvents isolation | Seed | Select | Isolated |
| INT-132 | P2 | DB | PrescriptionItems isolation via parent | Seed | Select | Isolated |
| INT-133 | P2 | DB | InternalNotes isolation | Seed | Select | Isolated |
| INT-134 | P2 | DB | NotificationFeedCursors isolation | Seed | Select | Isolated |
| INT-135 | P1 | DB | Third tenant registration uniqueness (slug/email) | Register via service/HTTP | DB constraints | No collision *(migrates ThirdTenantRegistration_*)* |
| INT-136 | P2 | DB | Unique indexes: patient phone per tenant (if any) | Dup phone same tenant | Insert | Fail; other tenant OK |
| INT-137 | P2 | DB | Cascade/soft-delete consistency under RLS | Delete patient | Children | Not visible / soft-deleted |
| INT-138 | P3 | DB | Large list under RLS performance smoke | Seed N | List | Completes < threshold |
| INT-139 | P1 | DB | Seeder defines three distinct hospitals | — | Assert seeder constants | Unique slugs/emails/patients *(migrates MultiHospitalE2eSeeder_Defines*)* |
| INT-140 | P2 | DB | Platform audit logs readable with bypass | Bypass | Select | Rows present |
| INT-141–149 | — | — | *Reserved for additional RLS tables / policy changes* | | | |

---

### F. Cross-tenant HTTP isolation — INT-200…INT-249

> Prefer HTTP rewrite of `TenantApiIsolationTests` and expand to all resource types.

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-200 | P0 | HTTP | Tenant B cannot GET Tenant A patient by id | A patient id, B token | `GET /api/patients/{id}` | 404 |
| INT-201 | P0 | HTTP | Tenant B list never includes A exclusive patient | Seeds | `GET /api/patients` | No A name/id |
| INT-202 | P0 | HTTP | Tenant B cannot PATCH A patient | — | Patch | 404 |
| INT-203 | P0 | HTTP | Tenant B cannot DELETE A patient | — | Delete | 404 |
| INT-204 | P0 | HTTP | Tenant B cannot GET A appointment | — | Get | 404 *(migrates Appointment_Get_ThrowsNotFound)* |
| INT-205 | P0 | HTTP | Tenant B cannot PATCH A appointment status | — | Status | 404 |
| INT-206 | P0 | HTTP | Tenant B cannot GET A conversation | — | Get | 404 *(migrates Conversation_Get_ThrowsNotFound)* |
| INT-207 | P0 | HTTP | Tenant B cannot post message to A conversation | — | Post message | 404/403 |
| INT-208 | P0 | HTTP | Hospital profile GET differs per tenant | A vs B | Get profile | Names Althan vs Surat *(migrates HospitalProfile_Get_IsScoped)* |
| INT-209 | P0 | HTTP | Tenant B cannot read A audit logs | Marker action on A | B list audits | Marker absent; A present |
| INT-210 | P0 | HTTP | Tenant B cannot GET A campaign | — | Get | 404 |
| INT-211 | P0 | HTTP | Tenant B cannot send A campaign | — | Send | 404 |
| INT-212 | P0 | HTTP | Tenant B cannot GET A clinical vitals for A patient | — | List vitals | 404/empty |
| INT-213 | P0 | HTTP | Tenant B cannot add vitals to A patient id | — | Post vitals | 404/403 |
| INT-214 | P0 | HTTP | Tenant B cannot GET A prescription | — | Get | 404 |
| INT-215 | P0 | HTTP | Tenant B cannot download A patient document | — | Download | 404 |
| INT-216 | P0 | HTTP | Tenant B cannot GET A staff profile | — | Get | 404 |
| INT-217 | P0 | HTTP | Tenant B cannot GET A referring doctor | — | Get | 404 |
| INT-218 | P0 | HTTP | Tenant B cannot list A users | — | Users list | No A emails |
| INT-219 | P0 | HTTP | Tenant B cannot disable A user by id | — | Disable | 404 |
| INT-220 | P0 | HTTP | Tenant B cannot read A notifications | Seed receipt A | B list | Empty of A ids |
| INT-221 | P0 | HTTP | Tenant B cannot read A WhatsApp settings secrets | — | GET settings | Tenant-local only |
| INT-222 | P0 | HTTP | Tenant B cannot use A template id | — | Delete template | 404 |
| INT-223 | P0 | HTTP | Create patient under A never visible to B | Create A | B search | Not found |
| INT-224 | P0 | HTTP | Create appointment under A never gettable by B | Create A | B get | 404 |
| INT-225 | P1 | HTTP | Holistic view for A patient denied to B | — | Holistic | 404 |
| INT-226 | P1 | HTTP | Import CSV under A does not touch B counts | Import A | B patient count | Unchanged |
| INT-227 | P1 | HTTP | Tasks isolation | A task id | B get/list | Absent |
| INT-228 | P1 | HTTP | Dashboard KPIs only count own tenant | Seed both | Overview A vs B | Different / scoped |
| INT-229 | P1 | HTTP | Tags list tenant-scoped | Tags on A only | B tags | No leak |
| INT-230 | P1 | HTTP | Lifestyle GET/PUT cross-tenant denied | A patient | B lifestyle | 404 |
| INT-231 | P1 | HTTP | Lab report download cross-tenant denied | A lab | B download | 404 |
| INT-232 | P1 | HTTP | Email inbox threads cross-tenant denied | — | Threads | Scoped |
| INT-233 | P1 | HTTP | AI summarize on A conversation denied to B | — | Summarize | 404 |
| INT-234 | P2 | HTTP | Referral analytics scoped | Seed revenue both | Analytics | Scoped |
| INT-235 | P2 | HTTP | Missed revenue scoped | — | Missed-revenue | Scoped |
| INT-236 | P2 | HTTP | Roles CRUD does not affect other tenant role rows | Custom role A | B list roles | No A custom |
| INT-237 | P2 | HTTP | Notification preferences role-defaults scoped | Edit A | B defaults | Unchanged |
| INT-238 | P2 | HTTP | Webhook-created conversation lands only on matching phone-number tenant | WA settings A/B | Inbound webhook | Only A sees thread |
| INT-239 | P0 | HTTP | IDOR matrix: for each resource type, swap tenant tokens | Fixture matrix | Get/Patch/Delete | All 404 |
| INT-240 | P1 | HTTP | Soft-deleted A patient not resurrected via B | Soft-delete A | B get | 404 |
| INT-241–249 | — | — | *Reserved* | | | |

---

### G. Patients HTTP CRUD — INT-300…INT-349

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-300 | P0 | HTTP | Create patient happy path | Admin/Reception | `POST /api/patients` | 200 id; GET returns entity |
| INT-301 | P0 | HTTP | Get patient by id | Existing | GET | 200 fields |
| INT-302 | P0 | HTTP | List patients default pagination | Seed > page size | List | Page + totals per contract |
| INT-303 | P0 | HTTP | List filter by `q` search | Distinct names | `?q=` | Matching only |
| INT-304 | P0 | HTTP | List filter by status | Mixed statuses | `?status=` | Filtered |
| INT-305 | P0 | HTTP | List filter by department | — | `?department=` | Filtered |
| INT-306 | P0 | HTTP | List filter by tag | Tagged | `?tag=` | Filtered |
| INT-307 | P0 | HTTP | List filter by inquiry_source | — | `?inquiry_source=` | Filtered |
| INT-308 | P0 | HTTP | Patch patient | — | PATCH | Fields updated |
| INT-309 | P0 | HTTP | Soft-delete patient | — | DELETE | GET 404; list excludes |
| INT-310 | P0 | HTTP | Holistic view aggregates subresources | Seed vitals/rx/allergies | Holistic | Sections populated |
| INT-311 | P1 | HTTP | Create validation fails (missing name/phone) | Bad body | POST | 400 |
| INT-312 | P1 | HTTP | Create without Patient.Create → 403 | Marketing w/o perm | POST | 403 |
| INT-313 | P1 | HTTP | View without Patient.View → 403 | Restricted role | GET/List | 403 |
| INT-314 | P1 | HTTP | Edit without Patient.Edit → 403 | — | PATCH | 403 |
| INT-315 | P1 | HTTP | Delete without Patient.Delete → 403 | — | DELETE | 403 |
| INT-316 | P1 | HTTP | CSV import inserts rows | Valid CSV | `POST import-csv` | `inserted` >0 |
| INT-317 | P1 | HTTP | CSV import skips duplicates/invalid | Mixed CSV | Import | `skipped` counted |
| INT-318 | P1 | HTTP | CSV import empty file | Empty | Import | 400/skipped |
| INT-319 | P1 | HTTP | Patient visits list | Seed visits | `GET .../visits` | 200 |
| INT-320 | P1 | HTTP | Patient timeline | Seed events | `GET .../timeline` | Ordered |
| INT-321 | P1 | HTTP | Create patient visit | — | `POST .../visits` | 200 id |
| INT-322 | P1 | HTTP | Lifestyle GET empty then PUT | New patient | GET/PUT lifestyle | Persists |
| INT-323 | P1 | HTTP | Tags endpoint returns tenant tags | Seed tags | `GET /api/tags` | 200 |
| INT-324 | P2 | HTTP | Pagination page_size bounds | Huge page_size | List | Capped |
| INT-325 | P2 | HTTP | Create sets TenantId from JWT not body | Body tries other tenant | Create | Stored as caller tenant |
| INT-326 | P2 | HTTP | Update unknown id → 404 | Random guid | PATCH | 404 |
| INT-327 | P2 | HTTP | Lead status transitions allowed values | — | PATCH status | Accepts enum; rejects junk |
| INT-328 | P2 | HTTP | Emergency contact / address round-trip | Full DTO | Create+Get | Equality |
| INT-329 | P2 | HTTP | Document upload list download delete | File | Documents APIs | CRUD works; download bytes |
| INT-330 | P2 | HTTP | Document upload rejects oversize/type | Bad file | Upload | 400 |
| INT-331 | P2 | HTTP | Concurrent creates unique ids | Parallel POST | — | Unique guids |
| INT-332 | P1 | HYBRID | Create patient writes audit log | Admin | Create | Audit action present |
| INT-333 | P2 | HTTP | Search is case-insensitive | Mixed case | `q` | Finds |
| INT-334 | P2 | HTTP | Soft-deleted excluded from search | Deleted | `q` | Absent |
| INT-335 | P3 | HTTP | Import large CSV smoke | 500 rows | Import | Completes |
| INT-336–349 | — | — | *Reserved (filters, merge, dedupe)* | | | |

---

### H. Appointments HTTP — INT-350…INT-389

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-350 | P0 | HTTP | Booking options | Staff seeded | `GET .../booking-options` | Doctors/slots structure |
| INT-351 | P0 | HTTP | Create appointment | Patient+doctor | POST | 200 id; status Scheduled |
| INT-352 | P0 | HTTP | Get appointment | — | GET | 200 |
| INT-353 | P0 | HTTP | List with status/from/to/doctor filters | Mixed | List | Filtered |
| INT-354 | P0 | HTTP | Patch appointment fields | — | PATCH | Updated |
| INT-355 | P0 | HTTP | Status → Confirmed | Scheduled | PATCH status | Confirmed |
| INT-356 | P0 | HTTP | Status → Completed | — | Status | Completed |
| INT-357 | P0 | HTTP | Status → Cancelled | — | Status | Cancelled |
| INT-358 | P0 | HTTP | Status → NoShow | — | Status | NoShow |
| INT-359 | P0 | HTTP | Invalid status string → 400 | — | Status | 400 |
| INT-360 | P1 | HTTP | Create validation (missing patient/time) | Bad body | POST | 400 |
| INT-361 | P1 | HTTP | Permission Appointment.Create required | No perm | POST | 403 |
| INT-362 | P1 | HTTP | Permission Appointment.Edit required | No perm | PATCH/status | 403 |
| INT-363 | P1 | HTTP | Permission Appointment.View required | No perm | GET/list | 403 |
| INT-364 | P1 | HYBRID | Create promotes patient lead status (if applicable) | NewInquiry patient | Create appt | Patient status AppointmentScheduled |
| INT-365 | P1 | HYBRID | Create may enqueue reminder / notification | — | Create | Notification/task side effect |
| INT-366 | P2 | HTTP | List pagination | Many appts | page/page_size | Correct page |
| INT-367 | P2 | HTTP | Get unknown id → 404 | — | GET | 404 |
| INT-368 | P2 | HTTP | Cannot create for other-tenant patient id | A patient, B token | POST | 404/400 |
| INT-369 | P2 | HTTP | Doctor filter by `doctor_user_id` | Two doctors | List | Filtered |
| INT-370 | P2 | HTTP | Timezone: ScheduledAt stored/returned consistently | Tenant TZ | Create+Get | Matches TenantTime rules |
| INT-371 | P3 | HOSTED | Scheduler marks NoShow after miss window | Past scheduled | Tick scheduler | Status NoShow + task |
| INT-372 | P3 | HOSTED | 24h reminder flagging | Appt in 24h | Tick | Reminder side effect |
| INT-373–389 | — | — | *Reserved* | | | |

---

### I. Clinical / EHR HTTP — INT-400…INT-459

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-400 | P0 | HTTP | Add vitals | Patient | `POST /api/clinical/vitals` | 200 id; BMI computed if H/W |
| INT-401 | P0 | HTTP | List vitals by patient | Seed | GET | Ordered list |
| INT-402 | P0 | HTTP | Vitals validation (invalid BP etc.) | Bad body | POST | 400 errors dict |
| INT-403 | P0 | HTTP | Add clinical note (SOAP) | — | POST notes | 200 |
| INT-404 | P0 | HTTP | List notes | — | GET | 200 |
| INT-405 | P0 | HTTP | Patch note | — | PATCH | Updated |
| INT-406 | P0 | HTTP | Add medical history | — | POST | 200 |
| INT-407 | P0 | HTTP | List medical history | — | GET | 200 |
| INT-408 | P0 | HTTP | Add family history | — | POST | 200 |
| INT-409 | P0 | HTTP | List family history | — | GET | 200 |
| INT-410 | P0 | HTTP | Create allergy | — | `POST /api/allergies` | 200 |
| INT-411 | P0 | HTTP | List allergies by patient | — | GET | 200 |
| INT-412 | P0 | HTTP | Delete allergy | — | DELETE | Gone |
| INT-413 | P0 | HTTP | Create prescription + items | — | `POST /api/prescriptions` | 200 |
| INT-414 | P0 | HTTP | Get prescription | — | GET | Items included |
| INT-415 | P0 | HTTP | List prescriptions by patient | — | GET patient | 200 |
| INT-416 | P0 | HTTP | Prescription print payload | — | GET print | 200 printable DTO |
| INT-417 | P0 | HTTP | Prescription requires ReasonForPrescribing | Missing reason | POST | 400 |
| INT-418 | P1 | HTTP | Lab report upload | File | `POST /api/lab-reports/upload` | 200 |
| INT-419 | P1 | HTTP | Lab report download | — | GET download | Bytes/content-type |
| INT-420 | P1 | HTTP | Visit get / patch / complete | Seed visit | Visits APIs | Status transitions |
| INT-421 | P1 | HTTP | Clinical.View vs Clinical.Edit enforcement | Nurse/Doctor matrix | GET vs POST | 403 where expected |
| INT-422 | P1 | HTTP | Allergy severity enum boundary | LifeThreatening | Create | Persists |
| INT-423 | P1 | HTTP | Cross-patient note id patch denied | Note of P1 as P2 context | PATCH wrong | 404 |
| INT-424 | P2 | HTTP | Holistic view includes new vitals/notes/allergies/rx | After creates | Holistic | Present |
| INT-425 | P2 | HTTP | Soft-delete patient hides clinical lists | Delete patient | List vitals | 404/empty |
| INT-426 | P2 | HTTP | Injection tracking (if exposed via prescriptions/clinical) | — | Create | Persists batch/expiry |
| INT-427 | P2 | HTTP | Concurrent vitals inserts | Parallel | POST | All persisted |
| INT-428 | P1 | HYBRID | Clinical writes are tenant-stamped | Create | DB TenantId | Matches JWT |
| INT-429 | P2 | HTTP | Unauthorized clinical routes → 401/403 | No/weak token | All clinical | Denied |
| INT-430–459 | — | — | *Reserved (more EHR entities)* | | | |

---

### J. Users / roles / permissions — INT-500…INT-549

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-500 | P0 | HTTP | List users | Admin | `GET /api/users` | 200; tenant-only |
| INT-501 | P0 | HTTP | Get user by id | — | GET | 200 |
| INT-502 | P0 | HTTP | Create user | — | POST | 200; can login |
| INT-503 | P0 | HTTP | Update user (PATCH/PUT) | — | Update | Fields changed |
| INT-504 | P0 | HTTP | Disable user | — | `POST .../disable` | Cannot login |
| INT-505 | P0 | HTTP | Enable user | Disabled | Enable | Can login |
| INT-506 | P0 | HTTP | Reset password | — | Reset | Login with new password |
| INT-507 | P0 | HTTP | Assign roles | — | assign-roles | Role reflected; JWT perms after re-login |
| INT-508 | P0 | HTTP | Assign permissions (overrides) | — | assign-permissions | Effective perms change |
| INT-509 | P0 | HTTP | Delete user (= disable) | — | DELETE | 204; inactive |
| INT-510 | P0 | HTTP | List roles | — | `GET /api/admin/roles` | Seeded RoleNames present |
| INT-511 | P0 | HTTP | Create custom role | — | POST roles | 200 |
| INT-512 | P0 | HTTP | Update role permissions | — | PUT roles/{name} | Updated |
| INT-513 | P0 | HTTP | Delete custom role | — | DELETE | Gone; system roles protected |
| INT-514 | P0 | HTTP | List permissions catalog | — | `GET /api/admin/permissions` | Contains CureFlowPermissions.All |
| INT-515 | P1 | HTTP | User.View/Create/Edit/Delete matrix | Roles without perms | Each verb | 403 |
| INT-516 | P1 | HTTP | Cannot create user with duplicate email in tenant | Existing | Create | 409/400 |
| INT-517 | P1 | HTTP | Same email allowed in different tenant | Tenant A+B | Create both | Both OK |
| INT-518 | P1 | HTTP | AssignRoles validates role name rules | Invalid name | Assign | 400 *(pairs with RoleNameRules unit)* |
| INT-519 | P1 | HTTP | Cannot delete/disable last SuperAdmin/TenantOwner (if guarded) | Sole owner | Disable | 400/403 |
| INT-520 | P1 | HTTP | Receptionist cannot manage roles | Reception token | POST roles | 403 |
| INT-521 | P1 | HTTP | Doctor default permissions allow clinical, deny user admin | Doctor JWT | Users POST / Clinical POST | 403 / 200 |
| INT-522 | P1 | HTTP | Marketing: campaign yes, clinical no | Marketing | Campaign vs Clinical | 200 / 403 |
| INT-523 | P1 | HTTP | Nurse: clinical view/edit per seed | Nurse | Vitals | Per RBAC seeder |
| INT-524 | P1 | HTTP | Staff / Viewer least privilege | Staff/Viewer | Sensitive routes | 403 |
| INT-525 | P2 | HTTP | List users filter `q`, `role`, `is_active` | Mixed | Query | Filtered |
| INT-526 | P2 | HTTP | CreateUserRequest binding (role enum / snake_case) | Varied payloads | POST | Accepts documented shapes |
| INT-527 | P2 | HTTP | Assign permissions empty list clears overrides | Overrides exist | Assign [] | Defaults restore |
| INT-528 | P2 | HTTP | System roles cannot be deleted | Admin/Doctor | DELETE | 400/403 |
| INT-529 | P2 | HTTP | Custom role name uniqueness per tenant | Dup name | Create | Conflict |
| INT-530 | P2 | HYBRID | RBAC seeder idempotent on boot | Seed twice | Roles count | Stable |
| INT-531 | P3 | HTTP | Permission claim forgery ignored (server loads perms) | Tampered JWT perms | Call | Server-side check wins |
| INT-532–549 | — | — | *Reserved* | | | |

---

### K. Notifications — INT-550…INT-579

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-550 | P0 | HTTP | List notifications | Seed receipts | `GET /api/notifications` | 200 |
| INT-551 | P0 | HTTP | Unread count | Mix read/unread | `GET unread-count` | Matches |
| INT-552 | P0 | HTTP | Mark one read | Unread | PATCH read | Count decrements |
| INT-553 | P0 | HTTP | Mark all read | Many unread | mark-all-read | Count 0 |
| INT-554 | P0 | HTTP | Delete notification | — | DELETE | Absent from list |
| INT-555 | P0 | HTTP | `unread_only=true` filter | Mix | List | Only unread |
| INT-556 | P1 | HTTP | Notifications require auth | No token | List | 401 |
| INT-557 | P1 | HTTP | User A cannot mark B’s receipt read | Two users | PATCH B id as A | 404/403 |
| INT-558 | P1 | HTTP | Limit parameter honored | Many | `limit=` | Length ≤ limit |
| INT-559 | P1 | HTTP | Get notification preferences | — | GET preferences | 200 |
| INT-560 | P1 | HTTP | Put notification preferences | — | PUT | Persists |
| INT-561 | P1 | HTTP | Reset preferences | Customized | POST reset | Defaults |
| INT-562 | P1 | HTTP | Role-defaults GET/PUT (Settings.Edit) | Admin | role-defaults | 200; non-admin 403 |
| INT-563 | P1 | HYBRID | Domain event creates notification receipt | Trigger appt/task | DB + list | New item |
| INT-564 | P2 | HTTP | Cross-tenant notification isolation | Seed A | B list | Empty of A |
| INT-565 | P2 | HTTP | Delete unknown id → 404/204 per contract | — | DELETE | Documented |
| INT-566 | P2 | HTTP | Preferences validation | Bad payload | PUT | 400 |
| INT-567 | P3 | HTTP | High-volume list performance smoke | 1k receipts | List | Completes |
| INT-568–579 | — | — | *Reserved* | | | |

---

### L. Campaigns — INT-600…INT-639

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-600 | P0 | HTTP | Create draft campaign | Audience JSON | POST | status draft |
| INT-601 | P0 | HTTP | Create scheduled campaign | ScheduledAt set | POST | status scheduled |
| INT-602 | P0 | HTTP | List campaigns | Seed | GET | 200 |
| INT-603 | P0 | HTTP | Get campaign by id | — | GET | 200 |
| INT-604 | P0 | HTTP | Suggested drafts | — | suggested-drafts | 200 array |
| INT-605 | P0 | HTTP | Preview audience | Filter matching patients | preview-audience | count + sample |
| INT-606 | P0 | HTTP | Patch campaign | Draft | PATCH | Updated |
| INT-607 | P0 | HTTP | Schedule campaign | Draft | POST schedule | scheduled |
| INT-608 | P0 | HTTP | Send campaign (demo/stub WA) | Audience ≥1 | POST send | sent/failed/total |
| INT-609 | P0 | HTTP | Delete campaign | — | DELETE | GET 404 |
| INT-610 | P1 | HTTP | Campaign.View vs Manage permissions | Roles | GET vs POST/send | 403 matrix |
| INT-611 | P1 | HTTP | Preview only counts own-tenant patients | Tags on A/B | Preview as A | B excluded |
| INT-612 | P1 | HTTP | Template variables `{name}` `{department}` substituted on send | Patients | Send | Stored recipient bodies expanded |
| INT-613 | P1 | HTTP | Empty audience send → 0 sent | No matches | Send | total 0 |
| INT-614 | P1 | HYBRID | Recipients rows created with statuses | Send | DB CampaignRecipients | Queued→Sent |
| INT-615 | P2 | HTTP | Cannot schedule past timestamp (if validated) | Past | Schedule | 400 |
| INT-616 | P2 | HTTP | Cannot send deleted campaign | Soft-delete | Send | 404 |
| INT-617 | P2 | HTTP | Idempotent double-send behavior | Already sent | Send again | Documented |
| INT-618 | P2 | HTTP | Audience filters: tags, departments, statuses, inactive_days | Combinations | Preview | Counts match seed |
| INT-619 | P2 | HTTP | Cross-tenant get/send denied | A id, B token | — | 404 |
| INT-620 | P3 | HOSTED | Scheduled campaign auto-send on scheduler | Future→now | Tick | Recipients sent |
| INT-621 | P3 | HTTP | Large audience send smoke | 200 patients | Send | Completes with stub |
| INT-622–639 | — | — | *Reserved* | | | |

---

### M. WhatsApp webhook & messaging — INT-650…INT-699

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-650 | P0 | HTTP | Webhook verify success | Valid mode+token | `GET /api/whatsapp/webhook` | 200 body=challenge |
| INT-651 | P0 | HTTP | Webhook verify failure | Bad token | GET verify | 403 Forbid |
| INT-652 | P0 | HTTP | Inbound webhook accepts Meta payload | Valid JSON + optional sig | `POST /api/whatsapp/webhook` | 200 `{ok:true}` |
| INT-653 | P0 | HYBRID | Inbound text message creates/updates Conversation+Message | Mapped phone_number_id → tenant | POST webhook | DB rows tenant-scoped |
| INT-654 | P0 | HYBRID | Inbound links to existing patient by phone | Patient phone match | Webhook | Conversation.PatientId set |
| INT-655 | P0 | HYBRID | Inbound creates lead patient when unknown | Unknown phone | Webhook | New patient NewInquiry |
| INT-656 | P0 | HTTP | Invalid HMAC signature rejected when secret configured | Bad `X-Hub-Signature-256` | POST | 401/403 or no persist *(product rule)* |
| INT-657 | P0 | HTTP | Valid HMAC accepted | Correct sig | POST | Persists |
| INT-658 | P0 | HTTP | Missing secret config accepts (dev behavior) | No app secret | POST | 200 *(document)* |
| INT-659 | P1 | HTTP | Status webhook (delivered/read) updates message status | Outbound msg | Status payload | DB status |
| INT-660 | P1 | HTTP | Media inbound message stored with media metadata | Image payload | Webhook | Message type media |
| INT-661 | P1 | HTTP | Webhook is AllowAnonymous (no JWT) | No token | POST | Not 401 |
| INT-662 | P1 | HTTP | Webhook does not require completed onboarding | Incomplete tenant WA | POST | Still processes |
| INT-663 | P1 | HTTP | Phone number id routes to correct tenant among many | A/B WA settings | Payload metadata | Only matching tenant conversation |
| INT-664 | P1 | HTTP | Unknown phone_number_id — safe no-op / log | Unmapped | POST | 200; no cross-tenant write |
| INT-665 | P1 | HTTP | Send message API (stub) | Conversation | `POST sendmessage` | 200; outbound Message row |
| INT-666 | P1 | HTTP | Send template message | — | sendtemplatemessage | 200 |
| INT-667 | P1 | HTTP | Send media | File | sendmedia | 200 |
| INT-668 | P1 | HTTP | Send campaigns endpoint | — | sendcampaigns | 200/perm |
| INT-669 | P1 | HTTP | Make contact | — | makecontact | 200 |
| INT-670 | P1 | HTTP | WhatsApp permission matrix (View/Send/Manage) | Roles | Messaging endpoints | 403/200 |
| INT-671 | P1 | HTTP | Settings GET/POST WhatsApp | Admin | `/api/settings/whatsapp` | Persist; secrets masked on GET |
| INT-672 | P1 | HTTP | Settings status endpoint | — | status | configured flags |
| INT-673 | P1 | HTTP | WhatsApp data: templates/groups/campaigns/contacts/health | Stub Meta | GET data routes | 200 |
| INT-674 | P2 | HTTP | Demo inbound `/api/whatsapp/demo/inbound` | Dev | POST | Creates conversation |
| INT-675 | P2 | HTTP | Demo register-phone | — | register-phone | Maps test phone |
| INT-676 | P2 | HTTP | Media GET by fileName authorization | Own media | GET media | 200; other tenant 404 |
| INT-677 | P2 | HYBRID | Duplicate webhook delivery idempotent | Same wamid twice | POST×2 | Single message or upsert |
| INT-678 | P2 | HTTP | Malformed JSON webhook | Truncated body | POST | 200/400 without 500 |
| INT-679 | P2 | HTTP | Extremely large body rejected | Huge payload | POST | 413/400 |
| INT-680 | P3 | HTTP | Burst webhooks concurrency | Parallel | POST×N | No crash; consistent DB |
| INT-681 | P1 | HTTP | Conversations list shows inbound thread | After webhook | `GET /api/conversations` | Thread visible |
| INT-682 | P1 | HTTP | Assign conversation + internal notes | — | assign/notes | Persist |
| INT-683–699 | — | — | *Reserved (interactive buttons, flows)* | | | |

---

### N. Conversations / inbox — INT-700…INT-729

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-700 | P0 | HTTP | List conversations | Seed | GET | 200 |
| INT-701 | P0 | HTTP | Get conversation | — | GET id | Messages included/contract |
| INT-702 | P0 | HTTP | Get by patient | — | GET patient/{id} | 200 |
| INT-703 | P0 | HTTP | Post outbound message | Manage perm | POST messages | 200; stored |
| INT-704 | P0 | HTTP | Upload media to conversation | — | POST media | 200 |
| INT-705 | P0 | HTTP | Download message media | — | GET media | Bytes |
| INT-706 | P0 | HTTP | Assign conversation | — | assign | Assignee set |
| INT-707 | P0 | HTTP | Add internal note | — | notes | Note stored |
| INT-708 | P1 | HTTP | Conversation.View vs Manage matrix | Roles | GET vs POST | 403 matrix |
| INT-709 | P1 | HTTP | Email inbox list threads | — | `GET /api/email/threads` | 200 |
| INT-710 | P1 | HTTP | Email threads by patient | — | threads/{patientId} | 200 |
| INT-711 | P1 | HTTP | Email send (stub SMTP) | — | POST send | 200; EmailMessages row |
| INT-712 | P2 | HTTP | Unanswered escalation creates task (hosted/trigger) | Old inbound | Tick/API | Task LeadEscalation |
| INT-713 | P2 | HTTP | AI draft-reply | Stub AI | `POST /api/ai/draft-reply` | 200 text |
| INT-714 | P2 | HTTP | AI summarize conversation | — | summarize/{id} | 200 |
| INT-715 | P2 | HTTP | Cross-tenant conversation media denied | A media, B | Download | 404 |
| INT-716–729 | — | — | *Reserved* | | | |

---

### O. Staff / schedules — INT-730…INT-749

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-730 | P0 | HTTP | List staff / doctors / nurses | Seed | GET endpoints | 200 |
| INT-731 | P0 | HTTP | Get staff by id | — | GET | 200 |
| INT-732 | P0 | HTTP | Create / patch / delete staff | Perms | CRUD | Works; soft-delete |
| INT-733 | P0 | HTTP | Booking options from staff | Schedules | booking-options | Slots |
| INT-734 | P1 | HTTP | Schedule CRUD for staffProfileId | — | schedules APIs | Persist |
| INT-735 | P1 | HTTP | Staff permission matrix | Roles | CRUD | 403/200 |
| INT-736 | P1 | HTTP | Cross-tenant staff id denied | — | GET | 404 |
| INT-737 | P2 | HTTP | Delete staff with future appointments — rule | Linked appts | Delete | 400 or cascade policy |
| INT-738–749 | — | — | *Reserved* | | | |

---

### P. Referrals / referring doctors — INT-750…INT-769

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-750 | P0 | HTTP | Create referring doctor | — | `POST /api/doctors` | 200 |
| INT-751 | P0 | HTTP | List / get doctors | — | GET | 200 |
| INT-752 | P0 | HTTP | Mark contacted | — | mark-contacted | Timestamp |
| INT-753 | P0 | HTTP | Create referral | — | `POST /api/referrals` | 200 |
| INT-754 | P0 | HTTP | Referral analytics | Seed revenue | analytics | Leaderboard scoped |
| INT-755 | P1 | HTTP | Referral.View / Manage matrix | Roles | Endpoints | 403/200 |
| INT-756 | P1 | HTTP | Cross-tenant doctor id denied | — | GET | 404 |
| INT-757–769 | — | — | *Reserved* | | | |

---

### Q. Tasks / dashboard / audit — INT-770…INT-799

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-770 | P0 | HTTP | Create / list / patch / delete task | — | `/api/tasks` | CRUD |
| INT-771 | P0 | HTTP | Dashboard overview KPIs | Seed today data | overview | Numbers ≥0; scoped |
| INT-772 | P0 | HTTP | Clinical overview | — | clinical-overview | 200 |
| INT-773 | P0 | HTTP | Missed revenue | — | missed-revenue | Categories present |
| INT-774 | P0 | HTTP | Audit logs list | Prior mutations | `GET /api/audit-logs` | Contains actions; tenant-scoped |
| INT-775 | P1 | HTTP | Audit.View permission | Without | List | 403 |
| INT-776 | P1 | HTTP | Dashboard.View permission | Without | Overview | 403 |
| INT-777 | P1 | HYBRID | Mutating APIs append audit entries | Patient create/update | Audit list | Action + entity type |
| INT-778 | P2 | HTTP | Task filters / types | Mixed TaskType | List | Filtered if supported |
| INT-779 | P3 | HOSTED | Scheduler creates follow-up tasks | Triggers | Tick | Tasks appear |
| INT-780–799 | — | — | *Reserved* | | | |

---

### R. Settings / integrations / templates — INT-800…INT-849

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-800 | P0 | HTTP | Hospital profile GET/PUT | — | `/api/hospital-profile` | Round-trip |
| INT-801 | P0 | HTTP | Departments list | Profile seeded | departments | 200 |
| INT-802 | P1 | HTTP | Hospital profile import (scraper+AI stub) | URL | import | Structured JSON saved |
| INT-803 | P1 | HTTP | Email settings GET/POST/status | Admin | `/api/settings/email` | Persist; secrets masked |
| INT-804 | P1 | HTTP | SMS settings GET/POST/status | Admin | `/api/settings/sms` | Persist |
| INT-805 | P1 | HTTP | WhatsApp settings (see also M) | Admin | settings/whatsapp | Persist |
| INT-806 | P1 | HTTP | Templates list / create / delete | — | `/api/templates` | CRUD |
| INT-807 | P1 | HTTP | Template placeholders list / create / delete | — | placeholders | CRUD; system protected |
| INT-808 | P1 | HTTP | Settings.View / Settings.Edit matrix | Roles | GET vs POST | 403 |
| INT-809 | P2 | HTTP | Cannot read other tenant settings via id guessing | — | GET | Tenant-local singleton |
| INT-810 | P2 | HTTP | Invalid integration credentials validation | Bad payload | POST | 400 |
| INT-811–849 | — | — | *Reserved (more integration types)* | | | |

---

### S. Public / tenant resolution — INT-850…INT-869

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-850 | P0 | HTTP | Public tenant by slug | Active slug | `GET /api/public/tenant/{slug}` | 200 name/slug; no secrets |
| INT-851 | P0 | HTTP | Public tenant unknown slug | — | GET | 404 |
| INT-852 | P1 | HTTP | Tenant-context endpoint | Host/header/slug rules | `GET /api/public/tenant-context` | Resolves expected tenant |
| INT-853 | P1 | HTTP | Suspended/rejected slug public behavior | Non-active | Public GET | 404 or limited DTO |
| INT-854 | P1 | HTTP | Reserved slugs not resolvable as hospitals | `admin` | Public | 404 |
| INT-855 | P2 | HTTP | Tenant resolution middleware sets context for subdomain/header | Configured host | Authenticated call | Correct TenantId |
| INT-856 | P2 | HTTP | Mismatched Host slug vs JWT tenant_id | Conflict | CRM call | 403/400 safe failure |
| INT-857–869 | — | — | *Reserved* | | | |

---

### T. Negative / security / abuse — INT-900…INT-949

| ID | P | Type | Scenario | Arrange | Act | Assert |
|----|---|------|----------|---------|-----|--------|
| INT-900 | P0 | HTTP | SQL injection in search `q` | Classic payloads | Patients list | 200 empty/safe; no 500 |
| INT-901 | P0 | HTTP | Path traversal on media/file endpoints | `../` names | Media/download | 400/404 |
| INT-902 | P0 | HTTP | XSS payloads stored then returned encoded/safe | Script in name | Create+Get | Stored as data; API JSON safe |
| INT-903 | P0 | HTTP | Mass assignment TenantId / IsDeleted ignored | Body fields | Create patient/user | Server values win |
| INT-904 | P0 | HTTP | Privilege escalation via role claim in JWT | Forged role | Admin API | 403 |
| INT-905 | P0 | HTTP | Privilege escalation via permission claim in JWT | Forged permission | Protected | 403 |
| INT-906 | P0 | HTTP | Platform bootstrap after setup closed | Owner exists | Bootstrap | Rejected |
| INT-907 | P0 | HTTP | SSRF on hospital import URL | Internal IPs | import | Blocked (`SafeRemoteUrl`) |
| INT-908 | P1 | HTTP | Rate limit login does not lock other IPs (if IP-partitioned) | Two clients | Burst one | Other still works |
| INT-909 | P1 | HTTP | Oversized JSON body | Huge POST | Any | 413/400 |
| INT-910 | P1 | HTTP | Method not allowed | GET on POST-only | — | 405 |
| INT-911 | P1 | HTTP | CSRF not required for bearer API (sanity) | — | POST with bearer | 200 without cookie CSRF |
| INT-912 | P1 | HTTP | Security headers present (if middleware) | — | GET health | Expected headers |
| INT-913 | P1 | HTTP | Verbose errors do not leak stack in Production env | Prod factory | Force 500 | Generic body |
| INT-914 | P2 | HTTP | Enum/string bomb on status fields | Huge string | PATCH | 400 |
| INT-915 | P2 | HTTP | Unicode / RTL / emoji in patient names | — | Create | Round-trip |
| INT-916 | P2 | HTTP | Null vs omitted optional fields | Variants | PATCH | Correct merge semantics |
| INT-917 | P3 | HTTP | Slowloris-style timeout smoke | Partial body | — | Connection closed safely |
| INT-918–949 | — | — | *Reserved* | | | |

---

### U. End-to-end SaaS journeys (multi-step) — INT-950…INT-979

| ID | P | Type | Scenario | Steps (HTTP) | Assert |
|----|---|------|----------|--------------|--------|
| INT-950 | P0 | HTTP | **New hospital happy path** | Platform bootstrap (if needed) → register-tenant → platform approve → onboarding steps → complete → create patient → create appointment → list dashboard | All 2xx; data visible |
| INT-951 | P0 | HTTP | **Rejected hospital path** | Register → reject → login → patients | 403 rejected |
| INT-952 | P0 | HTTP | **Suspend mid-operation** | Active CRM → platform suspend → patients | 403 suspended; session still 200 |
| INT-953 | P0 | HTTP | **Two-tenant isolation journey** | Provision A+B → exclusive patients → swap tokens on IDs | All IDOR 404 |
| INT-954 | P0 | HTTP | **WhatsApp lead → CRM** | Webhook inbound unknown phone → patient created → conversation → staff reply sendmessage → list inbox | Linked entities |
| INT-955 | P1 | HTTP | **Clinical encounter** | Create patient → vitals → note → allergy → prescription → holistic view | Aggregate complete |
| INT-956 | P1 | HTTP | **Campaign blast** | Tag patients → preview → create → send (stub) → recipients | Counts match |
| INT-957 | P1 | HTTP | **RBAC least privilege** | Create Marketing user → attempt clinical + users admin + campaign | Only campaign allowed |
| INT-958 | P1 | HTTP | **User lifecycle** | Admin creates doctor → assign role → reset password → disable → enable | Login states match |
| INT-959 | P1 | HTTP | **Referral revenue** | Create doctor → referral → analytics | Metrics update |
| INT-960 | P2 | HTTP | **Settings + messaging readiness** | Save WA/email/sms settings → status endpoints green → send demo message | Ready flags |
| INT-961 | P2 | HTTP | **Documents + labs** | Upload patient doc + lab → download both → delete doc | Storage isolation |
| INT-962 | P2 | HTTP | **Task follow-up loop** | Create task → patch done → dashboard follow-ups due | KPI moves |
| INT-963 | P2 | HTTP | **Audit trail journey** | Series of mutations → audit list | Chronological actions |
| INT-964 | P3 | HTTP | **Re-onboarding / profile update after complete** | Complete → update hospital profile → CRM still open | No regression to onboarding gate |
| INT-965 | P3 | HTTP | **Platform multi-tenant ops day** | List pending → approve batch → suspend one → filters | List integrity |
| INT-966–979 | — | — | *Reserved journeys* | | |

---

## 6. Permission × endpoint matrix (integration Theories)

Implement as `[Theory]` data driving INT-980+ (reserved). For each permission in `CureFlowPermissions.All`, cover:

| Check | Expectation |
|-------|-------------|
| Caller **with** permission | Not 403 for that endpoint’s primary verb |
| Caller **without** permission (authenticated) | 403 |
| Anonymous | 401 |

**High-value endpoint samples for the matrix**

- Patients: GET list, POST, PATCH, DELETE  
- Appointments: GET, POST, PATCH status  
- Clinical: POST vitals, GET vitals  
- Users: GET, POST, assign-roles  
- Roles: GET, POST  
- Campaigns: GET, POST, send  
- Conversations: GET, POST messages  
- WhatsApp: sendmessage, settings POST  
- Dashboard: overview  
- Audit: list  
- Settings: email/whatsapp POST  
- Referrals/doctors: GET, POST  
- Staff: GET, POST  

Reserve **INT-980 – INT-999** for generated permission Theories.

---

## 7. Mapping: catalog ← existing tests (detail)

| Existing test method | Suggested INT | Action |
|----------------------|---------------|--------|
| `TenantRlsIsolationTests.RawSql_WithoutTenantContext_ReturnsZeroRowsUnderRls` | INT-101 | Move |
| `TenantRlsIsolationTests.RawSql_WithTenantContext_ReturnsOnlyTenantRows` | INT-102 | Move |
| `TenantRlsIsolationTests.CrossTenant_GetById_ReturnsNull_UnderRls` | INT-103 | Move |
| `TenantIsolationTests.CrossTenant_GetById_ReturnsNull_WhenPatientBelongsToOtherTenant` | INT-122 | Move / merge |
| `TenantIsolationTests.WhereActive_*` | — | **Keep unit** |
| `MultiTenantIsolationTests.MultiHospitalE2eSeeder_DefinesThreeDistinctHospitals` | INT-139 | Move |
| `MultiTenantIsolationTests.TenantA_CannotGetTenantBPatient_ById` | INT-122 / INT-200 | Move DB + add HTTP |
| `MultiTenantIsolationTests.TenantA_CannotListTenantBPatients` | INT-123 / INT-201 | Move DB + add HTTP |
| `MultiTenantIsolationTests.ThirdTenantRegistration_DoesNotCollide_OnSlugOrEmail` | INT-135 / INT-018 | Move / HTTP |
| `TenantApiIsolationTests.HospitalProfile_Get_IsScopedToCurrentTenant` | INT-208 | Rewrite HTTP |
| `TenantApiIsolationTests.Appointment_Get_ThrowsNotFound_ForOtherTenantAppointment` | INT-204 | Rewrite HTTP |
| `TenantApiIsolationTests.Conversation_Get_ThrowsNotFound_ForOtherTenantConversation` | INT-206 | Rewrite HTTP |
| `AuditLogIsolationTests.TenantA_CannotRead_TenantB_AuditLogs` | INT-209 / INT-114 | Rewrite HTTP + DB |
| `MultiTenantAuthTests.RegisterAndLogin_ReturnsMatchingTenantId_InJwtAndResponse` | INT-010 / INT-011 | Rewrite HTTP |
| `MultiTenantAuthTests.RegisterThreeHospitals_ProducesDistinctSlugsAndTenantIds` | INT-018 | Rewrite HTTP |
| `MultiTenantAuthTests.RegisterTenant_RejectsReservedSlug` | INT-017 | Rewrite HTTP |
| `TenantSlugHelperTests.*` | — | **Keep unit** |

---

## 8. Implementation phases (suggested, not started)

| Phase | Deliver | INT bands |
|------|---------|-----------|
| **0** | Factory + Postgres fixture + health | INT-001…009 |
| **1** | Auth + platform lifecycle + onboarding | INT-010…099 |
| **2** | RLS + cross-tenant HTTP | INT-100…249 |
| **3** | Patients + appointments + clinical | INT-300…459 |
| **4** | Users/roles + notifications + campaigns | INT-500…639 |
| **5** | WhatsApp webhook + conversations | INT-650…729 |
| **6** | Remaining modules + security + journeys | INT-730…979 |
| **7** | Permission Theories | INT-980…999 |

---

## 9. Traceability to API controllers

| Controller | Primary INT bands |
|------------|-------------------|
| `HealthController` | A |
| `AuthController` | B, D, J |
| `PlatformAuthController` / `PlatformTenantsController` | C, U |
| `OnboardingController` | D |
| `TenantPublicController` | S |
| `PatientsController` / `PatientVisitsController` / `LifestyleController` / `PatientDocumentsController` / `TagsController` | G, F, I |
| `AppointmentsController` | H, F |
| `ClinicalController` / `AllergiesController` / `PrescriptionsController` / `LabReportsController` / `VisitsController` | I, F |
| `UsersController` / `RolesController` / `PermissionsController` | J |
| `NotificationsController` / `NotificationPreferencesController` | K |
| `CampaignsController` | L, F |
| `WhatsappWebhookController` / `WhatsappMessagingController` / `WhatsAppSettingsController` / `WhatsappDataController` / `WhatsappDemoController` | M, N |
| `ConversationsController` / `EmailInboxController` / `AiController` | N, F |
| `StaffController` | O |
| `ReferringDoctorsController` / `ReferralsController` | P |
| `TasksController` / `DashboardController` / `AuditLogsController` | Q |
| `HospitalProfileController` / `EmailSettingsController` / `SmsSettingsController` / `TemplatesController` | R, D |
| `DevController` | A (gated) |

---

## 10. Notes

1. **Catalog only** — do not treat checkboxes as implemented.  
2. Prefer **one assert focus per test**; journeys (U) are explicit multi-step exceptions.  
3. When product behavior is ambiguous (e.g. webhook invalid signature status code, activate-from-rejected), lock the assert to **current code** and file a product bug if wrong.  
4. Frontend Playwright matrices (`dotnet-frontend-test/e2e`) complement this catalog; they do **not** replace backend integration coverage.  
5. Update this file when new controllers/permissions/RLS tables ship — append IDs in the reserved gaps or extend the next hundred-block.

---

*Generated for Cure-Flow backend integration planning. IDs: `INT-001` … `INT-999`.*
