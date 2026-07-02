import React from "react";
import { cn } from "@/lib/utils";

/** Two-column page: optional side panel + main content (Settings, etc.). */
const PageSideLayout = ({ sidebar, children, className }) => (
  <div className={cn("flex flex-col lg:flex-row gap-2 h-full min-h-0 overflow-hidden p-2 flex-1", className)}>
    {sidebar}
    <div className="flex-1 min-w-0 min-h-0 overflow-hidden flex flex-col">{children}</div>
  </div>
);

export default PageSideLayout;
