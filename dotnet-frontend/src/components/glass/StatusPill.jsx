import React from "react";
import { cn } from "@/lib/utils";

const STATUS_STYLES = {
  new_inquiry: "bg-blue-50 text-blue-700 border-blue-200",
  follow_up: "bg-amber-50 text-amber-700 border-amber-200",
  follow_up_pending: "bg-amber-50 text-amber-700 border-amber-200",
  converted: "bg-emerald-50 text-emerald-700 border-emerald-200",
  lost: "bg-red-50 text-red-700 border-red-200",
  scheduled: "bg-violet-50 text-violet-700 border-violet-200",
  confirmed: "bg-sky-50 text-sky-700 border-sky-200",
  completed: "bg-emerald-50 text-emerald-700 border-emerald-200",
  no_show: "bg-red-50 text-red-700 border-red-200",
  cancelled: "bg-gray-50 text-gray-600 border-gray-200",
  pending: "bg-amber-50 text-amber-700 border-amber-200",
  in_progress: "bg-blue-50 text-blue-700 border-blue-200",
  done: "bg-emerald-50 text-emerald-700 border-emerald-200",
  draft: "bg-gray-50 text-gray-600 border-gray-200",
  sending: "bg-amber-50 text-amber-700 border-amber-200",
  sent: "bg-emerald-50 text-emerald-700 border-emerald-200",
  high: "bg-red-50 text-red-700 border-red-200",
  medium: "bg-amber-50 text-amber-700 border-amber-200",
  low: "bg-blue-50 text-blue-700 border-blue-200",
  emergency: "bg-red-100 text-red-800 border-red-300",
};

const VARIANT_STYLES = {
  muted: "bg-white/60 text-text-secondary border-white/60",
  neutral: "bg-secondary text-text-secondary border-subtle",
};

const StatusPill = ({ status, label, className, variant }) => {
  const key = (status || "").toLowerCase();
  const text = label || key.replace(/_/g, " ");
  return (
    <span
      className={cn(
        "status-pill capitalize",
        variant ? VARIANT_STYLES[variant] : (STATUS_STYLES[key] || VARIANT_STYLES.neutral),
        className,
      )}
    >
      {text}
    </span>
  );
};

export default StatusPill;
