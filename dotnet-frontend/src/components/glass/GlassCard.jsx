import React from "react";
import { Link } from "react-router-dom";
import { cn } from "@/lib/utils";

const GlassCard = ({ className, children, padding = true, variant = "default", ...props }) => (
  <div
    className={cn(
      variant === "dashboard" && "dashboard-glass-card",
      variant === "solid" && "solid-card",
      variant === "default" && "glass-card",
      padding && "p-6",
      className,
    )}
    {...props}
  >
    {children}
  </div>
);

export default GlassCard;
