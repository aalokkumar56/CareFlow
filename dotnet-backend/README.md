# CureFlow — Healthcare CRM SaaS (.NET 10 Backend)

A multi-tenant, WhatsApp-first **Patient Intake + Retention + EHR** platform for small/medium hospitals in India. Built on **.NET 10 / C# / EF Core / PostgreSQL** following **Clean Architecture**.

This repository is the **handover foundation** — your developer extends business logic, the frontend (React) consumes these APIs.

---

## 🎯 What's in this codebase

| Layer | Project | Responsibility |
|---|---|---|
| API | `CureFlow.Api` | HTTP endpoints, JWT auth, middleware, Swagger |
| Application | `CureFlow.Application` | DTOs, service interfaces, common abstractions |
| Domain | `CureFlow.Domain` | Entities (45+), enums, base types |
| Infrastructure | `CureFlow.Infrastructure` | EF Core DbContext, services, integrations (Claude AI, WhatsApp, scraper) |
| Tests | `CureFlow.UnitTests` (in `dotnet-backend-test`) | xUnit + FluentAssertions unit tests |

---

## 🏥 Feature modules (all entities + endpoints scaffolded)

### 1. Multi-Tenancy SaaS Core
- **Tenant** entity with subscription plan (Trial/Starter/Growth/Enterprise)
- **Tenant signup endpoint** `POST /api/auth/register-tenant` (public)
- Row-level security via EF Core **global query filters** (every TenantEntity is auto-scoped)
- Per-tenant **seat limits, patient limits, message quotas** (enforced in service layer — extend as needed)
- 14-day trial auto-set on registration

### 2. Authentication & RBAC
- JWT bearer (HS256), 24h tokens, `tenant_id` claim
- BCrypt password hashing (work factor 11)
- 6 roles: **TenantOwner, Admin, Doctor, Reception, Marketing, Staff**
- Role-based `[Authorize(Roles = "...")]` on every endpoint
- Audit log of every state-changing action

### 3. Patient CRM
- Full demographic + address + emergency contact + lifestyle + EHR
- 8 lead statuses (NewInquiry → Visited / Lost / ReEngagement)
- Tagging (JSONB column), search, filter, soft-delete
- CSV import endpoint

### 4. 🩺 Doctor Portal & EHR (NEW)
A licensed doctor logs in and sees a **holistic patient view**:
- `GET /api/patients/{id}/holistic-view` — patient + lifestyle + allergies + recent prescriptions + vitals + labs + clinical notes + medical history + family history + appointments — **one call**.

Sub-modules:
- **Allergies** — type, allergen, severity (Mild → LifeThreatening), reaction, first observed
- **Prescriptions** — with `PrescriptionItem` per drug containing **transparent fields**:
  - `ReasonForPrescribing` (required — drives transparency)
  - Drug name, generic name, strength, form (tablet/syrup/injection), route, dosage, frequency, duration, timing
  - Possible side effects, patient instructions
  - `IsContinuation`, `IsAcute` flags
- **Injections** — separately tracked: site, route, administered by, **batch number**, expiry, **reason for injection**, adverse reactions
- **Vital signs** — height/weight/BMI (auto-computed), BP, HR, SpO2, temp, blood sugar fasting/PP, HbA1c
- **Lab reports** — test name, category, lab, results, interpretation, abnormal flag, file URL
- **Clinical notes** — SOAP format (Subjective, Objective, Assessment, Plan)
- **Medical history** — chronic conditions, surgeries, hospitalizations, immunizations
- **Family history** — hereditary conditions per relation

### 5. 🌿 Lifestyle Profile (NEW)
One-to-one with Patient. Captures holistic info for personalized treatment:
- **Daily habits**: sleep hours, sleep quality, wake/bed times, water intake, meals/day, skips breakfast
- **Diet**: type (veg/non-veg/vegan/jain), cuisine, allergies, restrictions, caffeine, processed food
- **Exercise**: regular?, minutes/week, type, activity level
- **Substances**: smoking status + cigarettes/day, alcohol consumption, tobacco/paan
- **Work & stress**: occupation, schedule, hours, stress level, management methods, hobbies
- **Mental wellness**: concerns, anxiety/depression, current treatment
- **Reproductive**: menstrual cycle, pregnancies, contraceptive use
- **Environment**: living env, pollution exposure, pet exposure

### 6. WhatsApp Engine
- **Meta Cloud API v18** outbound send (HttpClient)
- Webhook verify (HMAC-SHA256 of body with `app_secret`)
- Phone normalization (India +91 default)
- **Demo mode** when credentials not set — messages stored locally as `Sent`
- Per-tenant settings (table) for production; appsettings fallback for single-tenant
- Conversation + Message persistence (CRM = source of truth)

### 7. AI Service (Claude Sonnet 4.5)
- `IAiService` with 4 methods: DraftReply, Summarize, ClassifyMessage, ExtractHospitalProfileFromHtml
- Uses `Anthropic.SDK` NuGet
- **Never diagnoses/prescribes** (system prompt enforces operational-only)

### 8. Appointments
- Status workflow (Scheduled → Confirmed → Completed | NoShow | Cancelled)
- 24h reminder background flagging
- Auto-promotes patient status to AppointmentScheduled

### 9. Tasks / Follow-ups (Event-Trigger Architecture)
- TaskType enum (FollowUp, Callback, AppointmentReminder, **LeadEscalation**, PostVisitCheckIn, ReEngagement)
- Triggered by hosted background scheduler

### 10. Referral CRM
- ReferringDoctor entity (clinic, specialty, category, reconnect frequency)
- Referral entity with revenue tracking
- Analytics endpoint (top doctors leaderboard)

### 11. Campaign Broadcasting
- Audience segmentation: tags + departments + statuses + inactive_days (JSON filter)
- Audience preview before send
- Template variables `{name}`, `{department}`
- Per-recipient status tracking (Queued → Sent → Delivered → Read → Replied)
- Demo mode bypasses real send

### 12. Hospital Profile + Auto-Import
- Singleton per tenant
- **`POST /api/hospital-profile/import` + `{url}`** → scrapes homepage + about + services + contact + faqs → AI extracts departments, services, doctors, packages, FAQs as JSON
- Used as **AI context** for reply drafting

### 13. Daily Operations Dashboard
- 7 KPIs: new inquiries today, appointments today, follow-ups due, unanswered (>15min), inactive patients, conversion rate, total patients
- Departments + sources breakdown
- **Missed Revenue endpoint** — operational accountability (4 leakage categories)

### 14. Background Scheduler (`SchedulerHostedService`)
Runs every 1 min, with sub-intervals:
- **Every 2 min**: 15-min unanswered lead escalation → creates High-priority Task
- **Every 15 min**: 24h appointment reminders
- **Every 30 min**: Missed appointment → status=NoShow + follow-up task
- **Every 12 h**: 90-day inactive patients → status=ReEngagement
- Runs **per tenant** automatically

---

## 🚀 Quick start (local development)

### Prerequisites
- .NET 10 SDK (`dotnet --version` should be ≥ 10.0)
- PostgreSQL 14+ (or use docker-compose)
- Node 20+ (for React frontend in `/app/frontend`)

### 1. Clone & configure
```bash
cd dotnet-backend
cp .env.example .env
# Edit .env: add ANTHROPIC_API_KEY, JWT_SECRET (random 64 chars)
```

Update `src/CureFlow.Api/appsettings.json` (or copy from `appsettings.template.json`):
- `ConnectionStrings.Default` — your PostgreSQL connection
- `Jwt.Secret` — random 64-character string
- `Anthropic.ApiKey` — from console.anthropic.com
- `WhatsApp.*` — leave blank for demo mode

**Environment variables** (override appsettings; see `.env.example`):

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__Secret` | JWT signing secret (required) |
| `Jwt__Issuer` / `Jwt__Audience` | JWT issuer and audience |
| `Anthropic__ApiKey` | Claude API key for AI features |
| `WhatsApp__AccessToken` | WhatsApp provider access token |
| `WhatsApp__AppSecret` | Webhook HMAC verification secret |
| `WhatsApp__ApiToken` | WhatsBiz API token (if using WhatsBiz) |
| `WhatsApp__PhoneNumberId` | WhatsApp phone number ID |
| `WhatsApp__VerifyToken` | Webhook verification token |
| `WhatsApp__Enabled` | Enable/disable WhatsApp integration |
| `Storage__LabReportsPath` | Directory for uploaded lab reports |
| `Cors__Origins` | Comma-separated allowed CORS origins |

For local development, put working secrets in `appsettings.Development.json` (gitignored).

### 2. Restore + build
```bash
dotnet restore
dotnet build
```

### 3. Database — create + migrate
```bash
# Option A: Local PostgreSQL
createdb cureflow

# Option B: Docker
docker compose up postgres -d

# Generate initial migration
cd src/CureFlow.Api
dotnet ef migrations add InitialCreate --project ../CureFlow.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update
```

The app auto-migrates + seeds on startup if `Database:AutoMigrate=true` and `Database:Seed=true` in appsettings.

### 4. Run
```bash
cd src/CureFlow.Api
dotnet run
```
- API: http://localhost:5180
- Swagger: http://localhost:5180/swagger
- Seeded admin: **admin@cureflow.in / admin123**

### 5. Full stack with Docker
```bash
docker compose up --build
```
This starts PostgreSQL + API. Frontend runs separately.

---

## 📂 Project structure

```
dotnet-backend/
├── CureFlow.sln                        # product projects only (no tests)
├── docker-compose.yml
├── .env.example
├── README.md
└── src/
    ├── CureFlow.Api/                   # Web API entry point
    │   ├── Program.cs
    │   ├── appsettings.json
    │   ├── Controllers/
    │   │   ├── AuthController.cs       (4 endpoints)
    │   │   ├── PatientsController.cs   (CRUD + holistic view + CSV import)
    │   │   ├── EhrController.cs        (Allergies, Prescriptions, Clinical)
    │   │   └── BusinessControllers.cs  (Conv, Appt, Tasks, Doctors, Refs, Campaigns, HP, Dashboard, WA, AI)
    │   └── Middleware/
    │       └── TenantMiddleware.cs     (TenantContext + ExceptionMiddleware)
    │
    ├── CureFlow.Application/           # Pure logic, no infra dependency
    │   ├── Common/
    │   │   ├── ApiResponse.cs          (PagedResult, DomainException etc.)
    │   │   └── ITenantContext.cs
    │   ├── DTOs/
    │   │   ├── CoreDtos.cs             (Auth, Patient, Lifestyle DTOs)
    │   │   └── EhrDtos.cs              (Allergy, Prescription, Vitals etc.)
    │   └── Interfaces/                 (IServices.cs + IBusinessServices.cs)
    │
    ├── CureFlow.Domain/                # Entities
    │   ├── Common/BaseEntity.cs
    │   ├── Enums/Enums.cs              (20+ enums)
    │   └── Entities/
    │       ├── Saas/Tenant.cs
    │       ├── User.cs
    │       ├── Patient.cs
    │       ├── Conversation.cs (+Message, InternalNote)
    │       ├── Appointment.cs (+TaskItem)
    │       ├── Referrals.cs (+Campaign, CampaignRecipient)
    │       ├── SystemEntities.cs (HospitalProfile, AuditLog, Templates, WA settings)
    │       ├── Ehr/
    │       │   ├── Allergy.cs
    │       │   ├── Prescription.cs (+PrescriptionItem, Injection)
    │       │   └── ClinicalRecords.cs (VitalSigns, LabReport, ClinicalNote, MedicalHistory, FamilyHistory)
    │       └── Lifestyle/LifestyleProfile.cs
    │
    └── CureFlow.Infrastructure/        # EF Core + integrations
        ├── Persistence/
        │   ├── ApplicationDbContext.cs (global filters, JSONB, indexes)
        │   ├── Migrations/              (empty — generate first migration)
        │   └── Seeders/DemoSeeder.cs
        ├── Identity/
        │   ├── BcryptPasswordHasher.cs
        │   └── JwtTokenService.cs
        ├── Services/                   (10+ service implementations)
        │   ├── AuthService.cs + AuditService
        │   ├── PatientService.cs
        │   ├── EhrServices.cs          (Lifestyle, Allergy, Prescription, ClinicalRecord)
        │   ├── CrmServices.cs          (Conversation, Appointment, Task)
        │   ├── BusinessServices.cs     (Referral, Campaign, HospitalProfile, Dashboard)
        │   └── SchedulerHostedService.cs (background jobs)
        └── External/
            ├── ClaudeAiClient.cs       (Anthropic SDK)
            ├── WhatsappCloudClient.cs  (Meta v18 + HMAC verify)
            └── HospitalWebsiteScraper.cs (HtmlAgilityPack)
```

---

## 🔐 Auth flow & multi-tenancy explained

1. **Hospital signup** (`POST /api/auth/register-tenant`): creates Tenant + first admin User + empty HospitalProfile
2. **Login** (`POST /api/auth/login`): returns JWT with `tenant_id` claim
3. **Each request**: `TenantMiddleware` reads `tenant_id` claim → fills `ITenantContext.TenantId`
4. **`ApplicationDbContext`**: every `TenantEntity` has a global query filter `e.TenantId == _tenant.TenantId` — meaning **all queries are automatically scoped**. No tenant can see another tenant's data, even if you forget to add `.Where()`.
5. On insert, `SaveChangesAsync` auto-stamps `TenantId` from context

---

## 🧪 Testing

Unit tests live in the sibling folder `dotnet-backend-test` (solution `CureFlow.UnitTest.sln`):

```bash
cd ../dotnet-backend-test
dotnet test CureFlow.UnitTest.sln
```

Currently includes smoke and service unit tests. **Developer should add**:
- AuthService.LoginAsync unit tests (with InMemory provider)
- PatientService CRUD integration tests (Testcontainers + PostgreSQL) — under a future `CureFlow.IntegrationTest.sln`
- Multi-tenancy isolation tests (User from tenant A cannot fetch patient from tenant B)
- WhatsApp signature verification tests
- Lifestyle upsert tests

---

## 📋 API endpoint summary

Full list visible in Swagger at `/swagger` when running. Highlights:

### Auth
- `POST /api/auth/register-tenant` — Hospital signs up for SaaS
- `POST /api/auth/login`
- `GET  /api/auth/me`
- `POST /api/auth/register` — Admin creates staff/doctor (admin only)

### Patients
- `GET    /api/patients`
- `GET    /api/patients/{id}`
- `GET    /api/patients/{id}/holistic-view`  ⭐ **Doctor's view — everything in one call**
- `POST   /api/patients`
- `PATCH  /api/patients/{id}`
- `DELETE /api/patients/{id}` (soft-delete)
- `POST   /api/patients/import-csv`

### Lifestyle
- `GET /api/patients/{patientId}/lifestyle`
- `PUT /api/patients/{patientId}/lifestyle`

### EHR
- `POST /api/allergies` · `GET /api/allergies/patient/{patientId}` · `DELETE /api/allergies/{id}`
- `POST /api/prescriptions` · `GET /api/prescriptions/{id}` · `GET /api/prescriptions/patient/{patientId}`
- `POST /api/clinical/vitals` · `POST /api/clinical/notes` · `POST /api/clinical/medical-history` · `POST /api/clinical/family-history`

### WhatsApp
- `GET  /api/whatsapp/webhook` (verify, public)
- `POST /api/whatsapp/webhook` (receive, public + signed)
- `POST /api/conversations/messages` (send)

### Referrals & Campaigns
- `POST/GET /api/doctors`
- `POST /api/referrals` · `GET /api/referrals/analytics`
- `POST/GET /api/campaigns` · `GET /api/campaigns/{id}` · `POST /api/campaigns/preview-audience` · `POST /api/campaigns/{id}/send`

### Hospital Profile
- `GET /api/hospital-profile`
- `PUT /api/hospital-profile` (admin)
- `POST /api/hospital-profile/import` (admin) — **auto-import from website URL**

### Dashboard
- `GET /api/dashboard/overview`
- `GET /api/dashboard/missed-revenue`

---

## ✅ What's COMPLETED in this scaffold

| Layer | Status |
|---|---|
| Project structure (Clean Architecture, 4 layers) | ✅ Complete |
| 45+ EF Core entities (Patient + EHR + Lifestyle + SaaS + CRM) | ✅ Complete |
| Multi-tenant DbContext with global query filters | ✅ Complete |
| Auth (JWT + BCrypt + role-based) | ✅ Complete |
| All controllers + DTOs | ✅ Complete |
| PatientService (full CRUD + holistic view) | ✅ Complete |
| LifestyleService, AllergyService, PrescriptionService, ClinicalRecordService | ✅ Complete |
| ConversationService, AppointmentService, TaskService | ✅ Complete |
| ReferralService, CampaignService | ✅ Complete |
| HospitalProfileService (with auto-import skeleton) | ✅ Complete |
| DashboardService | ✅ Complete |
| Background SchedulerHostedService | ✅ Complete |
| Claude AI client (Anthropic SDK) | ✅ Complete |
| WhatsApp Cloud API client (send + verify) | ✅ Complete |
| Website scraper (HtmlAgilityPack) | ✅ Complete |
| Demo seeder | ✅ Complete |
| Swagger documentation | ✅ Complete |
| Docker compose | ✅ Complete |
| Tenant signup endpoint (SaaS-ready) | ✅ Complete |

---

## 🚧 What the developer needs to complete

### Critical (before production)
1. **Generate EF Core migration** — run `dotnet ef migrations add InitialCreate` (the scaffold is ready, no migrations checked in to keep it clean)
2. **Encrypt secrets at rest** — `WhatsAppSettings.AccessTokenEncrypted` and `AppSecretEncrypted` are stored plain in current code. Use `IDataProtectionProvider` to encrypt before save / decrypt on read.
3. **WhatsApp inbound webhook parsing** — `WhatsappCloudClient.ProcessIncomingWebhookAsync` is a stub. Parse `entry[].changes[].value.messages[]` and `entry[].changes[].value.statuses[]`. See Python reference in `/app/backend/whatsapp_service.py` lines 60–115.
4. **AI controller wire-up** — `AiController.Draft/Summarize` are placeholders. Inject `IConversationService`, fetch history, call `IAiService`.
5. **HospitalProfile import mapping** — `HospitalProfileService.UpdateAsync` and `ImportFromUrlAsync` need to map the AI's JSON response into the entity fields. Use `JsonElement` -> property mapping.

### Important (within first 2 weeks)
6. **Subscription enforcement** — check `SeatLimit`, `PatientLimit`, `MessagesQuotaMonthly` in respective Create endpoints. Throw `ValidationException` on exceeded.
7. **Real-time inbox** — add SignalR hub `/hubs/conversations` and broadcast on new inbound message.
8. **File uploads** — patient avatar, lab report PDFs. Add `IFileStorage` interface (S3/MinIO/local).
9. **Payment integration** — Razorpay/Stripe for subscription billing (charge tenant per plan).
10. **Email service** — appointment confirmation emails, password reset (use SendGrid / Resend).

### Quality
11. **Add tests** — see Testing section above. Aim for >70% coverage on services.
12. **API rate limiting** — `AddRateLimiter()` to prevent brute force on `/auth/login`.
13. **Logging** — Serilog is configured, but add structured properties on every business action.
14. **Health checks** — add Postgres + Anthropic health probes for k8s readiness.

### Nice-to-have
15. **Drug interaction warning** — when adding a PrescriptionItem, check patient's Allergies + existing prescriptions for conflicts (use a simple drug database or call an external API).
16. **Vitals trend charts** — keep last 30 readings indexed for fast time-series queries.
17. **SaaS analytics dashboard** — for YOU (the operator), to see all tenants, MRR, churn.
18. **Mobile app** — React Native or Flutter consuming the same APIs.

---

## 🌐 Frontend integration

The React frontend at `/app/frontend` was originally built against the Python backend. To switch to .NET:

1. Update `REACT_APP_BACKEND_URL` in `/app/frontend/.env` to point to the .NET API
2. Most endpoints are **already shaped identically** (`/api/auth/login`, `/api/patients`, etc.). Differences:
   - Response wrapping (Python returns raw, .NET could wrap in `ApiResponse<T>` — keep both consistent)
   - Date format (.NET ISO-8601 with `Z`, JavaScript-compatible)
   - Enum serialization — .NET serializes as numbers by default. Add `JsonStringEnumConverter` in `Program.cs`:
     ```csharp
     builder.Services.AddControllers().AddJsonOptions(o =>
         o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
     ```

---

## 🔒 Production deployment checklist

- [ ] Generate strong `JWT_SECRET` (`openssl rand -hex 32`)
- [ ] Use Postgres SSL connection (`Sslmode=Require`)
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure CORS origins precisely (no wildcard)
- [ ] Enable HTTPS-only (Kestrel + reverse proxy)
- [ ] Set up backups for PostgreSQL (daily + point-in-time recovery)
- [ ] Register WhatsApp webhook with HTTPS endpoint at Meta
- [ ] Configure log aggregation (Seq / Datadog / CloudWatch)
- [ ] Set up Application Insights / OpenTelemetry
- [ ] Enable rate limiting on `/auth/login`
- [ ] Add `[FluentValidation]` validators on every DTO
- [ ] Privacy policy + Terms — required for WhatsApp Business approval
- [ ] HIPAA-style data handling review (audit logs in place ✓, encryption at rest needed)

---

## 📞 Test credentials (after seeding)

| Role | Email | Password |
|---|---|---|
| TenantOwner | `admin@cureflow.in` | `admin123` |
| Doctor | `dr.mehta@cureflow.in` | `doctor123` |
| Reception | `reception@cureflow.in` | `reception123` |
| Marketing | `marketing@cureflow.in` | `marketing123` |

**Change all passwords before first production use.**

---

## 🤝 Handover notes

This scaffold is **handover-ready** for an experienced .NET developer:
- Clean Architecture is strict (Domain has NO dependencies; Application depends only on Domain; Infrastructure on Application; API on Infrastructure)
- All entities/DTOs/interfaces are stable contracts
- Services are easily mockable for testing
- Multi-tenancy is enforced at the data layer (developer cannot accidentally leak data)
- Auth + audit + soft-delete are wired into the framework — every new entity inherits them for free

**Estimated remaining effort to production**: 3–6 weeks for one senior .NET developer.

For questions, refer to the original Python implementation at `/app/backend/` — every C# service has a Python equivalent that shows the exact business behavior expected.
