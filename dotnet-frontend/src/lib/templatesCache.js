import { api } from "@/lib/api";

let cached = null;
let inflight = null;

/** Fetch WhatsApp/message templates once per session; dedupes concurrent calls. */
export async function fetchTemplates({ signal, force = false } = {}) {
  if (!force && cached) return cached;
  if (!force && inflight) return inflight;

  inflight = api
    .get("/templates", { signal })
    .then((r) => {
      cached = r.data;
      return cached;
    })
    .finally(() => {
      inflight = null;
    });

  return inflight;
}

export function invalidateTemplates() {
  cached = null;
  inflight = null;
}
