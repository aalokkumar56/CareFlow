import { useLayoutEffect, useState } from "react";

/** True after the component mounts on the client — runs before paint. */
export function useMounted() {
  const [mounted, setMounted] = useState(false);
  useLayoutEffect(() => setMounted(true), []);
  return mounted;
}

export default useMounted;
