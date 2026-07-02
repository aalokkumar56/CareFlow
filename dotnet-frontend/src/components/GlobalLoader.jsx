import { useSyncExternalStore } from "react";
import { CircleNotchIcon } from "@phosphor-icons/react";
import {
  getGlobalLoaderVisible,
  subscribeGlobalLoader,
} from "@/lib/globalLoader";

const GlobalLoader = () => {
  const visible = useSyncExternalStore(subscribeGlobalLoader, getGlobalLoaderVisible);

  if (!visible) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      aria-label="Loading"
      data-testid="global-loader"
      className="fixed inset-0 z-[9999] flex items-center justify-center bg-[#022C22]/20 backdrop-blur-sm pointer-events-none"
    >
      <div className="glass-panel rounded-2xl px-8 py-6 flex flex-col items-center gap-3 border border-white/50 shadow-xl pointer-events-auto">
        <CircleNotchIcon
          weight="bold"
          className="w-10 h-10 text-[#064E3B] animate-spin"
          aria-hidden
        />
        <span className="text-sm font-medium text-[#022C22]">Loading…</span>
      </div>
    </div>
  );
};

export default GlobalLoader;
