import { chromium } from "playwright";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(__dirname, "..", "..", "assets", "appointments-verify.png");

const BASE = process.env.APP_URL || "http://localhost:3000";

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

async function login() {
  await page.goto(`${BASE}/login`, { waitUntil: "networkidle", timeout: 60000 });
  const email = page.locator('[data-testid="login-email"]');
  const password = page.locator('[data-testid="login-password"]');
  if (await email.count()) {
    await email.fill("admin@cureflow.in");
    await password.fill("admin123");
    await page.locator('[data-testid="login-form"]').evaluate((form) => form.requestSubmit());
    await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 15000 }).catch(() => {});
  }
}

await login();
if (page.url().includes("/login")) {
  console.error("FAIL: Could not log in — ensure backend API and frontend dev server are running.");
  await page.screenshot({ path: out.replace(".png", "-login-fail.png") });
  await browser.close();
  process.exit(1);
}
await page.goto(`${BASE}/appointments`, { waitUntil: "networkidle", timeout: 60000 });
await page.waitForSelector('[data-testid="appointments-kanban"]', { timeout: 15000 });
await page.waitForTimeout(1500);

const metrics = await page.evaluate(() => {
  const searchLabel = document.querySelector('[data-testid="appt-search"]')?.closest("label");
  const searchIcon = searchLabel?.querySelector("svg");

  let searchIconOffsetPx = null;
  if (searchLabel && searchIcon) {
    const labelRect = searchLabel.getBoundingClientRect();
    const iconRect = searchIcon.getBoundingClientRect();
    searchIconOffsetPx = Math.abs(
      (labelRect.top + labelRect.height / 2) - (iconRect.top + iconRect.height / 2),
    );
  }

  return {
    hasCalendarGrid: !!document.querySelector('[data-testid="week-calendar-grid"]'),
    hasKanban: !!document.querySelector('[data-testid="appointments-kanban"]'),
    kanbanHeight: document.querySelector('[data-testid="appointments-kanban"]')?.getBoundingClientRect().height ?? 0,
    hasNotification: !!document.querySelector('header [aria-label="Notifications"]'),
    hasHospitalBadge: !!document.querySelector('[data-testid="hospital-name-badge"]'),
    searchInHeader: !!document.querySelector("header [data-testid='appt-search']"),
    searchIconOffsetPx,
    draggableCards: document.querySelectorAll('[draggable="true"]').length,
  };
});

console.log(JSON.stringify(metrics, null, 2));
await page.screenshot({ path: out, fullPage: false });
console.log("Saved:", out);

await browser.close();

const failures = [];
if (metrics.hasCalendarGrid) failures.push("Week calendar grid should be removed");
if (!metrics.hasKanban) failures.push("Kanban board missing");
if (metrics.kanbanHeight < 400) failures.push(`Kanban not full height: ${metrics.kanbanHeight}px`);
if (metrics.hasNotification) failures.push("Notification bell should be hidden");
if (metrics.hasHospitalBadge) failures.push("Hospital badge should be hidden");
if (!metrics.searchInHeader) failures.push("Search should be in header top-right");
if (metrics.searchIconOffsetPx == null || metrics.searchIconOffsetPx > 2) {
  failures.push(`Search icon misaligned: offset ${metrics.searchIconOffsetPx}px`);
}

if (failures.length) {
  console.error("FAIL:\n" + failures.map((f) => `  - ${f}`).join("\n"));
  process.exit(1);
}

console.log("PASS: Appointments kanban-only layout verified");
