import React, { useEffect, useState } from "react";
import { api, normalizeApiError, openAuthorizedHtml } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { Pill, Plus, Trash, Pulse, Printer } from "@phosphor-icons/react";
import PaperNoteUpload from "@/components/ehr/PaperNoteUpload";

const emptyItem = () => ({
  drug_name: "", generic_name: "", strength: "", form: "tablet", route: "oral",
  dosage: "1-0-1", frequency: "twice daily", timing: "after food", quantity: "7", duration: "5 days",
  reason_for_prescribing: "", possible_side_effects: "", patient_instructions: "", is_acute: true, is_continuation: false,
});

const Prescriptions = ({
  patientId,
  patientName,
  doctorName,
  onDataChanged,
  visitId,
  appointmentId,
  chiefComplaint,
  autoOpen,
  onAutoOpenHandled,
}) => {
  const [rxs, setRxs] = useState([]);
  const [show, setShow] = useState(false);
  const [form, setForm] = useState({
    doctor_name: doctorName || "Dr.",
    diagnosis: "",
    chief_complaint: "",
    clinical_notes: "",
    follow_up_advice: "",
    next_visit_date: "",
    items: [emptyItem()],
  });

  const load = () =>
    api.get(`/prescriptions/patient/${patientId}`).then((r) => setRxs(r.data));

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [patientId]);

  useEffect(() => {
    if (!autoOpen) return;
    setShow(true);
    onAutoOpenHandled?.();
  }, [autoOpen, onAutoOpenHandled]);

  useEffect(() => {
    if (!chiefComplaint || !show) return;
    setForm((f) => (f.chief_complaint ? f : { ...f, chief_complaint: chiefComplaint }));
  }, [chiefComplaint, show]);

  const updateItem = (idx, patch) => {
    setForm((f) => ({
      ...f,
      items: f.items.map((it, i) => (i === idx ? { ...it, ...patch } : it)),
    }));
  };

  const addItem = () => setForm((f) => ({ ...f, items: [...f.items, emptyItem()] }));
  const removeItem = (idx) =>
    setForm((f) => ({ ...f, items: f.items.filter((_, i) => i !== idx) }));

  const printRx = async (rxId) => {
    try {
      await openAuthorizedHtml(`/prescriptions/${rxId}/print`);
    } catch (e) {
      toast.error(e.message || "Failed to open print view");
    }
  };

  const save = async () => {
    const validItems = form.items.filter((i) => i.drug_name.trim() && i.reason_for_prescribing.trim());
    if (validItems.length === 0) {
      toast.error("Add at least one drug with name and reason for prescribing.");
      return;
    }
    try {
      const res = await api.post("/prescriptions", {
        patient_id: patientId,
        appointment_id: appointmentId || null,
        visit_id: visitId || null,
        diagnosis: form.diagnosis || null,
        chief_complaint: form.chief_complaint || null,
        clinical_notes: form.clinical_notes || null,
        follow_up_advice: form.follow_up_advice || null,
        next_visit_date: form.next_visit_date || null,
        items: validItems.map((item) => ({
          drug_name: item.drug_name,
          generic_name: item.generic_name || null,
          strength: item.strength || null,
          form: item.form || null,
          route: item.route === "rectal" ? "other" : item.route,
          dosage: item.dosage || null,
          frequency: item.frequency || null,
          timing: item.timing || null,
          quantity: item.quantity ? parseInt(item.quantity, 10) : null,
          duration: item.duration || null,
          reason_for_prescribing: item.reason_for_prescribing,
          possible_side_effects: item.possible_side_effects || null,
          patient_instructions: item.patient_instructions || null,
          is_acute: item.is_acute,
          is_continuation: item.is_continuation,
        })),
        injections: [],
      });
      const rxId = res.data?.id;
      toast.success("Prescription saved", {
        action: rxId ? {
          label: "Print",
          onClick: () => printRx(rxId),
        } : undefined,
      });
      setShow(false);
      setForm({
        doctor_name: doctorName || "Dr.",
        diagnosis: "", chief_complaint: "", clinical_notes: "",
        follow_up_advice: "", next_visit_date: "", items: [emptyItem()],
      });
      load();
      onDataChanged?.();
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed to save");
    }
  };

  return (
    <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl" data-testid="ehr-prescriptions">
      <div className="px-5 py-3 border-b border-white/60 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Pill weight="fill" className="w-4 h-4 text-[#064E3B]" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Prescriptions</h3>
          <span className="text-[11px] text-text-muted">({rxs.length})</span>
        </div>
        <Button
          size="sm"
          className="rounded-sm h-7 text-[12px] bg-[#064E3B] hover:bg-[#022C22]"
          onClick={() => setShow(!show)}
          data-testid="rx-new-toggle"
        >
          <Plus className="w-3.5 h-3.5" /> New prescription
        </Button>
      </div>

      <div className="px-5 py-3 border-b border-white/60" data-testid="rx-paper-note-section">
        <PaperNoteUpload
          patientId={patientId}
          visitId={visitId}
          testIdPrefix="rx-paper-note"
          onUploaded={onDataChanged}
        />
      </div>

      {show && (
        <div className="p-5 border-b border-subtle bg-secondary/30 space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <Field label="Doctor name *">
              <Input
                value={form.doctor_name}
                onChange={(e) => setForm({ ...form, doctor_name: e.target.value })}
                className="rounded-sm" data-testid="rx-doctor"
              />
            </Field>
            <Field label="Chief complaint">
              <Input
                value={form.chief_complaint}
                onChange={(e) => setForm({ ...form, chief_complaint: e.target.value })}
                placeholder="e.g. headache 3 days"
                className="rounded-sm"
              />
            </Field>
            <Field label="Diagnosis">
              <Input
                value={form.diagnosis}
                onChange={(e) => setForm({ ...form, diagnosis: e.target.value })}
                className="rounded-sm" data-testid="rx-diagnosis"
              />
            </Field>
            <Field label="Next visit date">
              <Input
                type="date"
                value={form.next_visit_date}
                onChange={(e) => setForm({ ...form, next_visit_date: e.target.value })}
                className="rounded-sm"
              />
            </Field>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="text-[12px] uppercase tracking-wider text-text-muted font-semibold">
                Medications
              </div>
              <Button size="sm" variant="outline" className="rounded-sm h-7 text-[12px]" onClick={addItem}>
                <Plus className="w-3.5 h-3.5" /> Add drug
              </Button>
            </div>
            {form.items.map((it, idx) => (
              <div key={idx} className="border border-white/60 bg-white/70 backdrop-blur-sm p-3 rounded-xl space-y-2">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-2">
                  <Input
                    placeholder="Drug name *"
                    value={it.drug_name}
                    onChange={(e) => updateItem(idx, { drug_name: e.target.value })}
                    className="rounded-sm" data-testid={`rx-drug-${idx}`}
                  />
                  <Input
                    placeholder="Generic name"
                    value={it.generic_name}
                    onChange={(e) => updateItem(idx, { generic_name: e.target.value })}
                    className="rounded-sm"
                  />
                  <Input
                    placeholder="Strength (500mg)"
                    value={it.strength}
                    onChange={(e) => updateItem(idx, { strength: e.target.value })}
                    className="rounded-sm"
                  />
                </div>

                <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                  <div>
                    <label className="text-[9px] uppercase text-text-muted font-semibold block mb-1">Form</label>
                    <Select value={it.form} onValueChange={(v) => updateItem(idx, { form: v })}>
                      <SelectTrigger className="rounded-sm h-8 text-[12px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="tablet">Tablet</SelectItem>
                        <SelectItem value="capsule">Capsule</SelectItem>
                        <SelectItem value="syrup">Syrup</SelectItem>
                        <SelectItem value="injection">Injection</SelectItem>
                        <SelectItem value="cream">Cream</SelectItem>
                        <SelectItem value="ointment">Ointment</SelectItem>
                        <SelectItem value="liquid">Liquid</SelectItem>
                        <SelectItem value="powder">Powder</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <label className="text-[9px] uppercase text-text-muted font-semibold block mb-1">Route</label>
                    <Select value={it.route} onValueChange={(v) => updateItem(idx, { route: v })}>
                      <SelectTrigger className="rounded-sm h-8 text-[12px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="oral">Oral</SelectItem>
                        <SelectItem value="iv">IV</SelectItem>
                        <SelectItem value="im">IM</SelectItem>
                        <SelectItem value="subcutaneous">Subcutaneous</SelectItem>
                        <SelectItem value="topical">Topical</SelectItem>
                        <SelectItem value="inhalation">Inhalation</SelectItem>
                        <SelectItem value="other">Rectal / Other</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <Input
                    placeholder="Dosage (1-0-1)"
                    value={it.dosage}
                    onChange={(e) => updateItem(idx, { dosage: e.target.value })}
                    className="rounded-sm text-[12px]"
                  />
                  <Input
                    placeholder="Frequency"
                    value={it.frequency}
                    onChange={(e) => updateItem(idx, { frequency: e.target.value })}
                    className="rounded-sm text-[12px]"
                  />
                </div>

                <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                  <Input
                    placeholder="Timing (after food)"
                    value={it.timing}
                    onChange={(e) => updateItem(idx, { timing: e.target.value })}
                    className="rounded-sm text-[12px]"
                  />
                  <Input
                    placeholder="Quantity"
                    value={it.quantity}
                    onChange={(e) => updateItem(idx, { quantity: e.target.value })}
                    className="rounded-sm text-[12px]"
                  />
                  <Input
                    placeholder="Duration (7 days)"
                    value={it.duration}
                    onChange={(e) => updateItem(idx, { duration: e.target.value })}
                    className="rounded-sm text-[12px]"
                  />
                  <div>
                    <label className="text-[9px] uppercase text-text-muted font-semibold block mb-1">Type</label>
                    <Select value={it.is_acute ? "acute" : "continuation"} onValueChange={(v) => updateItem(idx, { is_acute: v === "acute" })}>
                      <SelectTrigger className="rounded-sm h-8 text-[12px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="acute">Acute</SelectItem>
                        <SelectItem value="continuation">Continuation</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                <Textarea
                  placeholder="Reason for prescribing * (transparency requirement)"
                  value={it.reason_for_prescribing}
                  onChange={(e) => updateItem(idx, { reason_for_prescribing: e.target.value })}
                  rows={2}
                  className="rounded-sm text-[12px]"
                  data-testid={`rx-reason-${idx}`}
                />

                <Textarea
                  placeholder="Possible side effects"
                  value={it.possible_side_effects}
                  onChange={(e) => updateItem(idx, { possible_side_effects: e.target.value })}
                  rows={1}
                  className="rounded-sm text-[12px]"
                />

                <div className="flex items-start gap-2">
                  <Textarea
                    placeholder="Patient instructions (after food, avoid alcohol...)"
                    value={it.patient_instructions}
                    onChange={(e) => updateItem(idx, { patient_instructions: e.target.value })}
                    rows={2}
                    className="rounded-sm text-[12px] flex-1"
                  />
                  {form.items.length > 1 && (
                    <button
                      type="button"
                      onClick={() => removeItem(idx)}
                      className="text-text-muted hover:text-red-600 p-2 mt-0.5"
                      aria-label="Remove medication"
                    >
                      <Trash weight="regular" className="w-4 h-4" />
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>

          <Field label="Clinical notes">
            <Textarea
              value={form.clinical_notes}
              onChange={(e) => setForm({ ...form, clinical_notes: e.target.value })}
              rows={2}
              className="rounded-sm"
            />
          </Field>
          <Field label="Follow-up advice">
            <Textarea
              value={form.follow_up_advice}
              onChange={(e) => setForm({ ...form, follow_up_advice: e.target.value })}
              rows={2}
              className="rounded-sm"
            />
          </Field>

          <div className="flex gap-2 justify-end">
            <Button variant="outline" className="rounded-sm" onClick={() => setShow(false)}>
              Cancel
            </Button>
            <Button onClick={save} className="rounded-sm bg-[#064E3B] hover:bg-[#022C22]" data-testid="rx-save">
              Save prescription
            </Button>
          </div>
        </div>
      )}

      <div className="divide-y divide-subtle">
        {rxs.length === 0 && (
          <div className="px-5 py-8 text-center text-text-muted text-[13px]">
            No prescriptions yet.
          </div>
        )}
        {rxs.map((rx) => (
          <div key={rx.id} className="px-5 py-4" data-testid={`rx-row-${rx.id}`}>
            <div className="flex items-start justify-between gap-2 mb-2">
              <div>
                <div className="text-[13px] font-medium text-[#022C22]">
                  {rx.diagnosis || rx.chief_complaint || "Consultation"}
                </div>
                <div className="text-[11px] text-text-muted">
                  {rx.doctor_name} · {new Date(rx.prescribed_at).toLocaleDateString()}
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                {rx.next_visit_date && (
                  <div className="text-right">
                    <div className="text-[10px] uppercase text-text-muted tracking-wider">Next visit</div>
                    <div className="text-[12px] text-[#022C22]">
                      {new Date(rx.next_visit_date).toLocaleDateString()}
                    </div>
                  </div>
                )}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="rounded-sm h-8"
                  data-testid={`rx-print-${rx.id}`}
                  onClick={() => printRx(rx.id)}
                >
                  <Printer weight="regular" className="w-3.5 h-3.5 mr-1" />
                  Print
                </Button>
              </div>
            </div>

            <div className="space-y-1.5 mt-2">
              {rx.items.map((it) => (
                <div key={it.id} className="border-l-2 border-[#064E3B] pl-3 py-1 text-[12px]">
                  <div className="text-[#022C22] font-medium">
                    {it.drug_name}
                    {it.generic_name && <span className="text-[11px] text-text-muted"> ({it.generic_name})</span>}
                  </div>
                  <div className="text-[11px] text-text-secondary mt-0.5 space-y-0.5">
                    {it.strength && <div>Strength: {it.strength}</div>}
                    <div>Form: {it.form} | Route: {it.route}</div>
                    <div>Dosage: {it.dosage} | Frequency: {it.frequency}</div>
                    {it.timing && <div>Timing: {it.timing}</div>}
                    {it.quantity && <div>Quantity: {it.quantity}</div>}
                    <div>Duration: {it.duration} | Type: {it.is_acute ? "Acute" : "Continuation"}</div>
                  </div>
                  {it.reason_for_prescribing && (
                    <div className="text-[11px] text-text-muted italic mt-1">
                      Reason: {it.reason_for_prescribing}
                    </div>
                  )}
                  {it.possible_side_effects && (
                    <div className="text-[11px] text-red-600 mt-1">
                      ⚠️ Side effects: {it.possible_side_effects}
                    </div>
                  )}
                  {it.patient_instructions && (
                    <div className="text-[11px] text-[#064E3B] mt-1">
                      💊 Patient instructions: {it.patient_instructions}
                    </div>
                  )}
                </div>
              ))}
            </div>

            {rx.follow_up_advice && (
              <div className="mt-2 text-[11px] text-text-secondary border-t border-subtle pt-2">
                <strong>Follow-up:</strong> {rx.follow_up_advice}
              </div>
            )}
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

export default Prescriptions;
