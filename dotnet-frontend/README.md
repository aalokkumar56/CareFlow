# CureFlow Frontend (React)

The same React frontend that powers the live Python/FastAPI prototype, now packaged as the official frontend for the **.NET Core 10 SaaS backend**.

The frontend talks to `${REACT_APP_BACKEND_URL}/api/*`. All API contracts are mirrored between the two backends, so this codebase works against either one — just change the env var.

## Quick start

```bash
# 1. Install
yarn install

# 2. Configure
cp .env.example .env
# Edit .env -> REACT_APP_BACKEND_URL=http://localhost:8080  (your .NET API)

# 3. Run
yarn start
```

## Features included

### CRM
- Daily Ops dashboard (missed-revenue, follow-up queue, urgent inbox)
- WhatsApp inbox with AI draft replies (Claude)
- Patients (search, filter, status, tags, CSV import)
- Appointments with WhatsApp reminders
- Tasks / follow-ups
- Doctor referral CRM + revenue analytics
- Broadcast campaigns
- Settings + audit logs

### EHR (Electronic Health Records) — NEW
Inside any patient profile (`/patients/:id`), 7 tabs:

1. **Timeline** — Messages, appointments, tasks combined chronologically
2. **Allergies** — Drug / food / environmental, with severity. Severe & life-threatening allergies surface as a banner on the patient header
3. **Prescriptions** — Multi-drug prescriptions with strength, dosage, duration, **reason-for-prescribing** (transparency), patient instructions, follow-up advice
4. **Vitals** — Height, weight, BP, HR, temperature, SpO₂, blood sugar, HbA1c, with auto-BMI
5. **Notes** — SOAP clinical notes (Subjective / Objective / Assessment / Plan)
6. **History** — Past medical history (conditions / surgeries / hospitalisations / immunisations) + family history
7. **Lifestyle** — 50-field holistic lifestyle profile: sleep, diet, exercise, substances, work & stress, mental wellness, environment

### Auth
- Email + password JWT login
- Stored in `localStorage` (key: `cureflow_token`)
- All API calls auto-attach `Authorization: Bearer <token>`
- 401 → redirected to `/login`

## API surface used (all under `${REACT_APP_BACKEND_URL}/api`)

```
POST   /auth/login
GET    /auth/me

GET    /patients
POST   /patients
GET    /patients/:id
PATCH  /patients/:id
DELETE /patients/:id
GET    /patients/:id/timeline
GET    /patients/:id/holistic-view
POST   /patients/import-csv

GET    /patients/:id/lifestyle
PUT    /patients/:id/lifestyle

GET    /allergies/patient/:patientId
POST   /allergies
DELETE /allergies/:id

POST   /prescriptions
GET    /prescriptions/patient/:patientId
GET    /prescriptions/:id

POST   /clinical/vitals
GET    /clinical/vitals/patient/:patientId
POST   /clinical/notes
GET    /clinical/notes/patient/:patientId
POST   /clinical/medical-history
GET    /clinical/medical-history/patient/:patientId
POST   /clinical/family-history
GET    /clinical/family-history/patient/:patientId

GET    /conversations
GET    /conversations/:id
POST   /conversations/:id/assign
POST   /conversations/:id/notes
POST   /messages/send

GET    /appointments
POST   /appointments
PATCH  /appointments/:id

GET    /tasks
POST   /tasks
PATCH  /tasks/:id
DELETE /tasks/:id

GET    /doctors
POST   /doctors
GET    /doctors/:id
PATCH  /doctors/:id
DELETE /doctors/:id
POST   /referrals
GET    /referrals/analytics

GET    /campaigns
POST   /campaigns
GET    /campaigns/:id
PATCH  /campaigns/:id
POST   /campaigns/:id/send
POST   /campaigns/preview-audience

GET    /hospital-profile
PUT    /hospital-profile
POST   /hospital-profile/import

GET    /dashboard/overview
GET    /dashboard/missed-revenue

GET    /settings/whatsapp
POST   /settings/whatsapp
GET    /audit-logs
```

## Stack

- React 18 + React Router 6
- Tailwind CSS + shadcn/ui
- @phosphor-icons/react
- axios
- sonner (toasts)
- craco

## Layout

```
src/
├── App.js                       # routes
├── lib/
│   ├── api.js                   # axios instance + auth interceptor
│   └── auth.jsx                 # AuthProvider context
├── components/
│   ├── layout/
│   │   ├── AppShell.jsx
│   │   ├── Sidebar.jsx
│   │   └── CommandPalette.jsx
│   └── ui/                      # shadcn primitives
└── pages/
    ├── Login.jsx
    ├── Dashboard.jsx            # Daily Ops
    ├── Inbox.jsx
    ├── Patients.jsx
    ├── PatientDetail.jsx        # NEW — tabbed: Timeline / EHR / Lifestyle
    ├── ehr/                     # NEW — EHR modules
    │   ├── Allergies.jsx
    │   ├── Prescriptions.jsx
    │   ├── Vitals.jsx
    │   ├── ClinicalNotes.jsx
    │   └── Lifestyle.jsx
    ├── Appointments.jsx
    ├── Tasks.jsx
    ├── Doctors.jsx
    ├── DoctorDetail.jsx
    ├── Campaigns.jsx
    ├── CampaignDetail.jsx
    ├── MissedRevenue.jsx
    └── Settings.jsx
```

## Notes for the integrating developer

- The frontend assumes the backend serializes JSON in `snake_case`. The .NET API has been configured for this in `Program.cs`.
- Auth tokens are persisted in `localStorage`. If you switch to httpOnly cookies, update `src/lib/api.js` and `src/lib/auth.jsx`.
- Severe allergies (severity = `severe` or `life_threatening`) automatically surface as a red banner on the patient profile — this is critical safety UX.
- The lifestyle profile is a single PUT upsert for the entire form.
