# UI Test Scenarios

Catalog of Cure-Flow Playwright / UI coverage: existing summary counts, plus **missing** Critical / High / Medium / Security scenarios.

**Source of truth for existing coverage:** prior UI gap analysis (2026-07-19) vs `dotnet-frontend` app surface and `dotnet-frontend-test/e2e`.  
**Status of this file:** catalog only — scenarios are **not** implemented here.

Stable IDs:

| Prefix | Meaning |
|--------|---------|
| `UI-CRIT-###` | Missing critical functional / mutation flows |
| `UI-HIGH-###` | Missing high-value depth / lifecycle / settings flows |
| `UI-MED-###` | Missing medium polish, mobile, a11y-behavior, analytics edges |
| `UI-SEC-###` | Missing security / abuse / isolation UI scenarios |

---

## Existing (summary counts only)

| Bucket | Approx. count | Notes |
|--------|--------------:|-------|
| Functional matrix (`*-matrix.spec.js`) | **~2,112–2,130** | Role × route × viewport × filter / search / columns |
| Design matrix (`e2e/design/*`, excl. screenshot-regression) | **~1,896** | Layout, glass, typography, a11y-design, components |
| Focused / smoke / domain / lifecycle / walkthrough | **~180–220** | Named cases in non-matrix specs |
| **Existing total (Playwright scenarios)** | **~4,200** | Breadth strong; mutation depth weak |

### Existing coverage by area (summary only)

| Area | Existing (summary) |
|------|--------------------|
| Auth & session | Login validation, API-session login/logout × roles, unauth redirects |
| Tenant lifecycle / onboarding / platform | Signup → pending → approve → onboard; setup links; multi-hospital isolation smoke |
| Navigation / shell / roles | Route×role×viewport loads; nav visibility; forbidden routes; AppShell |
| Dashboard | Period filters, widgets, doctor clinical vs ops, consultation happy path |
| Patients | Filters, search, detail tabs, create/edit basics, visit chart |
| Appointments | Create, kanban columns, timezone/hospital-local time |
| Tasks / follow-ups | Search, columns, create + complete one path |
| Referrals / doctors | Search, detail tabs, log referral, revenue badges |
| Campaigns | Draft create, column chrome, shallow detail crawl |
| Inbox / email / notifications | Search + page load; bell; prefs page; integrations panels |
| Staff / settings / analytics | Search, create user/staff, timezone save, analytics API match |
| Mobile / a11y / design | ~7 mobile pages; labels; tokens; glass; design matrices |

### Weak / shallow (exists but not deep)

| Area | Gap theme |
|------|-----------|
| Matrix suites | Load/visibility; rarely assert mutations or API side effects |
| Inbox | No send / reply / attach / read-unread |
| Appointments | No drag status, cancel, edit dialog, no-show |
| Campaigns | No schedule / send / cancel |
| Settings roles / templates / integrations | Route + inputs; little persist/reload |
| Platform | Approve covered; reject / suspend / activate missing |
| CRUD dialogs matrix | Opens only; does not submit |
| Mobile | Missing inbox, campaigns, tasks, referrals, platform |

---

## Missing — Critical

Prior gap-analysis titles are included verbatim; additional related Critical scenarios expand the same areas.

### WhatsApp inbox

1. **UI-CRIT-001** — admin: open conversation, send text message, message appears in thread
2. **UI-CRIT-002** — reception: send WhatsApp from patient detail panel
3. **UI-CRIT-003** — nurse: cannot send WhatsApp (compose/send hidden)
4. **UI-CRIT-004** — admin: attach file and send (or clear error if policy blocks)
5. **UI-CRIT-005** — marketing: can send WhatsApp when Conversation/WhatsApp.Send granted
6. **UI-CRIT-006** — staff: WhatsApp send hidden when WhatsApp.Send not granted
7. **UI-CRIT-007** — admin: mark conversation read/unread reflects in list badge
8. **UI-CRIT-008** — admin: empty compose cannot send (validation / disabled send)
9. **UI-CRIT-009** — admin: send fails gracefully when WhatsApp integration not configured
10. **UI-CRIT-010** — admin: conversation search opens matching thread and thread history loads

### Appointments mutations

11. **UI-CRIT-011** — drag card Scheduled → Confirmed updates status via API
12. **UI-CRIT-012** — mark completed from card action
13. **UI-CRIT-013** — cancel appointment confirms and moves to Cancelled
14. **UI-CRIT-014** — edit appointment dialog saves doctor/time/status
15. **UI-CRIT-015** — doctor filter Mine vs All Doctors changes board
16. **UI-CRIT-016** — mark no-show updates status and appears in analytics/missed-revenue when applicable
17. **UI-CRIT-017** — reception: create appointment with required fields validation errors
18. **UI-CRIT-018** — nurse without Appointment.Edit cannot drag status or open edit
19. **UI-CRIT-019** — conflict / double-book UI warning when overlapping slot (if product supports)
20. **UI-CRIT-020** — delete appointment (or soft-delete) with confirm dialog when permitted

### Campaigns lifecycle

21. **UI-CRIT-021** — campaign detail: schedule with datetime → Scheduled column
22. **UI-CRIT-022** — campaign detail: send now → confirm dialog → Active/Completed
23. **UI-CRIT-023** — cancel scheduled campaign
24. **UI-CRIT-024** — preview audience from new-campaign dialog
25. **UI-CRIT-025** — marketing: create draft → schedule → cancel full lifecycle
26. **UI-CRIT-026** — nurse: campaign manage actions hidden / forbidden
27. **UI-CRIT-027** — campaign send blocked when WhatsApp credentials missing (error UI)
28. **UI-CRIT-028** — campaign body placeholder insertion persists in draft save

### Platform ops

29. **UI-CRIT-029** — platform: reject pending tenant with reason
30. **UI-CRIT-030** — platform: suspend active hospital
31. **UI-CRIT-031** — platform: re-activate suspended hospital
32. **UI-CRIT-032** — pending-approval: Check approval status after approve navigates off waiting room
33. **UI-CRIT-033** — platform: pending tenant remains blocked from CRM until approved
34. **UI-CRIT-034** — platform: suspended hospital admin sees blocked/error state on login
35. **UI-CRIT-035** — platform console tenant list filters/search finds hospital by name
36. **UI-CRIT-036** — platform ops cannot approve with empty reject-reason when reject chosen

### Settings users

37. **UI-CRIT-037** — edit user role and name → row reflects change
38. **UI-CRIT-038** — reset user password dialog succeeds
39. **UI-CRIT-039** — user without User.Edit cannot open edit dialog
40. **UI-CRIT-040** — create user with each API role (admin/doctor/reception/nurse/marketing/staff)
41. **UI-CRIT-041** — deactivate / delete user with confirm → user cannot login
42. **UI-CRIT-042** — duplicate email create shows validation error
43. **UI-CRIT-043** — reception: settings users route forbidden

### Roles / permissions

44. **UI-CRIT-044** — create custom role with selected permissions and save
45. **UI-CRIT-045** — toggle permission on existing role and persist after reload
46. **UI-CRIT-046** — permissions catalog lists canonical codes
47. **UI-CRIT-047** — removing Patient.View from role hides Patients nav after re-login
48. **UI-CRIT-048** — custom role assigned to user gates feature buttons correctly
49. **UI-CRIT-049** — cannot save role with empty name / invalid characters
50. **UI-CRIT-050** — permissions page read-only for user without User.Edit (if applicable)

### Auth / session (critical depth)

51. **UI-CRIT-051** — login with wrong tenant slug / hospital context fails or isolates correctly
52. **UI-CRIT-052** — expired JWT redirects to login and clears protected UI
53. **UI-CRIT-053** — concurrent logout from another tab clears session on next navigation
54. **UI-CRIT-054** — onboarding incomplete: CRM routes remain blocked after refresh

---

## Missing — High

### Email inbox

55. **UI-HIGH-001** — open email thread and reply when SMTP configured
56. **UI-HIGH-002** — email compose disabled state when integration off
57. **UI-HIGH-003** — search threads returns expected subject
58. **UI-HIGH-004** — email compose validation (empty To / subject)
59. **UI-HIGH-005** — mark email thread read updates unread indicator
60. **UI-HIGH-006** — reception can open email inbox; marketing without Conversation.View cannot

### Doctors / referrals

61. **UI-HIGH-007** — create referring doctor via UI (new-doctor dialog → appears in kanban)
62. **UI-HIGH-008** — referral category filter narrows list
63. **UI-HIGH-009** — staff role can log referral; nurse cannot access /doctors
64. **UI-HIGH-010** — edit referring doctor profile persists after reload
65. **UI-HIGH-011** — doctor detail: log referral requires patient selection validation
66. **UI-HIGH-012** — doctor detail revenue tab matches API totals after new referral
67. **UI-HIGH-013** — delete / archive referring doctor with confirm (if supported)

### Templates

68. **UI-HIGH-014** — create WhatsApp template → row visible
69. **UI-HIGH-015** — edit template body and save
70. **UI-HIGH-016** — delete/archive template
71. **UI-HIGH-017** — template placeholder tokens insert into body
72. **UI-HIGH-018** — template list forbidden for role without Settings.View
73. **UI-HIGH-019** — invalid template body (empty name/body) shows validation

### Integrations

74. **UI-HIGH-020** — save WhatsApp credentials/panel fields (or validation errors)
75. **UI-HIGH-021** — SMS/Email panel toggle/save
76. **UI-HIGH-022** — integrations page forbidden for reception (deepen save)
77. **UI-HIGH-023** — WhatsApp access token field masked; blank leave keeps existing
78. **UI-HIGH-024** — verify token / phone number id validation errors surface in UI
79. **UI-HIGH-025** — after saving integrations, inbox send path unblocks (smoke)

### Hospital settings

80. **UI-HIGH-026** — add/edit department in hospital settings
81. **UI-HIGH-027** — save hospital profile name/phone (beyond timezone)
82. **UI-HIGH-028** — hospital timezone change reflects on appointments wall-clock
83. **UI-HIGH-029** — delete department blocked when patients/staff assigned (error UI)
84. **UI-HIGH-030** — hospital settings forbidden for nurse / marketing

### Patient mutations

85. **UI-HIGH-031** — delete patient (or soft-delete) with confirm
86. **UI-HIGH-032** — change patient status from detail
87. **UI-HIGH-033** — create appointment from patient Today tab
88. **UI-HIGH-034** — edit patient demographics persist across all detail tabs
89. **UI-HIGH-035** — patient without Patient.Delete: delete action hidden
90. **UI-HIGH-036** — upload patient document succeeds and appears in documents list
91. **UI-HIGH-037** — lifestyle / clinical fields save for permitted roles only
92. **UI-HIGH-038** — duplicate patient phone/email warning (if product supports)

### Tasks

93. **UI-HIGH-039** — drag follow-up Pending → In Progress
94. **UI-HIGH-040** — mark overdue task complete
95. **UI-HIGH-041** — empty state New Task button creates card
96. **UI-HIGH-042** — edit task assignee/due date saves
97. **UI-HIGH-043** — delete task with confirm
98. **UI-HIGH-044** — task search finds by patient name and filters board

### Staff

99. **UI-HIGH-045** — edit staff profile
100. **UI-HIGH-046** — delete/deactivate staff
101. **UI-HIGH-047** — clinical-only role sees staff list
102. **UI-HIGH-048** — create staff with department assignment
103. **UI-HIGH-049** — staff without Staff.Edit cannot open edit
104. **UI-HIGH-050** — marketing cannot access /staff

### Lifecycle pages

105. **UI-HIGH-051** — registration-received page after signup
106. **UI-HIGH-052** — signup validation errors (duplicate email, weak password)
107. **UI-HIGH-053** — pending-approval logout clears session
108. **UI-HIGH-054** — signup required fields empty submission blocked
109. **UI-HIGH-055** — onboarding: profile step mark done persists after refresh
110. **UI-HIGH-056** — onboarding: WhatsApp step deep-link returns to setup hub
111. **UI-HIGH-057** — onboarding: team step deep-link returns to setup hub
112. **UI-HIGH-058** — finish onboarding unlocks dashboard and sidebar CRM nav

### Multi-tenant UI

113. **UI-HIGH-059** — tenant A cannot open tenant B patient detail URL in browser (redirect/404/empty)
114. **UI-HIGH-060** — hospital badge shows correct name after login switch
115. **UI-HIGH-061** — tenant A cannot open tenant B doctor / campaign / appointment detail URLs
116. **UI-HIGH-062** — tenant A list APIs via UI never render tenant B rows after navigation
117. **UI-HIGH-063** — platform tenants page lists only platform-visible hospitals
118. **UI-HIGH-064** — hospital admin cannot open /platform routes

### Dashboard / notifications (high depth)

119. **UI-HIGH-065** — notification bell: mark all read clears badge
120. **UI-HIGH-066** — notification click navigates to related entity
121. **UI-HIGH-067** — dashboard stat card links land on filtered lists
122. **UI-HIGH-068** — AI insights / upcoming appointments refresh after create appointment
123. **UI-HIGH-069** — command palette patient search opens patient detail

### Users / roles (high depth)

124. **UI-HIGH-070** — assign custom role then verify feature button matrix for that user
125. **UI-HIGH-071** — password reset forces new login with new password
126. **UI-HIGH-072** — self-edit restrictions (cannot demote last admin / own critical role) if enforced in UI

---

## Missing — Medium

### Mobile

127. **UI-MED-001** — mobile: WhatsApp inbox conversation + compose reachable
128. **UI-MED-002** — mobile: campaigns kanban horizontal scroll
129. **UI-MED-003** — mobile: settings roles/permissions usable
130. **UI-MED-004** — mobile: referral CRM + doctor detail
131. **UI-MED-005** — mobile: email inbox
132. **UI-MED-006** — mobile: tasks / follow-ups kanban usable
133. **UI-MED-007** — mobile: platform tenants console usable
134. **UI-MED-008** — mobile: patient detail tabs scroll without clipping CTAs
135. **UI-MED-009** — mobile: create patient dialog keyboard does not obscure submit
136. **UI-MED-010** — tablet viewport: appointments edit dialog usable

### Command palette

137. **UI-MED-011** — ⌘K / button jumps to Patients/Appointments/Settings
138. **UI-MED-012** — palette finds appointment/doctor
139. **UI-MED-013** — palette Escape closes without navigation
140. **UI-MED-014** — palette arrow-key selection + Enter navigates
141. **UI-MED-015** — palette hidden routes not suggested for forbidden roles

### Notifications prefs

142. **UI-MED-016** — toggle category and Save persists after reload
143. **UI-MED-017** — Role defaults tab saves for admin
144. **UI-MED-018** — My prefs vs Role defaults tab switch preserves unsaved warning (if any)
145. **UI-MED-019** — non-admin can open /notifications/preferences and save own prefs
146. **UI-MED-020** — settings notifications page scroll + save on desktop and mobile

### Analytics

147. **UI-MED-021** — click section drills or links to appointments/patients
148. **UI-MED-022** — empty analytics state when no no-shows
149. **UI-MED-023** — analytics period filter updates totals
150. **UI-MED-024** — analytics mobile layout numbers match desktop for same filters
151. **UI-MED-025** — nurse/marketing analytics access matches permission matrix

### Campaign detail

152. **UI-MED-026** — suggested draft opens prefilled new campaign
153. **UI-MED-027** — campaign detail forbidden for nurse
154. **UI-MED-028** — campaign detail shows audience/status history after schedule
155. **UI-MED-029** — campaign detail back navigation returns to kanban with same filters

### Clinical

156. **UI-MED-030** — nurse: can edit vitals/allergies; cannot open WhatsApp
157. **UI-MED-031** — prescription print preview opens
158. **UI-MED-032** — complete consultation updates appointment status (partially in doctor-dashboard — split/assert edges)
159. **UI-MED-033** — doctor: clinical dashboard Mine scope excludes other doctors’ charts
160. **UI-MED-034** — reception cannot open clinical edit fields reserved for Clinical.Edit
161. **UI-MED-035** — visit chart paper note upload invalid file type shows error
162. **UI-MED-036** — consultation cancel mid-flow leaves appointment in expected status

### Design / a11y (behavior)

163. **UI-MED-037** — focus trap in create dialogs
164. **UI-MED-038** — Escape closes dialogs
165. **UI-MED-039** — settings nav keyboard order
166. **UI-MED-040** — tab order through login form is logical
167. **UI-MED-041** — dialog initial focus on first field / close button pattern
168. **UI-MED-042** — aria-live announces toast/error after failed save
169. **UI-MED-043** — icon-only buttons on patient detail have accessible names
170. **UI-MED-044** — reduced-motion: no broken layout when animations disabled (if supported)

### Empty / error / edge UI

171. **UI-MED-045** — patients empty state CTA opens new patient
172. **UI-MED-046** — appointments empty board messaging when no results for filter
173. **UI-MED-047** — API 500 on patients list shows recoverable error UI
174. **UI-MED-048** — network offline toast / retry on dashboard load
175. **UI-MED-049** — pagination next/prev updates list and URL/query if used
176. **UI-MED-050** — long patient name / Unicode name renders without overflow break

### Billing (conditional)

177. **UI-MED-051** — *No Billing UI route found* — skip unless product adds screens; permissions exist in `permissions.js` only
178. **UI-MED-052** — if Billing UI added later: Billing.View gates route; create/edit/delete map to permissions

### Misc polish

179. **UI-MED-053** — sidebar collapse preference persists across reload
180. **UI-MED-054** — hospital timezone dismiss banner stays dismissed in session
181. **UI-MED-055** — breadcrumb on patient detail shows Profile / expected trail
182. **UI-MED-056** — deep link to /settings/integrations after login lands correctly
183. **UI-MED-057** — logout from any settings subpage returns to /login
184. **UI-MED-058** — staff search no-results empty state
185. **UI-MED-059** — doctors kanban column empty state
186. **UI-MED-060** — campaigns suggested drafts section visibility for marketing only when drafts exist

---

## Missing — Security

Auth tokens are stored in `localStorage` (`cureflow_token`, `cureflow_user`, `cureflow_tenant`). Prefer Bearer JWT over cookies — CSRF applicability noted where relevant.

### XSS in forms / rendered content

187. **UI-SEC-001** — XSS in patient name field: script payload stored and not executed on list/detail
188. **UI-SEC-002** — XSS in patient notes / clinical free-text: not executed on render
189. **UI-SEC-003** — XSS in appointment notes: not executed in kanban card / dialog
190. **UI-SEC-004** — XSS in task title/description: not executed on board
191. **UI-SEC-005** — XSS in campaign title/body: not executed on kanban/detail/preview
192. **UI-SEC-006** — XSS in WhatsApp compose / template body: not executed in thread UI
193. **UI-SEC-007** — XSS in email subject/body: not executed in thread view
194. **UI-SEC-008** — XSS in referring doctor name/notes: not executed on CRM pages
195. **UI-SEC-009** — XSS in hospital name/department labels: not executed in shell badge/settings
196. **UI-SEC-010** — XSS in user display name: not executed in users table / shell
197. **UI-SEC-011** — XSS in notification message content: not executed in bell dropdown
198. **UI-SEC-012** — XSS via URL query/hash reflected into page title or filters without encoding
199. **UI-SEC-013** — SVG/HTML upload as patient document does not execute script when previewed
200. **UI-SEC-014** — markdown/HTML in campaign placeholders does not break out of text context

### CSRF (if applicable)

201. **UI-SEC-015** — CSRF: cross-origin form POST cannot mutate state using cookie session (N/A if Bearer-only — assert no cookie auth for API)
202. **UI-SEC-016** — CSRF: forged request from attacker origin with victim cookies fails when SameSite/CSRF token required
203. **UI-SEC-017** — state-changing UI actions send Authorization header, not rely on ambient cookies alone
204. **UI-SEC-018** — logout invalidates server session/token so stolen CSRF window closes (if cookie path exists)

### Auth bypass via URL

205. **UI-SEC-019** — unauthenticated direct URL to /patients redirects to login
206. **UI-SEC-020** — unauthenticated direct URL to /patients/:id redirects to login
207. **UI-SEC-021** — unauthenticated direct URL to /settings/users redirects to login
208. **UI-SEC-022** — unauthenticated direct URL to /settings/integrations redirects to login
209. **UI-SEC-023** — unauthenticated direct URL to /inbox redirects to login
210. **UI-SEC-024** — unauthenticated direct URL to /campaigns/:id redirects to login
211. **UI-SEC-025** — unauthenticated direct URL to /platform and /platform/tenants redirects to platform login
212. **UI-SEC-026** — unauthenticated deep link returns to intended route only after successful login (open redirect safe)
213. **UI-SEC-027** — open redirect: login `returnUrl`/`next` to external domain is rejected
214. **UI-SEC-028** — pending-approval user cannot bypass waiting room via direct / or /patients URL
215. **UI-SEC-029** — onboarding-incomplete user cannot bypass via direct CRM URLs

### Role escalation (UI)

216. **UI-SEC-030** — nurse crafts /settings/roles URL → forbidden / redirect, no role editor
217. **UI-SEC-031** — reception crafts /settings/integrations URL → forbidden
218. **UI-SEC-032** — marketing crafts /settings/users URL → forbidden
219. **UI-SEC-033** — staff crafts /appointments manage actions → create/edit hidden or API 403 surfaced
220. **UI-SEC-034** — nurse cannot reveal WhatsApp send by DOM toggle / forcing compose visible then submit (API 403)
221. **UI-SEC-035** — user without User.Create: new-user button hidden; forced POST via UI tools fails
222. **UI-SEC-036** — user without Patient.Delete: delete control absent; forced delete fails with error UI
223. **UI-SEC-037** — custom role stripped of Campaign.Manage cannot schedule/send even if UI briefly shows
224. **UI-SEC-038** — edit own user to `admin` via tampered client payload is rejected (UI shows error)
225. **UI-SEC-039** — permissions catalog UI does not allow granting platform-only privileges to hospital roles
226. **UI-SEC-040** — hospital admin cannot open platform ops by navigating to /platform after hospital login

### Session after logout

227. **UI-SEC-041** — logout clears `cureflow_token`, `cureflow_user`, `cureflow_tenant` from localStorage
228. **UI-SEC-042** — after logout, Back button cannot show authenticated patient PHI
229. **UI-SEC-043** — after logout, in-memory React state does not keep serving protected routes
230. **UI-SEC-044** — after logout, API calls from lingering page receive 401 and redirect to login
231. **UI-SEC-045** — logout on one tab invalidates subsequent actions on another tab
232. **UI-SEC-046** — pending-approval logout clears session (same as UI-HIGH-053; assert storage keys gone)
233. **UI-SEC-047** — platform logout does not leave hospital token usable on /platform
234. **UI-SEC-048** — re-login as different user does not flash previous user’s data (no stale cache)

### IDOR / cross-tenant URLs

235. **UI-SEC-049** — IDOR patient URLs: tenant A token + tenant B `/patients/{id}` shows empty/404/forbidden, never PHI
236. **UI-SEC-050** — IDOR doctor detail: cross-tenant `/doctors/{id}` blocked in UI
237. **UI-SEC-051** — IDOR campaign detail: cross-tenant `/campaigns/{id}` blocked in UI
238. **UI-SEC-052** — IDOR appointment deep links (if any) blocked across tenants
239. **UI-SEC-053** — IDOR staff profile IDs blocked across tenants
240. **UI-SEC-054** — IDOR user edit dialog cannot load other-tenant user by id
241. **UI-SEC-055** — sequential ID guessing in address bar does not leak other patients’ names in title/breadcrumb
242. **UI-SEC-056** — WhatsApp conversation id from tenant B not openable in tenant A inbox
243. **UI-SEC-057** — email thread id from tenant B not openable in tenant A email inbox
244. **UI-SEC-058** — document download URL for other-tenant patient fails in UI

### Forbidden route tampering

245. **UI-SEC-059** — forbidden route tampering: nurse → /campaigns redirected or empty shell, no campaign data
246. **UI-SEC-060** — forbidden route tampering: nurse → /settings/* blocked
247. **UI-SEC-061** — forbidden route tampering: marketing → /doctors blocked
248. **UI-SEC-062** — forbidden route tampering: staff → /settings/users blocked
249. **UI-SEC-063** — forbidden route tampering: reception → /settings/roles blocked
250. **UI-SEC-064** — client-side nav item removal cannot be undone by typing URL to gain data (API 403)
251. **UI-SEC-065** — Prefetch/hidden routes do not leak JSON into UI for unauthorized roles
252. **UI-SEC-066** — role switch simulation by editing `cureflow_user.role` in localStorage does not elevate permissions without valid JWT claims

### Sensitive data in localStorage / client storage

253. **UI-SEC-067** — sensitive data in localStorage: JWT present but password never stored
254. **UI-SEC-068** — WhatsApp access tokens / API tokens not persisted in plaintext localStorage after integrations save
255. **UI-SEC-069** — `cureflow_user` does not contain password hashes or refresh secrets beyond needed profile fields
256. **UI-SEC-070** — browser logs / logger localStorage buffer does not store full PHI message bodies by default
257. **UI-SEC-071** — sessionStorage timezone dismiss flag contains no secrets
258. **UI-SEC-072** — copying localStorage token to another browser profile is expected risk — logout clears; document as residual risk if refresh tokens absent
259. **UI-SEC-073** — DevTools Application panel after logout shows no residual auth keys
260. **UI-SEC-074** — masked integration secret fields never echo full secret back into DOM value attributes after save

### JWT / token tampering (UI observable)

261. **UI-SEC-075** — tampered JWT signature in localStorage → next API call fails → login redirect
262. **UI-SEC-076** — JWT payload permissions claim forged client-side does not unlock UI after /auth/me refresh (server wins)
263. **UI-SEC-077** — expired token mid-session shows re-auth, not silent success
264. **UI-SEC-078** — algorithm/`none` or malformed token rejected with safe error (no stack dump in UI)
265. **UI-SEC-079** — platform JWT cannot access hospital CRM UI routes
266. **UI-SEC-080** — hospital JWT cannot access platform tenants UI

### Clickjacking / UI redress

267. **UI-SEC-081** — app sets frame-ancestors / X-Frame-Options so login cannot be iframed (assert response headers via page request)
268. **UI-SEC-082** — critical dialogs (delete patient, send campaign) require explicit confirm — not single-click from framed lure

### Input / injection via UI

269. **UI-SEC-083** — SQL/meta characters in patient search do not error-leak schema; safe empty/error
270. **UI-SEC-084** — path traversal in document filename upload rejected with clear error
271. **UI-SEC-085** — oversized upload rejected with clear error (no hang)
272. **UI-SEC-086** — prototype-pollution style keys in JSON forms do not alter client auth object
273. **UI-SEC-087** — CRLF / header-injection strings in hospital name do not break UI shell

### Multi-tenant / platform isolation (security)

274. **UI-SEC-088** — after login as hospital A, switching URL slug/host for hospital B without re-auth fails closed
275. **UI-SEC-089** — platform approve/reject actions require platform session; hospital session gets 401/redirect
276. **UI-SEC-090** — suspended tenant cannot use stale localStorage token to load dashboard PHI
277. **UI-SEC-091** — rejected tenant remains on waiting room; Check status does not leak other tenants’ status details
278. **UI-SEC-092** — signup enumeration: duplicate email error does not reveal unrelated tenant internals

### PHI / privacy in UI

279. **UI-SEC-093** — patient phone/email masked or permission-gated for roles without need-to-know (if product policy)
280. **UI-SEC-094** — browser title/tab does not leak full PHI on shared screens after navigation away
281. **UI-SEC-095** — print prescription preview does not include unrelated patients
282. **UI-SEC-096** — error toasts never include raw stack traces or connection strings
283. **UI-SEC-097** — command palette results respect role — no forbidden patient rows
284. **UI-SEC-098** — analytics export/download (if any) respects tenant + role

### Session fixation / concurrent sessions

285. **UI-SEC-099** — login issues new token; pre-login crafted localStorage token is replaced
286. **UI-SEC-100** — password reset invalidates old token on next authenticated navigation
287. **UI-SEC-101** — impersonation / “view as” (if absent) — confirm no hidden debug role switcher in production UI

### Content Security / third-party

288. **UI-SEC-102** — external links in inbox/email open with noopener/noreferrer
289. **UI-SEC-103** — WhatsApp media URLs from untrusted hosts not auto-loaded if SafeRemoteUrl policy applies in UI
290. **UI-SEC-104** — integrations webhook URL fields reject `javascript:` / non-http(s) schemes in UI validation

---

## Totals (this catalog)

| Section | Count |
|---------|------:|
| Missing Critical (`UI-CRIT-*`) | **54** |
| Missing High (`UI-HIGH-*`) | **72** |
| Missing Medium (`UI-MED-*`) | **60** |
| Missing Security (`UI-SEC-*`) | **104** |
| **Missing total** | **290** |
| Existing (summary) | **~4,200** Playwright scenarios already generated/named |

### Mapping notes

- Every suggested title from the 2026-07-19 UI gap analysis Critical / High / Medium tables is present (verbatim where applicable).
- Security section is additive beyond that audit (XSS, CSRF applicability, auth bypass, role escalation, logout session, IDOR, route tampering, localStorage secrets, JWT tampering, clickjacking, PHI leakage, platform vs hospital token separation).
- **Do not implement from this file until prioritized** — treat IDs as the backlog contract for future Playwright work.
- Recompute existing matrix counts anytime:

```bash
cd dotnet-frontend-test
node -e "const { countMatrixScenarios } = require('./e2e/helpers/test-matrix'); console.log(countMatrixScenarios());"
```
