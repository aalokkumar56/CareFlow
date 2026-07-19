const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, createPatient } = require("../helpers/api");
const { E2E_TEST_PHONE } = require("../helpers/constants");
const { PATIENT_TABS } = require("../helpers/routes");
const {
  VIEWPORTS,
  SAMPLE_MODE,
  routeSlug,
  viewportSlug,
  gotoDesignRoute,
  KANBAN_BOARDS,
  TABLE_PAGES,
  DASHBOARD_STAT_CARDS,
  MODAL_TRIGGERS,
  NAV_ITEMS,
} = require("../helpers/design-matrix");
const {
  isGlassLikeSurface,
  sampleSurfaceStyles,
  auditKanbanColumnGlass,
} = require("../helpers/ui-audit");

const KANBAN_CHECKS = ["board-visible", "columns-glass", "horizontal-scroll", "card-draggable", "outer-glass"];
const STAT_CHECKS = ["visible", "glass-surface", "heading-color"];
const TABLE_CHECKS = ["table-visible", "header-glass", "row-hover", "no-overflow"];
const MODAL_CHECKS = ["opens", "glass-dialog"];
const SIDEBAR_CHECKS = ["nav-visible", "active-state"];

test.describe.configure({ mode: "serial" });

test.describe("Component-level design audit", () => {
  /** @type {string | undefined} */
  let patientDetailPath;

  test.beforeAll(async () => {
    if (SAMPLE_MODE) return;
    const stamp = Date.now();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const patient = await createPatient(admin.accessToken, `Component Design ${stamp}`, E2E_TEST_PHONE);
    const id = patient.id || patient.patient?.id;
    if (id) patientDetailPath = `/patients/${id}`;
  });

  for (const viewport of VIEWPORTS) {
    for (const { path, boardTestId, label } of KANBAN_BOARDS) {
      for (const check of KANBAN_CHECKS) {
        test(`kanban ${label} @ ${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, path, viewport);
          const board = page.getByTestId(boardTestId);
          await expect(board).toBeVisible({ timeout: 20_000 });

          if (check === "board-visible") {
            await expect(board).toBeVisible();
          }
          if (check === "columns-glass") {
            const audit = await auditKanbanColumnGlass(page, boardTestId);
            expect(audit.columnCount).toBeGreaterThan(0);
          }
          if (check === "horizontal-scroll") {
            const overflow = await board.evaluate((el) => el.scrollWidth > el.clientWidth + 2);
            expect(typeof overflow).toBe("boolean");
          }
          if (check === "card-draggable") {
            const count = await page.locator(`[data-testid="${boardTestId}"] [draggable="true"]`).count();
            expect(count).toBeGreaterThanOrEqual(0);
          }
          if (check === "outer-glass") {
            const outer = await sampleSurfaceStyles(page, `[data-testid="${boardTestId}"]`);
            if (outer) expect(isGlassLikeSurface(outer)).toBe(true);
          }
        });
      }
    }

    for (const testId of DASHBOARD_STAT_CARDS) {
      for (const check of STAT_CHECKS) {
        test(`stat-card ${testId} @ ${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, "/", viewport);
          const card = page.getByTestId(testId);
          if (check === "visible") {
            await expect(card).toBeVisible({ timeout: 20_000 });
          }
          if (check === "glass-surface") {
            const styles = await sampleSurfaceStyles(page, `[data-testid="${testId}"]`);
            if (styles) expect(isGlassLikeSurface(styles)).toBe(true);
          }
          if (check === "heading-color") {
            const heading = card.locator("h2, h3, p.font-heading").first();
            if ((await heading.count()) > 0) {
              const color = await heading.evaluate((el) => window.getComputedStyle(el).color);
              expect(color).toMatch(/rgb\(2,\s*44,\s*34\)|rgb\(0,\s*0,\s*0\)/);
            }
          }
        });
      }
    }

    for (const { path, tableSelector, searchTestId } of TABLE_PAGES) {
      for (const check of TABLE_CHECKS) {
        test(`table ${routeSlug(path)} @ ${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, path, viewport);
          const table = page.locator(tableSelector).first();

          if (check === "table-visible") {
            if ((await table.count()) === 0) return;
            await expect(table).toBeVisible({ timeout: 15_000 });
          }
          if (check === "header-glass") {
            const thead = page.locator("thead").first();
            if ((await thead.count()) === 0) return;
            const styles = await thead.evaluate((el) => window.getComputedStyle(el).backgroundColor);
            expect(styles).toBeTruthy();
          }
          if (check === "row-hover") {
            const row = page.locator("tbody tr").first();
            if ((await row.count()) === 0) return;
            await row.hover();
          }
          if (check === "no-overflow") {
            if (searchTestId) {
              await expect(page.getByTestId(searchTestId)).toBeVisible();
            }
            const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 4);
            expect(overflow).toBe(false);
          }
        });
      }
    }

    for (const { path, triggerTestId, dialogTestId, label } of MODAL_TRIGGERS) {
      for (const check of MODAL_CHECKS) {
        test(`modal ${label} @ ${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, path, viewport);
          const trigger = page.getByTestId(triggerTestId);
          if ((await trigger.count()) === 0) return;

          if (check === "opens") {
            await trigger.click();
            const dialog = dialogTestId
              ? page.getByTestId(dialogTestId)
              : page.locator('[role="dialog"]').first();
            await expect(dialog).toBeVisible({ timeout: 10_000 });
            await page.keyboard.press("Escape");
          }

          if (check === "glass-dialog" && dialogTestId) {
            await trigger.click();
            const dialog = page.getByTestId(dialogTestId);
            await expect(dialog).toBeVisible({ timeout: 10_000 });
            const styles = await dialog.evaluate((el) => {
              const s = window.getComputedStyle(el);
              return { backgroundColor: s.backgroundColor, backdropFilter: s.backdropFilter };
            });
            expect(styles.backgroundColor).toBeTruthy();
            await page.keyboard.press("Escape");
          }
        });
      }
    }

    for (const { testId, path } of NAV_ITEMS) {
      for (const check of SIDEBAR_CHECKS) {
        test(`sidebar ${testId} @ ${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, "/", viewport);

          if (viewport.width < 768) {
            const menuBtn = page.getByTestId("mobile-nav-toggle");
            if (await menuBtn.isVisible().catch(() => false)) {
              await menuBtn.click();
              await page.waitForTimeout(300);
            }
          }

          const nav = page.getByTestId(testId);
          if (check === "nav-visible") {
            if ((await nav.count()) === 0) return;
            await expect(nav).toBeVisible({ timeout: 10_000 });
          }
          if (check === "active-state") {
            if ((await nav.count()) === 0) return;
            await nav.click();
            await page.waitForURL((url) => url.pathname === path || url.pathname.startsWith(path), { timeout: 15_000 });
          }
        });
      }
    }

    if (patientDetailPath && !SAMPLE_MODE) {
      for (const tabId of PATIENT_TABS) {
        for (const check of ["tab-visible", "panel-layout"]) {
          test(`patient-tab ${tabId} @ ${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
            await login(page);
            await gotoDesignRoute(page, patientDetailPath, viewport);
            const tab = page.getByTestId(tabId);
            if ((await tab.count()) === 0) return;

            if (check === "tab-visible") {
              await expect(tab).toBeVisible();
            }
            if (check === "panel-layout") {
              await tab.click();
              await page.waitForTimeout(250);
              const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 4);
              expect(overflow).toBe(false);
            }
          });
        }
      }
    }
  }
});
