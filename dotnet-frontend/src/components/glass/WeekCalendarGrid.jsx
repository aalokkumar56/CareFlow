import React, { useMemo } from "react";
import { addDays, format, isSameDay } from "date-fns";
import { cn } from "@/lib/utils";
import GlassCard from "./GlassCard";

const HOURS = [9, 10, 11, 12, 13, 14, 15, 16];
const HOUR_START = HOURS[0];
const HOUR_END = HOURS[HOURS.length - 1];
const HOUR_SPAN = HOUR_END - HOUR_START + 1;
const ROW_HEIGHT = "2.75rem";

const DEPT_COLORS = {
  Cardiology: "bg-violet-100 border-violet-200/80 text-violet-900",
  Orthopedics: "bg-sky-100 border-sky-200/80 text-sky-900",
  Pediatrics: "bg-pink-100 border-pink-200/80 text-pink-900",
  Gynecology: "bg-rose-100 border-rose-200/80 text-rose-900",
  "General Medicine": "bg-emerald-100 border-emerald-200/80 text-emerald-900",
  "Diabetes Care": "bg-amber-100 border-amber-200/80 text-amber-900",
  ENT: "bg-orange-100 border-orange-200/80 text-orange-900",
  Dental: "bg-teal-100 border-teal-200/80 text-teal-900",
};

const formatHour = (h) => {
  if (h === 12) return "12 PM";
  if (h < 12) return `${h} AM`;
  return `${h - 12} PM`;
};

const isInCalendarHours = (date) => {
  if (!date) return false;
  const hour = date.getHours();
  return hour >= HOUR_START && hour <= HOUR_END;
};

const getTimeTopPercent = (date) => {
  if (!date) return 0;
  const hour = date.getHours();
  const minute = date.getMinutes();
  const offset = (hour - HOUR_START) + minute / 60;
  const clamped = Math.max(0, Math.min(HOUR_SPAN - 0.05, offset));
  return (clamped / HOUR_SPAN) * 100;
};

const WeekCalendarGrid = ({
  weekStart,
  items,
  getItemDate,
  renderCard,
  getItemKey,
  getGroupLabel,
  className,
  viewMode = "week",
  focusDay,
}) => {
  const allDays = useMemo(
    () => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)),
    [weekStart],
  );

  const days = viewMode === "day" && focusDay ? [focusDay] : allDays;
  const dayCount = days.length;

  const grouped = useMemo(() => {
    const map = {};
    days.forEach((d) => { map[d.toISOString()] = []; });
    items.forEach((item) => {
      const dt = getItemDate(item);
      if (!dt || !isInCalendarHours(dt)) return;
      const dayKey = days.find((d) => isSameDay(d, dt))?.toISOString();
      if (dayKey) map[dayKey].push(item);
    });
    Object.keys(map).forEach((key) => {
      map[key].sort((a, b) => getItemDate(a) - getItemDate(b));
    });
    return map;
  }, [days, items, getItemDate]);

  const gridTemplateColumns = viewMode === "day"
    ? `3rem 1fr`
    : `3rem repeat(${dayCount}, minmax(0, 1fr))`;

  return (
    <GlassCard
      padding={false}
      className={cn("overflow-hidden", className)}
    >
      <div
        data-testid="week-calendar-grid"
        className="week-calendar-grid grid w-full"
        style={{
          gridTemplateColumns,
          gridTemplateRows: `auto repeat(${HOUR_SPAN}, ${ROW_HEIGHT})`,
        }}
      >
        {/* Corner + day headers (row 1) */}
        <div className="border-b border-slate-200/60 bg-white/20 min-h-[3rem]" style={{ gridColumn: 1, gridRow: 1 }} />
        {days.map((day, i) => {
          const isToday = isSameDay(day, new Date());
          return (
            <div
              key={day.toISOString()}
              style={{ gridColumn: i + 2, gridRow: 1 }}
              className={cn(
                "flex flex-col items-center justify-center border-b border-l border-slate-200/50 px-1 py-2 min-h-[3rem]",
                isToday && "bg-teal-50/90 border-x border-teal-200/70",
              )}
            >
              <div className={cn(
                "text-[10px] uppercase tracking-wider font-semibold leading-none",
                isToday ? "text-teal-700" : "text-slate-400",
              )}>
                {format(day, "EEE")}
              </div>
              <div className={cn(
                "text-xs font-semibold mt-1 leading-none",
                isToday ? "text-teal-800" : "text-slate-800",
              )}>
                {format(day, "MMM d")}
              </div>
            </div>
          );
        })}

        {/* Hour rows */}
        {HOURS.map((h, hourIdx) => (
          <React.Fragment key={h}>
            <div
              style={{ gridColumn: 1, gridRow: hourIdx + 2 }}
              className="flex items-start justify-end px-1.5 pt-1 text-[10px] text-slate-400 border-b border-r border-slate-200/50 bg-white/10"
            >
              {formatHour(h)}
            </div>
            {days.map((day, dayIdx) => {
              const isToday = isSameDay(day, new Date());
              return (
                <div
                  key={`${day.toISOString()}-${h}`}
                  style={{ gridColumn: dayIdx + 2, gridRow: hourIdx + 2 }}
                  className={cn(
                    "border-b border-l border-slate-100/80",
                    isToday && "bg-teal-50/30",
                  )}
                />
              );
            })}
          </React.Fragment>
        ))}

        {/* Appointment overlays — one per day column, spanning all hour rows */}
        {days.map((day, dayIdx) => {
          const dayItems = grouped[day.toISOString()] || [];
          return (
            <div
              key={`overlay-${day.toISOString()}`}
              style={{ gridColumn: dayIdx + 2, gridRow: `2 / ${HOUR_SPAN + 2}` }}
              className="relative z-[1] pointer-events-none min-h-0"
            >
              {dayItems.slice(0, 5).map((item) => {
                const dt = getItemDate(item);
                const dept = getGroupLabel?.(item) || "";
                return (
                  <div
                    key={getItemKey(item)}
                    className="absolute left-0.5 right-0.5 pointer-events-auto"
                    style={{ top: `${getTimeTopPercent(dt)}%` }}
                  >
                    {renderCard?.(item, { compact: true, colorClass: DEPT_COLORS[dept] })}
                  </div>
                );
              })}
              {dayItems.length > 5 && (
                <div className="absolute bottom-1 left-1 text-[10px] text-teal-700 font-medium pointer-events-auto">
                  +{dayItems.length - 5} more
                </div>
              )}
            </div>
          );
        })}
      </div>
    </GlassCard>
  );
};

export { DEPT_COLORS, HOURS };
export default WeekCalendarGrid;
