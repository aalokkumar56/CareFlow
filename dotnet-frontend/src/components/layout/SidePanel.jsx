import React from "react";
import { cn } from "@/lib/utils";

const WIDTH = {
  sm: "w-full lg:w-[10.5rem]",
  md: "w-[14rem]",
};

const VISIBILITY = {
  md: "hidden md:flex",
  lg: "hidden lg:flex",
  xl: "hidden xl:flex",
  always: "flex",
};

/**
 * Reusable glass left/right rail — same shell everywhere; pass screen-specific content as children.
 */
const SidePanel = ({
  children,
  className,
  size = "md",
  visible = "md",
  testId,
  as = "aside",
}) => {
  const Tag = as;
  return (
    <Tag
      data-testid={testId}
      className={cn(
        VISIBILITY[visible] ?? VISIBILITY.md,
        WIDTH[size] ?? WIDTH.md,
        "shrink-0 flex-col glass-sidebar h-full min-h-0 overflow-hidden",
        className,
      )}
    >
      {children}
    </Tag>
  );
};

export default SidePanel;
