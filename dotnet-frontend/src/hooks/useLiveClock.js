import { useEffect, useState } from "react";

const formatClock = (date) => ({
  date: date.toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" }),
  time: date.toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit", hour12: true }),
});

export function useLiveClock(intervalMs = 60_000) {
  const [clock, setClock] = useState(() => ({ date: "", time: "" }));

  useEffect(() => {
    const tick = () => setClock(formatClock(new Date()));
    tick();
    const id = window.setInterval(tick, intervalMs);
    return () => window.clearInterval(id);
  }, [intervalMs]);

  return clock;
}
