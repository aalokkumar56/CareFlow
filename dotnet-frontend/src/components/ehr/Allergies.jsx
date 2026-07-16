import React, { useEffect, useState } from "react";
import {
  api, normalizeApiError, ALLERGY_TYPE_LABELS, ALLERGY_SEVERITY_LABELS,
} from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { Warning, Plus, Trash } from "@phosphor-icons/react";

const SEVERITY_COLORS = {
  mild: "bg-yellow-50 text-yellow-800 border-yellow-200",
  moderate: "bg-orange-50 text-orange-800 border-orange-200",
  severe: "bg-red-50 text-red-800 border-red-200",
  life_threatening: "bg-red-100 text-red-900 border-red-300 font-semibold",
};

const Allergies = ({ patientId, onDataChanged, items: controlledItems, onItemsChange, skipInitialFetch = false }) => {
  const isControlled = controlledItems !== undefined;
  const [localItems, setLocalItems] = useState(controlledItems ?? []);
  const items = isControlled ? controlledItems : localItems;
  const setItems = isControlled ? onItemsChange : setLocalItems;
  const [show, setShow] = useState(false);
  const [form, setForm] = useState({
    type: "drug", allergen: "", severity: "mild", reaction: "", notes: "",
  });

  const load = () =>
    api.get(`/allergies/patient/${patientId}`).then((r) => setItems(r.data));

  useEffect(() => {
    if (isControlled || skipInitialFetch) return;
    load();
    /* eslint-disable-next-line */
  }, [patientId, isControlled, skipInitialFetch]);

  useEffect(() => {
    if (isControlled && controlledItems) setItems(controlledItems);
    /* eslint-disable-next-line */
  }, [controlledItems, isControlled]);

  const add = async () => {
    if (!form.allergen.trim()) { toast.error("Allergen is required"); return; }
    try {
      await api.post("/allergies", { patient_id: patientId, ...form });
      toast.success("Allergy added");
      setForm({ type: "drug", allergen: "", severity: "mild", reaction: "", notes: "" });
      setShow(false);
      load();
      onDataChanged?.();
    } catch (e) { toast.error(normalizeApiError(e, "Failed to add allergy")); }
  };

  const remove = async (id) => {
    if (!window.confirm("Remove this allergy?")) return;
    await api.delete(`/allergies/${id}`);
    toast.success("Removed");
    load();
    onDataChanged?.();
  };

  return (
    <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl" data-testid="ehr-allergies">
      <div className="px-5 py-3 border-b border-white/60 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Warning weight="fill" className="w-4 h-4 text-red-600" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Allergies</h3>
          <span className="text-[11px] text-text-muted">({items.length})</span>
        </div>
        <Button
          size="sm"
          variant="outline"
          className="rounded-sm h-7 text-[12px]"
          onClick={() => setShow(!show)}
          data-testid="allergy-add-toggle"
        >
          <Plus className="w-3.5 h-3.5" /> Add
        </Button>
      </div>

      {show && (
        <div className="p-4 border-b border-subtle bg-secondary/30 space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <Field label="Type">
              <Select value={form.type} onValueChange={(v) => setForm({ ...form, type: v })}>
                <SelectTrigger className="rounded-sm h-9" data-testid="allergy-type"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="drug">Drug</SelectItem>
                  <SelectItem value="food">Food</SelectItem>
                  <SelectItem value="environmental">Environmental</SelectItem>
                  <SelectItem value="insect">Insect / Sting</SelectItem>
                  <SelectItem value="other">Other</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field label="Severity">
              <Select value={form.severity} onValueChange={(v) => setForm({ ...form, severity: v })}>
                <SelectTrigger className="rounded-sm h-9" data-testid="allergy-severity"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="mild">Mild</SelectItem>
                  <SelectItem value="moderate">Moderate</SelectItem>
                  <SelectItem value="severe">Severe</SelectItem>
                  <SelectItem value="life_threatening">Life-threatening</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field label="Allergen *">
              <Input
                value={form.allergen}
                onChange={(e) => setForm({ ...form, allergen: e.target.value })}
                placeholder="e.g. Penicillin, peanuts"
                className="rounded-sm" data-testid="allergy-allergen"
              />
            </Field>
            <Field label="Reaction">
              <Input
                value={form.reaction}
                onChange={(e) => setForm({ ...form, reaction: e.target.value })}
                placeholder="e.g. hives, anaphylaxis"
                className="rounded-sm"
              />
            </Field>
          </div>
          <Field label="Notes">
            <Textarea
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
              rows={2}
              className="rounded-sm"
            />
          </Field>
          <Button onClick={add} className="rounded-sm bg-[#064E3B] hover:bg-[#022C22]" data-testid="allergy-save">
            Save allergy
          </Button>
        </div>
      )}

      <div className="divide-y divide-subtle">
        {items.length === 0 && (
          <div className="px-5 py-8 text-center text-text-muted text-[13px]">
            No allergies recorded. Click <strong>Add</strong> to record one.
          </div>
        )}
        {items.map((a) => (
          <div key={a.id} className="px-5 py-4 flex items-start justify-between gap-3" data-testid={`allergy-row-${a.id}`}>
            <div className="flex-1 min-w-0 space-y-2">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-[14px] font-semibold text-[#022C22]">{a.allergen}</span>
                <span className={`text-[11px] px-2 py-0.5 rounded-sm border ${SEVERITY_COLORS[a.severity]}`}>
                  {ALLERGY_SEVERITY_LABELS[a.severity] || a.severity.replace(/_/g, " ")}
                </span>
              </div>
              <div className="flex flex-wrap gap-x-3 gap-y-1 text-[12px] text-text-secondary">
                <span>
                  <span className="text-text-muted">Type:</span>{" "}
                  {ALLERGY_TYPE_LABELS[a.type] || a.type}
                </span>
                {a.reaction && (
                  <span>
                    <span className="text-text-muted">Reaction:</span> {a.reaction}
                  </span>
                )}
                {a.recorded_by_name && (
                  <span>
                    <span className="text-text-muted">Recorded by:</span> {a.recorded_by_name}
                  </span>
                )}
                {a.created_at && (
                  <span>
                    <span className="text-text-muted">Added:</span>{" "}
                    {new Date(a.created_at).toLocaleDateString(undefined, { dateStyle: "medium" })}
                  </span>
                )}
              </div>
              {a.notes && (
                <div className="text-[12px] text-[#022C22] bg-secondary/30 border border-subtle rounded-sm px-3 py-2 leading-relaxed">
                  {a.notes}
                </div>
              )}
            </div>
            <button
              type="button"
              onClick={() => remove(a.id)}
              className="text-text-muted hover:text-red-600 p-1 shrink-0"
              data-testid={`allergy-delete-${a.id}`}
              aria-label="Delete allergy"
            >
              <Trash weight="regular" className="w-4 h-4" />
            </button>
          </div>
        ))}
      </div>
    </div>
  );
};

const Field = ({ label, children }) => (
  <div>
    <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">
      {label}
    </label>
    {children}
  </div>
);

export default Allergies;
