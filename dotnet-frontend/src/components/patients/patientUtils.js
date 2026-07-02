import {
  Heartbeat, Bone, Baby, Flower, Stethoscope, Drop, Ear, Tooth,
} from "@phosphor-icons/react";

const AVATAR_GRADIENTS = [
  "from-sky-300 to-blue-400",
  "from-violet-300 to-purple-400",
  "from-rose-300 to-pink-400",
  "from-emerald-300 to-teal-400",
  "from-amber-300 to-orange-400",
];

export const SOURCE_OPTIONS = [
  { value: "all", label: "All Sources" },
  { value: "website_form", label: "Website Form" },
  { value: "phone_call", label: "Phone Call" },
  { value: "whatsapp", label: "WhatsApp" },
  { value: "manual", label: "Manual Entry" },
  { value: "referral", label: "Referral" },
  { value: "csv_import", label: "CSV Import" },
];

export const DEPT_ICONS = {
  Cardiology: Heartbeat,
  Orthodontics: Tooth,
  Orthopedics: Bone,
  Pediatrics: Baby,
  Gynecology: Flower,
  "General Medicine": Stethoscope,
  "Diabetes Care": Drop,
  ENT: Ear,
  Dental: Tooth,
};

export function hashString(str) {
  let h = 0;
  for (let i = 0; i < (str || "").length; i += 1) {
    h = (h << 5) - h + str.charCodeAt(i);
    h |= 0;
  }
  return Math.abs(h);
}

export function avatarGradient(name) {
  return AVATAR_GRADIENTS[hashString(name) % AVATAR_GRADIENTS.length];
}

export function formatPatientId(patient) {
  if (!patient?.id) return "—";
  const year = patient.created_at
    ? new Date(patient.created_at).getFullYear()
    : new Date().getFullYear();
  const seq = hashString(patient.id).toString().slice(0, 6).padStart(6, "0");
  return `#PT-${year}-${seq}`;
}

export function isVipPatient(patient) {
  const tags = patient?.tags || [];
  return tags.some((t) => String(t).toLowerCase() === "vip");
}

export function formatSource(source) {
  if (!source) return "—";
  const map = {
    website: "Website Form",
    website_form: "Website Form",
    phone: "Phone Call",
    phone_call: "Phone Call",
    whatsapp: "WhatsApp",
    manual: "Manual Entry",
    referral: "Referral",
    csv_import: "CSV Import",
  };
  return map[source] || source.replace(/_/g, " ");
}

export function relativeTime(dateStr) {
  if (!dateStr) return "—";
  const d = new Date(dateStr);
  const now = new Date();
  const diffMs = now - d;
  const mins = Math.floor(diffMs / 60000);
  if (mins < 1) return "Just now";
  if (mins < 60) return `${mins} min ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs} hour${hrs === 1 ? "" : "s"} ago`;
  const days = Math.floor(hrs / 24);
  if (days < 7) return `${days} day${days === 1 ? "" : "s"} ago`;
  return d.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
}

export function formatRupee(amount) {
  if (amount == null || amount === "") return "—";
  return `₹${Number(amount).toLocaleString("en-IN")}`;
}

export function getInitials(name) {
  if (!name) return "?";
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0][0]?.toUpperCase() || "?";
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}
