# Cure-Flow Enterprise Refactoring Report

**Date:** 2026-05-28  
**Scope:** Phase A (analysis) + Phase B Wave 1 (interfaces, DTOs, enums, domain entity splits)  
**Routes:** Unchanged (backward compatible)

---

## Executive Summary

Wave 1 refactored the Application and Domain layers to enforce **one type per file** with feature-folder organization. Namespaces remain `CureFlow.Application.DTOs`, `CureFlow.Application.Interfaces`, and `CureFlow.Domain.Enums` so existing usings compile without churn. The .NET solution builds with **0 errors**; the React frontend builds successfully (no import changes required).

| Metric | Count |
|--------|------:|
| New/relocated Application files (DTOs + Interfaces + Common splits) | ~110 |
| Enum files (was 1 bundled) | 22 |
| Domain entity files split (WhatsApp + Referral/Campaign) | 9 |
| Bundled files removed | 10 |
| API controllers | 27 |
| Documented API endpoints | ~95 |

---

## Task 1 — API Analysis

### Global auth model

- **Default:** JWT Bearer + `FallbackPolicy` requires authenticated user (`ServiceCollectionExtensions.AddCureFlowAuthentication`).
- **Anonymous:** `login`, `register-tenant`, `health`, WhatsApp `webhook` GET/POST.
- **Fine-grained:** `Permission:*` policies per endpoint (RBAC).

### Controller / endpoint inventory

| Route prefix | Controller | Method | Path | Auth |
|--------------|------------|--------|------|------|
| `api/auth` | AuthController | POST | `/login` | Anonymous |
| | | POST | `/register-tenant` | Anonymous |
| | | GET | `/me` | JWT (fallback) |
| | | POST | `/register` | Permission:User.Create |
| `api/patients` | PatientsController | GET | `/` | Permission:Patient.View |
| | | GET | `/{id}` | Permission:Patient.View |
| | | GET | `/{id}/holistic-view` | Permission:Patient.View |
| | | POST | `/` | Permission:Patient.Create |
| | | PATCH | `/{id}` | Permission:Patient.Edit |
| | | DELETE | `/{id}` | Permission:Patient.Delete |
| | | POST | `/import-csv` | Permission:Patient.Create |
| `api/patients/{patientId}/lifestyle` | PatientLifestyleController | GET | `/` | Permission:Patient.View |
| | | PUT | `/` | Permission:Patient.Edit |
| `api/appointments` | AppointmentsController | POST/GET/PATCH | CRUD + `/{id}/status` | Appointment.* permissions |
| `api/clinical` | ClinicalController | POST/GET/PATCH | vitals, notes, medical-history, family-history | Clinical.* |
| `api/allergies` | AllergiesController | GET/POST/DELETE | patient list, create, delete | Clinical.* |
| `api/prescriptions` | PrescriptionsController | POST/GET | create, by id, by patient, print | Clinical.* |
| `api/patients/{patientId}` | VisitsController | GET/POST | visits, timeline | Clinical.* |
| `api/visits` | VisitsController | GET/PATCH/POST | detail, update, complete | Clinical.* |
| `api/lab-reports` | VisitsController | POST/GET | upload, download | Clinical.* |
| `api/staff` | StaffController | GET/POST/PATCH/DELETE | staff + schedules | Staff.* |
| `api/conversations` | ConversationsController | GET/POST | list, patient, messages, assign, notes | Conversation.* |
| `api/tasks` | TasksController | POST/GET/PATCH/DELETE | task board | Clinical.* |
| `api/campaigns` | CampaignsController | POST/GET/DELETE | CRUD, preview, send | Campaign.* |
| `api/referrals` | ReferralsController | POST | create referral | Referral.Manage |
| | | GET | `/analytics` | Referral.View |
| `api/doctors` | ReferringDoctorsController | POST/GET | CRUD referring doctors | Referral.* |
| | | POST | `/{id}/mark-contacted` | Referral.Manage |
| `api/dashboard` | DashboardController | GET | overview, missed-revenue | Dashboard.View |
| `api/hospital-profile` | HospitalProfileController | GET/PUT/POST | profile + import | Settings.* |
| `api/templates` | TemplatesController | GET/POST/DELETE | quick templates | Settings.* |
| `api/settings/whatsapp` | WhatsAppSettingsController | GET/POST | tenant WA config | WhatsApp.Manage |
| `api/whatsapp` | WhatsappDataController | GET | templates, groups, campaigns, contacts, health | WhatsApp.View |
| | WhatsappMessagingController | POST | sendmessage, sendtemplatemessage, sendmedia, sendcampaigns, makecontact | WhatsApp.Send/Manage |
| | | GET | media/{fileName} | WhatsApp.View |
| | WhatsappWebhookController | GET/POST | webhook | Anonymous |
| `api/tags` | TagsController | GET | `/` | Permission:Patient.View |
| `api/ai` | AiController | POST | draft-reply, summarize/{id} | Conversation.View |
| `api/users` | UsersController | GET/POST/PATCH/PUT/DELETE | user admin | User.* |
| `api/admin/roles` | RolesController | GET | `/` | User.View |
| `api/admin/permissions` | PermissionsController | GET | `/` | User.View |
| `api/audit-logs` | AuditLogsController | GET | `/` | Audit.View |
| `api/client-logs` | ClientLogsController | POST | `/` | JWT |
| `api/health` | HealthController | GET | `/` | Anonymous |

**Removed / legacy (git):** `EhrController` (replaced by `ClinicalController` + granular controllers), standalone `WhatsappController` (split into Data/Messaging/Webhook).

### Duplicate / overlapping API report

| Issue | Endpoints | Recommendation |
|-------|-----------|----------------|
| **Dual template APIs** | `GET /api/templates` (tenant quick templates) vs `GET /api/whatsapp/templates` (WhatsBiz cache) | Rename to `/api/quick-templates` vs `/api/whatsapp/provider-templates` (Wave 2+) |
| **Dual campaign concepts** | `GET /api/whatsapp/campaigns` (provider) vs `/api/campaigns` (tenant CRM) | Document clearly; consider namespace prefix `/api/crm/campaigns` |
| **Doctor naming** | `/api/doctors` = referring doctors; `/api/staff/doctors` = internal staff | Rename referring to `/api/referring-doctors` (breaking — coordinate with FE) |
| **Users vs auth register** | `POST /api/auth/register` vs `POST /api/users` | Consolidate on `/api/users` with shared service |
| **PATCH + PUT on users** | Both map to same handler | Drop redundant `PUT` alias |

### Unused API report (not called from `dotnet-frontend`)

Grep of `api.get/post/put/patch/delete` and `apiGet` paths:

| Endpoint | Notes |
|----------|-------|
| `GET /api/patients/{id}` | FE uses `holistic-view` only |
| `GET /api/appointments/{id}` | List + patch only in UI |
| `GET /api/prescriptions/{id}` | Patient list only |
| `GET /api/prescriptions/{id}/print` | Not wired in FE yet |
| `POST /api/conversations/{id}/assign` | Not in FE |
| `POST /api/conversations/{id}/notes` | Not in FE |
| `POST /api/auth/register` | Admin uses `/api/users` |
| `POST /api/auth/register-tenant` | Onboarding only (no FE page) |
| `GET /api/whatsapp/groups` | Unused |
| `GET /api/whatsapp/campaigns` | Unused (CRM campaigns used instead) |
| `GET /api/whatsapp/contacts` | Unused |
| `POST /api/whatsapp/sendcodecampaigns` | Unused |
| `POST /api/whatsapp/makecontact` | Unused |
| `POST /api/ai/*` | No direct FE calls (may use later) |
| `POST /api/lab-reports/upload` | Not in FE grep |
| `PATCH /api/visits/{id}` | Partial visit UX |
| `POST /api/visits/{id}/complete` | Not in FE grep |
| `DELETE /api/patients/{id}` | Not in FE grep |
| `GET /api/users/{id}` | List-only in settings |

### Fat controllers (DbContext / logic in API layer)

| Controller | Issue |
|------------|-------|
| `TemplatesController` | Injects `ApplicationDbContext` directly |
| `WhatsAppSettingsController` | Injects `ApplicationDbContext` directly |
| `ReferringDoctorsController` | Uses `IReferralService` + `ApplicationDbContext` (mixed) |

**Recommendation:** Extract `IQuickTemplateService`, `IWhatsAppSettingsService`; remove DbContext from API project entirely (Wave 2).

### Non-RESTful / naming issues

- Action-style routes: `mark-contacted`, `import-csv`, `preview-audience`, `sendmessage` (legacy WhatsBiz parity).
- Nested resource inconsistency: `GET /clinical/vitals/patient/{id}` vs REST `GET /patients/{id}/vitals`.
- `api/doctors` semantic overload (referring vs staff).
- Snake_case JSON + kebab routes — acceptable but document in OpenAPI.

### Endpoint consolidation plan (Wave 2+, routes frozen in Wave 1)

1. **Clinical resource tree:** `/api/patients/{id}/vitals|notes|allergies|prescriptions|visits`
2. **WhatsApp module:** `/api/integrations/whatsapp/{messaging|data|webhook}`
3. **Admin module:** `/api/admin/users|roles|permissions` (move users under admin)
4. **Deprecate** duplicate PUT/PATCH and unused provider campaign endpoints with sunset headers.

---

## Tasks 2–4 — One-class-per-file inventory

### Before Wave 1 (bundled files)

| Project | Bundled file | Types | Violation |
|---------|--------------|------:|-----------|
| Application | `IBusinessServices.cs` | 14 interfaces | Yes |
| Application | `IServices.cs` | 9 interfaces | Yes |
| Application | `CoreDtos.cs` | 12 records | Yes |
| Application | `EhrDtos.cs` | 18 records | Yes |
| Application | `WhatsAppDtos.cs` | 17 classes | Yes |
| Application | `StaffDtos.cs` | 6 records | Yes |
| Application | `VisitDtos.cs` | 6 records | Yes |
| Application | `UserManagementDtos.cs` | 8 records | Yes |
| Application | `ApiResponse.cs` | 6 classes | Yes |
| Domain | `Enums.cs` | 22 enums | Yes |
| Domain | `WhatsAppEntities.cs` | 5 entities | Yes |
| Domain | `Referrals.cs` | 4 entities | Yes |

### After Wave 1

| Project | Violation files remaining | Notes |
|---------|--------------------------:|-------|
| **Application** | 0 (target files) | All listed bundles split |
| **Domain** | 0 (target files) | Enums + WhatsApp + Referral/Campaign split |
| **Infrastructure** | 5 | Deferred to Wave 2 (see below) |

### Infrastructure violations (Wave 2 scope)

| File | Types | Count |
|------|-------|------:|
| `Services/CrmServices.cs` | ConversationService, AppointmentService, … | 3 |
| `Services/EhrServices.cs` | LifestyleService, AllergyService, … | 4 |
| `Services/BusinessServices.cs` | ReferralService, … | 2 |
| `Services/AuthService.cs` | AuthService + helper | 2 |
| `Services/QueryServices.cs` | TagService, … | 2 |
| `Identity/UserPermissionService.cs` | 2 types | 2 |

**Infrastructure violation count:** ~5 files, ~13 extra types (not split in Wave 1 per scope).

---

## Phase B Wave 1 — Changes implemented

### 1. Interfaces

- **Removed:** `IBusinessServices.cs`, `IServices.cs`
- **Added:** `Application/Interfaces/BusinessServices/I*.cs` (14 files)
- **Added:** `Application/Interfaces/Services/I*.cs` (9 files)
- **Unchanged:** `IUserManagementService`, `IWhatsappProvider`, `IWhatsappApiService`, `IWhatsappWebhookRelayService`
- **DI:** `ServiceCollectionExtensions` unchanged — registers concrete types by interface name (same namespaces).

### 2. DTOs

Feature folders under `Application/DTOs/`:

- `Auth/` (6), `Patient/` (5), `Appointment/` (3)
- `Ehr/` (18), `WhatsApp/` (17)
- `Staff/` (6), `Visit/` (6), `UserManagement/` (8)

Namespace: `CureFlow.Application.DTOs` (flat, no breaking usings).

### 3. Enums

- **Removed:** `Domain/Enums/Enums.cs`
- **Added:** 22 files in `Domain/Enums/*.cs`

### 4. Domain entities

- **Removed:** `WhatsAppEntities.cs`, `Referrals.cs`
- **Added:** `Entities/WhatsApp/*.cs` (5), `Entities/Referral/*.cs` (2), `Entities/Campaign/*.cs` (2)

### 5. Common types

- **Split:** `PagedResult`, `DomainException`, `NotFoundException`, `ForbiddenException`, `ValidationException`
- **Preserved:** `ApiResponse<T>` in `Common/ApiResponse.cs`

### Automation

Script: `scripts/wave1-split.ps1` (re-runnable only on clean tree).

---

## Phase C — Build & verify

| Step | Result |
|------|--------|
| `dotnet build CureFlow.sln` | **Succeeded** (0 errors, 3 pre-existing warnings) |
| `npm run build` (dotnet-frontend) | **Succeeded** |
| API routes | **Unchanged** |
| Duplicate type definitions | **None** (bundled files deleted) |

---

## Refactor recommendations (future waves)

### Wave 2 — Infrastructure & API hygiene

- Split `CrmServices.cs`, `EhrServices.cs`, `BusinessServices.cs`, `QueryServices.cs`
- Remove `ApplicationDbContext` from controllers
- Add `GlobalUsings` or feature namespaces if desired

### Wave 3 — API consolidation (with FE coordination)

- Clinical nested routes under patients
- Referring doctors rename
- OpenAPI tag grouping

### Wave 4 — Optional architecture

- Repository pattern (only where query complexity warrants)
- MediatR/CQRS **not recommended** until bounded contexts are clearer

---

## Files created / moved summary

| Category | Files created (approx.) | Files deleted |
|----------|------------------------:|--------------:|
| Interfaces | 23 | 2 |
| DTOs | 72 | 3 (+ Staff/Visit/UserMgmt unbundled) |
| Enums | 22 | 1 |
| Domain entities | 9 | 2 |
| Common | 5 | 0 (ApiResponse split + restored generic) |
| **Total new .cs** | **~131** | **~10 bundled** |

---

*Generated as part of Cure-Flow Enterprise Refactoring — Wave 1 complete.*
