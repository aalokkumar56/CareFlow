/**
 * Mobile viewport gaps not covered by mobile-audit.spec.js.
 * Focus: inbox, campaigns, tasks, referrals (UI-MED-001/002/004/006).
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-MED-001 WhatsApp inbox conversation + compose reachable
 *   UI-MED-002 campaigns kanban horizontal scroll
 *   UI-MED-004 referral CRM + doctor detail
 *   UI-MED-006 tasks / follow-ups kanban usable
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, createDoctor, createTask } = require("../helpers/api");

const MOBILE_VIEWPORT = { width: 390, height: 844 };

/** @param {import('@playwright/test').Page} page */
async function assertNoHorizontalOverflow(page, label) {
  const overflow = await page.evaluate(() => {
    const doc = document.documentElement;
    return doc.scrollWidth > doc.clientWidth + 2;
  });
  expect(overflow, `${label}: page should not scroll horizontally`).toBe(false);
}

/** @param {import('@playwright/test').Page} page */
async function assertMobileNavToggle(page) {
  const toggle = page.getByTestId("mobile-nav-toggle").first();
  await expect(toggle).toBeVisible();
  await toggle.click();
  const mobileSheet = page.locator('[role="dialog"]');
  await expect(mobileSheet.getByTestId("nav-patients")).toBeVisible({ timeout: 10_000 });
  await page.keyboard.press("Escape");
}

/** @param {import('@playwright/test').Page} page @param {import('@playwright/test').Locator} locator */
async function assertControlReachable(page, locator, viewportHeight = MOBILE_VIEWPORT.height) {
  await locator.scrollIntoViewIfNeeded();
  const box = await locator.boundingBox();
  expect(box, "Control should have a bounding box").not.toBeNull();
  expect(box.y + box.height, "Control should not be clipped below viewport").toBeLessThanOrEqual(
    viewportHeight + 8,
  );
  expect(box.y, "Control should not be clipped above viewport").toBeGreaterThanOrEqual(-4);
}

test.describe("Mobile gaps — inbox, campaigns, tasks, referrals", () => {
  test.use({ viewport: MOBILE_VIEWPORT });

  let doctorId;
  let doctorName;

  test.beforeAll(async () => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    doctorName = `E2E Mobile Referrer ${Date.now()}`;
    const doctor = await createDoctor(admin.accessToken, doctorName);
    doctorId = doctor.id;

    const due = new Date();
    due.setDate(due.getDate() + 1);
    await createTask(admin.accessToken, {
      title: `E2E Mobile Task ${Date.now()}`,
      dueAt: due.toISOString(),
    }).catch(() => {});
  });

  test("UI-MED-001: WhatsApp inbox conversation + compose reachable", async ({ page }) => {
    await login(page);
    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });
    await assertNoHorizontalOverflow(page, "WhatsApp inbox");
    await assertMobileNavToggle(page);
    await assertControlReachable(page, page.getByTestId("inbox-search"));

    const firstConv = page.locator("[data-testid^='conv-']").first();
    if (await firstConv.isVisible().catch(() => false)) {
      await firstConv.click();
      const compose = page.getByPlaceholder(/type a whatsapp message|whatsapp sending is disabled|message/i);
      await expect(compose).toBeVisible({ timeout: 15_000 });
      await assertControlReachable(page, compose);
    } else {
      await expect(page.getByText(/no conversations|select a conversation/i).first()).toBeVisible();
    }

    await expect(page.getByTestId("app-sidebar")).toBeHidden();
  });

  test("UI-MED-002: campaigns kanban horizontal scroll", async ({ page }) => {
    await login(page);
    await page.goto("/campaigns", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("campaigns-kanban")).toBeVisible({ timeout: 20_000 });
    await assertMobileNavToggle(page);

    const kanban = page.getByTestId("campaigns-kanban");
    const scroll = await kanban.evaluate((el) => {
      const scroller =
        el.closest("[data-testid='campaigns-kanban']") ||
        el.querySelector("[class*='overflow']") ||
        el;
      return {
        scrollWidth: scroller.scrollWidth,
        clientWidth: scroller.clientWidth,
        canScroll: scroller.scrollWidth > scroller.clientWidth + 2,
      };
    });

    // On narrow viewports the board should either scroll horizontally or wrap without page overflow.
    const pageOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2,
    );
    expect(
      scroll.canScroll || !pageOverflow,
      "Campaigns board should scroll horizontally or fit without page overflow",
    ).toBeTruthy();

    if (scroll.canScroll) {
      await kanban.evaluate((el) => {
        const scroller = el.querySelector("[class*='overflow']") || el;
        scroller.scrollLeft = Math.min(120, scroller.scrollWidth);
      });
    }

    const newBtn = page.getByTestId("new-campaign-btn");
    if (await newBtn.isVisible().catch(() => false)) {
      await assertControlReachable(page, newBtn);
    }
  });

  test("UI-MED-006: tasks / follow-ups kanban usable", async ({ page }) => {
    await login(page);
    await page.goto("/tasks", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("followups-kanban")).toBeVisible({ timeout: 20_000 });
    await assertNoHorizontalOverflow(page, "Tasks");
    await assertMobileNavToggle(page);

    const search = page.getByTestId("task-search");
    await expect(search).toBeVisible();
    await assertControlReachable(page, search);

    const newTask = page.getByTestId("new-task-btn");
    if (await newTask.isVisible().catch(() => false)) {
      await assertControlReachable(page, newTask);
      await newTask.click();
      const title = page.getByTestId("task-title");
      if (await title.isVisible().catch(() => false)) {
        await expect(title).toBeVisible();
        await page.keyboard.press("Escape");
      }
    }

    await expect(page.getByTestId("app-sidebar")).toBeHidden();
  });

  test("UI-MED-004: referral CRM + doctor detail on mobile", async ({ page }) => {
    test.skip(!doctorId, "No referring doctor seeded");

    await login(page);
    await page.goto("/doctors", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("referral-kanban")).toBeVisible({ timeout: 20_000 });
    await assertMobileNavToggle(page);

    const search = page.getByTestId("doctors-search");
    await expect(search).toBeVisible();
    await assertControlReachable(page, search);
    await search.fill(doctorName);
    await expect(page.getByTestId(`doctor-row-${doctorId}`)).toBeVisible({ timeout: 15_000 });

    const newDoctor = page.getByTestId("new-doctor-btn");
    if (await newDoctor.isVisible().catch(() => false)) {
      await assertControlReachable(page, newDoctor);
    }

    // Board may scroll horizontally; document must not.
    const pageOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2,
    );
    expect(pageOverflow, "Referral CRM should not cause page-level horizontal overflow").toBe(false);

    await page.goto(`/doctors/${doctorId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("doctor-name")).toContainText(doctorName, { timeout: 20_000 });
    await expect(page.getByTestId("doctor-tab-overview")).toBeVisible();
    await page.getByTestId("doctor-tab-referrals").click();
    await expect(page.getByTestId("log-referral-btn")).toBeVisible();
    await assertControlReachable(page, page.getByTestId("log-referral-btn"));
    await assertNoHorizontalOverflow(page, "Doctor detail");
    await assertMobileNavToggle(page);
  });
});
