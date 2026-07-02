import { chromium } from "playwright";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(__dirname, "..", "..", "assets", "followups-verify.png");

const BASE = process.env.APP_URL || "http://localhost:3000";
const VIEWPORT = { width: 1440, height: 900 };

async function login(page) {
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

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: VIEWPORT });

await login(page);
if (page.url().includes("/login")) {
  console.error("FAIL: Could not log in.");
  await browser.close();
  process.exit(1);
}

await page.goto(`${BASE}/tasks`, { waitUntil: "networkidle", timeout: 60000 });
await page.waitForSelector('[data-testid="followups-kanban"]', { timeout: 15000 });
await page.waitForTimeout(1500);

const metrics = await page.evaluate(({ vw }) => {
  const text = document.body.innerText;
  return {
    hasKanban: !!document.querySelector('[data-testid="followups-kanban"]'),
    hasWeekCalendar: !!document.querySelector('[data-testid="week-calendar-grid"]'),
    hasViewTabs: text.includes("Hybrid") && [...document.querySelectorAll("button")].some((b) => b.textContent?.trim() === "Calendar"),
    hasDateNav: [...document.querySelectorAll("button")].some((b) => b.textContent?.trim() === "Today"),
    hasNotification: !!document.querySelector('header [aria-label="Notifications"]'),
    hasHospitalBadge: !!document.querySelector('[data-testid="hospital-name-badge"]'),
    searchInHeader: !!document.querySelector("header [data-testid='task-search']"),
    kanbanHeight: document.querySelector('[data-testid="followups-kanban"]')?.getBoundingClientRect().height ?? 0,
    pageOverflowPx: document.documentElement.scrollHeight - document.documentElement.clientHeight,
    hasHorizontalScroll: document.documentElement.scrollWidth > vw + 2,
    columnCount: document.querySelectorAll('[data-testid="followups-kanban"] .grid > .min-w-0').length,
  };
}, { vw: VIEWPORT.width });

console.log(JSON.stringify(metrics, null, 2));
await page.screenshot({ path: out, fullPage: false });
console.log("Saved:", out);

await browser.close();

const failures = [];
if (!metrics.hasKanban) failures.push("Kanban board missing");
if (metrics.hasViewTabs) failures.push("Calendar/Kanban/Hybrid tabs should be removed");
if (metrics.hasWeekCalendar) failures.push("Week calendar grid should be removed");
if (metrics.hasDateNav) failures.push("Date navigation should be removed");
if (metrics.hasNotification) failures.push("Notification bell should be hidden");
if (metrics.hasHospitalBadge) failures.push("Hospital badge should be hidden");
if (!metrics.searchInHeader) failures.push("Search should be in header");
if (metrics.kanbanHeight < 400) failures.push(`Kanban not full height: ${metrics.kanbanHeight}px`);
if (metrics.pageOverflowPx > 8) failures.push(`Page scrolls: ${metrics.pageOverflowPx}px overflow`);
if (metrics.columnCount !== 4) failures.push(`Expected 4 columns, got ${metrics.columnCount}`);

if (failures.length) {
  console.error("FAIL:\n" + failures.map((f) => `  - ${f}`).join("\n"));
  process.exit(1);
}

console.log("PASS: Follow-ups kanban-only layout verified");
