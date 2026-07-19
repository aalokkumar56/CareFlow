# API Test Scenarios

Catalog of Cure-Flow backend unit + HTTP + security test scenarios.  
**Source audit:** Backend API coverage audit (2026-07-19) vs `dotnet-backend` (~41 controllers / ~170 endpoints) and `dotnet-backend-test` (`CureFlow.UnitTest.sln`).  
**Status:** Catalog only — not implemented.

---

## Existing (summary counts only)

| Area | Files (approx) | Fact/Theory | Notes |
|------|----------------|------------:|-------|
| Smoke / domain basics | `SmokeTests.cs` | 2 | Enums, AuthResponse equality |
| Multi-tenant auth & slugs | `MultiTenantAuthTests.cs` | 6 | Slug helpers + register/login JWT (DB) |
| Multi-tenant DB isolation | `MultiTenantIsolationTests.cs` | 4 | Patient get/list isolation (DB) |
| Tenant SQL filters | `TenantIsolationTests.cs` | 5 | `WhereActive_*` + cross-tenant get |
| PostgreSQL RLS | `TenantRlsIsolationTests.cs` | 3 | Raw SQL + RLS context (DB) |
| Service-level tenant API | `TenantApiIsolationTests.cs` | 3 | Hospital profile / appointment / conversation |
| Audit log isolation | `AuditLogIsolationTests.cs` | 1 | Cross-tenant audit read blocked (DB) |
| Time / appointment message format | `TenantTimeHelperTests.cs`, `AppointmentMessageTimeTests.cs` | 10 | IANA TZ helpers + template placeholders |
| Notifications (service) | `NotificationServiceTests.cs` | 19 | RBAC, preferences, audience, publish, read paths |
| WhatsApp notification types | `WhatsappNotificationTypeTests.cs` | 4 | Permission gating for inbound / escalation |
| WhatsApp API service | `WhatsappApiServiceTests.cs` | 6 | Send validation, demo mode |
| WhatsApp settings | `WhatsAppSettingsServiceTests.cs` | 6 | Status / provider / defaults |
| WhatsApp webhook helpers | `WhatsappWebhookProcessorTests.cs` | 8 | Phone id extract, signature verify |
| WhatsApp media policy | `WhatsappMediaPolicyTests.cs` | 10 | MIME / size / SVG reject |
| WhatsApp phone helper | `WhatsappPhoneHelperTests.cs` | 3 | Normalize / display |
| Email service | `EmailServiceTests.cs` | 8 | Status + send reject paths |
| SSRF / safe remote URL | `SafeRemoteUrlTests.cs` | 15 | HTTPS, private IP, DNS, bounded stream |
| Dapper / infra | `AnyArrayParameterTests.cs` | 3 | Array / jsonb parameter expansion |
| Users / roles helpers | `CreateUserRequestBindingTests.cs`, `RoleNameRulesTests.cs` | 3 | Snake_case binding; protected `super_admin` |
| **Total existing** | **~21 test files** | **119** | **0** `WebApplicationFactory` HTTP suites |
| IntegrationTest project | `integration/README.md` only | 0 | Placeholder; DB suites should relocate later |

**Coverage signal:** Strong on multi-tenant isolation helpers, notifications, WhatsApp utilities. Weak on business CRUD. Zero true HTTP contract tests.

---

## Missing — Critical

Prior audit Critical themes (1–18), expanded into concrete scenarios.

### Auth & tenant lifecycle

1. **API-CRIT-001** — `POST /api/auth/login` with valid hospital credentials returns 200 and JWT containing `sub`, `email`, `tenant_id`, `role`, and `permission` claims
2. **API-CRIT-002** — `POST /api/auth/login` with wrong password returns 401 and no token
3. **API-CRIT-003** — `POST /api/auth/login` with unknown email returns 401 (no user enumeration beyond status)
4. **API-CRIT-004** — `POST /api/auth/login` for user in Pending tenant returns gated response (cannot use app APIs until approved)
5. **API-CRIT-005** — `POST /api/auth/login` for Suspended tenant is rejected
6. **API-CRIT-006** — `POST /api/auth/login` for Rejected tenant is rejected
7. **API-CRIT-007** — `POST /api/auth/login` for disabled/inactive user is rejected
8. **API-CRIT-008** — `POST /api/auth/register-tenant` creates tenant in Pending status with owner user
9. **API-CRIT-009** — After register-tenant, owner JWT (if issued) cannot access protected hospital APIs until platform approval
10. **API-CRIT-010** — `POST /api/auth/register-tenant` rejects reserved slug (e.g. `platform`, `admin`, `api`)
11. **API-CRIT-011** — `POST /api/auth/register-tenant` rejects duplicate slug / colliding hospital email
12. **API-CRIT-012** — `GET /api/auth/me` without token returns 401
13. **API-CRIT-013** — `GET /api/auth/me` with valid token returns user identity, roles, permissions
14. **API-CRIT-014** — `GET /api/auth/session` without token returns 401
15. **API-CRIT-015** — `GET /api/auth/session` reflects current tenant status, onboarding flags, timezone
16. **API-CRIT-016** — `POST /api/auth/register` creates user only in caller's tenant (User.Create)
17. **API-CRIT-017** — `POST /api/auth/register` without User.Create permission returns 403
18. **API-CRIT-018** — `POST /api/auth/register` cannot set `TenantId` / foreign tenant via body (mass-assignment resistance)

### Platform SaaS lifecycle

19. **API-CRIT-019** — `GET /api/platform/auth/setup-status` anonymous: reports whether bootstrap is needed
20. **API-CRIT-020** — `POST /api/platform/auth/bootstrap` succeeds only when no PlatformUser exists
21. **API-CRIT-021** — `POST /api/platform/auth/bootstrap` second call rejected (already bootstrapped)
22. **API-CRIT-022** — `POST /api/platform/auth/login` valid credentials return platform JWT with `platform_user=true` and role `platform_ops` (no `tenant_id`)
23. **API-CRIT-023** — `POST /api/platform/auth/login` wrong password → 401
24. **API-CRIT-024** — Hospital JWT on `GET/PATCH /api/platform/tenants*` → 401/403 (PlatformUser policy)
25. **API-CRIT-025** — Platform JWT on hospital patient/appointment APIs → 401/403 (not a tenant user)
26. **API-CRIT-026** — `PATCH /api/platform/tenants/{id}/approve` transitions Pending → Active and records `ApprovedByPlatformUserId`
27. **API-CRIT-027** — After approve, hospital owner can login and access hospital APIs
28. **API-CRIT-028** — `PATCH /api/platform/tenants/{id}/reject` blocks subsequent hospital login
29. **API-CRIT-029** — `PATCH /api/platform/tenants/{id}/suspend` blocks hospital login / API access
30. **API-CRIT-030** — `PATCH /api/platform/tenants/{id}/activate` restores suspended tenant access
31. **API-CRIT-031** — Platform tenant list returns all tenants; hospital user cannot list platform tenants
32. **API-CRIT-032** — Invalid platform status transition (e.g. approve already Active) returns 400/409

### Authorization (permission matrix)

33. **API-CRIT-033** — Unauthenticated request to any non-`[AllowAnonymous]` endpoint → 401 (fallback policy)
34. **API-CRIT-034** — Authenticated user lacking `Patient.View` → `GET /api/patients` 403
35. **API-CRIT-035** — Lacking `Patient.Create` → `POST /api/patients` 403
36. **API-CRIT-036** — Lacking `Patient.Edit` → `PATCH /api/patients/{id}` 403
37. **API-CRIT-037** — Lacking `Patient.Delete` → `DELETE /api/patients/{id}` 403
38. **API-CRIT-038** — Lacking `Appointment.View/Create/Edit` → corresponding appointment endpoints 403
39. **API-CRIT-039** — Lacking `Clinical.View/Edit` → clinical/visits/prescriptions/allergies/lab/docs 403
40. **API-CRIT-040** — Lacking `Conversation.View/Manage` → conversation list/send/media/assign 403
41. **API-CRIT-041** — Lacking `WhatsApp.View/Send/Manage` → WhatsApp data/messaging endpoints 403
42. **API-CRIT-042** — Lacking `Campaign.View/Manage` → campaign endpoints 403
43. **API-CRIT-043** — Lacking `User.View/Create/Edit/Delete` → users/roles endpoints 403
44. **API-CRIT-044** — Lacking `Staff.View/Create/Edit/Delete` → staff endpoints 403
45. **API-CRIT-045** — Lacking `Settings.View/Edit` → hospital/WhatsApp/email/SMS settings 403
46. **API-CRIT-046** — Lacking `Dashboard.View` → dashboard/tasks endpoints 403
47. **API-CRIT-047** — Lacking `Audit.View` → audit-logs 403
48. **API-CRIT-048** — Lacking `Referral.View/Manage` → referrals/referring-doctors 403
49. **API-CRIT-049** — Permission claims in JWT are enforced server-side (forged extra `permission` claim without valid signature rejected — see Security)
50. **API-CRIT-050** — Role change / permission revoke takes effect on next token (or immediate if DB-backed checks exist)

### Cross-tenant isolation (IDOR)

51. **API-CRIT-051** — Tenant A JWT cannot `GET` Tenant B patient by id → 404/403
52. **API-CRIT-052** — Tenant A cannot list Tenant B patients in `GET /api/patients`
53. **API-CRIT-053** — Tenant A cannot `PATCH`/`DELETE` Tenant B patient
54. **API-CRIT-054** — Tenant A cannot get/patch Tenant B appointment
55. **API-CRIT-055** — Tenant A cannot get/send/assign Tenant B conversation
56. **API-CRIT-056** — Tenant A cannot download Tenant B conversation media / patient document / lab report
57. **API-CRIT-057** — Tenant A cannot read Tenant B audit logs
58. **API-CRIT-058** — Tenant A cannot get Tenant B hospital profile
59. **API-CRIT-059** — Tenant A cannot update Tenant B users/staff/campaigns/templates
60. **API-CRIT-060** — Creating resource with body `patientId`/`appointmentId` belonging to other tenant → 404/403 (not silent cross-write)
61. **API-CRIT-061** — Campaign audience preview never includes other-tenant patients
62. **API-CRIT-062** — RLS: raw SQL without tenant context returns zero rows; with tenant context only that tenant

### Patients & appointments

63. **API-CRIT-063** — Patient create returns created resource scoped to caller tenant
64. **API-CRIT-064** — Patient list supports filters `q` / status / department / tag and only returns caller tenant
65. **API-CRIT-065** — Patient get by id + holistic-view for own tenant
66. **API-CRIT-066** — Patient update persists allowed fields only
67. **API-CRIT-067** — Patient soft-delete hides from list/get for tenant
68. **API-CRIT-068** — Patient CSV import validates rows; rejects malformed; creates only in caller tenant
69. **API-CRIT-069** — Patient CSV import oversized / wrong content-type rejected
70. **API-CRIT-070** — Appointment booking-options returns doctors/schedules for tenant
71. **API-CRIT-071** — Appointment create with valid patient + schedule succeeds
72. **API-CRIT-072** — Appointment create with other-tenant patientId fails
73. **API-CRIT-073** — Appointment list/get scoped to tenant; datetime filters use hospital timezone day bounds
74. **API-CRIT-074** — Appointment patch updates allowed fields
75. **API-CRIT-075** — Appointment status transition valid path succeeds (e.g. Scheduled → Confirmed → Completed)
76. **API-CRIT-076** — Appointment status invalid transition → 400
77. **API-CRIT-077** — Appointment WhatsApp/email template placeholders format time in Tenant.Timezone (12h)

### Conversations & WhatsApp webhook

78. **API-CRIT-078** — Conversation list/get scoped to tenant + Conversation.View
79. **API-CRIT-079** — Conversation get by patient id scoped
80. **API-CRIT-080** — Send message requires Conversation.Manage; persists outbound message
81. **API-CRIT-081** — Assign conversation / add notes require Conversation.Manage
82. **API-CRIT-082** — Media upload rejects blocked MIME (e.g. SVG); accepts allowed types within size
83. **API-CRIT-083** — Media download only for messages in caller tenant
84. **API-CRIT-084** — `GET /api/whatsapp/webhook` verify challenge returns hub.challenge when token matches
85. **API-CRIT-085** — Webhook verify with wrong verify token → 403/401
86. **API-CRIT-086** — `POST /api/whatsapp/webhook` with valid signature accepted
87. **API-CRIT-087** — `POST /api/whatsapp/webhook` with invalid/missing signature rejected
88. **API-CRIT-088** — Inbound webhook routes to correct tenant by `phone_number_id`
89. **API-CRIT-089** — Inbound webhook for unknown phone_number_id does not leak/create wrong-tenant data
90. **API-CRIT-090** — Inbound message creates/updates conversation and triggers notification RBAC rules

---

## Missing — High

Prior audit High themes (19–30), expanded.

### Users / roles / staff

91. **API-HIGH-001** — `GET /api/users` list tenant-scoped with User.View
92. **API-HIGH-002** — `GET /api/users/{id}` own-tenant only
93. **API-HIGH-003** — `POST /api/users` create with role binding (snake_case / PascalCase)
94. **API-HIGH-004** — `PATCH`/`PUT /api/users/{id}` update profile fields
95. **API-HIGH-005** — Disable user blocks subsequent login
96. **API-HIGH-006** — Enable user restores login
97. **API-HIGH-007** — Reset password invalidates old credentials; new password works
98. **API-HIGH-008** — Assign-roles updates effective permissions on next session
99. **API-HIGH-009** — Assign-permissions grants/revokes fine-grained permissions
100. **API-HIGH-010** — Delete user soft-deletes / removes access; cannot delete last owner if protected
101. **API-HIGH-011** — Cannot assign protected `super_admin` role via API (RoleNameRules)
102. **API-HIGH-012** — Role list; create custom role; update; delete
103. **API-HIGH-013** — Cannot delete/rename protected system roles (`super_admin` / built-ins)
104. **API-HIGH-014** — `GET /api/permissions` catalog returns all CureFlowPermissions
105. **API-HIGH-015** — Staff list / doctors / nurses / get by id
106. **API-HIGH-016** — Staff create / patch / delete with Staff.* permissions
107. **API-HIGH-017** — Staff schedules CRUD; booking-options stay consistent after schedule change
108. **API-HIGH-018** — Staff profile cannot be created for other-tenant userId

### Clinical / visits / documents

109. **API-HIGH-019** — Clinical vitals create + list by patient
110. **API-HIGH-020** — Clinical notes create / list / patch
111. **API-HIGH-021** — Medical history create + list
112. **API-HIGH-022** — Family history create + list
113. **API-HIGH-023** — Cross-tenant patientId on any clinical write → 404/403
114. **API-HIGH-024** — Visit create / list / timeline for patient
115. **API-HIGH-025** — Visit get / patch / complete
116. **API-HIGH-026** — Prescription create + get + list by patient + print
117. **API-HIGH-027** — Allergy add / list / remove
118. **API-HIGH-028** — Lifestyle get / put for patient
119. **API-HIGH-029** — Lab report upload (Clinical.Edit) + download (Clinical.View)
120. **API-HIGH-030** — Lab report download blocked for other-tenant id
121. **API-HIGH-031** — Patient documents list / upload / download / delete
122. **API-HIGH-032** — Patient document upload rejects oversized / disallowed content-type
123. **API-HIGH-033** — Soft-deleted patient clinical data not writable via new visits

### Campaigns / onboarding / hospital / settings

124. **API-HIGH-034** — Campaign create / list / get / patch / delete
125. **API-HIGH-035** — Campaign preview-audience returns only tenant patients matching filters
126. **API-HIGH-036** — Campaign schedule + send (mocked WhatsApp provider)
127. **API-HIGH-037** — Campaign suggested-drafts shape + auth
128. **API-HIGH-038** — Campaign send without Campaign.Manage → 403
129. **API-HIGH-039** — Onboarding `POST profile` / `whatsapp` / `team` markers persist
130. **API-HIGH-040** — Onboarding `complete` gated until required steps done
131. **API-HIGH-041** — Onboarding endpoints require authenticated hospital user (not platform)
132. **API-HIGH-042** — Hospital profile get / put with Settings.View/Edit
133. **API-HIGH-043** — Hospital departments list (authorized)
134. **API-HIGH-044** — Hospital profile import URL uses SafeRemoteUrl (SSRF blocked for private IPs)
135. **API-HIGH-045** — Notification preferences get / put / reset for current user
136. **API-HIGH-046** — Role-defaults get/put require Settings.Edit
137. **API-HIGH-047** — Notifications list / unread-count / mark read / mark-all / delete scoped to recipient
138. **API-HIGH-048** — User A cannot mark-read / delete User B notifications
139. **API-HIGH-049** — Email settings status / get / save; secrets not returned in plaintext if policy requires
140. **API-HIGH-050** — SMS settings status / get / save
141. **API-HIGH-051** — Email inbox threads list / by patient / send (Conversation.* permissions)
142. **API-HIGH-052** — WhatsApp settings status / get / save (Settings.Edit); AccessToken not echoed raw

### Subscription / quotas (product gap — test when enforced)

143. **API-HIGH-053** — SeatLimit exceeded on user create → 400 ValidationException
144. **API-HIGH-054** — PatientLimit exceeded on patient create → 400
145. **API-HIGH-055** — MessagesQuotaMonthly exceeded on WhatsApp send / campaign → 400

---

## Missing — Medium

Prior audit Medium themes (31–42), expanded.

### Dashboard / tasks / templates / tags / audit

146. **API-MED-001** — `GET /api/dashboard/overview` shape + Dashboard.View
147. **API-MED-002** — `GET /api/dashboard/clinical-overview` requires Dashboard.View + Appointment.View + Clinical.View
148. **API-MED-003** — `GET /api/dashboard/missed-revenue` shape + auth
149. **API-MED-004** — Dashboard day ranges respect Tenant.Timezone
150. **API-MED-005** — Tasks create / list / patch / delete with Dashboard.View
151. **API-MED-006** — Tasks scoped to tenant; cannot patch other-tenant task
152. **API-MED-007** — Templates list / create / delete require Settings.*
153. **API-MED-008** — Template placeholders list / create / delete
154. **API-MED-009** — Tags list with Patient.View; tenant-scoped
155. **API-MED-010** — Audit logs list tenant-scoped; pagination/filter if present

### Referrals / AI / WhatsApp data & messaging / public / health / dev

156. **API-MED-011** — Referrals create (Referral.Manage) + analytics (Referral.View)
157. **API-MED-012** — Referring doctors CRUD + mark-contacted
158. **API-MED-013** — Referring doctor other-tenant id inaccessible
159. **API-MED-014** — AI `POST draft-reply` with mocked IAiService returns draft
160. **API-MED-015** — AI `POST summarize/{conversationId}` tenant-scoped conversation only
161. **API-MED-016** — AI endpoints without Conversation.View → 403
162. **API-MED-017** — WhatsApp data: templates / groups / campaigns / contacts / health (WhatsApp.View)
163. **API-MED-018** — WhatsApp `sendmessage` / `sendtemplatemessage` / `sendmedia` (mocked provider)
164. **API-MED-019** — WhatsApp `sendcampaigns` requires Campaign.Manage
165. **API-MED-020** — WhatsApp `makecontact` requires WhatsApp.Manage
166. **API-MED-021** — WhatsApp media GET by fileName tenant-scoped / path traversal rejected
167. **API-MED-022** — Public `GET /api/public/tenant/{slug}` returns safe public fields only (no secrets)
168. **API-MED-023** — Public `GET /api/public/tenant-context` anonymous behavior
169. **API-MED-024** — Public tenant endpoints do not expose AccessToken / AppSecret / password hashes
170. **API-MED-025** — `GET /api/health` anonymous 200
171. **API-MED-026** — Dev `POST seed-multi-hospitals` allowed in Development only
172. **API-MED-027** — Dev seed endpoint returns 404/403 outside Development/Production
173. **API-MED-028** — WhatsApp demo inbound / register-phone gated (disabled in Production or secret-gated)
174. **API-MED-029** — WhatsApp demo endpoints do not allow arbitrary tenant impersonation

### Validation / soft-delete / concurrency / misc service

175. **API-MED-030** — FluentValidation: CreatePatient invalid payload → 400 with field errors
176. **API-MED-031** — Invalid Guid route params → 400/404 consistently
177. **API-MED-032** — Soft-deleted entities excluded from default lists
178. **API-MED-033** — Idempotent webhook delivery (duplicate message id) does not duplicate rows
179. **API-MED-034** — Scheduler hosted services do not process other-tenant jobs when tenant context set
180. **API-MED-035** — CORS rejects disallowed origins in Production config
181. **API-MED-036** — Swagger UI disabled outside Development

---

## Missing — Security

Authn/authz, JWT tampering, IDOR, mass assignment, injection, rate limits, platform vs hospital separation, privilege escalation, and related controls.

### Authentication & session

182. **API-SEC-001** — Missing `Authorization` header on protected route → 401
183. **API-SEC-002** — Malformed Bearer token (not JWT) → 401
184. **API-SEC-003** — Empty Bearer token → 401
185. **API-SEC-004** — Expired JWT → 401 (ValidateLifetime)
186. **API-SEC-005** — JWT with future `nbf` → 401
187. **API-SEC-006** — JWT signed with wrong secret → 401
188. **API-SEC-007** — JWT `alg=none` attack rejected
189. **API-SEC-008** — JWT with wrong `iss` → 401
190. **API-SEC-009** — JWT with wrong `aud` → 401
191. **API-SEC-010** — Clock skew within 2 minutes accepted; beyond rejected
192. **API-SEC-011** — Login does not return password hash / internal secrets in body
193. **API-SEC-012** — Login response cookies (if any) are HttpOnly Secure SameSite appropriately — or confirm token is header-only
194. **API-SEC-013** — Disabled user token (issued before disable) rejected on subsequent requests if server re-validates — document expected behavior
195. **API-SEC-014** — Password stored with BCrypt; plaintext never persisted
196. **API-SEC-015** — Weak password policy enforced on register/reset (if configured)

### JWT tampering & claim forgery

197. **API-SEC-016** — Tamper `tenant_id` claim in otherwise valid JWT (re-sign fails / unsigned fails) → 401
198. **API-SEC-017** — Unsigned modification of `permission` claims → 401
199. **API-SEC-018** — Add forged `platform_user=true` to hospital JWT without re-sign → 401
200. **API-SEC-019** — Re-sign hospital JWT after flipping `platform_user` using stolen secret is out-of-band; with app secret, PlatformUser routes still must not grant tenant data access incorrectly — verify claim semantics
201. **API-SEC-020** — Elevate `role` claim to `super_admin` in tampered token → 401 without valid signature
202. **API-SEC-021** — Inject extra `permission` claims in tampered token → 401
203. **API-SEC-022** — Swap `sub` to another user id in tampered token → 401
204. **API-SEC-023** — Use JWT from Tenant A against Tenant B resources (valid signature, wrong tenant) → isolation failure expected as 404/403 (IDOR suite)
205. **API-SEC-024** — Replay old JTI after logout/rotation if revocation exists; else document absence of denylist
206. **API-SEC-025** — Algorithm confusion (HS256 key as RSA) rejected

### Platform vs hospital token separation

207. **API-SEC-026** — Platform JWT missing `tenant_id`; hospital JWT missing `platform_user`
208. **API-SEC-027** — Platform JWT cannot call `/api/patients`, `/api/appointments`, `/api/auth/me` hospital session
209. **API-SEC-028** — Hospital JWT cannot call `/api/platform/tenants` approve/reject/suspend/activate
210. **API-SEC-029** — Hospital JWT cannot call platform bootstrap/login as authenticated elevation
211. **API-SEC-030** — Mixing tokens: Authorization hospital + body claiming platform id ignored
212. **API-SEC-031** — TenantMiddleware sets platform context only for `platform_user` claim; hospital context only for `tenant_id`
213. **API-SEC-032** — Platform ops cannot read patient PHI via platform routes (no patient endpoints under platform)
214. **API-SEC-033** — Platform audit actions record PlatformUserId, not hospital UserId

### Authorization & privilege escalation

215. **API-SEC-034** — Receptionist cannot grant self User.Edit / assign-roles
216. **API-SEC-035** — User with User.Edit cannot assign permissions they do not hold (if policy exists) — or document full admin capability
217. **API-SEC-036** — Cannot create user with role `super_admin` / `platform_ops`
218. **API-SEC-037** — Cannot rename custom role to protected name
219. **API-SEC-038** — Cannot delete last TenantOwner / super_admin in tenant
220. **API-SEC-039** — Vertical escalation: Viewer role hitting Clinical.Edit endpoints → 403
221. **API-SEC-040** — Horizontal escalation: Doctor A accessing Doctor B’s private notes if product isolates by author — document rule
222. **API-SEC-041** — Marketing role cannot access Clinical.* or Patient.Delete
223. **API-SEC-042** — Assign-permissions endpoint rejects unknown permission strings
224. **API-SEC-043** — Onboarding complete does not auto-grant Settings.Edit beyond owner defaults
225. **API-SEC-044** — Dev seed endpoint cannot be used to create platform admin in Production

### Cross-tenant IDOR (security-focused)

226. **API-SEC-045** — IDOR: sequential/guessable Guid still blocked across tenants for patients
227. **API-SEC-046** — IDOR: appointments, conversations, messages, media, documents, lab reports, prescriptions, visits, staff, users, campaigns, templates, tasks, referrals, audit logs
228. **API-SEC-047** — IDOR: create child resource referencing foreign parent id (vitals for other-tenant patient)
229. **API-SEC-048** — IDOR: notification id from other user/tenant
230. **API-SEC-049** — IDOR: WhatsApp media `{fileName}` path traversal (`../`) and cross-tenant filenames
231. **API-SEC-050** — IDOR: email inbox thread by other-tenant patientId
232. **API-SEC-051** — Bulk list endpoints never include other-tenant rows even with crafted filter query params
233. **API-SEC-052** — Export/CSV if any never includes other-tenant PHI

### Mass assignment

234. **API-SEC-053** — Patient create/update ignores client-supplied `TenantId`, `Id`, `IsDeleted`, `CreatedAt`
235. **API-SEC-054** — User create ignores `TenantId`, `PasswordHash`, `IsPlatform`, elevated flags
236. **API-SEC-055** — Appointment create ignores `TenantId` / forged status history
237. **API-SEC-056** — Hospital profile update ignores subscription limits / SeatLimit / billing fields if not admin-platform
238. **API-SEC-057** — WhatsApp settings update cannot set another tenant’s row via id in body
239. **API-SEC-058** — Campaign create ignores `TenantId` and forced Sent status
240. **API-SEC-059** — Role/permission assign body cannot inject platform_ops
241. **API-SEC-060** — JSON merge-patch cannot overwrite audit fields (`CreatedBy`, `UpdatedBy`)

### Injection (SQL / NoSQL / command / LDAP / template)

242. **API-SEC-061** — Patient search `q` with SQL meta-characters does not break query / leak rows
243. **API-SEC-062** — Filter query params (`status`, `department`, `tag`) parameterized — no raw concatenation exploit
244. **API-SEC-063** — Dapper dynamic SQL fragments (`AnyArrayParameter`) safe against array injection
245. **API-SEC-064** — Webhook JSON payload with malicious nested fields does not cause SQL injection
246. **API-SEC-065** — Template placeholder / campaign message body does not evaluate server-side code
247. **API-SEC-066** — AI prompt injection via conversation history does not exfiltrate other-tenant data (mocked boundary)
248. **API-SEC-067** — File upload filename with path traversal stored safely
249. **API-SEC-068** — Header injection via `Name`/`Email` fields does not poison logs/email headers unsafely
250. **API-SEC-069** — Postgres `set_config` tenant id cannot be overridden by client header spoofing

### SSRF / unsafe egress

251. **API-SEC-070** — Hospital import URL `http://127.0.0.1` blocked
252. **API-SEC-071** — Import URL `http://169.254.169.254` (cloud metadata) blocked
253. **API-SEC-072** — Import URL DNS rebinding / private IP after resolve blocked
254. **API-SEC-073** — Import only allows https (or documented http policy)
255. **API-SEC-074** — Bounded response size abort on huge remote document
256. **API-SEC-075** — WhatsApp media relay fetch (if any) uses SafeRemoteUrl policy

### Rate limiting & brute force

257. **API-SEC-076** — `POST /api/auth/login` rate limit policy `login`: 10/min/IP (non-relaxed) → 429
258. **API-SEC-077** — `POST /api/platform/auth/login` same `login` policy → 429 after limit
259. **API-SEC-078** — `POST /api/auth/register-tenant` policy: 3/10min/IP → 429
260. **API-SEC-079** — `POST /api/platform/auth/bootstrap` policy `platform-bootstrap`: 3/10min/IP → 429
261. **API-SEC-080** — Rate limit RejectionStatusCode is 429
262. **API-SEC-081** — `RateLimiting:Relaxed` / Development raises limits; Production enforces strict limits
263. **API-SEC-082** — Rate limit keyed by remote IP (proxy: document X-Forwarded-For trust behavior)
264. **API-SEC-083** — No rate limit bypass by varying User-Agent alone
265. **API-SEC-084** — Webhook endpoint not globally rate-limited into DoS of Meta retries — document expected behavior

### WhatsApp webhook security

266. **API-SEC-085** — Webhook signature HMAC validation required for POST
267. **API-SEC-086** — Replay of old signed payload: document idempotency / timestamp tolerance
268. **API-SEC-087** — Webhook GET verify token constant-time compare (timing-safe)
269. **API-SEC-088** — Webhook does not expose internal errors / stack traces
270. **API-SEC-089** — Demo inbound endpoints disabled or secret-gated in Production
271. **API-SEC-090** — App secret / access token never logged in plain text

### Secrets & sensitive data exposure

272. **API-SEC-091** — API responses never return `PasswordHash`, `AccessTokenEncrypted` plaintext, `AppSecret`
273. **API-SEC-092** — Error responses in Production omit stack traces / connection strings
274. **API-SEC-093** — Public tenant endpoint strips secrets
275. **API-SEC-094** — Jwt:Secret placeholder rejected at startup in Production (<32 chars / REPLACE-WITH)
276. **API-SEC-095** — Health endpoint does not dump config secrets
277. **API-SEC-096** — Audit log entries do not store full PHI payloads beyond policy
278. **API-SEC-097** — Swagger in non-Dev does not expose internal DTOs with secrets

### File upload / media security

279. **API-SEC-098** — SVG / HTML / executable MIME rejected on WhatsApp and document uploads
280. **API-SEC-099** — Oversized upload rejected (policy limits)
281. **API-SEC-100** — Empty file rejected
282. **API-SEC-101** — Content-Type mismatch / polyglot file handled safely
283. **API-SEC-102** — Downloaded media Content-Disposition does not allow XSS via filename
284. **API-SEC-103** — Stored files not served as executable from API host

### CSRF / CORS / HTTP security headers

285. **API-SEC-104** — CORS Production: only configured origins; no `*` with credentials
286. **API-SEC-105** — Preflight for disallowed origin fails
287. **API-SEC-106** — State-changing APIs are Bearer-token based (CSRF low risk); document if cookie auth ever added
288. **API-SEC-107** — HTTPS redirection / HSTS when behind TLS terminator (config check)
289. **API-SEC-108** — Security headers present if middleware configured (X-Content-Type-Options, etc.) — document gaps

### Multi-tenant RLS & data-layer defense-in-depth

290. **API-SEC-109** — EF/Dapper global filters include TenantId for tenant entities
291. **API-SEC-110** — `IgnoreQueryFilters` / `IgnoreTenant` paths are explicit and tested for admin-only use
292. **API-SEC-111** — RLS policy denies rows when `app.current_tenant_id` unset
293. **API-SEC-112** — RLS policy returns only matching tenant when set
294. **API-SEC-113** — Setting tenant context to Tenant B while using Tenant A JWT is impossible from HTTP (middleware overwrites)
295. **API-SEC-114** — Cross-tenant INSERT blocked by RLS even if application bug omitted TenantId

### Input abuse & DoS

296. **API-SEC-115** — Extremely large JSON body rejected (Kestrel/form limits)
297. **API-SEC-116** — Deeply nested JSON does not crash serializer
298. **API-SEC-117** — CSV import bomb (million rows) rejected or capped
299. **API-SEC-118** — Campaign send to huge audience respects quota / batching limits
300. **API-SEC-119** — Regex/search ReDoS on patient `q` if any regex used

### Business logic security

301. **API-SEC-120** — Suspended tenant token (if still unexpired) cannot mutate data
302. **API-SEC-121** — Pending tenant cannot access PHI APIs
303. **API-SEC-122** — Rejected tenant cannot re-register same slug without platform action
304. **API-SEC-123** — User cannot disable/delete self if it orphan-locks tenant (policy)
305. **API-SEC-124** — Password reset for other-tenant user id fails
306. **API-SEC-125** — Assign-roles to other-tenant user id fails
307. **API-SEC-126** — Platform approve of non-existent tenant → 404
308. **API-SEC-127** — Idempotent approve does not double-side-effect dangerously

---

## Placement guide (Unit vs Integration / HTTP)

| Stay / add in **UnitTest** | Move / add in future **IntegrationTest** + HTTP |
|----------------------------|--------------------------------------------------|
| Pure helpers already covered (slug, phone, media policy, time, SafeRemoteUrl mocked DNS, notification resolvers mocked, webhook signature unit, role name rules, binding) | All `CUREFLOW_TEST_CONNECTION` / `DatabaseIntegration` suites |
| New pure unit: validators, JWT claim builder helpers, permission catalog | WebApplicationFactory HTTP for API-CRIT / API-HIGH / API-MED / API-SEC |
| | Testcontainers PostgreSQL + RLS |
| | Platform approve → hospital login E2E |
| | Webhook processor against real DB |
| | Rate-limit HTTP 429 tests |

---

## Count totals

| Category | Count |
|----------|------:|
| Existing Fact/Theory | **119** |
| Missing Critical (`API-CRIT-*`) | **90** (001–090) |
| Missing High (`API-HIGH-*`) | **55** (001–055) |
| Missing Medium (`API-MED-*`) | **36** (001–036) |
| Missing Security (`API-SEC-*`) | **127** (001–127) |
| **Total catalogued missing scenarios** | **308** |
| Product controllers | **41** |
| Product HTTP endpoints | **~170** |
| Existing HTTP `WebApplicationFactory` suites | **0** |

---

## Recommended implementation order

1. Scaffold IntegrationTest + `WebApplicationFactory`; relocate DB isolation suites  
2. Auth + platform lifecycle (`API-CRIT-001`–`032`) + rate limits (`API-SEC-076`–`081`)  
3. JWT / platform-hospital separation / privilege escalation (`API-SEC-001`–`044`)  
4. Permission matrix + cross-tenant IDOR (`API-CRIT-033`–`062`, `API-SEC-045`–`052`)  
5. Patients + appointments + conversations + webhook (`API-CRIT-063`–`090`)  
6. Users/staff/clinical/campaigns High set  
7. Medium surface + remaining SEC (SSRF, uploads, mass assignment, injection)
