import React from "react";
import { Link } from "@/lib/navigation";
import { DotsThree, Eye, ArrowSquareOut } from "@phosphor-icons/react";
import { Area, AreaChart, ResponsiveContainer } from "recharts";
import ClientChart from "@/components/charts/ClientChart";
import { cn } from "@/lib/utils";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

const DashboardStatCard = ({
  icon: Icon,
  label,
  value,
  trend,
  trendLabel,
  iconBg,
  iconColor,
  href,
  onClick,
  testId,
  showMenu = true,
  menuItems,
  sparkData,
  sparkColor = "#6366F1",
  size = "default",
  className,
}) => {
  const compact = size === "compact";
  const iconSlot = compact ? "w-7 shrink-0" : "w-9 shrink-0";
  const iconBox = compact ? "w-7 h-7 rounded-lg" : "w-9 h-9 rounded-xl";
  const iconSize = compact ? "w-[14px] h-[14px]" : "w-[18px] h-[18px]";
  const gridGap = compact ? "gap-x-2 gap-y-0.5" : "gap-x-2.5 gap-y-1";

  const inner = (
    <div className={cn(
      "dashboard-stat-card h-full flex flex-col justify-center",
      compact && "dashboard-stat-card--compact",
      className,
    )}>
      <div className="flex items-center justify-between gap-1">
        <div className={cn("grid grid-cols-[auto_1fr] min-w-0 flex-1", gridGap)}>
          <div className={cn(iconSlot, "row-span-3 flex items-center")}>
            <div className={cn("flex items-center justify-center", iconBox, iconBg)}>
              <Icon weight="fill" className={cn(iconSize, iconColor)} />
            </div>
          </div>
          <p className="text-ui-label uppercase tracking-wide text-text-secondary font-semibold leading-tight truncate">{label}</p>
          <p className={cn(
            "font-heading font-bold tracking-tight text-[#022C22]",
            compact ? "text-xl" : "text-2xl lg:text-[1.75rem] leading-none",
          )}>
            {value}
          </p>
          {(trend || trendLabel) && (
            <p className="col-start-2 text-ui-sm flex items-center gap-1 min-w-0">
              {trend && <span className="font-semibold text-emerald-600 shrink-0">{trend}</span>}
              {trendLabel && <span className="text-text-muted truncate">{trendLabel}</span>}
            </p>
          )}
        </div>
        {sparkData?.length > 0 ? (
          <ClientChart className={cn("shrink-0 self-center", compact ? "w-[52px] h-[28px]" : "w-[68px] h-[36px]")} minHeight={compact ? 28 : 36}>
            <ResponsiveContainer width={compact ? 52 : 68} height={compact ? 28 : 36}>
              <AreaChart data={sparkData} margin={{ top: 2, right: 0, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id={`spark-${testId || label}`} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor={sparkColor} stopOpacity={0.35} />
                    <stop offset="100%" stopColor={sparkColor} stopOpacity={0} />
                  </linearGradient>
                </defs>
                <Area
                  type="monotone"
                  dataKey="count"
                  stroke={sparkColor}
                  strokeWidth={1.5}
                  fill={`url(#spark-${testId || label})`}
                  dot={false}
                  isAnimationActive={false}
                />
              </AreaChart>
            </ResponsiveContainer>
          </ClientChart>
        ) : showMenu ? (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button
                type="button"
                data-testid={testId ? `${testId}-menu` : undefined}
                className="p-0.5 -mr-0.5 rounded-md text-slate-400 hover:text-slate-500 transition-colors shrink-0 self-start"
                aria-label="More options"
                onClick={(e) => e.preventDefault()}
              >
                <DotsThree weight="bold" className="w-4 h-4" />
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="rounded-xl glass-card border-white/60 text-[11px]">
              {menuItems || (
                <>
                  {href && (
                    <DropdownMenuItem asChild>
                      <Link to={href} className="flex items-center gap-2">
                        <ArrowSquareOut className="w-3.5 h-3.5" /> View details
                      </Link>
                    </DropdownMenuItem>
                  )}
                  {!href && onClick && (
                    <DropdownMenuItem onClick={onClick}>
                      <Eye className="w-3.5 h-3.5 mr-2" /> View details
                    </DropdownMenuItem>
                  )}
                </>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null}
      </div>
    </div>
  );

  if (href) {
    return (
      <Link to={href} data-testid={testId} className="block h-full min-h-0">
        {inner}
      </Link>
    );
  }

  if (onClick) {
    return (
      <button type="button" data-testid={testId} onClick={onClick} className="text-left w-full h-full min-h-0">
        {inner}
      </button>
    );
  }

  return <div data-testid={testId} className="h-full min-h-0">{inner}</div>;
};

export default DashboardStatCard;
