import { useEffect, useState } from "react";
import { api } from "@/lib/api";

/** Single backend source: GET /hospital-profile/departments */
export const DEPARTMENTS_API = "/hospital-profile/departments";

export function normalizeDepartments(raw) {
  if (!Array.isArray(raw)) return [];
  return raw
    .map((d) => (typeof d === "string" ? d : d?.name))
    .filter(Boolean);
}

export async function fetchDepartments() {
  try {
    const r = await api.get(DEPARTMENTS_API);
    return normalizeDepartments(r.data);
  } catch {
    return [];
  }
}

export function useDepartments() {
  const [departments, setDepartments] = useState([]);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    return fetchDepartments()
      .then(setDepartments)
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  return { departments, loading, reload: load };
}

export default useDepartments;
