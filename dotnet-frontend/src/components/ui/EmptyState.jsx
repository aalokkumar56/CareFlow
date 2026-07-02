import React from "react";
import { cn } from "@/lib/utils";

const EmptyState = ({
  illustration,
  title,
  description,
  action,
  className,
  testId = "empty-state",
  compact = false,
}) => (
  <div
    data-testid={testId}
    className={cn(
      "flex flex-col items-center justify-center text-center",
      compact ? "py-8 px-4" : "py-12 px-6",
      className,
    )}
  >
    {illustration && (
      <img
        src={illustration}
        alt=""
        aria-hidden="true"
        className={cn("mb-4 select-none", compact ? "w-36 h-auto" : "w-44 sm:w-52 h-auto")}
        loading="lazy"
      />
    )}
    {title && (
      <h3 className="font-heading text-ui-md sm:text-base font-semibold text-[#022C22] mb-1">
        {title}
      </h3>
    )}
    {description && (
      <p className="text-ui-base text-text-secondary max-w-sm leading-relaxed mb-4">
        {description}
      </p>
    )}
    {action}
  </div>
);

export default EmptyState;
