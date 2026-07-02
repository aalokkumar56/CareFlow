const MAX_LOCAL = 200;
const localLogs = [];

const toSnakeCase = (key) =>
  key.replace(/[A-Z]/g, (letter) => `_${letter.toLowerCase()}`);

export const keysToSnakeCase = (value) => {
  if (value == null || typeof value !== "object") return value;
  if (value instanceof FormData || value instanceof Date) return value;
  if (Array.isArray(value)) return value.map(keysToSnakeCase);
  return Object.fromEntries(
    Object.entries(value).map(([key, nested]) => [toSnakeCase(key), keysToSnakeCase(nested)]),
  );
};

const writeLocal = (level, message, source = "ui", extra = {}) => {
  const entry = {
    level,
    message: String(message),
    source,
    url: extra.url ?? (typeof window !== "undefined" ? window.location.href : ""),
    at: new Date().toISOString(),
  };
  localLogs.push(entry);
  if (localLogs.length > MAX_LOCAL) localLogs.shift();

  if (process.env.NODE_ENV === "development") {
    try {
      const key = `cureflow_client_logs_${new Date().toISOString().slice(0, 10)}`;
      const stored = JSON.parse(localStorage.getItem(key) || "[]");
      stored.push(entry);
      if (stored.length > MAX_LOCAL) stored.shift();
      localStorage.setItem(key, JSON.stringify(stored));
    } catch {
      // ignore quota / private mode
    }
  }
};

const formatArgs = (args) =>
  args
    .map((arg) => {
      if (typeof arg === "string") return arg;
      try {
        return JSON.stringify(arg);
      } catch {
        return String(arg);
      }
    })
    .join(" ");

export const initClientLogger = () => {
  ["debug", "info", "warn", "error"].forEach((level) => {
    const original = console[level]?.bind(console);
    if (!original) return;

    console[level] = (...args) => {
      original(...args);
      writeLocal(level, formatArgs(args), "console");
    };
  });

  window.addEventListener("error", (event) => {
    writeLocal("error", event.message || "Unhandled error", "window", { url: event.filename });
  });

  window.addEventListener("unhandledrejection", (event) => {
    const reason = event.reason?.message || String(event.reason ?? "Unhandled rejection");
    writeLocal("error", reason, "promise");
  });

  writeLocal("info", "UI session started", "bootstrap");
};

export const logClientEvent = (level, message, source = "app") => writeLocal(level, message, source);

/** @deprecated kept for api.js — logs locally only, no network call */
export const logToServer = (level, message, source = "app") => writeLocal(level, message, source);

export const getClientLogs = () => [...localLogs];
