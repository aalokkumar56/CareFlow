/** @typedef {{ skipGlobalLoader?: boolean }} ApiLoaderConfig */

const SHOW_DELAY_MS = 75;

let pendingCount = 0;
let visible = false;
/** @type {ReturnType<typeof setTimeout> | null} */
let showTimer = null;
/** @type {Set<() => void>} */
const listeners = new Set();

const notify = () => {
  listeners.forEach((listener) => listener());
};

const updateVisibility = () => {
  if (pendingCount > 0) {
    if (!visible && !showTimer) {
      showTimer = setTimeout(() => {
        showTimer = null;
        if (pendingCount > 0 && !visible) {
          visible = true;
          notify();
        }
      }, SHOW_DELAY_MS);
    }
    return;
  }

  if (showTimer) {
    clearTimeout(showTimer);
    showTimer = null;
  }
  if (visible) {
    visible = false;
    notify();
  }
};

export const subscribeGlobalLoader = (listener) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};

export const getGlobalLoaderVisible = () => visible;

export const getGlobalLoaderPendingCount = () => pendingCount;

export const incrementGlobalLoader = () => {
  pendingCount += 1;
  updateVisibility();
};

export const decrementGlobalLoader = () => {
  pendingCount = Math.max(0, pendingCount - 1);
  updateVisibility();
};

/** True when this axios/fetch config should not trigger the overlay. */
export const shouldSkipGlobalLoader = (config = {}) => {
  if (config.skipGlobalLoader) return true;

  const url = String(config.url || "");
  if (url.includes("/auth/login") || url.includes("/auth/me")) return true;

  return false;
};

/** @internal test helper */
export const __resetGlobalLoaderForTests = () => {
  pendingCount = 0;
  visible = false;
  if (showTimer) {
    clearTimeout(showTimer);
    showTimer = null;
  }
  notify();
};
