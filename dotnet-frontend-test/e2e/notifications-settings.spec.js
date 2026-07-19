const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");

const CATEGORY_SECTIONS = ["Inbox", "Appointments", "Tasks", "Campaigns", "Clinical", "Patients"];

test.describe("Notifications settings page", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/settings/notifications");
    await expect(page.getByTestId("page-title")).toHaveText("Notifications");
  });

  test("page loads with title and category sections", async ({ page }) => {
    await expect(page.getByTestId("page-title")).toHaveText("Notifications");

    for (const category of CATEGORY_SECTIONS) {
      await expect(page.getByRole("heading", { name: category, exact: true })).toBeVisible();
    }
  });

  test("scroll reaches bottom toggles and save buttons", async ({ page }) => {
    const scrollContainer = page.getByTestId("notifications-prefs-scroll");
    await expect(scrollContainer).toBeVisible();

    const saveBtn = page.getByRole("button", { name: "Save preferences" });
    const resetBtn = page.getByRole("button", { name: "Reset to role defaults" });

    await scrollContainer.evaluate((el) => {
      el.scrollTop = el.scrollHeight;
    });

    await expect(saveBtn).toBeVisible();
    await expect(resetBtn).toBeVisible();
    await expect(saveBtn).toBeEnabled();

    const lastSwitch = page.getByRole("switch").last();
    await lastSwitch.scrollIntoViewIfNeeded();
    await expect(lastSwitch).toBeVisible();
    await expect(lastSwitch).toBeEnabled();
  });

  test("My preferences and Role defaults tabs work", async ({ page }) => {
    const personalTab = page.getByRole("tab", { name: "My preferences", exact: true });
    const rolesTab = page.getByRole("tab", { name: "Role defaults", exact: true });

    await expect(personalTab).toBeVisible();
    await expect(rolesTab).toBeVisible();

    await expect(page.getByRole("heading", { name: "Inbox", exact: true })).toBeVisible();

    await rolesTab.click();
    await expect(page.getByRole("columnheader", { name: "Type" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Save role defaults" })).toBeVisible();

    await personalTab.click();
    await expect(page.getByRole("heading", { name: "Inbox", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Save preferences" })).toBeVisible();
  });

  test("mobile viewport: content scrollable without clipping", async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto("/settings/notifications");
    await expect(page.getByTestId("page-title")).toHaveText("Notifications");
    await expect(page.getByRole("switch").first()).toBeVisible();

    await expect(page.getByTestId("app-sidebar")).toBeHidden();

    const scrollContainer = page.getByTestId("notifications-prefs-scroll");
    await expect(scrollContainer).toBeVisible();

    const { scrollHeight, clientHeight } = await scrollContainer.evaluate((el) => ({
      scrollHeight: el.scrollHeight,
      clientHeight: el.clientHeight,
    }));
    expect(scrollHeight).toBeGreaterThan(clientHeight);

    await scrollContainer.evaluate((el) => {
      el.scrollTop = el.scrollHeight;
    });

    const saveBtn = page.getByRole("button", { name: "Save preferences" });
    await expect(saveBtn).toBeVisible();

    const box = await saveBtn.boundingBox();
    expect(box).not.toBeNull();
    expect(box.y + box.height).toBeLessThanOrEqual(667 + 2);
  });
});
