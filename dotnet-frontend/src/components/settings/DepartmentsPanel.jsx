import React, { useEffect, useState } from "react";
import GlassCard from "@/components/glass/GlassCard";
import FormField from "@/components/forms/FormField";
import { api, normalizeApiError } from "@/lib/api";
import { normalizeDepartments } from "@/hooks/useDepartments";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { X } from "@phosphor-icons/react";
import { toast } from "sonner";

const DepartmentsPanel = ({ fillHeight = false }) => {
  const [departments, setDepartments] = useState([]);
  const [name, setName] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [profile, setProfile] = useState(null);

  const load = () => {
    setLoading(true);
    api.get("/hospital-profile")
      .then((r) => {
        setProfile(r.data);
        setDepartments(normalizeDepartments(r.data?.departments));
      })
      .catch(() => {
        setProfile(null);
        setDepartments([]);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const add = () => {
    const trimmed = name.trim();
    if (!trimmed) return;
    if (departments.some((d) => d.toLowerCase() === trimmed.toLowerCase())) {
      toast.error("Department already exists");
      return;
    }
    setDepartments((prev) => [...prev, trimmed].sort((a, b) => a.localeCompare(b)));
    setName("");
  };

  const remove = (dept) => {
    setDepartments((prev) => prev.filter((d) => d !== dept));
  };

  const save = async () => {
    setSaving(true);
    try {
      await api.put("/hospital-profile", {
        ...(profile || {}),
        departments,
      });
      toast.success("Departments saved");
      load();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to save"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <GlassCard
      padding={false}
      className={fillHeight ? "h-full flex flex-col overflow-hidden p-4 sm:p-5" : "p-4 sm:p-5"}
    >
      <div className="mb-4">
        <h2 className="font-heading text-[15px] font-semibold text-[#022C22]">Departments</h2>
        <p className="text-[12px] text-text-secondary mt-0.5">
          Used when booking appointments, assigning patients, and filtering lists.
        </p>
      </div>

      {loading ? (
        <p className="text-[13px] text-text-muted">Loading…</p>
      ) : (
        <>
          <div className="flex flex-wrap gap-2 mb-4 min-h-[2rem]">
            {departments.length === 0 && (
              <span className="text-[13px] text-text-muted">No departments yet.</span>
            )}
            {departments.map((d) => (
              <span
                key={d}
                className="inline-flex items-center gap-1.5 pl-3 pr-1.5 py-1 rounded-full bg-white/60 border border-white/70 text-[12px] font-medium text-[#022C22]"
              >
                {d}
                <button
                  type="button"
                  onClick={() => remove(d)}
                  className="p-0.5 rounded-full hover:bg-white/80 text-text-muted"
                  aria-label={`Remove ${d}`}
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              </span>
            ))}
          </div>

          <div className="flex flex-wrap gap-2 items-end mb-4">
            <FormField label="Add department" className="flex-1 min-w-[200px]">
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), add())}
                placeholder="e.g. Cardiology"
                className="rounded-xl h-9"
              />
            </FormField>
            <Button type="button" variant="outline" onClick={add} className="rounded-xl h-9">
              Add
            </Button>
          </div>

          <Button type="button" onClick={save} disabled={saving} className="btn-primary rounded-xl h-9">
            {saving ? "Saving…" : "Save departments"}
          </Button>
        </>
      )}
    </GlassCard>
  );
};

export default DepartmentsPanel;
