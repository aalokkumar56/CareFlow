import React from "react";
import { cn } from "@/lib/utils";

/**
 * CureFlow platform brand mark — not a hospital name.
 * Use on public auth pages and platform console.
 */
const CureFlowMark = ({
  variant = "default",
  className,
  light = false,
  testId = "cureflow-brand",
}) => {
  const isCompact = variant === "compact";

  return (
    <div
      className={cn("flex flex-col items-center", className)}
      data-testid={testId}
    >
      <img
        src="/design/cureflow-logo.svg"
        alt="CureFlow"
        className={cn(
          "w-auto",
          isCompact ? "h-7" : "h-9",
          light && "brightness-0 invert",
        )}
        width={isCompact ? 120 : 160}
        height={isCompact ? 28 : 36}
      />
      {variant === "platform" && (
        <span
          className={cn(
            "text-xs font-medium mt-1 tracking-wide uppercase",
            light ? "text-white/70" : "text-muted-foreground",
          )}
        >
          Platform Ops
        </span>
      )}
    </div>
  );
};

export default CureFlowMark;
