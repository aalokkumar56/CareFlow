import { useEffect, useState } from "react";
import { api } from "@/lib/api";

export const DEFAULT_HOSPITAL_NAME = "Cure & Care Hospital";

let profileInflight = null;
let profileCache = null;

export function useHospitalProfile() {
  const [hospitalName, setHospitalName] = useState(
    profileCache?.name || DEFAULT_HOSPITAL_NAME,
  );

  useEffect(() => {
    if (profileCache?.name) {
      setHospitalName(profileCache.name);
      return undefined;
    }

    const controller = new AbortController();
    if (!profileInflight) {
      profileInflight = api
        .get("/hospital-profile")
        .then((r) => {
          profileCache = r.data;
          return r;
        })
        .finally(() => {
          profileInflight = null;
        });
    }

    profileInflight
      .then((r) => {
        if (!controller.signal.aborted) {
          setHospitalName(r.data?.name || DEFAULT_HOSPITAL_NAME);
        }
      })
      .catch(() => {});

    return () => controller.abort();
  }, []);

  return { hospitalName };
}
