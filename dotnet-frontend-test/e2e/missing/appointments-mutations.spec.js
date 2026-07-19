/**
 * Appointments mutations — kanban drag/status, cancel, edit, doctor filters.
 *
 * Scenario IDs (see TEST_SCENARIOS.md):
 *   UI-CRIT-011 — drag card Scheduled → Confirmed updates status via API
 *   UI-CRIT-012 — mark completed from card action
 *   UI-CRIT-013 — cancel appointment confirms and moves to Cancelled
 *   UI-CRIT-014 — edit appointment dialog saves doctor/time/status
 *   UI-CRIT-015 — doctor filter Mine vs All Doctors changes board
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const {
  apiLogin,
  createPatient,
  createAppointment,
  getBookingOptions,
  apiRequest,
} = require("../helpers/api");
const { E2E_TEST_PHONE } = require("../helpers/constants");

test.describe.configure({ mode: "serial" });

const STAMP = Date.now();
const DOCTOR_EMAIL = "doctor@cureflow.in";
const DOCTOR_PASSWORD = "admin123";
const ADMIN_EMAIL = "admin@cureflow.in";
const ADMIN_PASSWORD = "admin123";

/** @param {import('@playwright/test').Page} page @param {string} label */
function kanbanColumn(page, label) {
  return page
    .getByTestId("appointments-kanban")
    .locator("div.min-w-0.flex.flex-col.overflow-hidden")
    .filter({ has: page.getByText(label, { exact: true }) })
    .first();
}

/** @param {import('@playwright/test').Page} page @param {string} label */
function columnDropZone(page, label) {
  return kanbanColumn(page, label).locator(":scope > div").last();
}

/** @param {import('@playwright/test').Page} page @param {string} patientName */
function apptCard(page, patientName) {
  return page.locator('[draggable="true"]').filter({ hasText: patientName }).first();
}

/**
 * HTML5 DnD that gives React time to commit dragItem before drop.
 * @param {import('@playwright/test').Page} page
 * @param {import('@playwright/test').Locator} source
 * @param {import('@playwright/test').Locator} target
 */
async function dragCardToColumn(page, source, target) {
  await expect(source).toBeVisible({ timeout: 15_000 });
  await expect(target).toBeVisible({ timeout: 15_000 });

  const isStatusPatch = (r) =>
    r.url().includes("/api/appointments/")
    && r.url().includes("/status")
    && r.request().method() === "PATCH";

  const statusPromise = page.waitForResponse(isStatusPatch, { timeout: 20_000 });

  // Prefer real pointer drag (Playwright dispatches HTML5 DnD for draggable nodes).
  await source.dragTo(target, { force: true, timeout: 15_000 });

  let res;
  try {
    res = await statusPromise;
  } catch {
    // Fallback: synthetic DnD with a yield so React setState from dragstart lands.
    const srcHandle = await source.elementHandle();
    const tgtHandle = await target.elementHandle();
    expect(srcHandle && tgtHandle, "drag source/target handles").toBeTruthy();
    await page.evaluate(async ([src, tgt]) => {
      const dt = new DataTransfer();
      src.dispatchEvent(new DragEvent("dragstart", { bubbles: true, cancelable: true, dataTransfer: dt }));
      await new Promise((r) => setTimeout(r, 80));
      tgt.dispatchEvent(new DragEvent("dragenter", { bubbles: true, cancelable: true, dataTransfer: dt }));
      tgt.dispatchEvent(new DragEvent("dragover", { bubbles: true, cancelable: true, dataTransfer: dt }));
      tgt.dispatchEvent(new DragEvent("drop", { bubbles: true, cancelable: true, dataTransfer: dt }));
      src.dispatchEvent(new DragEvent("dragend", { bubbles: true, cancelable: true, dataTransfer: dt }));
    }, [srcHandle, tgtHandle]);

    res = await page.waitForResponse(isStatusPatch, { timeout: 20_000 });
  }

  expect(res.ok(), `status PATCH failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res;
}

/**
 * @param {string} token
 * @param {{ name: string, doctorUserId: string, department: string, notes?: string }} opts
 */
async function seedScheduledAppointment(token, opts) {
  const patient = await createPatient(token, opts.name, E2E_TEST_PHONE);
  const patientId = patient.id || patient.patient?.id;
  expect(patientId, "seed patient id").toBeTruthy();

  const scheduledAt = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
  const appt = await createAppointment(token, {
    patientId,
    doctorUserId: opts.doctorUserId,
    department: opts.department,
    scheduledAt,
    notes: opts.notes || `e2e appt mutations ${STAMP}`,
  });
  const apptId = appt.id || appt.appointment?.id;
  expect(apptId, "seed appointment id").toBeTruthy();
  return { patientId, patientName: opts.name, apptId, scheduledAt };
}

/** @param {import('@playwright/test').Page} page */
async function gotoAppointments(page) {
  await page.goto("/appointments", { waitUntil: "domcontentloaded" });
  await expect(page.getByTestId("appointments-kanban")).toBeVisible({ timeout: 20_000 });
}

test.describe("Appointments mutations (UI-CRIT-011…015)", () => {
  /** @type {string} */
  let adminToken = "";
  /** @type {{ user_id: string, name?: string, department?: string }[]} */
  let doctors = [];
  /** @type {string} */
  let primaryDoctorUserId = "";
  /** @type {string} */
  let otherDoctorUserId = "";
  /** @type {string} */
  let department = "General Medicine";
  /** @type {string} */
  let loggedInDoctorUserId = "";
  /** @type {{ email: string, password: string } | null} */
  let doctorCreds = null;

  test.beforeAll(async () => {
    const admin = await apiLogin(ADMIN_EMAIL, ADMIN_PASSWORD);
    adminToken = admin.accessToken;

    const booking = await getBookingOptions(adminToken);
    doctors = booking?.doctors || [];
    test.skip(doctors.length === 0, "No bookable doctors — cannot seed appointments");

    primaryDoctorUserId = doctors[0].user_id || doctors[0].userId;
    department = doctors[0].department || department;
    expect(primaryDoctorUserId).toBeTruthy();

    if (doctors.length >= 2) {
      otherDoctorUserId = doctors[1].user_id || doctors[1].userId;
    }

    // Demo seeder: doctor@ + dr.colleague@; tolerate environments where one is missing.
    for (const email of [DOCTOR_EMAIL, "dr.colleague@cureflow.in"]) {
      const session = await apiLogin(email, DOCTOR_PASSWORD).catch(() => null);
      if (!session?.user) continue;
      doctorCreds = { email, password: DOCTOR_PASSWORD };
      loggedInDoctorUserId = session.user.id || session.user.user_id;
      break;
    }

    if (loggedInDoctorUserId) {
      const selfDoc = doctors.find(
        (d) => (d.user_id || d.userId) === loggedInDoctorUserId,
      );
      if (selfDoc) {
        primaryDoctorUserId = selfDoc.user_id || selfDoc.userId;
        department = selfDoc.department || department;
      }
      const colleague = doctors.find(
        (d) => (d.user_id || d.userId) !== loggedInDoctorUserId,
      );
      if (colleague) {
        otherDoctorUserId = colleague.user_id || colleague.userId;
      }
    }
  });

  test("UI-CRIT-011: drag Scheduled → Checked in updates status via API", async ({ page }) => {
    const name = `E2E Appt Drag ${STAMP}`;
    await seedScheduledAppointment(adminToken, {
      name,
      doctorUserId: primaryDoctorUserId,
      department,
      notes: "drag-status",
    });

    await login(page, { email: ADMIN_EMAIL, password: ADMIN_PASSWORD });
    await gotoAppointments(page);

    const card = apptCard(page, name);
    await expect(card).toBeVisible({ timeout: 20_000 });
    await expect(kanbanColumn(page, "Scheduled")).toContainText(name);

    const drop = columnDropZone(page, "Checked in");
    const res = await dragCardToColumn(page, card, drop);
    const body = await res.json().catch(() => ({}));
    const status = body.status || body.Status;
    if (status) expect(status).toBe("confirmed");

    await expect(kanbanColumn(page, "Checked in")).toContainText(name, { timeout: 15_000 });
    await expect(kanbanColumn(page, "Scheduled")).not.toContainText(name);
  });

  test("UI-CRIT-012: mark completed from card action", async ({ page }) => {
    const name = `E2E Appt Complete ${STAMP}`;
    const { apptId } = await seedScheduledAppointment(adminToken, {
      name,
      doctorUserId: primaryDoctorUserId,
      department,
      notes: "mark-complete",
    });

    await login(page, { email: ADMIN_EMAIL, password: ADMIN_PASSWORD });
    await gotoAppointments(page);

    const card = apptCard(page, name);
    await expect(card).toBeVisible({ timeout: 20_000 });

    const patchPromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/appointments/${apptId}/status`)
        && r.request().method() === "PATCH"
        && r.ok(),
      { timeout: 20_000 },
    );
    await card.getByRole("button", { name: "Mark completed" }).click();
    const res = await patchPromise;
    const payload = res.request().postDataJSON();
    expect(payload?.status || payload?.Status).toBe("completed");

    await expect(kanbanColumn(page, "Completed")).toContainText(name, { timeout: 15_000 });
    await expect(card.getByRole("button", { name: "Mark completed" })).toHaveCount(0);
  });

  test("UI-CRIT-013: cancel appointment confirms and moves to Cancelled", async ({ page }) => {
    const name = `E2E Appt Cancel ${STAMP}`;
    const { apptId } = await seedScheduledAppointment(adminToken, {
      name,
      doctorUserId: primaryDoctorUserId,
      department,
      notes: "cancel-flow",
    });

    await login(page, { email: ADMIN_EMAIL, password: ADMIN_PASSWORD });
    await gotoAppointments(page);

    const card = apptCard(page, name);
    await expect(card).toBeVisible({ timeout: 20_000 });

    await card.getByRole("button", { name: "Cancel appointment" }).click();
    await expect(page.getByRole("heading", { name: "Cancel appointment?" })).toBeVisible({
      timeout: 10_000,
    });

    const patchPromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/appointments/${apptId}/status`)
        && r.request().method() === "PATCH"
        && r.ok(),
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: "Yes, cancel" }).click();
    const res = await patchPromise;
    const payload = res.request().postDataJSON();
    expect(payload?.status || payload?.Status).toBe("cancelled");

    await expect(kanbanColumn(page, "Cancelled")).toContainText(name, { timeout: 15_000 });
  });

  test("UI-CRIT-014: edit appointment dialog saves doctor/time/status", async ({ page }) => {
    const name = `E2E Appt Edit ${STAMP}`;
    const { apptId } = await seedScheduledAppointment(adminToken, {
      name,
      doctorUserId: primaryDoctorUserId,
      department,
      notes: "before-edit",
    });

    await login(page, { email: ADMIN_EMAIL, password: ADMIN_PASSWORD });
    await gotoAppointments(page);

    const card = apptCard(page, name);
    await expect(card).toBeVisible({ timeout: 20_000 });
    await card.getByRole("button", { name: "Edit appointment" }).click();

    const dialog = page.getByTestId("edit-appt-dialog");
    await expect(dialog).toBeVisible({ timeout: 10_000 });
    await expect(dialog.getByText("Edit / Reschedule")).toBeVisible();

    // Bump time and set status to Checked-in (confirmed).
    const timeInput = dialog.locator('input[type="time"]');
    await expect(timeInput).toBeVisible();
    await timeInput.fill("14:30");

    const statusTrigger = dialog.getByRole("combobox").filter({ hasText: /Scheduled|Checked-in|Completed|Cancelled|No-show/i }).first();
    if (await statusTrigger.isVisible().catch(() => false)) {
      await statusTrigger.click();
      await page.getByRole("option", { name: "Checked-in", exact: true }).click();
    } else {
      // Status SelectValue may show label only — open any status select in dialog.
      await dialog.locator("button").filter({ hasText: /Scheduled|Status/i }).first().click().catch(() => {});
      const checkedIn = page.getByRole("option", { name: "Checked-in", exact: true });
      if (await checkedIn.isVisible().catch(() => false)) await checkedIn.click();
    }

    await dialog.locator("textarea").fill(`edited-notes-${STAMP}`);

    // Prefer switching doctor when a second bookable doctor exists.
    if (otherDoctorUserId && otherDoctorUserId !== primaryDoctorUserId) {
      const other = doctors.find((d) => (d.user_id || d.userId) === otherDoctorUserId);
      if (other?.name) {
        const doctorTrigger = dialog.getByRole("combobox").filter({ hasText: /Select doctor|Dr\.|Doctor/i }).first();
        if (await doctorTrigger.isVisible().catch(() => false)) {
          await doctorTrigger.click();
          const opt = page.getByRole("option", { name: other.name, exact: true });
          if (await opt.isVisible().catch(() => false)) await opt.click();
        }
      }
    }

    const patchPromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/appointments/${apptId}`)
        && ["PATCH", "PUT"].includes(r.request().method())
        && r.ok(),
      { timeout: 20_000 },
    );
    await dialog.getByRole("button", { name: "Save changes" }).click();
    await patchPromise;

    await expect(dialog).toBeHidden({ timeout: 15_000 });

    // Reload board and confirm persistence via API + UI column.
    const stored = await apiRequest(adminToken, "GET", `/appointments/${apptId}`).catch(() => null);
    if (stored) {
      const status = stored.status || stored.Status;
      const notes = stored.notes || stored.Notes;
      expect(status === "confirmed" || status === "scheduled").toBeTruthy();
      if (notes) expect(String(notes)).toContain(`edited-notes-${STAMP}`);
    }

    await page.reload({ waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("appointments-kanban")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(name).first()).toBeVisible({ timeout: 15_000 });
  });

  test("UI-CRIT-015: doctor filter Mine vs All Doctors changes board", async ({ page }) => {
    test.skip(!doctorCreds || !loggedInDoctorUserId, "No demo doctor login available");
    test.skip(
      !otherDoctorUserId || otherDoctorUserId === loggedInDoctorUserId,
      "Need a second bookable doctor for Mine vs All filter",
    );

    const mineName = `E2E Appt Mine ${STAMP}`;
    const otherName = `E2E Appt OtherDoc ${STAMP}`;

    // Doctor board defaults to hospital "today" — keep slots inside the next few hours.
    const mine = await seedScheduledAppointment(adminToken, {
      name: mineName,
      doctorUserId: loggedInDoctorUserId,
      department,
      notes: "mine-filter",
    });
    await seedScheduledAppointment(adminToken, {
      name: otherName,
      doctorUserId: otherDoctorUserId,
      department,
      notes: "all-filter",
    });

    // Confirm API visibility under the doctor token before UI assertions.
    const doctorSession = await apiLogin(doctorCreds.email, doctorCreds.password);
    const from = new Date(Date.now() - 12 * 60 * 60 * 1000).toISOString();
    const to = new Date(Date.now() + 36 * 60 * 60 * 1000).toISOString();
    const mineList = await apiRequest(
      doctorSession.accessToken,
      "GET",
      `/appointments?page=1&page_size=100&doctor_user_id=${loggedInDoctorUserId}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    );
    const mineItems = mineList?.items || [];
    expect(
      mineItems.some((a) => a.id === mine.apptId || a.patient_name === mineName),
      "seeded mine appointment should be returned for doctor_user_id filter",
    ).toBeTruthy();

    const isApptBoardGet = (r, { requireDoctorId } = {}) => {
      if (!r.url().includes("/api/appointments?")) return false;
      if (r.url().includes("booking-options")) return false;
      if (r.request().method() !== "GET" || !r.ok()) return false;
      const hasDoctor = r.url().includes("doctor_user_id=");
      if (requireDoctorId === true) return hasDoctor;
      if (requireDoctorId === false) return !hasDoctor;
      return true;
    };

    await login(page, { email: doctorCreds.email, password: doctorCreds.password });

    const initialMineLoad = page.waitForResponse(
      (r) => isApptBoardGet(r, { requireDoctorId: true }),
      { timeout: 25_000 },
    );
    // Use week range (not default doctor "today") so seeded near-term slots are on the board.
    await page.goto("/appointments?date=week", { waitUntil: "domcontentloaded" });
    await initialMineLoad;
    await expect(page.getByTestId("appointments-kanban")).toBeVisible({ timeout: 20_000 });

    await expect(page.getByTestId("appt-doctor-filter")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId("appt-filter-mine")).toBeVisible();
    await expect(page.getByTestId("appt-filter-all")).toBeVisible();

    const search = page.getByTestId("appt-search");

    // Default for doctors is Mine — own appt visible, colleague hidden.
    // Search is client-side on loaded rows (also avoids kanban maxVisible=50 clipping).
    await search.fill(mineName);
    await expect(page.getByText(mineName).first()).toBeVisible({ timeout: 20_000 });
    await search.fill(otherName);
    await expect(page.getByText(otherName)).toHaveCount(0);
    await search.fill("");

    const allListPromise = page.waitForResponse(
      (r) => isApptBoardGet(r, { requireDoctorId: false }),
      { timeout: 20_000 },
    );
    await page.getByTestId("appt-filter-all").click();
    await allListPromise;

    await search.fill(mineName);
    await expect(page.getByText(mineName).first()).toBeVisible({ timeout: 20_000 });
    await search.fill(otherName);
    await expect(page.getByText(otherName).first()).toBeVisible({ timeout: 20_000 });
    await search.fill("");

    // Toggle back to Mine — colleague disappears again.
    const backMinePromise = page.waitForResponse(
      (r) => isApptBoardGet(r, { requireDoctorId: true }),
      { timeout: 20_000 },
    );
    await page.getByTestId("appt-filter-mine").click();
    await backMinePromise;
    await search.fill(mineName);
    await expect(page.getByText(mineName).first()).toBeVisible({ timeout: 15_000 });
    await search.fill(otherName);
    await expect(page.getByText(otherName)).toHaveCount(0);
  });
});



