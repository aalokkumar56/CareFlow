import React from "react";
import { cn } from "@/lib/utils";

const ViewModeTabs = ({ modes, value, onChange, className }) => (
  <div className={cn("inline-flex items-center gap-1 p-1 rounded-xl bg-white/60 backdrop-blur border border-white/50", className)}>
    {modes.map((mode) => (
      <button
        key={mode.id}
        type="button"
        data-testid={`view-mode-${mode.id}`}
        onClick={() => onChange(mode.id)}
        className={cn(
          "px-3 py-1.5 text-[12px] sm:text-[13px] rounded-lg transition-colors capitalize font-medium",
          value === mode.id
            ? "bg-[#064E3B] text-white shadow-sm"
            : "text-text-secondary hover:text-[#022C22] hover:bg-white/70",
        )}
      >
        {mode.label}
      </button>
    ))}
  </div>
);

export default ViewModeTabs;
