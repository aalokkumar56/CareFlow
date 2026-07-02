import React, { useState } from "react";
import { cn } from "@/lib/utils";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";

const ColumnHeader = ({ col, colItems, compact, Icon }) => {
  const headerInner = (
    <div className={cn(
      "rounded-xl flex items-center justify-between border border-white/60 backdrop-blur-sm shrink-0 w-full h-9",
      compact ? "px-1.5 mb-1" : "px-2.5 mb-1.5",
      col.headerClass,
      col.description && "cursor-help",
    )}>
      <div className="flex items-center gap-1 min-w-0">
        {Icon && (
          <div className={cn(
            "rounded-md flex items-center justify-center shrink-0",
            compact ? "w-5 h-5" : "w-7 h-7 rounded-lg",
            col.iconBg || "bg-white/60",
          )}>
            <Icon weight="fill" className={cn(compact ? "w-2.5 h-2.5" : "w-4 h-4", col.iconColor)} />
          </div>
        )}
        <div className={cn(
          "font-semibold text-slate-800 truncate",
          compact ? "text-[10px] leading-tight" : "text-[12px]",
        )}>
          {col.label}
        </div>
      </div>
      <span className={cn(
        "font-semibold text-slate-500 tabular-nums shrink-0",
        compact ? "text-[10px] ml-1" : "text-[12px] ml-2",
      )}>
        {colItems.length}
      </span>
    </div>
  );

  if (!col.description) return headerInner;

  return (
    <Tooltip delayDuration={200}>
      <TooltipTrigger asChild>
        <button type="button" className="w-full h-9 text-left border-0 bg-transparent p-0 focus:outline-none focus-visible:ring-2 focus-visible:ring-teal-400/60 rounded-xl">
          {headerInner}
        </button>
      </TooltipTrigger>
      <TooltipContent side="top" className="max-w-[220px] text-xs leading-snug">
        {col.description}
      </TooltipContent>
    </Tooltip>
  );
};

const KanbanBoard = ({
  columns,
  items,
  getColumnId,
  getItemKey,
  renderCard,
  onMove,
  emptyLabel = "Drop here",
  emptyLabels = {},
  maxVisible = 4,
  fillHeight = false,
  compact = false,
  className,
}) => {
  const [dragItem, setDragItem] = useState(null);
  const [overColumn, setOverColumn] = useState(null);

  const grouped = columns.reduce((acc, col) => {
    acc[col.id] = items.filter((item) => getColumnId(item) === col.id);
    return acc;
  }, {});

  const handleDrop = (columnId) => {
    if (dragItem && onMove) onMove(dragItem, columnId);
    setDragItem(null);
    setOverColumn(null);
  };

  const colCount = columns.length;
  const gridColsClass = (() => {
    if (colCount >= 5) return "grid-cols-5";
    if (colCount === 4) return "grid-cols-2 lg:grid-cols-4";
    if (colCount === 3) return "grid-cols-1 md:grid-cols-3";
    if (colCount === 2) return "grid-cols-1 sm:grid-cols-2";
    return "grid-cols-1 sm:grid-cols-2 xl:grid-cols-4";
  })();

  const gapClass = compact ? "gap-1" : "gap-2 sm:gap-3";

  return (
    <TooltipProvider delayDuration={200}>
    <div className={cn(
      "grid min-w-0 w-full",
      gapClass,
      fillHeight ? cn("h-full min-h-0", gridColsClass) : gridColsClass,
      className,
    )}>
      {columns.map((col) => {
        const colItems = grouped[col.id] || [];
        const visible = colItems.slice(0, maxVisible);
        const hiddenCount = colItems.length - visible.length;
        const Icon = col.icon;
        const colEmptyLabel = emptyLabels[col.id] || emptyLabel;

        return (
          <div
            key={col.id}
            data-testid={col.testId}
            className={cn("min-w-0 flex flex-col overflow-hidden", fillHeight && "h-full min-h-0")}
          >
            <ColumnHeader col={col} colItems={colItems} compact={compact} Icon={Icon} />
            <div
              className={cn(
                "rounded-lg transition-all border border-transparent min-w-0",
                compact ? "space-y-1 p-1" : "space-y-1.5 rounded-xl p-1.5",
                fillHeight ? "flex-1 min-h-0 overflow-y-auto scrollbar-thin" : "flex-1 min-h-[80px]",
                overColumn === col.id
                  ? "bg-teal-50/50 backdrop-blur-sm ring-2 ring-dashed ring-[#064E3B]/30 border-teal-400/40"
                  : "bg-white/22 backdrop-blur-md border border-white/60",
              )}
              onDragOver={(e) => { e.preventDefault(); setOverColumn(col.id); }}
              onDragLeave={() => setOverColumn(null)}
              onDrop={(e) => { e.preventDefault(); handleDrop(col.id); }}
            >
              {colItems.length === 0 && overColumn === col.id && (
                <div className={cn(
                  "text-center text-teal-700 font-medium",
                  compact ? "text-[10px] py-6" : "text-[12px] py-10",
                )}>
                  {colEmptyLabel}
                </div>
              )}
              {visible.map((item) => (
                <div
                  key={getItemKey(item)}
                  draggable={!!onMove}
                  onDragStart={() => setDragItem(item)}
                  onDragEnd={() => { setDragItem(null); setOverColumn(null); }}
                  className={cn(
                    "min-w-0 w-full max-w-full",
                    onMove && "cursor-grab active:cursor-grabbing",
                    dragItem && getItemKey(dragItem) === getItemKey(item) && "opacity-40",
                  )}
                >
                  {renderCard(item, { columnId: col.id, compact })}
                </div>
              ))}
              {hiddenCount > 0 && (
                <button
                  type="button"
                  className="w-full h-9 rounded-xl text-left font-medium text-teal-700 hover:text-teal-800 px-2 flex items-center text-[13px]"
                >
                  + {hiddenCount} more
                </button>
              )}
              {colItems.length === 0 && overColumn !== col.id && (
                <div className={cn(
                  "rounded-lg border border-dashed border-white/50 bg-white/10 text-center text-text-secondary",
                  compact ? "py-6 text-ui-sm" : "rounded-xl py-10 text-ui-base",
                )}>
                  {colEmptyLabel}
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
    </TooltipProvider>
  );
};

export const KanbanCard = ({
  title,
  subtitle,
  meta,
  badge,
  actions,
  colorClass,
  className,
  trailing,
  compact,
  ...rest
}) => (
  <div
    {...rest}
    className={cn(
    "rounded-lg border border-white/60 backdrop-blur-sm shadow-sm",
    compact ? "p-1.5" : "rounded-xl p-3",
    colorClass || "bg-white/70",
    className,
  )}>
    <div className="flex items-start justify-between gap-1">
      <div className="min-w-0 flex-1">
        {compact && meta && (
          <div className="text-[9px] font-medium text-slate-400 mb-0.5 truncate">{meta}</div>
        )}
        <div className={cn("font-semibold text-slate-800 truncate", compact ? "text-[10px] leading-tight" : "text-[13px]")}>
          {title}
        </div>
        {subtitle && (
          <div className={cn("text-slate-600 truncate flex items-center gap-1 min-w-0", compact ? "text-[9px] mt-0" : "text-[11px] mt-0.5")}>
            {subtitle}
          </div>
        )}
        {!compact && meta && <div className="text-[10px] text-slate-400 mt-1">{meta}</div>}
      </div>
      {trailing || badge}
    </div>
    {actions && (
      <div className={cn("flex items-center gap-0.5", compact ? "mt-1" : "mt-2 gap-1.5")}>
        {actions}
      </div>
    )}
  </div>
);

export default KanbanBoard;
