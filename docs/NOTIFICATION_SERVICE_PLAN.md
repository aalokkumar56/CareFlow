# Cure-Flow Notification Service — Architecture & Implementation Plan

> **Status:** Implemented — see **§13 (v2: Scalable fan-out-on-read architecture)** for the current design  
> **Stack:** .NET 10 / Dapper / PostgreSQL backend · React frontend  
> **Last updated:** 2026-06-28

---

## 1. Executive Summary

Cure-Flow today has **no notification backend**. The header bell in `AppShell.jsx` is a non-functional placeholder; the Dashboard derives a badge from missed-revenue data with a hardcoded fallback (`12`). RBAC is mature (JWT `permission` claims, `UserPermissionService`, eight seeded roles in `RbacSeeder.cs`). The `IntegrationEvents` table exists as an outbox but is **never written or read** in application code — a strong hook for async delivery. Real-time UX today is **polling only** (Inbox 10s, Email 15s, chat 5s); SignalR is documented as future work in `dotnet-backend/README.md`.

**Recommended approach:** Build a **permission-gated, tenant-scoped in-app notification service** first, wired to existing domain events (webhooks, schedulers, CRM/EHR services). Use **`IntegrationEvents` as an outbox** for reliable fan-out. Add **user preferences + role defaults** in Phase 2. Defer **SignalR** to Phase 3 (start with polling at 30–60s for the bell); email/push/WhatsApp for staff are optional later channels.

**Core privacy rule (non-negotiable):** A user never receives a notification unless they hold **all** required permissions for that type. User preferences can only **narrow** delivery within permitted types — they cannot override RBAC.

| Phase | Scope | Est. duration |
|---|---|---|
| **Phase 1 — MVP** | `Notifications` table, service + API, bell UI, hooks in webhook + scheduler + key services | 1–2 weeks |
| **Phase 2 — Preferences** | `NotificationPreference`, `RoleNotificationDefault`, settings UI, admin role matrix | 1–2 weeks |
| **Phase 3 — Real-time & channels** | SignalR hub, optional email digest via `IEmailService`, push later | 1–2 weeks |

---

## 2. Current-State Analysis

### 2.1 RBAC (Existing)

| Component | Path | Role |
|---|---|---|
| Permission constants | `dotnet-backend/src/CureFlow.Application/Common/CureFlowPermissions.cs` | 33 codes across 13 groups |
| Role seeding | `dotnet-backend/src/CureFlow.Infrastructure/Persistence/Seeders/RbacSeeder.cs` | 8 system roles + permission mappings |
| Runtime resolution | `dotnet-backend/src/CureFlow.Infrastructure/Identity/UserPermissionService.cs` | `UserRoleAssignments` → `RolePermissions` |
| API enforcement | `PermissionAuthorizationHandler` + `[Authorize(Policy = "Permission:…")]` | JWT claim check |
| Frontend | `dotnet-frontend/src/lib/permissions.js`, `usePermissions.js`, `RequirePermission.jsx` | Mirrors backend; sidebar/settings gated |

**Seeded roles:** SuperAdmin, Admin, Receptionist, Doctor, Nurse, Marketing, Staff, Billing.

Custom per-user permissions are implemented by creating a **tenant-specific role** (`UserManagementService.AssignPermissionsAsync`), not a separate user-permission table. Notification eligibility follows this same model.

### 2.2 Event Sources (Notification Triggers)

| Domain | Trigger location | Event examples |
|---|---|---|
| **WhatsApp** | `WhatsappWebhookProcessor` | Inbound message, `AwaitingReplySince` set |
| **Scheduler** | `SchedulerHostedService` | Lead escalation → `TaskItem`, 24h appt reminder task, no-show, patient re-engagement |
| **Campaigns** | `CampaignSchedulerHostedService`, `CampaignService.SendAsync` | Scheduled send start/complete/fail |
| **Appointments** | `AppointmentService` | Create, reschedule, cancel, status → no-show/completed |
| **Tasks** | `TaskService`, scheduler | Create, assign (`AssignedTo`), overdue |
| **Conversations** | `ConversationService` | Assign staff, priority change |
| **Patients** | `PatientService` | New patient, import batch |
| **Referrals** | `ReferralService` | New referral, reconnect due |
| **Clinical** | `VisitService`, `PrescriptionService`, `LabReportsController` | Lab upload, prescription, visit |
| **Audit / admin** | `UserManagementService`, `AuthService` | User create/disable, role change, password reset |
| **Integrations** | Webhook failures, `WhatsappStartupValidationHostedService` | Config errors |

### 2.3 Existing Patterns

| Pattern | Status | Reuse for notifications |
|---|---|---|
| `AuditLog` + `IAuditService` | Active | Compliance only — **not** user-facing; do not conflate |
| `IntegrationEvent` | **Unused** (table + entity only) | **Outbox** for async `NotificationDispatcher` |
| `TaskItem` | Active CRM work queue | Complement notifications; link via `EntityType`/`EntityId` |
| `IEmailService` / `ISmsService` | Patient outbound | Phase 3 staff email digests |
| SignalR | **Not implemented** | Phase 3; README suggests `/hubs/conversations` |
| Frontend polling | Inbox 10s, Email 15s, chat 5s | Phase 1: poll `/api/notifications/unread-count` every 30–60s |

### 2.4 UI Placeholders

- **`AppShell.jsx`:** `notificationCount` prop, bell button with **no click handler**
- **`Dashboard.jsx`:** badge from `missedRevenue` + hardcoded fallback `|| 12`
- Most pages pass `hideNotifications` — only Dashboard shows the bell today
- **Settings:** `SettingsNav.jsx` lists Users, Roles, Permissions, Templates, Hospital, Integrations — **no Notifications entry**

---

## 3. Architecture

### 3.1 Design Principles

1. **RBAC is the ceiling** — preferences only narrow delivery within permitted types.
2. **Tenant isolation** — all rows scoped by `TenantId` (matches `TenantEntity`).
3. **Directed vs broadcast** — prefer targeted recipients (assignee, doctor on appointment) over tenant-wide blast.
4. **Idempotency** — `DedupeKey` on notifications to avoid duplicate scheduler ticks.
5. **Separation** — `AuditLog` = immutable compliance; `Notification` = actionable, dismissible, user-specific.

### 3.2 Delivery Channels

| Channel | Phase | Notes |
|---|---|---|
| **In-app** | 1 (required) | Persisted rows + bell UI + optional full page |
| **Email** | 3 | Reuse `IEmailService`; digest or immediate for high-priority |
| **Push** | Future | Web Push / mobile |
| **WhatsApp (staff)** | Future | Not patient messaging; separate from `IWhatsappMessagingService` |

### 3.3 Recipient Resolution Algorithm

```
1. Event published (sync or via IntegrationEvents outbox)
2. Lookup NotificationTypeDefinition → RequiredPermissions[], RecipientStrategy
3. Resolve candidate UserIds (assignee | users-with-permission | role-members)
4. For each user:
     a. permissions = UserPermissionService.GetPermissionCodesAsync(user)
     b. SKIP if missing any RequiredPermission
     c. effective = ResolvePreference(user, type, channel=in_app)
     d. SKIP if effective == disabled
     e. INSERT Notification (or enqueue for email in Phase 3)
5. Phase 3: SignalR push to user group `tenant:{tid}:user:{uid}`
```

### 3.4 Real-Time Recommendation

| Option | Pros | Cons |
|---|---|---|
| **Polling (Phase 1)** | Matches existing inbox pattern; no infra change | Latency 30–60s |
| **SignalR (Phase 3)** | Instant; JWT auth groups | New hub, reconnect, scale-out (Redis backplane) |

**Recommendation:** Phase 1 polling; Phase 3 add `NotificationHub` at `/hubs/notifications` with groups `tenant:{tenantId}:user:{userId}`. Reuse CORS credentials already configured in `ServiceCollectionExtensions.cs`.

### 3.5 Preference Hierarchy

```
Effective delivery =
  IF user lacks RequiredPermissions → NEVER
  ELSE IF tenant admin disabled type globally (TenantNotificationPolicy) → NEVER
  ELSE IF user has explicit NotificationPreference → use it
  ELSE IF user's role has RoleNotificationDefault → use it
  ELSE → use system default from NotificationTypeSeeder
```

Admins (`Settings.Edit`) configure **role defaults** and **tenant policies**. All users configure **personal overrides** for types they are allowed to see (settings page hides forbidden categories).

---

## 4. Data Model

### 4.1 `Notification` (`TenantEntity`)

| Column | Type | Notes |
|---|---|---|
| `UserId` | `uuid` | Recipient |
| `Type` | `varchar` | e.g. `whatsapp.inbound_message` |
| `Title` | `text` | Short headline |
| `Body` | `text` | Optional detail (no PHI beyond what user can already view) |
| `Severity` | `smallint` | Info / Warning / Critical |
| `EntityType` | `text` | `conversation`, `appointment`, … |
| `EntityId` | `uuid` | Deep-link target |
| `ActionUrl` | `text` | Frontend route e.g. `/inbox?c={id}` |
| `IsRead` | `bool` | |
| `ReadAt` | `timestamptz` | |
| `DedupeKey` | `text` | Unique per tenant+user+key |
| `MetadataJson` | `jsonb` | Extra context |

**Indexes:** `(TenantId, UserId, IsRead, CreatedAt DESC)`, unique `(TenantId, UserId, DedupeKey)` where not null.

### 4.2 `NotificationPreference` (`TenantEntity`)

| Column | Type | Notes |
|---|---|---|
| `UserId` | `uuid` | |
| `NotificationType` | `varchar` | |
| `Channel` | `varchar` | `in_app`, `email` |
| `Enabled` | `bool` | |
| `UpdatedAt` | `timestamptz` | |

**Unique:** `(TenantId, UserId, NotificationType, Channel)`.

### 4.3 `RoleNotificationDefault`

| Column | Type | Notes |
|---|---|---|
| `RoleId` | `uuid` | FK → `Roles` |
| `NotificationType` | `varchar` | |
| `Channel` | `varchar` | |
| `Enabled` | `bool` | |
| `IsSystem` | `bool` | Seeded vs admin-edited |

### 4.4 `NotificationTypeDefinition` (reference/seed, not tenant-scoped)

| Column | Notes |
|---|---|
| `Code` | PK string |
| `Category` | Inbox, Appointments, Clinical, … |
| `RequiredPermissionsJson` | `["Conversation.View"]` |
| `DefaultSeverity` | |
| `RecipientStrategy` | `assigned_staff`, `appointment_doctor`, `permission_holders`, `admins` |
| `DefaultEnabledByRoleJson` | Seed matrix |

### 4.5 `IntegrationEvents` Outbox (Reuse Existing Entity)

Existing entity at `dotnet-backend/src/CureFlow.Domain/Entities/IntegrationEvent.cs`:

| Field | Usage for notifications |
|---|---|
| `EventType` | `notification.dispatch` |
| `PayloadJson` | `{ type, tenantId, entityType, entityId, metadata, dedupeKey }` |
| `Processed` | Set `true` after `NotificationDispatcherHostedService` fan-out |

Background `NotificationDispatcherHostedService` processes unprocessed rows (same pattern as campaign scheduler).

### 4.6 `NotificationType` Enum (Application Layer)

```csharp
public enum NotificationType
{
    WhatsappInboundMessage,
    WhatsappLeadEscalation,
    AppointmentCreated,
    AppointmentRescheduled,
    AppointmentCancelled,
    AppointmentReminderDue,
    AppointmentNoShow,
    TaskAssigned,
    TaskOverdue,
    CampaignScheduled,
    CampaignCompleted,
    CampaignFailed,
    ReferralCreated,
    ReferralReconnectDue,
    PatientReEngagement,
    ClinicalLabUploaded,
    ClinicalPrescriptionAdded,
    AuditSecurityEvent,
    SystemIntegrationError,
}
```

Store as snake_case string in DB (`whatsapp.inbound_message`) per project JSON conventions.

---

## 5. Notification Catalog

Types × required permissions × recipient strategy.

| Code | Category | Required permission(s) | Recipient strategy |
|---|---|---|---|
| `whatsapp.inbound_message` | Inbox | `Conversation.View` | Assigned staff, else all with permission (cap 10) |
| `whatsapp.lead_escalation` | Inbox | `Conversation.Manage` | Users with permission |
| `appointment.created` | Appointments | `Appointment.View` | Doctor on appt + reception pool |
| `appointment.rescheduled` | Appointments | `Appointment.View` | Same |
| `appointment.cancelled` | Appointments | `Appointment.View` | Same |
| `appointment.reminder_due` | Appointments | `Appointment.View` | Reception |
| `appointment.no_show` | Appointments | `Appointment.View` | Reception + doctor |
| `task.assigned` | Tasks | `Dashboard.View` | `AssignedTo` user |
| `task.overdue` | Tasks | `Dashboard.View` | Assignee or reception |
| `campaign.scheduled` | Campaigns | `Campaign.View` | Creator + marketing |
| `campaign.completed` | Campaigns | `Campaign.View` | Creator + marketing |
| `campaign.failed` | Campaigns | `Campaign.Manage` | Creator + admins |
| `referral.created` | Referrals | `Referral.View` | Staff with permission |
| `referral.reconnect_due` | Referrals | `Referral.Manage` | Staff |
| `patient.re_engagement` | Patients | `Patient.View` | Marketing |
| `clinical.lab_uploaded` | Clinical | `Clinical.View` | Treating doctor if known |
| `clinical.prescription_added` | Clinical | `Clinical.View` | Prescribing doctor |
| `audit.security_event` | Admin | `Audit.View` | Admins |
| `system.integration_error` | Admin | `Settings.Edit` | Admins |

**PHI minimization:** Body shows patient first name + action, not clinical details. Lab notifications: "Lab report uploaded for [Patient]" — no result values.

---

## 6. Role Matrix Example

Default enablement by role (Phase 2 seeder baseline). ✓ = enabled by default; — = disabled by default. Users can override within RBAC limits in Phase 2.

| Notification type | Required permission(s) | SuperAdmin / Admin | Receptionist | Doctor | Nurse | Marketing | Staff |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| `whatsapp.inbound_message` | `Conversation.View` | ✓ | ✓ | ✓* | — | — | ✓ |
| `whatsapp.lead_escalation` | `Conversation.Manage` | ✓ | ✓ | — | — | — | — |
| `appointment.created` | `Appointment.View` | ✓ | ✓ | ✓** | ✓ | — | — |
| `appointment.no_show` | `Appointment.View` | ✓ | ✓ | ✓** | — | — | — |
| `task.assigned` | `Dashboard.View` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `campaign.completed` | `Campaign.View` | ✓ | — | — | — | ✓ | — |
| `referral.new` | `Referral.View` | ✓ | — | — | — | — | ✓ |
| `clinical.lab_uploaded` | `Clinical.View` | ✓ | — | ✓ | ✓ | — | — |
| `audit.security_event` | `Audit.View` | ✓ | — | — | — | — | — |

\* Doctor: only when `AssignedStaffId` / conversation is linked to them (directed).  
\** Doctor: only for appointments where `DoctorUserId` matches.

---

## 7. Settings UI Concept

### 7.1 Placement

| Surface | Audience | Permission |
|---|---|---|
| **`/settings/notifications`** (new) | Personal preferences | `Settings.View` (all staff) |
| **Role defaults tab** (admin) | Tenant role matrix | `Settings.Edit` |
| **Bell dropdown** (global) | Quick access | Any authenticated user |

Add to `SettingsNav.jsx`:

```javascript
{ to: "/settings/notifications", label: "Notifications", icon: Bell, permission: PERMISSIONS.SettingsView }
```

Route in `App.js` alongside other settings pages. Use `SettingsLayout` wrapper (hides bell in settings, consistent with other settings pages).

### 7.2 Personal Preferences Page

- **Grouped accordions** mirroring permission groups: Inbox, Appointments, Tasks, Campaigns, Referrals, Clinical, Admin.
- Each row: label, description, **in-app toggle** (Phase 2), email toggle (disabled "Coming soon" until Phase 3).
- **Hide entire groups** when `usePermissions().can(required)` is false.
- Show helper: "Controlled by your role. Contact admin to change access."
- **Reset to role defaults** button.

### 7.3 Admin Role Matrix (Phase 2)

- Reuse patterns from `RolesPermissionsPanel.jsx` (checkbox grid, `GlassCard`, save via API).
- Table: rows = notification types, columns = roles (read-only SuperAdmin column).
- Cannot enable a cell if role lacks required permission (disabled + tooltip).

### 7.4 Bell UI (Phase 1)

- Wire `AppShell` bell → `NotificationDropdown` (Sheet on mobile).
- Remove `hideNotifications` from pages once real notifications exist (or show only when count > 0).
- Dashboard: replace fake `notifCount` with API data from `/api/notifications/unread-count`.

---

## 8. API Design

### 8.1 `NotificationsController` — `api/notifications`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/` | Authenticated | Paginated list (`?unread_only=true&limit=50`) |
| GET | `/unread-count` | Authenticated | Badge count |
| PATCH | `/{id}/read` | Owner | Mark one read |
| POST | `/mark-all-read` | Authenticated | Bulk |
| DELETE | `/{id}` | Owner | Soft-delete |

### 8.2 `NotificationPreferencesController` — `api/notification-preferences`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/` | Self | Merged effective preferences |
| PUT | `/` | Self | Upsert user overrides |
| GET | `/role-defaults` | `Settings.Edit` | Role matrix for tenant |
| PUT | `/role-defaults` | `Settings.Edit` | Update role defaults |

DTOs use snake_case (existing `AddJsonOptions` convention).

---

## 9. Backend Services

| Interface | Implementation | Responsibility |
|---|---|---|
| `INotificationPublisher` | `NotificationPublisher` | Write outbox or dispatch sync |
| `INotificationService` | `NotificationService` | CRUD for user notifications |
| `INotificationPreferenceService` | `NotificationPreferenceService` | Resolve effective prefs |
| `INotificationRecipientResolver` | `NotificationRecipientResolver` | RBAC + strategy |
| `NotificationDispatcherHostedService` | Background | Process `IntegrationEvents` |

Register in `ServiceCollectionExtensions.AddCureFlowInfrastructure`.

### Integration Points (Publish Calls)

1. `WhatsappWebhookProcessor` — after inbound message insert
2. `SchedulerHostedService` — after task/escalation creation
3. `AppointmentService` — Create, Update, UpdateStatus
4. `TaskService.CreateAsync` — when `AssignedTo` set
5. `CampaignService.SendAsync` — on completion/failure
6. `ReferralService.CreateReferralAsync`
7. `VisitService.UploadLabReportAsync`
8. `UserManagementService` — security-sensitive audit actions

Keep calls **non-blocking**: publish to outbox in same transaction where possible (Dapper transaction in service), dispatcher handles fan-out.

---

## 10. Phased Implementation

### Phase 1 — MVP (In-App Only) — ~1–2 weeks

**Backend**

- [ ] Migration: `Notifications` table
- [ ] Domain entity + `TableMap` entry
- [ ] `NotificationTypeDefinition` seeder (code-only or DB seed)
- [ ] `INotificationPublisher` + `NotificationService` + resolver (permission check only, all enabled)
- [ ] `NotificationsController`
- [ ] Hook 4–5 high-value triggers: inbound WhatsApp, lead escalation, task assigned, appointment no-show, campaign complete
- [ ] Optional: write `IntegrationEvents` + dispatcher (or sync insert for MVP simplicity)

**Frontend**

- [ ] `useNotifications` hook (poll unread count every 30s)
- [ ] `NotificationDropdown` component
- [ ] Wire `AppShell` bell
- [ ] `NotificationsPage` or dropdown-only for MVP
- [ ] Deep links to `/inbox`, `/appointments`, `/tasks`, etc.

**Tests**

- Recipient resolver respects RBAC (reception does not get `clinical.lab_uploaded`)
- Tenant isolation
- Dedupe key prevents duplicate scheduler notifications

### Phase 2 — Preferences + Role Matrix — ~1–2 weeks

- [ ] Migrations: `NotificationPreferences`, `RoleNotificationDefaults`
- [ ] `NotificationPreferenceService` with hierarchy
- [ ] `NotificationPreferencesController`
- [ ] `NotificationTypeSeeder` + `RoleNotificationDefaultSeeder` (from role matrix above)
- [ ] `/settings/notifications` page + admin role matrix tab
- [ ] Expand triggers to full catalog
- [ ] Filter publisher through preference resolver

### Phase 3 — Real-Time & Email — ~1–2 weeks

- [ ] `AddSignalR()` + `NotificationHub` with JWT `[Authorize]`
- [ ] Push on insert to `Clients.Group($"t:{tenantId}:u:{userId}")`
- [ ] Frontend `@microsoft/signalr` client; fallback to polling
- [ ] Email channel: immediate for `Severity.Critical`, daily digest for others
- [ ] Consider Redis backplane if multi-instance

---

## 11. Critical Gaps

1. **`IntegrationEvents` is dead code** — either adopt as outbox or drop; recommend adopt.
2. **No `AssignedTo` on many scheduler-created tasks** — escalation uses it; appointment reminders do not; extend task creation to assign for better directed notifications.
3. **Dashboard notification count is fake** — replace before production (`notifCount` fallback `|| 12` in `Dashboard.jsx`).
4. **Webhook tenant resolution uses first active tenant** — multi-tenant webhook routing must be fixed independently; notifications inherit this limitation.
5. **No per-user permission table** — notification eligibility follows role assignments only (consistent with today's model).

---

## 12. File Checklist

### New Files

| Area | Files |
|---|---|
| **Domain** | `Notification.cs`, `NotificationPreference.cs`, `RoleNotificationDefault.cs`, `NotificationType.cs`, `NotificationSeverity.cs` |
| **Application** | `INotificationService.cs`, `INotificationPublisher.cs`, `INotificationPreferenceService.cs`, `INotificationRecipientResolver.cs`, DTOs, `NotificationTypeDefinitions.cs` |
| **Infrastructure** | `NotificationService.cs`, `NotificationPublisher.cs`, `NotificationRecipientResolver.cs`, `NotificationPreferenceService.cs`, `NotificationDispatcherHostedService.cs`, `NotificationTypeSeeder.cs`, `RoleNotificationDefaultSeeder.cs` |
| **API** | `NotificationsController.cs`, `NotificationPreferencesController.cs`, `NotificationHub.cs` (Phase 3) |
| **Frontend** | `pages/settings/NotificationsPage.jsx`, `components/notifications/NotificationDropdown.jsx`, `hooks/useNotifications.js` |

### Modified Files

| File | Change |
|---|---|
| `SettingsNav.jsx` | Add Notifications nav item |
| `App.js` | Add `/settings/notifications` route |
| `AppShell.jsx` | Wire bell to dropdown + real count |
| `Dashboard.jsx` | Remove fake `notifCount` fallback |
| `ServiceCollectionExtensions.cs` | Register notification services + hub (Phase 3) |
| `TableMap.cs` | Map new tables |
| Trigger services (see §9) | Add `INotificationPublisher` calls |

---

## 13. v2: Scalable Fan-Out-On-Read Architecture

> **Status:** Implemented (replaces the Phase 1 fan-out-on-write `Notifications` table).
> **Migration:** `NotificationFeedModel` (`20260628101739_NotificationFeedModel`).

### 13.1 Why the change

The original design wrote **one `Notification` row per recipient** (fan-out-on-write). A
broadcast to 200 permission holders produced 200 rows; two broadcasts produced 400. This does
not scale and couples write latency to audience size. It also surfaced a bug: the recipient
resolver **inserted the creator** into the recipient list, so a user was notified about their
own action.

v2 switches to **fan-out-on-read (a feed model)**: each event is written **once** and the
per-user feed is materialized at read time by joining shared events to lazy per-user state.

### 13.2 New tables

| Table | Purpose | Key columns |
|---|---|---|
| **`NotificationEvents`** | One row per event (shared by all viewers) | `Type`, `Title`, `Body`, `Severity`, `EntityType`, `EntityId`, `ActionUrl`, `DedupeKey`, `MetadataJson`, `CreatedByUserId`, `AudienceMode`, `TargetUserIdsJson`, `RequiredPermissionsCsv` |
| **`NotificationReceipts`** | Lazy per-user state (created only on read/dismiss) | `NotificationEventId`, `UserId`, `ReadAt`, `DismissedAt` |
| **`NotificationFeedCursors`** | Per-user cursor for O(1) mark-all-read + feed floor | `UserId`, `LastReadAllAt`, `FeedSince` |

**Enum** `NotificationAudienceMode { Directed = 0, Permission = 1 }` (`Domain/Enums`).

**Indexes:**
- `NotificationEvents`: `(TenantId, CreatedAt)`, `(TenantId, Type)`, unique `(TenantId, Type, DedupeKey)` where `DedupeKey IS NOT NULL`.
- `NotificationReceipts`: unique `(TenantId, UserId, NotificationEventId)` where `IsDeleted = false`.
- `NotificationFeedCursors`: unique `(TenantId, UserId)` where `IsDeleted = false`.

The old `Notifications` table is **dropped** by the migration (feature was unreleased).
`NotificationPreferences` and `RoleNotificationDefaults` are **kept** unchanged.

### 13.3 Write path (`NotificationPublisher`) — O(1) for broadcasts

The `NotificationRecipientResolver` no longer enumerates users. It maps the type's
`RecipientStrategy` onto an audience:

| Strategy | Audience | Behaviour |
|---|---|---|
| `Assignee`, `AssignedStaff` (with a concrete assignee/target) | **Directed** | Stores the small explicit target set in `TargetUserIdsJson` (JSON array of user-id strings). **Creator removed** from the set. |
| `PermissionHolders`, `Admins`, `AppointmentDoctor`, unassigned `AssignedStaff` | **Permission** | No user enumeration. Stores `AudienceMode = Permission` + denormalized `RequiredPermissionsCsv`; visibility is computed at read time. |

> **Design note:** `AppointmentDoctor` maps to **Permission** so reception *and* the appointment's
> doctor (both holding `Appointment.View`) keep seeing appointment events — preserving the
> original reception-fanout behaviour without per-user rows.

The publisher then:
1. Resolves the audience (skips a directed event with no recipients, e.g. self-assignment).
2. **Event-level dedupe:** skips insert if an event already exists for `(TenantId, Type, DedupeKey)`.
3. Stamps `CreatedByUserId = request.CreatorUserId ?? db.UserId` — **the creator is never a recipient.**
4. Inserts **exactly one** `NotificationEvent` row.

SignalR/email remain Phase 3 TODOs.

### 13.4 Read path (`NotificationService`) — per requesting user

`allowedTypes` for the feed = types the user **has permission for** (RBAC) **and has enabled**
in preferences (`GetEffectivePreferencesAsync`). Preferences now narrow at **read** time because
events are shared.

A user's visible events are those where, in a single query:
- `Type = ANY(@allowedTypes)` (RBAC ceiling, also covers directed), **and**
- `AudienceMode = Permission` **OR** (`Directed` AND `@userId ∈ TargetUserIdsJson` via `jsonb_array_elements_text`), **and**
- `CreatedByUserId IS DISTINCT FROM @userId` (**creator excluded**), **and**
- `CreatedAt >= cursor.FeedSince`, **and**
- no receipt with `DismissedAt` for `(event, user)`.

`IsRead = receipt.ReadAt IS NOT NULL OR event.CreatedAt <= cursor.LastReadAllAt`.

| Operation | Behaviour |
|---|---|
| `ListAsync` | LEFT JOIN receipt + per-user cursor; `ORDER BY CreatedAt DESC LIMIT clamp(1..100, default 50)`; supports `unreadOnly`. |
| `GetUnreadCountAsync` | Counts visible events where `NOT IsRead`. |
| `MarkReadAsync(eventId)` | Validates visibility + RBAC (else `Forbidden`/`NotFound`); **upserts one receipt** with `ReadAt = now` for this user only. |
| `MarkAllReadAsync` | Sets `cursor.LastReadAllAt = now` — **O(1)**, no per-event receipts. |
| `DeleteAsync(eventId)` | Upserts a receipt with `DismissedAt = now` — per-user hide; the event row stays for others. |

The `NotificationDto` shape is unchanged and `Id` is the **event id**, so the frontend
(`useNotifications`, `NotificationDropdown`) keeps working without changes. The feed cursor is
**lazily created** on first read using the user's `CreatedAt`, so brand-new users do not inherit
pre-existing broadcasts.

### 13.5 Trade-offs

| | Fan-out-on-write (v1) | Fan-out-on-read (v2) |
|---|---|---|
| Write cost | O(recipients) rows | **O(1)** — one event row |
| Read cost | Index seek on own rows | Join events ⨝ receipts ⨝ cursor (filtered) |
| Storage | Row per (user, event) | One row + sparse receipts |
| Mark-all-read | UPDATE N rows | **O(1)** cursor update |
| Preference changes | Baked in at write | Applied live at read |
| New users | — | Cursor `FeedSince` prevents backfill |

The cost moves to read time, mitigated by the event/receipt indexes and small per-user limits.
SignalR (Phase 3) can push the single event id to permission/directed groups for instant updates.

---

---

## Agent / CI Testing Protocol

### Rule: Stop all services after every agent test run

Whenever an AI agent or CI job starts a backend or frontend dev server for testing purposes (Playwright, integration tests, smoke tests), **it must stop every process it started before finishing its turn.**

#### Why
- Stale `dotnet run` and `npm start` processes accumulate across agent sessions and waste memory / lock ports.
- A subsequent agent run may pick up a stale server running old code, causing false positives or port conflicts.
- The user should always have a clean baseline between sessions.

#### How (PowerShell — Windows dev environment)

**Stop by known port (preferred — surgical, leaves IDE tooling untouched):**
```powershell
foreach ($port in 3000, 5000, 5001, 5180, 8080) {
    $c = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
    if ($c) { Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue }
}
```

**Verify nothing is left listening:**
```powershell
foreach ($port in 3000, 5000, 5001, 5180, 8080) {
    $c = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
    if ($c) { Write-Warning "Port $port still in use by PID $($c.OwningProcess)" }
}
Write-Output "Port check complete"
```

#### Checklist for every agent that starts services
- [ ] Record every PID / port when the server is started.
- [ ] After tests finish (pass or fail), stop those PIDs using `Stop-Process -Force`.
- [ ] Confirm all dev ports are free before ending the turn.
- [ ] Do **not** kill IDE helper processes (e.g. the Cursor `dotnet` language server) — only kill ports 3000/5000/5001/5173/5180/8080.

---

## Appendix: Related Documentation

- RBAC permissions: `dotnet-backend/src/CureFlow.Application/Common/CureFlowPermissions.cs`
- Database conventions: `docs/DATABASE.md`
- Enterprise refactoring notes: `docs/ENTERPRISE_REFACTORING_REPORT.md`
