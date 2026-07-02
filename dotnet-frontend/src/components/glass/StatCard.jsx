import React from "react";
import { Link } from "react-router-dom";
import { cn } from "@/lib/utils";
import GlassCard from "./GlassCard";

const StatCard = ({
  icon: Icon,
  label,
  value,
  trend,
  trendLabel,
  accent = "bg-primary-soft text-[#064E3B]",
  href,
  onClick,
  testId,
  className,
  compact = false,
}) => {
  const inner = compact ? (
    <div className="flex items-center gap-2 min-w-0">
      <div className={cn("w-7 h-7 rounded-lg flex items-center justify-center shrink-0", accent)}>
        <Icon weight="regular" className="w-[14px] h-[14px]" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="text-[10px] uppercase tracking-[0.06em] text-text-muted font-semibold leading-tight truncate">{label}</div>
        <div className="font-heading text-lg sm:text-2xl font-semibold tracking-tight text-[#022C22] leading-tight truncate">{value}</div>
      </div>
    </div>
  ) : (
    <>
      <div className="flex items-start justify-between gap-2 mb-3">
        <div className={cn("w-9 h-9 rounded-xl flex items-center justify-center shrink-0", accent)}>
          <Icon weight="regular" className="w-[18px] h-[18px]" />
        </div>
      </div>
      <div className="text-[11px] uppercase tracking-[0.08em] text-text-muted font-semibold mb-1">{label}</div>
      <div className="font-heading text-2xl sm:text-3xl font-semibold tracking-tight text-[#022C22]">{value}</div>
      {(trend || trendLabel) && (
        <div className="text-[12px] mt-1.5 flex items-center gap-1.5 flex-wrap">
          {trend && (
            <span className={cn("font-medium", trend.startsWith("+") || trend.startsWith("↑") ? "text-emerald-600" : "text-text-secondary")}>
              {trend}
            </span>
          )}
          {trendLabel && <span className="text-text-secondary">{trendLabel}</span>}
        </div>
      )}
    </>
  );

  const glassPadding = compact ? false : true;
  const glassClass = cn(compact && "p-2.5 sm:p-6", className);

  const cardClass = cn(
    "transition-all hover:shadow-lg hover:-translate-y-0.5",
    (href || onClick) && "cursor-pointer",
    glassClass,
  );

  if (href) {
    return (
      <Link to={href} data-testid={testId} className={cardClass}>
        <GlassCard padding={glassPadding} className="h-full">{inner}</GlassCard>
      </Link>
    );
  }

  if (onClick) {
    return (
      <button type="button" data-testid={testId} onClick={onClick} className={cn("text-left w-full", cardClass)}>
        <GlassCard padding={glassPadding} className="h-full">{inner}</GlassCard>
      </button>
    );
  }

  return (
    <GlassCard data-testid={testId} padding={glassPadding} className={cn("h-full", glassClass)}>
      {inner}
    </GlassCard>
  );
};

export default StatCard;
