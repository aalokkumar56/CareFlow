import React, { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Plus, Trash } from "@phosphor-icons/react";
import { toast } from "sonner";

/**
 * Shows available merge fields for message templates with click-to-insert.
 * Admins can add custom placeholders (Settings.Edit) with optional static values.
 */
const TemplatePlaceholdersPanel = ({
  onInsert,
  canManage = false,
  className = "",
}) => {
  const [placeholders, setPlaceholders] = useState([]);
  const [showAdd, setShowAdd] = useState(false);
  const [form, setForm] = useState({
    key: "",
    label: "",
    description: "",
    example: "",
    static_value: "",
  });

  const load = () =>
    api.get("/templates/placeholders")
      .then((r) => setPlaceholders(r.data || []))
      .catch(() => setPlaceholders([]));

  useEffect(() => { load(); }, []);

  const insert = (key) => {
    if (onInsert) onInsert(`{${key}}`);
  };

  const addCustom = async () => {
    if (!form.key || !form.label) {
      toast.error("Key and label are required");
      return;
    }
    try {
      await api.post("/templates/placeholders", {
        key: form.key,
        label: form.label,
        description: form.description || null,
        example: form.example || null,
        staticValue: form.static_value || null,
      });
      toast.success("Custom placeholder added");
      setForm({ key: "", label: "", description: "", example: "", static_value: "" });
      setShowAdd(false);
      load();
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed to add placeholder");
    }
  };

  const remove = async (id) => {
    try {
      await api.delete(`/templates/placeholders/${id}`);
      toast.success("Placeholder removed");
      load();
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed to remove");
    }
  };

  return (
    <div className={`rounded-sm border border-[#064E3B]/15 bg-primary-soft/40 p-3 ${className}`}>
      <div className="text-[10px] uppercase tracking-[0.1em] text-[#064E3B] font-semibold mb-2">
        Supported merge fields
      </div>
      <p className="text-[11px] text-text-secondary mb-2">
        Click a field to insert it into your message. Use lowercase keys like {"{name}"}.
      </p>
      <div className="flex flex-wrap gap-1.5 mb-2">
        {placeholders.map((p) => (
          <div key={p.id} className="flex items-center gap-0.5">
            <button
              type="button"
              title={[p.label, p.description, p.example ? `e.g. ${p.example}` : null].filter(Boolean).join(" · ")}
              onClick={() => insert(p.key)}
              className="text-[11px] font-mono px-2 py-0.5 rounded-sm border border-[#064E3B]/25 bg-white text-[#064E3B] hover:bg-[#064E3B] hover:text-white transition-colors"
            >
              {`{${p.key}}`}
            </button>
            {canManage && !p.is_system && (
              <button
                type="button"
                onClick={() => remove(p.id)}
                className="text-text-muted hover:text-red-700 p-0.5"
                title="Remove custom placeholder"
                aria-label="Remove custom placeholder"
              >
                <Trash className="w-3 h-3" />
              </button>
            )}
          </div>
        ))}
        {placeholders.length === 0 && (
          <span className="text-[11px] text-text-muted">Loading fields…</span>
        )}
      </div>

      {placeholders.length > 0 && (
        <details className="text-[11px] text-text-secondary">
          <summary className="cursor-pointer hover:text-[#064E3B]">Field reference</summary>
          <ul className="mt-2 space-y-1 pl-1">
            {placeholders.map((p) => (
              <li key={`ref-${p.id}`}>
                <span className="font-mono text-[#064E3B]">{`{${p.key}}`}</span>
                {" — "}
                <span className="text-[#022C22]">{p.label}</span>
                {p.description && <span className="text-text-muted"> ({p.description})</span>}
                {p.static_value && <span className="text-text-muted"> · static: {p.static_value}</span>}
              </li>
            ))}
          </ul>
        </details>
      )}

      {canManage && (
        <div className="mt-3 pt-3 border-t border-[#064E3B]/10">
          {!showAdd ? (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowAdd(true)}
              className="rounded-sm h-7 text-[11px] border-[#064E3B]/30 text-[#064E3B]"
            >
              <Plus weight="bold" className="w-3 h-3 mr-1" /> Add custom field
            </Button>
          ) : (
            <div className="space-y-2">
              <div className="grid grid-cols-2 gap-2">
                <Input
                  placeholder="key e.g. offer_code"
                  value={form.key}
                  onChange={(e) => setForm({ ...form, key: e.target.value })}
                  className="rounded-sm h-8 text-[12px]"
                />
                <Input
                  placeholder="Label"
                  value={form.label}
                  onChange={(e) => setForm({ ...form, label: e.target.value })}
                  className="rounded-sm h-8 text-[12px]"
                />
              </div>
              <Input
                placeholder="Description (optional)"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                className="rounded-sm h-8 text-[12px]"
              />
              <Input
                placeholder="Static value at send time (optional)"
                value={form.static_value}
                onChange={(e) => setForm({ ...form, static_value: e.target.value })}
                className="rounded-sm h-8 text-[12px]"
              />
              <div className="flex gap-2">
                <Button type="button" size="sm" onClick={addCustom} className="rounded-sm h-7 text-[11px] bg-[#064E3B]">
                  Save field
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => setShowAdd(false)} className="rounded-sm h-7 text-[11px]">
                  Cancel
                </Button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default TemplatePlaceholdersPanel;
