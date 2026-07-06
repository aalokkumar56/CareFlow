import axios from "axios";
import { toast } from "sonner";
import { keysToSnakeCase, logToServer } from "@/lib/logger";
import {
  decrementGlobalLoader,
  incrementGlobalLoader,
  shouldSkipGlobalLoader,
} from "@/lib/globalLoader";

const envBackendUrl = process.env.NEXT_PUBLIC_BACKEND_URL?.trim();
const normalizedEnvBackendUrl = envBackendUrl
  ? envBackendUrl.replace(/\/+$/, "")
  : null;

export const BACKEND_URL =
  normalizedEnvBackendUrl ||
  (typeof window !== "undefined" ? window.location.origin : "http://localhost:5180");
export const API_BASE = normalizedEnvBackendUrl ? `${normalizedEnvBackendUrl}/api` : "/api";

export const api = axios.create({
  baseURL: API_BASE,
  headers: { "Content-Type": "application/json" },
});

const STATUS_MESSAGES = {
  401: "Your session has expired. Please sign in again.",
  403: "You don't have permission to access this.",
  404: "The requested resource was not found.",
  422: "Please check your input and try again.",
  500: "Something went wrong on our end. Please try again later.",
};

export const normalizeApiError = (error, fallbackMessage = "Request failed") => {
  const status = error?.response?.status;
  const data = error?.response?.data;

  if (status === 403) return STATUS_MESSAGES[403];
  if (status === 401) return STATUS_MESSAGES[401];
  if (status === 422) {
    if (data?.errors && typeof data.errors === "object") {
      const firstKey = Object.keys(data.errors)[0];
      const firstValue = firstKey ? data.errors[firstKey]?.[0] : null;
      if (firstValue) return firstValue;
    }
    return STATUS_MESSAGES[422];
  }
  if (status >= 500) return STATUS_MESSAGES[500];

  if (typeof data === "string" && data.trim()) return data;
  if (data?.error) return data.error;
  if (data?.detail) return data.detail;
  if (data?.title) return data.title;
  if (data?.message) return data.message;
  if (data?.errors && typeof data.errors === "object") {
    const firstKey = Object.keys(data.errors)[0];
    const firstValue = firstKey ? data.errors[firstKey]?.[0] : null;
    if (firstValue) return firstValue;
  }
  if (status && STATUS_MESSAGES[status]) return STATUS_MESSAGES[status];
  if (error?.code === "ERR_NETWORK") return "Unable to reach the server. Check your connection.";
  return fallbackMessage;
};

/** Show a toast for a failed API call (skips cancelled requests). */
export const toastApiError = (error, fallbackMessage = "Request failed") => {
  if (error?.code === "ERR_CANCELED") return;
  toast.error(normalizeApiError(error, fallbackMessage));
};

/** In-flight deduplication for identical GET requests (e.g. StrictMode double-mount). */
const inflightGets = new Map();

const getRequestKey = (config) => {
  const method = String(config.method || "get").toLowerCase();
  const base = config.baseURL || "";
  const url = config.url || "";
  const params = config.params ? JSON.stringify(config.params) : "";
  return `${method}:${base}${url}?${params}`;
};

const originalGet = api.get.bind(api);
api.get = (url, config = {}) => {
  // AbortSignal + dedupe breaks under React StrictMode: mount-1 abort cancels the
  // shared in-flight promise before mount-2 consumes a stale rejection.
  if (config?.signal) return originalGet(url, config);

  const key = getRequestKey({ ...config, method: "get", url, baseURL: api.defaults.baseURL });
  const existing = inflightGets.get(key);
  if (existing) return existing;

  const request = originalGet(url, config).finally(() => {
    if (inflightGets.get(key) === request) inflightGets.delete(key);
  });
  inflightGets.set(key, request);
  return request;
};

export const apiGet = async (url, config) => (await api.get(url, config)).data;
export const apiPost = async (url, payload) => (await api.post(url, payload)).data;
export const apiPut = async (url, payload) => (await api.put(url, payload)).data;
export const apiPatch = async (url, payload) => (await api.patch(url, payload)).data;
export const apiDelete = async (url) => (await api.delete(url)).data;

api.interceptors.request.use((cfg) => {
  const token = localStorage.getItem("cureflow_token");
  if (token) cfg.headers.Authorization = `Bearer ${token}`;

  if (
    cfg.data &&
    typeof cfg.data === "object" &&
    !(cfg.data instanceof FormData) &&
    ["post", "put", "patch"].includes(String(cfg.method || "").toLowerCase())
  ) {
    cfg.data = keysToSnakeCase(cfg.data);
  }

  if (!shouldSkipGlobalLoader(cfg)) {
    incrementGlobalLoader();
    cfg.__globalLoaderTracked = true;
  }

  return cfg;
});

api.interceptors.response.use(
  (r) => {
    if (r.config?.__globalLoaderTracked) decrementGlobalLoader();
    return r;
  },
  (err) => {
    if (err?.config?.__globalLoaderTracked) decrementGlobalLoader();
    if (err?.code === "ERR_CANCELED") return Promise.reject(err);

    const status = err.response?.status;
    const method = err.config?.method?.toUpperCase() ?? "HTTP";
    const path = err.config?.url ?? "";
    logToServer("error", `${method} ${path} failed (${status ?? "network"})`, "api");

    if (err.response?.status === 401) {
      localStorage.removeItem("cureflow_token");
      localStorage.removeItem("cureflow_user");
      if (window.location.pathname !== "/login") {
        window.location.href = "/login";
      }
    }
    return Promise.reject(err);
  }
);

export const formatPhone = (p) => {
  if (!p) return "";
  const s = String(p);
  if (s.length === 12 && s.startsWith("91")) {
    return `+91 ${s.slice(2, 7)} ${s.slice(7)}`;
  }
  return s.startsWith("+") ? s : `+${s}`;
};

export const STATUS_LABELS = {
  new_inquiry: "New Inquiry",
  contacted: "Contacted",
  appointment_scheduled: "Appt. Scheduled",
  follow_up_pending: "Follow-up Pending",
  visited: "Visited",
  no_response: "No Response",
  lost: "Lost",
  re_engagement: "Re-engagement",
};

export const STATUS_COLORS = {
  new_inquiry: "bg-blue-50 text-blue-700 border-blue-200",
  contacted: "bg-amber-50 text-amber-700 border-amber-200",
  appointment_scheduled: "bg-emerald-50 text-emerald-700 border-emerald-200",
  follow_up_pending: "bg-orange-50 text-orange-700 border-orange-200",
  visited: "bg-green-50 text-green-700 border-green-200",
  no_response: "bg-gray-50 text-gray-600 border-gray-200",
  lost: "bg-red-50 text-red-700 border-red-200",
  re_engagement: "bg-purple-50 text-purple-700 border-purple-200",
};

export const PRIORITY_COLORS = {
  low: "bg-gray-50 text-gray-600 border-gray-200",
  medium: "bg-blue-50 text-blue-700 border-blue-200",
  high: "bg-orange-50 text-orange-700 border-orange-200",
  emergency: "bg-red-50 text-red-700 border-red-200",
};

export const CATEGORY_LABELS = {
  appointment_inquiry: "Appointment",
  package_inquiry: "Package",
  emergency: "Emergency",
  reports: "Reports",
  follow_up: "Follow-up",
  referral: "Referral",
  general: "General",
};

export const BLOOD_GROUP_LABELS = {
  unknown: "Unknown",
  a_pos: "A+",
  a_neg: "A-",
  b_pos: "B+",
  b_neg: "B-",
  ab_pos: "AB+",
  ab_neg: "AB-",
  o_pos: "O+",
  o_neg: "O-",
};

export const formatBloodGroup = (value) => {
  if (value == null || value === "" || String(value).toLowerCase() === "unknown") return null;
  return BLOOD_GROUP_LABELS[String(value).toLowerCase()] || value;
};

export const GENDER_LABELS = {
  male: "Male",
  female: "Female",
  other: "Other",
  unknown: "Unknown",
};

export const formatGender = (value) => {
  if (value == null || value === "" || String(value).toLowerCase() === "unknown") return null;
  return GENDER_LABELS[String(value).toLowerCase()] || value;
};

export const formatPatientDemographics = (patient) => {
  const parts = [
    patient?.age ? `${patient.age}y` : null,
    formatGender(patient?.gender),
    formatBloodGroup(patient?.blood_group),
  ].filter(Boolean);
  return parts.length ? parts.join(" · ") : "—";
};

const authHeaders = () => {
  const token = localStorage.getItem("cureflow_token");
  return token ? { Authorization: `Bearer ${token}` } : {};
};

const withFetchLoader = async (path, config = {}, run) => {
  const skip = shouldSkipGlobalLoader({ ...config, url: path });
  if (!skip) incrementGlobalLoader();
  try {
    return await run();
  } finally {
    if (!skip) decrementGlobalLoader();
  }
};

/** Fetch a protected resource as a Blob (e.g. lab report download). */
export const fetchAuthorizedBlob = async (path, config = {}) =>
  withFetchLoader(path, config, async () => {
    const res = await fetch(`${API_BASE}${path}`, { headers: authHeaders() });
    if (!res.ok) throw new Error(`Download failed (${res.status})`);
    return res.blob();
  });

/** Open HTML from an authenticated endpoint in a new tab (e.g. prescription print). */
export const openAuthorizedHtml = async (path, config = {}) =>
  withFetchLoader(path, config, async () => {
    const res = await fetch(`${API_BASE}${path}`, { headers: authHeaders() });
    if (!res.ok) throw new Error(`Request failed (${res.status})`);
    const html = await res.text();
    const blob = new Blob([html], { type: "text/html;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const win = window.open(url, "_blank");
    if (win) setTimeout(() => URL.revokeObjectURL(url), 60_000);
    else URL.revokeObjectURL(url);
  });

/** Trigger a browser download for an authenticated file endpoint. */
export const downloadAuthorizedFile = async (path, filename = "download", config = {}) => {
  const blob = await fetchAuthorizedBlob(path, config);
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
};

/** Map hosted/protected media URLs to authenticated API paths (relative to API_BASE). */
export const resolveProtectedMediaPath = (url) => {
  if (!url || typeof url !== "string") return null;
  const normalized = url.trim();
  // Conversation-scoped CRM media (stored by CureFlow).
  if (normalized.startsWith("/api/conversations/messages/")) return normalized.replace(/^\/api/, "");
  if (normalized.startsWith("/conversations/messages/")) return normalized;
  if (normalized.startsWith("/api/whatsapp/media/")) return normalized.replace(/^\/api/, "");
  if (normalized.startsWith("http://") || normalized.startsWith("https://")) {
    try {
      const parsed = new URL(normalized);
      if (parsed.pathname.startsWith("/api/conversations/messages/"))
        return parsed.pathname.replace(/^\/api/, "");
      if (parsed.pathname.startsWith("/api/whatsapp/media/"))
        return parsed.pathname.replace(/^\/api/, "");
      const legacy = parsed.pathname.match(/\/webhook-media\/([^/?#]+)/);
      if (legacy) return `/whatsapp/media/${legacy[1]}`;
    } catch {
      return null;
    }
    return null;
  }
  const legacy = normalized.match(/\/webhook-media\/([^/?#]+)/);
  if (legacy) return `/whatsapp/media/${legacy[1]}`;
  return null;
};

export const isProtectedMediaUrl = (url) => Boolean(resolveProtectedMediaPath(url));

export const ALLERGY_TYPE_LABELS = {
  drug: "Drug / Medication",
  food: "Food",
  environmental: "Environmental",
  insect: "Insect / Sting",
  other: "Other",
};

export const ALLERGY_SEVERITY_LABELS = {
  mild: "Mild",
  moderate: "Moderate",
  severe: "Severe",
  life_threatening: "Life-threatening",
};

export const NOTE_TYPE_LABELS = {
  progress: "Progress Note",
  soap: "SOAP Note",
  discharge: "Discharge Summary",
  other: "Clinical Note",
};

export const MEDICAL_CATEGORY_LABELS = {
  condition: "Medical Condition",
  surgery: "Surgery",
  hospitalization: "Hospitalization",
  immunization: "Immunization",
};

export const FAMILY_RELATION_LABELS = {
  father: "Father",
  mother: "Mother",
  sibling: "Sibling",
  grandparent: "Grandparent",
  other: "Other relative",
};
