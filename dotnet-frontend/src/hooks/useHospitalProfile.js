import { useEffect, useState } from "react";
import { api } from "@/lib/api";

export const DEFAULT_HOSPITAL_NAME = "Cure & Care Hospital";

export function useHospitalProfile() {
  const [hospitalName, setHospitalName] = useState(DEFAULT_HOSPITAL_NAME);

  useEffect(() => {
    const controller = new AbortController();
    api.get("/hospital-profile", { signal: controller.signal })
      .then((r) => setHospitalName(r.data?.name || DEFAULT_HOSPITAL_NAME))
      .catch(() => {});
    return () => controller.abort();
  }, []);

  return { hospitalName };
}
