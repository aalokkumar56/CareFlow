import React, { useLayoutEffect, useRef, useState } from "react";
import { cn } from "@/lib/utils";

/**
 * Defers Recharts until the wrapper has real dimensions (ResizeObserver).
 * Prevents ResponsiveContainer width/height -1 warnings in flex/grid layouts.
 */
const ClientChart = ({ className, minHeight = 50, children }) => {
  const containerRef = useRef(null);
  const [size, setSize] = useState({ width: 0, height: 0 });

  useLayoutEffect(() => {
    const el = containerRef.current;
    if (!el) return undefined;

    const update = () => {
      const { width, height } = el.getBoundingClientRect();
      const w = Math.floor(width);
      const h = Math.floor(height);
      setSize((prev) => (prev.width === w && prev.height === h ? prev : { width: w, height: h }));
    };

    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const ready = size.width > 0 && size.height > 0;

  return (
    <div
      ref={containerRef}
      className={cn("w-full h-full min-w-0", className)}
      style={{ minHeight }}
    >
      {ready ? children : null}
    </div>
  );
};

export default ClientChart;
