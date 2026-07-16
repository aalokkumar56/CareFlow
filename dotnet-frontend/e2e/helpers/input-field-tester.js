/**
 * Exercise every visible input, textarea, and combobox in a scope.
 * Fills sample values and records pass/fail — does not submit forms.
 */
const { expect } = require("@playwright/test");

const SAMPLE = {
  text: "E2E input test",
  email: "e2e-inputs@cureflow.test",
  password: "TestPass123!",
  phone: "919876543210",
  number: "42",
  date: "2026-07-15",
  time: "10:30",
  datetime: "2026-07-15T10:30",
  textarea: "E2E textarea sample content for validation.",
  search: "e2e search",
};

/**
 * @typedef {{ area: string, field: string, status: 'ok' | 'skip' | 'fail', note?: string }} FieldResult
 */

/** @returns {FieldResult[]} */
function createFieldResults() {
  const results = [];
  return {
    ok(area, field, note) {
      results.push({ area, field, status: "ok", note });
    },
    skip(area, field, note) {
      results.push({ area, field, status: "skip", note });
    },
    fail(area, field, note) {
      results.push({ area, field, status: "fail", note });
    },
    list: () => results,
    summary() {
      const ok = results.filter((r) => r.status === "ok").length;
      const skip = results.filter((r) => r.status === "skip").length;
      const fail = results.filter((r) => r.status === "fail");
      return { ok, skip, fail, total: results.length, failures: fail };
    },
  };
}

async function closeOverlay(page) {
  if (page.isClosed()) return;
  for (let i = 0; i < 3; i++) {
    const dialog = page.getByRole("dialog");
    if (!(await dialog.isVisible().catch(() => false))) break;
    const cancel = dialog.getByRole("button", { name: /cancel|close/i }).first();
    if (await cancel.isVisible().catch(() => false)) {
      await cancel.click({ timeout: 3_000 }).catch(() => {});
    } else {
      await page.keyboard.press("Escape").catch(() => {});
    }
    await page.waitForTimeout(150).catch(() => {});
  }
}

async function exerciseDialog(page, dialogTestId, area, log) {
  const dialog = page.getByTestId(dialogTestId);
  await expect(dialog).toBeVisible({ timeout: 10_000 });
  await exerciseAllInputsIn(page, dialog, area, log);
  const cancelBtn = dialog.getByRole("button", { name: /cancel/i });
  if (await cancelBtn.isVisible().catch(() => false)) {
    await cancelBtn.click();
  } else {
    await closeOverlay(page);
  }
  await dialog.waitFor({ state: "hidden", timeout: 8_000 }).catch(() => closeOverlay(page));
}

/** Fill a text-like input and verify it accepted the value. */
async function exerciseTextInput(page, locator, value, { area, field, log }) {
  try {
    await locator.waitFor({ state: "visible", timeout: 8_000 });
    if (await locator.isDisabled().catch(() => false)) {
      log.skip(area, field, "disabled");
      return;
    }
    await locator.scrollIntoViewIfNeeded();
    await locator.fill(value);
    const current = await locator.inputValue();
    if (!current || current.trim() === "") {
      log.fail(area, field, "value not retained after fill");
      return;
    }
    log.ok(area, field);
  } catch (err) {
    log.fail(area, field, err.message);
  }
}

/** Open a Radix select/combobox and pick the first available option. */
async function exerciseCombobox(page, locator, { area, field, log }) {
  try {
    await locator.waitFor({ state: "visible", timeout: 8_000 });
    if (await locator.isDisabled().catch(() => false)) {
      log.skip(area, field, "disabled");
      return;
    }
    await locator.click();
    const option = page.getByRole("option").first();
    await option.waitFor({ state: "visible", timeout: 5_000 });
    await option.click();
    log.ok(area, field);
  } catch (err) {
    await closeOverlay(page);
    log.fail(area, field, err.message);
  }
}

/**
 * Sweep all inputs/textareas/comboboxes inside a container.
 * @param {import('@playwright/test').Page} page
 * @param {import('@playwright/test').Locator} root
 */
async function exerciseAllInputsIn(page, root, area, log) {
  const inputs = root.locator(
    'input:visible:not([type="hidden"]):not([type="file"]):not([type="checkbox"]):not([type="radio"])',
  );
  const inputCount = await inputs.count();
  for (let i = 0; i < inputCount; i++) {
    const input = inputs.nth(i);
    const type = (await input.getAttribute("type")) || "text";
    const testId = (await input.getAttribute("data-testid")) || `input-${type}-${i}`;
    const placeholder = await input.getAttribute("placeholder");
    const field = testId || placeholder || `input-${i}`;

    let value = SAMPLE.text;
    if (type === "search") value = SAMPLE.search;
    else if (type === "email") value = SAMPLE.email;
    else if (type === "password") value = SAMPLE.password;
    else if (type === "number") value = SAMPLE.number;
    else if (type === "date") value = SAMPLE.date;
    else if (type === "time") value = SAMPLE.time;
    else if (type === "datetime-local") value = SAMPLE.datetime;
    else if (type === "tel") value = SAMPLE.phone;

    await exerciseTextInput(page, input, value, { area, field, log });
  }

  const textareas = root.locator("textarea:visible");
  const taCount = await textareas.count();
  for (let i = 0; i < taCount; i++) {
    const ta = textareas.nth(i);
    const testId = (await ta.getAttribute("data-testid")) || `textarea-${i}`;
    await exerciseTextInput(page, ta, SAMPLE.textarea, { area, field: testId, log });
  }

  const comboboxes = root.locator('[role="combobox"]:visible');
  const cbCount = await comboboxes.count();
  for (let i = 0; i < cbCount; i++) {
    const cb = comboboxes.nth(i);
    const testId = (await cb.getAttribute("data-testid")) || `combobox-${i}`;
    await exerciseCombobox(page, cb, { area, field: testId, log });
  }
}

async function exerciseByTestId(page, testId, value, area, log) {
  const loc = page.getByTestId(testId);
  if (!(await loc.isVisible().catch(() => false))) {
    log.skip(area, testId, "not visible");
    return;
  }
  const tag = await loc.evaluate((el) => el.tagName.toLowerCase()).catch(() => "input");
  if (tag === "button" || (await loc.getAttribute("role")) === "combobox") {
    await exerciseCombobox(page, loc, { area, field: testId, log });
  } else {
    await exerciseTextInput(page, loc, value, { area, field: testId, log });
  }
}

/** Known list-page search + filter fields */
const LIST_PAGE_FIELDS = [
  { path: "/patients", area: "Patients list", fields: [
    { testId: "patients-search", value: SAMPLE.search },
    { testId: "filter-dept", combobox: true },
    { testId: "filter-status", combobox: true },
    { testId: "filter-source", combobox: true },
  ]},
  { path: "/appointments", area: "Appointments list", fields: [
    { testId: "appt-search", value: SAMPLE.search },
  ]},
  { path: "/tasks", area: "Tasks list", fields: [
    { testId: "task-search", value: SAMPLE.search },
  ]},
  { path: "/staff", area: "Staff list", fields: [
    { testId: "staff-search", value: SAMPLE.search },
  ]},
  { path: "/inbox", area: "Inbox list", fields: [
    { testId: "inbox-search", value: SAMPLE.search },
  ]},
  { path: "/doctors", area: "Doctors list", fields: [
    { testId: "doctors-search", value: SAMPLE.search },
  ]},
  { path: "/settings/users", area: "Users list", fields: [
    { testId: "users-search", value: SAMPLE.search },
  ]},
];

module.exports = {
  SAMPLE,
  createFieldResults,
  closeOverlay,
  exerciseTextInput,
  exerciseCombobox,
  exerciseAllInputsIn,
  exerciseDialog,
  exerciseByTestId,
  LIST_PAGE_FIELDS,
};
