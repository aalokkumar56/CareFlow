# CureFlow — Product Requirements Document

## Original Problem Statement
Build a production-ready, WhatsApp-first Patient Intake & Retention CRM for Cure & Care Hospital (Althan, Surat, India). Increase patient intake, reduce missed follow-ups, automate communication, and create a scalable healthcare growth operating system. NOT a full HMS/ERP.

## Architecture
- **Frontend**: React 19 + React Router 7 + Tailwind + shadcn UI + Phosphor icons + Recharts
- **Backend**: FastAPI + Motor (async MongoDB) + APScheduler
- **Database**: MongoDB (relational discipline: refs, indexes, audit collections, soft delete)
- **AI**: Emergent LLM Key → Claude Sonnet 4.5 (async, non-streaming)
- **WhatsApp**: Meta Cloud API v18 (webhook + outbound send) — credentials in settings UI
- **Auth**: JWT with roles (admin, staff, reception, marketing) + bcrypt
- **Event-trigger architecture**: Trigger (inbound msg / scheduled job) → Rules (15-min escalation, 24h reminder, etc.) → Action Queue (tasks collection)

## User Personas
1. **Hospital management (Admin)** — overall accountability, settings, audit, user management
2. **Reception staff** — primary inbox operator, schedules appointments, manages tasks
3. **Marketing team** — campaigns, lead source tracking, conversion analytics
4. **Doctor outreach coordinators (Staff)** — referral CRM, doctor relationships

## Core Requirements (Static)
- WhatsApp as the primary communication channel
- Patients do NOT install an app
- AI assists but never diagnoses/prescribes
- All messages stored locally (CRM = source of truth)
- Operational accountability via "Missed Revenue Dashboard"
- Lead escalation if no reply in 15 minutes

## Phase 1 MVP — Implemented (2026-05-11)
- [x] JWT auth with 4 roles, seeded users
- [x] Patient/Lead CRM (CRUD, search, filter by status/dept/tag, CSV import, timeline, soft delete)
- [x] WhatsApp Cloud API integration (webhook verify + receive + send + HMAC-SHA256 signature)
- [x] Conversation inbox (list + thread + send + assign + internal notes)
- [x] AI: draft reply, conversation summary, urgency/category classification (Claude Sonnet 4.5)
- [x] Appointments (create + list + status transitions, reminder flag)
- [x] Tasks / Follow-ups (CRUD + priority + due dates)
- [x] Daily Operations Dashboard (6 KPIs + 7-day trend + dept breakdown + sources + status pipeline)
- [x] Missed Revenue Dashboard (4 leakage categories)
- [x] Event-trigger scheduler (escalation 2-min, appt reminders 15-min, missed-appt 30-min, post-visit 1h, re-engagement 12h)
- [x] Quick reply templates CRUD
- [x] Settings page (WA credentials, templates, users, audit log)
- [x] Tag aggregation system
- [x] Audit logs (admin-only)
- [x] Command palette (Cmd+K) — search patients, navigate
- [x] Design: Outfit + IBM Plex Sans, forest green, flat surfaces, Phosphor icons, dense tables

## Phase 2 — Implemented (2026-05-15)
- [x] **Doctor Referral CRM** — referring doctors CRUD with clinic/specialty/category, reconnect frequency, mark-contacted action, per-doctor patient count + revenue analytics, top doctors leaderboard
- [x] **Referrals system** — log patient referrals with revenue tracking, auto-linked from patient creation
- [x] **Campaign Broadcasting** — audience segmentation (tags + departments + statuses + inactive_days), live audience preview, template variables ({name}, {department}), draft/sending/sent workflow, per-recipient delivery stats (sent/delivered/replied/failed), 5-card stats dashboard per campaign
- [x] **Hospital Profile module** — singleton collection storing name/tagline/about/address/phones/emails/departments/services/doctors/packages/FAQs/working_hours
- [x] **Auto-import from website** — admin pastes hospital website URL → backend fetches homepage + about + services + contact + faqs + team pages → Claude AI extracts structured JSON → populates profile in one click (tested live against cureandcarehospital.in: 11 departments, 14 services, 6 FAQs)
- [x] **AI hospital context** — draft_reply and summarize_conversation now include hospital profile (name, address, phones, services, packages) in system prompt so AI replies naturally reference correct info
- [x] **Referring doctor dropdown** on patient creation form (auto-creates referral record)
- [x] **Sidebar nav** — Referral CRM + Campaigns added

## Backlog (Phase 2)
- [ ] Doctor Referral CRM (referring doctor profiles, revenue tracking)
- [ ] Campaign broadcasting (audience segmentation, WA template broadcasts, analytics)
- [ ] AI lead scoring (predict conversion likelihood)
- [ ] Conversation auto-assignment by load
- [ ] Reports export (CSV/PDF)
- [ ] Real-time WebSocket for live inbox updates (replace polling)

## Phase 3
- [ ] Multi-branch support
- [ ] Visual automation builder (Trigger → Rule → Action UI)
- [ ] SaaS white-label architecture
- [ ] Patient mobile-friendly booking landing pages

## Test Credentials
See `/app/memory/test_credentials.md`

## Test Status
- Backend pytest: 39/39 passed (100%)
- Frontend Playwright: all 7 routes verified

## Key Files
- Backend: `/app/backend/{server.py, models.py, auth.py, ai_service.py, whatsapp_service.py, scheduler.py, seed.py}`
- Frontend: `/app/frontend/src/{App.js, pages/*, components/layout/*, lib/*}`
