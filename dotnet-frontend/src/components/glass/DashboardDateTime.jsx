import React from "react";
import { CalendarBlank } from "@phosphor-icons/react";
import { useLiveClock } from "@/hooks/useLiveClock";
import { cn } from "@/lib/utils";

const DashboardDateTime = ({ className }) => {
  const { date, time } = useLiveClock();

  return (
    <div
      data-testid="dashboard-datetime"
      className={cn(
        "items-center gap-1.5 px-3 py-1.5 rounded-full glass-input text-xs text-slate-700 font-medium whitespace-nowrap",
        className,
      )}
    >
      <CalendarBlank weight="regular" className="w-3.5 h-3.5 text-slate-500 shrink-0" />
      <span>{date}</span>
      <span className="text-slate-300" aria-hidden="true">·</span>
      <time className="text-slate-500 tabular-nums">{time}</time>
    </div>
  );
};

export default DashboardDateTime;
