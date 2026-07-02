import React, { useState } from "react";
import { cn } from "@/lib/utils";
import ReferrerCard from "./ReferrerCard";

const ReferralKanban = ({
  columns,
  items,
  getColumnId,
  getItemKey,
  onMove,
  emptyLabel = "+ Drop here",
  fillHeight = false,
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

  return (
    <div className={cn(
      "grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3 md:gap-4 min-w-0 h-full min-h-0",
      className,
    )}>
      {columns.map((col) => {
        const colItems = grouped[col.id] || [];
        const Icon = col.icon;
        const totalReferrals = colItems.reduce((sum, d) => sum + (d.patients_referred || 0), 0);

        return (
          <div
            key={col.id}
            className={cn(
              "min-w-0 flex flex-col rounded-2xl border border-white/60 bg-white/22 backdrop-blur-md shadow-sm overflow-hidden",
              col.accentClass,
              fillHeight && "h-full min-h-0",
            )}
          >
            <div className={cn("px-2.5 py-2 border-b border-white/50 shrink-0", col.headerClass)}>
              <div className="flex items-start justify-between gap-2">
                <div className="flex items-center gap-2 min-w-0">
                  {Icon && (
                    <div className={cn("w-7 h-7 rounded-lg flex items-center justify-center shrink-0", col.iconBg)}>
                      <Icon weight="fill" className={cn("w-4 h-4", col.iconColor)} />
                    </div>
                  )}
                  <div className="min-w-0">
                    <div className="text-ui-sm font-semibold text-[#022C22] truncate">{col.label}</div>
                    {col.subtitle && (
                      <div className="text-ui-caption text-text-secondary truncate">{col.subtitle}</div>
                    )}
                  </div>
                </div>
                <span className="text-ui-sm font-semibold text-slate-500 tabular-nums shrink-0">{colItems.length}</span>
              </div>
              <div className="mt-2 grid grid-cols-2 gap-2 text-ui-caption">
                <div>
                  <div className="text-text-muted">Total Referrals</div>
                  <div className="font-semibold text-[#022C22] tabular-nums">{totalReferrals}</div>
                </div>
                <div>
                  <div className="text-text-muted">In Stage</div>
                  <div className="font-semibold text-[#022C22] tabular-nums">{colItems.length}</div>
                </div>
              </div>
            </div>

            <div
              className={cn(
                "flex flex-col gap-1.5 p-2 min-w-0 flex-1 min-h-0 transition-all",
                overColumn === col.id
                  ? "bg-teal-50/60 ring-2 ring-inset ring-teal-400/50"
                  : "bg-white/15",
              )}
              onDragOver={(e) => { e.preventDefault(); setOverColumn(col.id); }}
              onDragLeave={() => setOverColumn(null)}
              onDrop={(e) => { e.preventDefault(); handleDrop(col.id); }}
            >
              <div className={cn("space-y-1.5 min-h-0 flex-1 overflow-y-auto scrollbar-thin")}>
                {colItems.map((item) => (
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
                    <ReferrerCard doctor={item} />
                  </div>
                ))}
              </div>

              <div className={cn(
                "rounded-lg border border-dashed py-2.5 text-center text-ui-caption shrink-0",
                overColumn === col.id ? "border-teal-400 text-teal-700 bg-teal-50/50" : "border-white/70 text-slate-400 bg-white/20",
              )}>
                {emptyLabel}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};

export default ReferralKanban;
