import React, { useEffect, useState } from "react";
import { api, normalizeApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import { Heartbeat, Plus } from "@phosphor-icons/react";

const INTEGER_VITAL_FIELDS = new Set([
  "systolic_bp",
  "diastolic_bp",
  "heart_rate",
  "respiratory_rate",
  "oxygen_saturation",
]);

const Vitals = ({ patientId, onDataChanged }) => {
  const [items, setItems] = useState([]);
  const [show, setShow] = useState(false);
  const [form, setForm] = useState({});

  const load = () =>
    api.get(`/clinical/vitals/patient/${patientId}`).then((r) => setItems(r.data));

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [patientId]);

  const save = async () => {
    const payload = { patient_id: patientId };
    Object.entries(form).forEach(([k, v]) => {
      if (v === "" || v === null || v === undefined) return;
      if (k === "notes") {
        payload[k] = v;
        return;
      }
      const num = INTEGER_VITAL_FIELDS.has(k) ? parseInt(String(v), 10) : parseFloat(String(v));
      if (!Number.isFinite(num)) return;
      payload[k] = num;
    });
    const hasMeasurement = Object.keys(payload).some((k) => k !== "patient_id");
    if (!hasMeasurement) {
      toast.error("Enter at least one measurement");
      return;
    }
    try {
      await api.post("/clinical/vitals", payload);
      toast.success("Vitals recorded");
      setForm({});
      setShow(false);
      load();
      onDataChanged?.();
    } catch (e) { toast.error(normalizeApiError(e, "Failed to save vitals")); }
  };

  const setField = (k, v) => setForm({ ...form, [k]: v });

  return (
    <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl" data-testid="ehr-vitals">
      <div className="px-5 py-3 border-b border-white/60 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Heartbeat weight="fill" className="w-4 h-4 text-red-500" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Vitals & Measurements</h3>
          <span className="text-[11px] text-text-muted">({items.length})</span>
        </div>
        <Button
          size="sm"
          variant="outline"
          className="rounded-sm h-7 text-[12px]"
          onClick={() => setShow(!show)}
          data-testid="vitals-add-toggle"
        >
          <Plus className="w-3.5 h-3.5" /> Record
        </Button>
      </div>

      {show && (
        <div className="p-4 border-b border-subtle bg-secondary/30 space-y-3">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
            <NumberField label="Height (cm)" v={form.height_cm} set={(x) => setField("height_cm", x)} step="0.1" min="30" max="300" />
            <NumberField label="Weight (kg)" v={form.weight_kg} set={(x) => setField("weight_kg", x)} step="0.1" min="0.5" max="500" />
            <NumberField label="Systolic BP" v={form.systolic_bp} set={(x) => setField("systolic_bp", x)} step="1" min="50" max="300" />
            <NumberField label="Diastolic BP" v={form.diastolic_bp} set={(x) => setField("diastolic_bp", x)} step="1" min="30" max="200" />
            <NumberField label="Heart rate (bpm)" v={form.heart_rate} set={(x) => setField("heart_rate", x)} step="1" min="20" max="300" />
            <NumberField label="Temperature (°F)" v={form.temperature} set={(x) => setField("temperature", x)} step="0.1" min="90" max="115" />
            <NumberField label="Resp. rate" v={form.respiratory_rate} set={(x) => setField("respiratory_rate", x)} step="1" min="5" max="80" />
            <NumberField label="SpO₂ (%)" v={form.oxygen_saturation} set={(x) => setField("oxygen_saturation", x)} step="1" min="50" max="100" />
            <NumberField label="Fasting sugar" v={form.blood_sugar_fasting} set={(x) => setField("blood_sugar_fasting", x)} step="0.1" />
            <NumberField label="PP sugar" v={form.blood_sugar_postprandial} set={(x) => setField("blood_sugar_postprandial", x)} step="0.1" />
            <NumberField label="HbA1c" v={form.hba1c} set={(x) => setField("hba1c", x)} step="0.1" />
          </div>
          <Textarea
            placeholder="Notes (optional)"
            value={form.notes || ""}
            onChange={(e) => setField("notes", e.target.value)}
            rows={2}
            className="rounded-sm"
          />
          <Button onClick={save} className="rounded-sm bg-[#064E3B] hover:bg-[#022C22]" data-testid="vitals-save">
            Save vitals
          </Button>
        </div>
      )}

      <div className="divide-y divide-subtle">
        {items.length === 0 && (
          <div className="px-5 py-8 text-center text-text-muted text-[13px]">
            No vitals recorded yet.
          </div>
        )}
        {items.map((v) => (
          <div key={v.id} className="px-5 py-3" data-testid={`vitals-row-${v.id}`}>
            <div className="text-[11px] text-text-muted mb-1">
              {new Date(v.measured_at).toLocaleString()} · {v.recorded_by_name || "—"}
            </div>
            <div className="flex flex-wrap gap-x-4 gap-y-1 text-[12px]">
              {v.height_cm && <Stat label="Ht" val={`${v.height_cm} cm`} />}
              {v.weight_kg && <Stat label="Wt" val={`${v.weight_kg} kg`} />}
              {v.bmi && <Stat label="BMI" val={v.bmi} />}
              {(v.systolic_bp || v.diastolic_bp) && (
                <Stat label="BP" val={`${v.systolic_bp || "—"}/${v.diastolic_bp || "—"}`} />
              )}
              {v.heart_rate && <Stat label="HR" val={`${v.heart_rate} bpm`} />}
              {v.temperature && <Stat label="Temp" val={`${v.temperature}°F`} />}
              {v.respiratory_rate && <Stat label="RR" val={`${v.respiratory_rate} /min`} />}
              {v.oxygen_saturation && <Stat label="SpO₂" val={`${v.oxygen_saturation}%`} />}
              {v.blood_sugar_fasting && <Stat label="FBS" val={v.blood_sugar_fasting} />}
              {v.blood_sugar_postprandial && <Stat label="PPBS" val={v.blood_sugar_postprandial} />}
              {v.hba1c && <Stat label="HbA1c" val={v.hba1c} />}
            </div>
            {v.notes && <div className="text-[11px] text-text-secondary mt-1">{v.notes}</div>}
          </div>
        ))}
      </div>
    </div>
  );
};

const NumberField = ({ label, v, set, step = "0.01", min, max }) => (
  <div>
    <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">
      {label}
    </label>
    <Input
      type="number"
      step={step}
      min={min}
      max={max}
      value={v ?? ""}
      onChange={(e) => set(e.target.value)}
      className="rounded-sm h-9"
    />
  </div>
);

const Stat = ({ label, val }) => (
  <span className="text-[#022C22]">
    <span className="text-text-muted text-[10px] uppercase tracking-wider mr-1">{label}</span>
    <span className="font-medium">{val}</span>
  </span>
);

export default Vitals;
