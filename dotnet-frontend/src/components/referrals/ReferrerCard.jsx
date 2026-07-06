import React from "react";
import { Link } from "@/lib/navigation";
import { Users, CurrencyInr } from "@phosphor-icons/react";
import { cn } from "@/lib/utils";

const AVATAR_COLORS = [
  "bg-sky-100 text-sky-700",
  "bg-violet-100 text-violet-700",
  "bg-amber-100 text-amber-800",
  "bg-emerald-100 text-emerald-700",
  "bg-rose-100 text-rose-700",
  "bg-indigo-100 text-indigo-700",
];

export const getReferrerInitials = (name = "") => {
  const parts = name.replace(/^Dr\.?\s*/i, "").trim().split(/\s+/).filter(Boolean);
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  return (parts[0]?.slice(0, 2) || "?").toUpperCase();
};

export const getReferrerAvatarColor = (name = "") => {
  let hash = 0;
  for (let i = 0; i < name.length; i += 1) hash = name.charCodeAt(i) + ((hash << 5) - hash);
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
};

const ReferrerCard = ({ doctor, className }) => {
  const initials = getReferrerInitials(doctor.name);
  const avatarColor = getReferrerAvatarColor(doctor.name);
  const location = [doctor.clinic, doctor.specialty].filter(Boolean).join(" · ") || "—";

  return (
    <Link
      to={`/doctors/${doctor.id}`}
      data-testid={`doctor-row-${doctor.id}`}
      draggable={false}
      className={cn(
        "block w-full min-w-0 rounded-xl border border-white/70 bg-white/85 shadow-sm",
        "px-2.5 py-2 hover:bg-white/95 transition-colors",
        className,
      )}
    >
      <div className="flex items-start gap-2 min-w-0">
        <div className={cn("w-9 h-9 rounded-full flex items-center justify-center text-ui-label font-bold shrink-0", avatarColor)}>
          {initials}
        </div>
        <div className="min-w-0 flex-1">
          <div className="font-semibold text-ui-sm text-[#022C22] truncate">{doctor.name}</div>
          {doctor.specialty && (
            <div className="text-ui-caption text-text-secondary truncate">{doctor.specialty}</div>
          )}
          {doctor.clinic && (
            <div className="text-ui-caption text-text-muted truncate">{doctor.clinic}</div>
          )}
          {!doctor.specialty && !doctor.clinic && (
            <div className="text-ui-caption text-text-muted truncate">{location}</div>
          )}
        </div>
      </div>
      <div className="mt-1.5 flex items-center justify-between gap-2">
        <span className="inline-flex items-center gap-0.5 text-ui-caption text-text-secondary bg-white/70 border border-white/60 rounded-md px-1.5 py-0.5">
          <Users weight="fill" className="w-3 h-3 text-text-muted" />
          {doctor.patients_referred ?? 0}
        </span>
        {(doctor.total_revenue ?? 0) > 0 && (
          <span
            className="inline-flex items-center gap-0.5 text-ui-caption font-medium text-[#022C22] bg-emerald-50/80 border border-emerald-100 rounded-md px-1.5 py-0.5"
            data-testid={`doctor-revenue-${doctor.id}`}
          >
            <CurrencyInr weight="bold" className="w-3 h-3 text-emerald-600" />
            ₹{(doctor.total_revenue ?? 0).toLocaleString("en-IN")}
          </span>
        )}
      </div>
    </Link>
  );
};

export default ReferrerCard;
