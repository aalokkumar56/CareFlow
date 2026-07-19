const {
  apiLogin,
  searchPatients,
  createPatient,
  getBookingOptions,
  createAppointment,
  updateAppointmentStatus,
  getDashboardOverview,
  apiRequest,
} = require("./api");
const { E2E_TEST_PHONE, E2E_DASHBOARD_PATIENT_NAME } = require("./constants");

/** @param {string} token @param {string} patientId */
async function listPatientAppointments(token, patientId) {
  const data = await apiRequest(token, "GET", "/appointments?page=1&page_size=100");
  const items = data?.items || [];
  return items.filter((a) => a.patient_id === patientId);
}

/** @returns {Promise<{ patientId: string, patientName: string, upcomingAppointmentId?: string, completedAppointmentId?: string }>} */
async function seedDashboardData() {
  const admin = await apiLogin("admin@cureflow.in", "admin123");
  const token = admin.accessToken;

  let patientId;
  const existing = await searchPatients(token, E2E_TEST_PHONE);
  const match = existing.find((p) => p.phone === E2E_TEST_PHONE || p.name === E2E_DASHBOARD_PATIENT_NAME);
  if (match) {
    patientId = match.id;
  } else {
    const created = await createPatient(token, E2E_DASHBOARD_PATIENT_NAME, E2E_TEST_PHONE);
    patientId = created.id || created.patient?.id;
  }

  const booking = await getBookingOptions(token);
  const doctors = booking?.doctors || [];
  if (doctors.length === 0) {
    throw new Error("No bookable doctors — cannot seed dashboard appointments");
  }
  const doctor = doctors[0];
  const department = doctor.department || booking?.departments?.[0] || "General Medicine";

  const patientAppts = await listPatientAppointments(token, patientId);
  const now = Date.now();
  const hasUpcoming = patientAppts.some(
    (a) =>
      new Date(a.scheduled_at).getTime() > now
      && !["completed", "cancelled", "no_show"].includes(a.status),
  );
  const hasCompletedThisMonth = patientAppts.some((a) => a.status === "completed");

  let upcomingAppointmentId;
  if (!hasUpcoming) {
    const scheduledAt = new Date(now + 3 * 60 * 60 * 1000).toISOString();
    const appt = await createAppointment(token, {
      patientId,
      doctorUserId: doctor.user_id,
      doctorName: doctor.name,
      department,
      scheduledAt,
      notes: "e2e dashboard upcoming",
    });
    upcomingAppointmentId = appt.id;
  }

  let completedAppointmentId;
  if (!hasCompletedThisMonth) {
    const completedAt = new Date(now + 4 * 60 * 60 * 1000).toISOString();
    const completedAppt = await createAppointment(token, {
      patientId,
      doctorUserId: doctor.user_id,
      doctorName: doctor.name,
      department,
      scheduledAt: completedAt,
      notes: "e2e dashboard revenue",
    });
    completedAppointmentId = completedAppt.id;
    await updateAppointmentStatus(token, completedAppointmentId, "completed");
  }

  // Warm dashboard cache with post-seed data (overview caches ~60s server-side).
  await new Promise((r) => setTimeout(r, 500));
  await getDashboardOverview(token);

  return {
    patientId,
    patientName: E2E_DASHBOARD_PATIENT_NAME,
    upcomingAppointmentId,
    completedAppointmentId,
  };
}

module.exports = { seedDashboardData };
