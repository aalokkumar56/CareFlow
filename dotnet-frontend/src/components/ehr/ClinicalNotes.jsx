import React, { useEffect, useState } from "react";
import { api, normalizeApiError, NOTE_TYPE_LABELS, MEDICAL_CATEGORY_LABELS, FAMILY_RELATION_LABELS } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import { toast } from "sonner";
import { Note, Plus, FirstAid, UsersFour, PencilSimple } from "@phosphor-icons/react";

export const ClinicalNotes = ({
  patientId,
  onDataChanged,
  visitId,
  appointmentId,
  autoOpen,
  onAutoOpenHandled,
}) => {
  const [items, setItems] = useState([]);
  const [show, setShow] = useState(false);
  const [form, setForm] = useState({
    note_type: "progress", subjective: "", objective: "", assessment: "", plan: "",
  });
  const [editNote, setEditNote] = useState(null);
  const [editForm, setEditForm] = useState(null);

  const load = () =>
    api.get(`/clinical/notes/patient/${patientId}`).then((r) => setItems(r.data));

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [patientId]);

  useEffect(() => {
    if (!autoOpen) return;
    setShow(true);
    onAutoOpenHandled?.();
  }, [autoOpen, onAutoOpenHandled]);

  const notifyChange = () => {
    load();
    onDataChanged?.();
  };

  const save = async () => {
    try {
      await api.post("/clinical/notes", {
        patient_id: patientId,
        appointment_id: appointmentId || undefined,
        visit_id: visitId || undefined,
        ...form,
      });
      toast.success("Note saved");
      setForm({ note_type: "progress", subjective: "", objective: "", assessment: "", plan: "" });
      setShow(false);
      notifyChange();
    } catch (e) { toast.error(normalizeApiError(e, "Failed to save note")); }
  };

  const openEdit = (n) => {
    setEditNote(n);
    setEditForm({
      note_type: n.note_type || "progress",
      subjective: n.subjective || "",
      objective: n.objective || "",
      assessment: n.assessment || "",
      plan: n.plan || "",
    });
  };

  const saveEdit = async () => {
    try {
      await api.patch(`/clinical/notes/${editNote.id}`, editForm);
      toast.success("Note updated");
      setEditNote(null);
      setEditForm(null);
      notifyChange();
    } catch (e) { toast.error(normalizeApiError(e, "Failed to update note")); }
  };

  return (
    <div className="space-y-4" data-testid="ehr-notes">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Note weight="fill" className="w-4 h-4 text-[#064E3B]" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Clinical Notes (SOAP)</h3>
          <span className="text-[11px] text-text-muted">({items.length})</span>
        </div>
        <Button size="sm" variant="outline" className="rounded-sm h-7 text-[12px]" onClick={() => setShow(!show)}>
          <Plus className="w-3.5 h-3.5" /> Add
        </Button>
      </div>

      {show && (
        <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl p-4 space-y-3">
          <div>
            <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Type</label>
            <Select value={form.note_type} onValueChange={(v) => setForm({ ...form, note_type: v })}>
              <SelectTrigger className="rounded-sm h-9 w-48"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="progress">Progress</SelectItem>
                <SelectItem value="soap">SOAP</SelectItem>
                <SelectItem value="discharge">Discharge</SelectItem>
                <SelectItem value="other">Other</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <Soap label="Subjective" v={form.subjective} set={(x) => setForm({ ...form, subjective: x })} />
          <Soap label="Objective" v={form.objective} set={(x) => setForm({ ...form, objective: x })} />
          <Soap label="Assessment" v={form.assessment} set={(x) => setForm({ ...form, assessment: x })} />
          <Soap label="Plan" v={form.plan} set={(x) => setForm({ ...form, plan: x })} />
          <Button onClick={save} className="rounded-sm bg-[#064E3B] hover:bg-[#022C22]" data-testid="note-save">
            Save note
          </Button>
        </div>
      )}

      {items.length === 0 && (
        <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl px-5 py-8 text-center text-text-muted text-[13px]">
          No notes yet.
        </div>
      )}

      <div className="space-y-4">
        {items.map((n) => (
          <article key={n.id} className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl p-5 shadow-sm">
            <div className="flex items-start justify-between gap-3 flex-wrap mb-4">
              <div>
                <div className="flex items-center gap-2 flex-wrap">
                  <h4 className="text-[14px] font-semibold text-[#022C22]">
                    {NOTE_TYPE_LABELS[n.note_type] || n.note_type}
                  </h4>
                  <span className="text-[10px] uppercase tracking-wider font-semibold text-[#064E3B] bg-[#064E3B]/8 px-2 py-0.5 rounded-sm">
                    SOAP
                  </span>
                </div>
                <p className="text-[11px] text-text-muted mt-1">
                  {n.author_name || "Unknown author"} · {new Date(n.created_at).toLocaleString(undefined, {
                    dateStyle: "medium",
                    timeStyle: "short",
                  })}
                </p>
              </div>
              <Button
                size="sm"
                variant="ghost"
                className="rounded-sm h-7 text-[12px] text-[#064E3B]"
                onClick={() => openEdit(n)}
                data-testid={`note-edit-${n.id}`}
              >
                <PencilSimple className="w-3.5 h-3.5 mr-1" /> Edit
              </Button>
            </div>

            <div className="space-y-4">
              {n.subjective && <SoapBlock label="Subjective" text={n.subjective} />}
              {n.objective && <SoapBlock label="Objective" text={n.objective} />}
              {n.assessment && <SoapBlock label="Assessment" text={n.assessment} />}
              {n.plan && <SoapBlock label="Plan" text={n.plan} />}
              {!n.subjective && !n.objective && !n.assessment && !n.plan && (
                <p className="text-[12px] text-text-muted italic">No SOAP content recorded.</p>
              )}
            </div>
          </article>
        ))}
      </div>

      <Dialog open={!!editNote} onOpenChange={(v) => { if (!v) { setEditNote(null); setEditForm(null); } }}>
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="font-heading">Edit Clinical Note</DialogTitle>
          </DialogHeader>
          {editForm && (
            <div className="space-y-3">
              <div>
                <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Type</label>
                <Select value={editForm.note_type} onValueChange={(v) => setEditForm({ ...editForm, note_type: v })}>
                  <SelectTrigger className="rounded-sm h-9"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="progress">Progress</SelectItem>
                    <SelectItem value="soap">SOAP</SelectItem>
                    <SelectItem value="discharge">Discharge</SelectItem>
                    <SelectItem value="other">Other</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Soap label="Subjective" v={editForm.subjective} set={(x) => setEditForm({ ...editForm, subjective: x })} />
              <Soap label="Objective" v={editForm.objective} set={(x) => setEditForm({ ...editForm, objective: x })} />
              <Soap label="Assessment" v={editForm.assessment} set={(x) => setEditForm({ ...editForm, assessment: x })} />
              <Soap label="Plan" v={editForm.plan} set={(x) => setEditForm({ ...editForm, plan: x })} />
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => { setEditNote(null); setEditForm(null); }} className="rounded-sm">Cancel</Button>
            <Button onClick={saveEdit} className="rounded-sm bg-[#064E3B] hover:bg-[#022C22]">Save changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};

const Soap = ({ label, v, set }) => (
  <div>
    <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">{label}</label>
    <Textarea value={v} onChange={(e) => set(e.target.value)} rows={2} className="rounded-sm" />
  </div>
);

const SoapBlock = ({ label, text }) => (
  <div>
    <h5 className="text-[11px] uppercase tracking-[0.12em] text-[#064E3B] font-semibold mb-1.5">{label}</h5>
    <p className="text-[13px] text-[#022C22] whitespace-pre-wrap leading-relaxed">{text}</p>
  </div>
);

// -------- Medical & Family History --------

export const MedicalHistory = ({ patientId }) => {
  const [items, setItems] = useState([]);
  const [show, setShow] = useState(false);
  const [form, setForm] = useState({ category: "condition", title: "", onset_date: "", is_ongoing: true, description: "" });

  const load = () =>
    api.get(`/clinical/medical-history/patient/${patientId}`).then((r) => setItems(r.data));
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [patientId]);

  const save = async () => {
    if (!form.title.trim()) { toast.error("Title required"); return; }
    try {
      await api.post("/clinical/medical-history", {
        patient_id: patientId,
        category: form.category,
        title: form.title.trim(),
        onset_date: form.onset_date || null,
        is_ongoing: form.is_ongoing,
        description: form.description || null,
      });
      toast.success("Added");
      setForm({ category: "condition", title: "", onset_date: "", is_ongoing: true, description: "" });
      setShow(false);
      load();
    } catch (e) { toast.error(normalizeApiError(e, "Failed to save medical history")); }
  };

  return (
    <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl" data-testid="ehr-medical-history">
      <div className="px-5 py-3 border-b border-white/60 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <FirstAid weight="fill" className="w-4 h-4 text-[#064E3B]" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Past Medical History</h3>
          <span className="text-[11px] text-text-muted">({items.length})</span>
        </div>
        <Button size="sm" variant="outline" className="rounded-sm h-7 text-[12px]" onClick={() => setShow(!show)}>
          <Plus className="w-3.5 h-3.5" /> Add
        </Button>
      </div>

      {show && (
        <div className="p-4 border-b border-subtle bg-secondary/30 space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Category</label>
              <Select value={form.category} onValueChange={(v) => setForm({ ...form, category: v })}>
                <SelectTrigger className="rounded-sm h-9"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="condition">Condition</SelectItem>
                  <SelectItem value="surgery">Surgery</SelectItem>
                  <SelectItem value="hospitalization">Hospitalization</SelectItem>
                  <SelectItem value="immunization">Immunization</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Title *</label>
              <Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} className="rounded-sm" placeholder="e.g. Type 2 Diabetes" />
            </div>
            <div>
              <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Onset date</label>
              <Input type="date" value={form.onset_date} onChange={(e) => setForm({ ...form, onset_date: e.target.value })} className="rounded-sm" />
            </div>
            <div className="flex items-center pt-6 gap-2 text-[13px]">
              <input
                type="checkbox"
                checked={form.is_ongoing}
                onChange={(e) => setForm({ ...form, is_ongoing: e.target.checked })}
              />
              Ongoing
            </div>
          </div>
          <Textarea
            placeholder="Description"
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            rows={2}
            className="rounded-sm"
          />
          <Button onClick={save} className="rounded-sm bg-[#064E3B] hover:bg-[#022C22]">Save</Button>
        </div>
      )}

      <div className="divide-y divide-subtle">
        {items.length === 0 && (
          <div className="px-5 py-8 text-center text-text-muted text-[13px]">No history items.</div>
        )}
        {items.map((h) => (
          <div key={h.id} className="px-5 py-3">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-[13px] font-medium text-[#022C22]">{h.title}</span>
              <span className="text-[11px] text-text-muted">
                {MEDICAL_CATEGORY_LABELS[h.category] || h.category}
              </span>
              {h.is_ongoing && <span className="text-[10px] uppercase bg-orange-50 text-orange-700 border border-orange-200 px-1.5 py-0.5 rounded-sm">Ongoing</span>}
              {h.onset_date && <span className="text-[11px] text-text-muted">Since {new Date(h.onset_date).toLocaleDateString()}</span>}
            </div>
            {h.description && <div className="text-[12px] text-text-secondary mt-1 leading-relaxed">{h.description}</div>}
          </div>
        ))}
      </div>
    </div>
  );
};

export const FamilyHistory = ({ patientId }) => {
  const [items, setItems] = useState([]);
  const [show, setShow] = useState(false);
  const [form, setForm] = useState({ relation: "father", condition: "", age_of_onset: "", notes: "" });

  const load = () =>
    api.get(`/clinical/family-history/patient/${patientId}`).then((r) => setItems(r.data));
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [patientId]);

  const save = async () => {
    if (!form.condition.trim()) { toast.error("Condition required"); return; }
    try {
      await api.post("/clinical/family-history", {
        patient_id: patientId,
        relation: form.relation,
        condition: form.condition,
        age_of_onset: form.age_of_onset ? Number(form.age_of_onset) : null,
        notes: form.notes || null,
      });
      toast.success("Added");
      setForm({ relation: "father", condition: "", age_of_onset: "", notes: "" });
      setShow(false);
      load();
    } catch (e) { toast.error(normalizeApiError(e, "Failed to save family history")); }
  };

  return (
    <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl" data-testid="ehr-family-history">
      <div className="px-5 py-3 border-b border-white/60 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <UsersFour weight="fill" className="w-4 h-4 text-[#064E3B]" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Family History</h3>
          <span className="text-[11px] text-text-muted">({items.length})</span>
        </div>
        <Button size="sm" variant="outline" className="rounded-sm h-7 text-[12px]" onClick={() => setShow(!show)}>
          <Plus className="w-3.5 h-3.5" /> Add
        </Button>
      </div>

      {show && (
        <div className="p-4 border-b border-subtle bg-secondary/30 space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Relation</label>
              <Select value={form.relation} onValueChange={(v) => setForm({ ...form, relation: v })}>
                <SelectTrigger className="rounded-sm h-9"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="father">Father</SelectItem>
                  <SelectItem value="mother">Mother</SelectItem>
                  <SelectItem value="sibling">Sibling</SelectItem>
                  <SelectItem value="grandparent">Grandparent</SelectItem>
                  <SelectItem value="other">Other</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Condition *</label>
              <Input value={form.condition} onChange={(e) => setForm({ ...form, condition: e.target.value })} className="rounded-sm" placeholder="e.g. Diabetes" />
            </div>
            <div>
              <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Age of onset</label>
              <Input type="number" value={form.age_of_onset} onChange={(e) => setForm({ ...form, age_of_onset: e.target.value })} className="rounded-sm" />
            </div>
          </div>
          <Textarea
            placeholder="Notes"
            value={form.notes}
            onChange={(e) => setForm({ ...form, notes: e.target.value })}
            rows={2}
            className="rounded-sm"
          />
          <Button onClick={save} className="rounded-sm bg-[#064E3B] hover:bg-[#022C22]">Save</Button>
        </div>
      )}

      <div className="divide-y divide-subtle">
        {items.length === 0 && (
          <div className="px-5 py-8 text-center text-text-muted text-[13px]">No family history.</div>
        )}
        {items.map((h) => (
          <div key={h.id} className="px-5 py-3">
            <div className="text-[13px] text-[#022C22]">
              <span className="font-medium">{FAMILY_RELATION_LABELS[h.relation] || h.relation}</span>
              <span className="text-text-muted mx-1.5">·</span>
              <span>{h.condition}</span>
              {h.age_of_onset != null && (
                <span className="text-text-muted text-[11px] ml-2">(onset at age {h.age_of_onset})</span>
              )}
            </div>
            {h.notes && <div className="text-[12px] text-text-secondary mt-1 leading-relaxed">{h.notes}</div>}
          </div>
        ))}
      </div>
    </div>
  );
};
