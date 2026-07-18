import { fromZonedTime, formatInTimeZone } from "date-fns-tz";

export const DEFAULT_HOSPITAL_TIMEZONE = "Asia/Kolkata";

/** Resolve IANA timezone from auth tenant (snake_case or camelCase). */
export function resolveHospitalTimezone(tenant) {
  const tz = tenant?.timezone ?? tenant?.Timezone;
  return typeof tz === "string" && tz.trim() ? tz.trim() : DEFAULT_HOSPITAL_TIMEZONE;
}

/**
 * Convert hospital wall-clock date+time to UTC ISO for API.
 * @param {string} dateStr YYYY-MM-DD
 * @param {string} timeStr HH:mm
 * @param {string} [timeZone]
 */
export function hospitalLocalToUtcIso(dateStr, timeStr, timeZone = DEFAULT_HOSPITAL_TIMEZONE) {
  if (!dateStr || !timeStr) return null;
  const wall = `${dateStr}T${timeStr.length === 5 ? `${timeStr}:00` : timeStr}`;
  return fromZonedTime(wall, timeZone).toISOString();
}

/** Format UTC instant as hospital-local 12h time (e.g. 11:00 AM). */
export function formatHospitalTime12h(iso, timeZone = DEFAULT_HOSPITAL_TIMEZONE) {
  if (!iso) return "—";
  return formatInTimeZone(new Date(iso), timeZone, "h:mm a");
}

/** Format UTC instant as hospital-local date. */
export function formatHospitalDate(iso, timeZone = DEFAULT_HOSPITAL_TIMEZONE, pattern = "d MMM yyyy") {
  if (!iso) return "—";
  return formatInTimeZone(new Date(iso), timeZone, pattern);
}

/** Format UTC instant as hospital-local date + time for cards. */
export function formatHospitalDateTime(iso, timeZone = DEFAULT_HOSPITAL_TIMEZONE, pattern = "MMM d, h:mm a") {
  if (!iso) return "—";
  return formatInTimeZone(new Date(iso), timeZone, pattern);
}

/** Split UTC ISO into hospital-local date (YYYY-MM-DD) and time (HH:mm) for form fields. */
export function utcIsoToHospitalFormParts(iso, timeZone = DEFAULT_HOSPITAL_TIMEZONE) {
  if (!iso) return { date: "", time: "10:00" };
  const d = new Date(iso);
  return {
    date: formatInTimeZone(d, timeZone, "yyyy-MM-dd"),
    time: formatInTimeZone(d, timeZone, "HH:mm"),
  };
}

/** Hospital calendar "today" as YYYY-MM-DD. */
export function hospitalTodayDateStr(timeZone = DEFAULT_HOSPITAL_TIMEZONE) {
  return formatInTimeZone(new Date(), timeZone, "yyyy-MM-dd");
}

/** Add Gregorian calendar days to a YYYY-MM-DD string. */
export function addCalendarDays(yyyyMmDd, deltaDays) {
  const [y, m, d] = yyyyMmDd.split("-").map(Number);
  const utc = new Date(Date.UTC(y, m - 1, d + deltaDays));
  const yy = utc.getUTCFullYear();
  const mm = String(utc.getUTCMonth() + 1).padStart(2, "0");
  const dd = String(utc.getUTCDate()).padStart(2, "0");
  return `${yy}-${mm}-${dd}`;
}

/** Next calendar day string (YYYY-MM-DD) in Gregorian arithmetic. */
function nextCalendarDay(yyyyMmDd) {
  return addCalendarDays(yyyyMmDd, 1);
}

/** Format a calendar date string (no timezone conversion). */
export function formatCalendarDateStr(yyyyMmDd, pattern = "MMM d") {
  if (!yyyyMmDd) return "—";
  const [y, m, d] = yyyyMmDd.split("-").map(Number);
  // Local Date used only to format Y/M/D components for UI labels.
  return formatInTimeZone(new Date(Date.UTC(y, m - 1, d, 12)), "UTC", pattern);
}

/** Sunday (weekStartsOn=0) of the hospital calendar week containing "today". */
export function hospitalStartOfWeekDateStr(timeZone = DEFAULT_HOSPITAL_TIMEZONE, weekStartsOn = 0) {
  const today = hospitalTodayDateStr(timeZone);
  // date-fns-tz: i = ISO day of week 1=Mon … 7=Sun
  const isoDow = Number(formatInTimeZone(new Date(), timeZone, "i"));
  const sunBased = isoDow % 7; // Sun=0 … Sat=6
  const diff = (sunBased - weekStartsOn + 7) % 7;
  return addCalendarDays(today, -diff);
}

/** UTC ISO range covering one hospital calendar day [from, to). */
export function hospitalDayRangeUtcIso(dateStr, timeZone = DEFAULT_HOSPITAL_TIMEZONE) {
  const day = dateStr || hospitalTodayDateStr(timeZone);
  const from = fromZonedTime(`${day}T00:00:00`, timeZone).toISOString();
  const to = fromZonedTime(`${nextCalendarDay(day)}T00:00:00`, timeZone).toISOString();
  return { from, to };
}

/** UTC ISO range covering 7 hospital calendar days starting at weekStartYyyyMmDd [from, to). */
export function hospitalWeekRangeUtcIso(weekStartYyyyMmDd, timeZone = DEFAULT_HOSPITAL_TIMEZONE) {
  const start = weekStartYyyyMmDd || hospitalStartOfWeekDateStr(timeZone);
  const from = fromZonedTime(`${start}T00:00:00`, timeZone).toISOString();
  const to = fromZonedTime(`${addCalendarDays(start, 7)}T00:00:00`, timeZone).toISOString();
  return { from, to };
}

/** Hospital calendar day (YYYY-MM-DD) for a UTC instant. */
export function hospitalDateStrFromIso(iso, timeZone = DEFAULT_HOSPITAL_TIMEZONE) {
  if (!iso) return "";
  return formatInTimeZone(new Date(iso), timeZone, "yyyy-MM-dd");
}

/** True if UTC instant falls on the given hospital calendar day (default today). */
export function isSameHospitalDay(iso, timeZone = DEFAULT_HOSPITAL_TIMEZONE, dayStr = null) {
  if (!iso) return false;
  const target = dayStr || hospitalTodayDateStr(timeZone);
  return hospitalDateStrFromIso(iso, timeZone) === target;
}
