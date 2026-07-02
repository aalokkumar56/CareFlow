import React from "react";
import { cn } from "@/lib/utils";

/** Compact stat-card grid — matches dashboard row sizing. */
const StatCardsRow = ({ children, columns = 4, compact = false, className }) => (
  <div
    className={cn(
      "grid shrink-0",
      compact ? "gap-1.5" : "gap-2.5",
      columns === 3 && "grid-cols-3",
      columns === 4 && "grid-cols-2 xl:grid-cols-4",
      className,
    )}
  >
    {children}
  </div>
);

export default StatCardsRow;
