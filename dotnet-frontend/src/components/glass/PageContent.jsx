import React from "react";

import { cn } from "@/lib/utils";



const PageContent = ({ children, className, wide: _wide, fill, flush }) => (
  <div
    className={cn(
      flush ? "px-1 sm:px-2 lg:px-3" : "p-2 sm:p-3 lg:p-4",
      "w-full max-w-none",
      fill ? "h-full min-h-0 overflow-hidden" : "space-y-4 sm:space-y-5",
      className,
    )}
  >
    {children}
  </div>
);



export default PageContent;

