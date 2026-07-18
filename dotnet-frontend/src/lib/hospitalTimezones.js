/** Curated IANA timezones for hospital settings (value = IANA id). */
export const HOSPITAL_TIMEZONES = [
  { value: "Asia/Kolkata", label: "India (Asia/Kolkata) — IST" },
  { value: "Asia/Dubai", label: "UAE (Asia/Dubai) — GST" },
  { value: "Asia/Singapore", label: "Singapore (Asia/Singapore) — SGT" },
  { value: "Europe/London", label: "United Kingdom (Europe/London)" },
  { value: "Europe/Paris", label: "Central Europe (Europe/Paris)" },
  { value: "America/New_York", label: "US Eastern (America/New_York)" },
  { value: "America/Chicago", label: "US Central (America/Chicago)" },
  { value: "America/Denver", label: "US Mountain (America/Denver)" },
  { value: "America/Los_Angeles", label: "US Pacific (America/Los_Angeles)" },
  { value: "America/Toronto", label: "Canada Eastern (America/Toronto)" },
  { value: "America/Vancouver", label: "Canada Pacific (America/Vancouver)" },
  { value: "Australia/Sydney", label: "Australia Eastern (Australia/Sydney)" },
];

export function labelForTimezone(ianaId) {
  const hit = HOSPITAL_TIMEZONES.find((z) => z.value === ianaId);
  return hit?.label || ianaId;
}

/** Detect browser IANA timezone (suggestion only — never auto-apply as hospital TZ). */
export function detectBrowserTimezone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
  } catch {
    return null;
  }
}
