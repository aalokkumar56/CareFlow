import {
  HOSPITAL_TIMEZONES,
  detectBrowserTimezone,
  labelForTimezone,
} from "./hospitalTimezones";

describe("hospitalTimezones", () => {
  test("includes Asia/Kolkata and America/New_York", () => {
    const values = HOSPITAL_TIMEZONES.map((z) => z.value);
    expect(values).toContain("Asia/Kolkata");
    expect(values).toContain("America/New_York");
  });

  test("labelForTimezone returns curated label", () => {
    expect(labelForTimezone("Asia/Kolkata")).toMatch(/India/);
  });

  test("detectBrowserTimezone returns a string or null", () => {
    const tz = detectBrowserTimezone();
    expect(tz === null || typeof tz === "string").toBe(true);
  });
});
