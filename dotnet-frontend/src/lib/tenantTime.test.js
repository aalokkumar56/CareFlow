import {
  hospitalLocalToUtcIso,
  formatHospitalTime12h,
  formatHospitalDate,
  utcIsoToHospitalFormParts,
  resolveHospitalTimezone,
  hospitalDayRangeUtcIso,
  hospitalWeekRangeUtcIso,
  addCalendarDays,
  DEFAULT_HOSPITAL_TIMEZONE,
} from "./tenantTime";

describe("tenantTime", () => {
  test("resolveHospitalTimezone defaults to Asia/Kolkata", () => {
    expect(resolveHospitalTimezone(null)).toBe(DEFAULT_HOSPITAL_TIMEZONE);
    expect(resolveHospitalTimezone({ timezone: "America/New_York" })).toBe("America/New_York");
  });

  test("11:00 Asia/Kolkata stores as 05:30Z", () => {
    const iso = hospitalLocalToUtcIso("2026-07-18", "11:00", "Asia/Kolkata");
    expect(iso).toBe("2026-07-18T05:30:00.000Z");
  });

  test("05:30Z displays as 11:00 AM in Kolkata", () => {
    expect(formatHospitalTime12h("2026-07-18T05:30:00.000Z", "Asia/Kolkata")).toBe("11:00 AM");
    expect(formatHospitalDate("2026-07-18T05:30:00.000Z", "Asia/Kolkata", "dd MMM yyyy")).toBe("18 Jul 2026");
  });

  test("form parts round-trip hospital wall clock", () => {
    const iso = hospitalLocalToUtcIso("2026-07-18", "11:00", "Asia/Kolkata");
    expect(utcIsoToHospitalFormParts(iso, "Asia/Kolkata")).toEqual({
      date: "2026-07-18",
      time: "11:00",
    });
  });

  test("hospital day range for Kolkata spans previous UTC evening", () => {
    const { from, to } = hospitalDayRangeUtcIso("2026-07-18", "Asia/Kolkata");
    expect(from).toBe("2026-07-17T18:30:00.000Z");
    expect(to).toBe("2026-07-18T18:30:00.000Z");
  });

  test("hospital week range is seven hospital days", () => {
    const { from, to } = hospitalWeekRangeUtcIso("2026-07-12", "Asia/Kolkata");
    expect(from).toBe("2026-07-11T18:30:00.000Z");
    expect(to).toBe("2026-07-18T18:30:00.000Z");
    expect(addCalendarDays("2026-07-12", 7)).toBe("2026-07-19");
  });
});
