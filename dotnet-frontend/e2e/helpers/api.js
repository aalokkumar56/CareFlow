const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";

// Cache tokens per credentials. The /api/auth/login endpoint is rate-limited to 10/min per IP,
// and every UI + API login routes through here, so memoizing avoids tripping the limiter.
const _tokenCache = new Map();

/** @returns {Promise<{ accessToken: string, user: object }>} */
async function apiLogin(email, password, retries = 4) {
  const cacheKey = `${email}::${password}`;
  if (_tokenCache.has(cacheKey)) return _tokenCache.get(cacheKey);

  let lastError;
  for (let attempt = 0; attempt < retries; attempt += 1) {
    try {
      const res = await fetch(`${apiURL}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      if (res.status === 429 && attempt < retries - 1) {
        await new Promise((r) => setTimeout(r, 6000 * (attempt + 1)));
        continue;
      }
      if (!res.ok) {
        throw new Error(`Login failed: ${res.status} ${await res.text()}`);
      }
      const data = await res.json();
      const result = {
        accessToken: data.accessToken || data.access_token,
        user: data.user,
        tenant: data.tenant,
      };
      _tokenCache.set(cacheKey, result);
      return result;
    } catch (error) {
      lastError = error;
      if (attempt === retries - 1) throw error;
    }
  }
  throw lastError;
}

/** @param {string} token */
async function apiRequest(token, method, path, body) {
  const res = await fetch(`${apiURL}/api${path}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`${method} ${path} failed: ${res.status} ${text}`);
  }
  if (res.status === 204) return null;
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

const { E2E_TEST_PHONE } = require("./constants");

/** @param {string} token */
async function createPatient(token, name, phone) {
  const normalizedPhone = phone || E2E_TEST_PHONE;
  return apiRequest(token, "POST", "/patients", {
    name,
    phone: normalizedPhone,
    inquiry_source: "e2e",
  });
}

/** @param {string} token @param {string} query */
async function searchPatients(token, query) {
  const data = await apiRequest(
    token,
    "GET",
    `/patients?q=${encodeURIComponent(query)}&page=1&page_size=10`,
  );
  return data?.items || [];
}

/** @param {string} token */
async function getBookingOptions(token) {
  return apiRequest(token, "GET", "/appointments/booking-options");
}

/**
 * @param {string} token
 * @param {{ patientId: string, doctorUserId: string, doctorName?: string, department: string, scheduledAt: string, notes?: string }} opts
 */
async function createAppointment(token, opts) {
  return apiRequest(token, "POST", "/appointments", {
    patient_id: opts.patientId,
    doctor_user_id: opts.doctorUserId,
    doctor_name: opts.doctorName,
    department: opts.department,
    scheduled_at: opts.scheduledAt,
    notes: opts.notes || "e2e",
  });
}

/** @param {string} token @param {string} appointmentId @param {string} status */
async function updateAppointmentStatus(token, appointmentId, status) {
  return apiRequest(token, "PATCH", `/appointments/${appointmentId}/status`, { status });
}

/** @param {string} token */
async function getDashboardOverview(token) {
  return apiRequest(token, "GET", "/dashboard/overview");
}

/** @param {string} token */
async function markAllNotificationsRead(token) {
  return apiRequest(token, "POST", "/notifications/mark-all-read");
}

/** @param {string} token */
async function getUnreadCount(token) {
  const data = await apiRequest(token, "GET", "/notifications/unread-count");
  return Number(data?.count) || 0;
}

/** @param {string} token */
async function createNurseUser(token, email, password) {
  return apiRequest(token, "POST", "/auth/register", {
    name: "E2E Nurse",
    email,
    password,
    role: "nurse",
  });
}

/** Registers a user with the given role (used so the notification creator differs from the viewer). */
async function createUser(token, { name, email, password, role }) {
  return apiRequest(token, "POST", "/auth/register", { name, email, password, role });
}

/** @param {string} token */
async function ensureNotificationTypesEnabled(token, types = ["patient.created", "appointment.created"]) {
  await apiRequest(token, "PUT", "/notification-preferences", {
    preferences: types.map((notification_type) => ({
      notification_type,
      in_app_enabled: true,
    })),
  });
}

/** @param {string} token */
async function createTask(token, { title, dueAt }) {
  return apiRequest(token, "POST", "/tasks", {
    title,
    type: "follow_up",
    priority: "medium",
    due_at: dueAt,
    notes: "e2e follow-up",
  });
}

/** @param {string} token @returns {Promise<string[]>} */
async function getDepartments(token) {
  const data = await apiRequest(token, "GET", "/hospital-profile/departments");
  if (!Array.isArray(data)) return [];
  return data
    .map((d) => (typeof d === "string" ? d : d?.name))
    .filter(Boolean);
}

/** @param {string} token */
async function listDoctors(token) {
  return apiRequest(token, "GET", "/doctors");
}

/** @param {string} token */
async function createDoctor(token, name) {
  return apiRequest(token, "POST", "/doctors", {
    name,
    category: "specialist",
    reconnect_days: 30,
  });
}

/** @param {string} token */
async function createReferral(token, { doctorId, patientId, revenue = 5000, notes = "e2e referral" }) {
  return apiRequest(token, "POST", "/referrals", {
    doctor_id: doctorId,
    patient_id: patientId,
    revenue,
    notes,
  });
}

/** @param {string} token @param {string} patientId @param {object} patch */
async function patchPatient(token, patientId, patch) {
  return apiRequest(token, "PATCH", `/patients/${patientId}`, patch);
}

/** @param {string} token @param {string} patientId */
async function getPatient(token, patientId) {
  return apiRequest(token, "GET", `/patients/${patientId}`);
}

/** @param {{ hospitalName: string, adminName: string, adminEmail: string, adminPassword: string, phone?: string }} opts */
async function registerTenant(opts) {
  const res = await fetch(`${apiURL}/api/auth/register-tenant`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      hospital_name: opts.hospitalName,
      admin_name: opts.adminName,
      admin_email: opts.adminEmail,
      admin_password: opts.adminPassword,
      phone: opts.phone,
    }),
  });
  if (!res.ok) {
    throw new Error(`register-tenant failed: ${res.status} ${await res.text()}`);
  }
  return res.json();
}

async function platformApproveTenant(tenantId) {
  const res = await fetch(`${apiURL}/api/platform/tenants/${tenantId}/approve`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) {
    throw new Error(`approve failed: ${res.status} ${await res.text()}`);
  }
  return res.json();
}

module.exports = {
  apiURL,
  apiLogin,
  apiRequest,
  createPatient,
  searchPatients,
  getBookingOptions,
  createAppointment,
  updateAppointmentStatus,
  getDashboardOverview,
  markAllNotificationsRead,
  getUnreadCount,
  createNurseUser,
  createUser,
  ensureNotificationTypesEnabled,
  createTask,
  getDepartments,
  listDoctors,
  createDoctor,
  createReferral,
  patchPatient,
  getPatient,
  registerTenant,
  platformApproveTenant,
};
