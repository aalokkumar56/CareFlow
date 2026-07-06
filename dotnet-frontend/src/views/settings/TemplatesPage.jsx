import React, { useEffect, useRef, useState } from "react";

import RequirePermission from "@/components/RequirePermission";

import SettingsLayout from "@/components/settings/SettingsLayout";

import SettingsNav from "@/components/settings/SettingsNav";

import TemplatePlaceholdersPanel from "@/components/TemplatePlaceholdersPanel";

import GlassCard from "@/components/glass/GlassCard";

import StatusPill from "@/components/glass/StatusPill";

import { api } from "@/lib/api";

import { fetchTemplates, invalidateTemplates } from "@/lib/templatesCache";

import { Input } from "@/components/ui/input";

import { Button } from "@/components/ui/button";

import { Textarea } from "@/components/ui/textarea";

import {

  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,

} from "@/components/ui/select";

import {

  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,

} from "@/components/ui/dialog";

import { Plus, Trash } from "@phosphor-icons/react";

import { toast } from "sonner";

import usePermissions from "@/hooks/usePermissions";

import { PERMISSIONS } from "@/lib/permissions";



const CATEGORIES = [

  { value: "general", label: "General" },

  { value: "appointment", label: "Appointment" },

  { value: "follow_up", label: "Follow-up" },

  { value: "billing", label: "Billing" },

  { value: "reminder", label: "Reminder" },

  { value: "marketing", label: "Marketing" },

];



const CATEGORY_META = {

  general: { status: "draft", label: "General" },

  appointment: { status: "confirmed", label: "Appointment" },

  appointment_confirmation: { status: "confirmed", label: "Confirmation" },

  appointment_rescheduled: { status: "scheduled", label: "Rescheduled" },

  appointment_cancelled: { status: "cancelled", label: "Cancelled" },

  follow_up: { status: "follow_up", label: "Follow-up" },

  billing: { status: "converted", label: "Billing" },

  marketing: { status: "scheduled", label: "Marketing" },

  reminder: { status: "pending", label: "Reminder" },

};



const getCategoryMeta = (category = "general") => {

  const key = (category || "general").toLowerCase();

  return CATEGORY_META[key] || { status: "draft", label: key.replace(/_/g, " ") };

};



const highlightBody = (body) => {

  const parts = String(body || "").split(/(\{[^}]+\})/g);

  return parts.map((part, i) => (

    part.match(/^\{[^}]+\}$/)

      ? <span key={i} className="text-[#4338CA] font-medium">{part}</span>

      : part

  ));

};



const TemplatesPage = () => (

  <RequirePermission permission={PERMISSIONS.SettingsView}>

    <TemplatesPageContent />

  </RequirePermission>

);



const TemplatesPageContent = () => {

  const { can } = usePermissions();

  const canEdit = can(PERMISSIONS.SettingsEdit);

  const [templates, setTemplates] = useState([]);

  const [open, setOpen] = useState(false);

  const [form, setForm] = useState({ name: "", body: "", category: "general" });

  const tplBodyRef = useRef(null);



  useEffect(() => {

    fetchTemplates().then(setTemplates).catch(() => setTemplates([]));

  }, []);



  const insertPlaceholder = (token) => {

    const el = tplBodyRef.current;

    if (el && typeof el.selectionStart === "number") {

      const start = el.selectionStart;

      const end = el.selectionEnd;

      const next = form.body.slice(0, start) + token + form.body.slice(end);

      setForm({ ...form, body: next });

      requestAnimationFrame(() => {

        el.focus();

        el.setSelectionRange(start + token.length, start + token.length);

      });

      return;

    }

    setForm({ ...form, body: `${form.body}${token}` });

  };



  const create = async () => {

    if (!form.name || !form.body) { toast.error("Name and body required"); return; }

    await api.post("/templates", form);

    setForm({ name: "", body: "", category: "general" });

    setOpen(false);

    invalidateTemplates();

    fetchTemplates({ force: true }).then(setTemplates);

    toast.success("Template saved");

  };



  const remove = async (id) => {

    await api.delete(`/templates/${id}`);

    invalidateTemplates();

    fetchTemplates({ force: true }).then(setTemplates);

    toast.success("Removed");

  };



  return (

    <SettingsLayout title="Templates" sidebar={<SettingsNav />}>
      <div className="flex-1 min-w-0 flex flex-col gap-2 min-h-0 overflow-hidden">

        <GlassCard padding={false} className="flex-1 min-h-0 overflow-hidden flex flex-col">

          <div className="px-3 py-2 border-b border-white/45 flex items-center justify-between gap-2 shrink-0">

            <div>

              <h2 className="font-heading text-ui-base font-semibold text-[#022C22]">Quick Reply Templates</h2>

              <p className="text-ui-caption text-text-secondary">Reusable message snippets for the inbox</p>

            </div>

            {canEdit && (

              <Dialog open={open} onOpenChange={setOpen}>

                <DialogTrigger asChild>

                  <Button data-testid="new-tpl-btn" className="btn-primary h-9 rounded-xl">

                    <Plus weight="bold" className="w-3.5 h-3.5 mr-1" /> New

                  </Button>

                </DialogTrigger>

                <DialogContent className="rounded-xl glass-card max-w-lg">

                  <DialogHeader><DialogTitle className="font-heading">New Template</DialogTitle></DialogHeader>

                  <div className="space-y-3">

                    <div>

                      <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Name</label>

                      <Input data-testid="tpl-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="rounded-xl" placeholder="Appointment confirmation" />

                    </div>

                    <div>

                      <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Category</label>

                      <Select value={form.category} onValueChange={(v) => setForm({ ...form, category: v })}>

                        <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>

                        <SelectContent>

                          {CATEGORIES.map((c) => (

                            <SelectItem key={c.value} value={c.value}>{c.label}</SelectItem>

                          ))}

                        </SelectContent>

                      </Select>

                    </div>

                    <div>

                      <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Message body</label>

                      <TemplatePlaceholdersPanel onInsert={insertPlaceholder} canManage={canEdit} className="mb-2" />

                      <Textarea

                        ref={tplBodyRef}

                        data-testid="tpl-body"

                        value={form.body}

                        onChange={(e) => setForm({ ...form, body: e.target.value })}

                        className="rounded-xl"

                        rows={4}

                        placeholder="Hi {name}, your appointment with {doctor} on {date} is confirmed."

                      />

                    </div>

                  </div>

                  <DialogFooter>

                    <Button variant="outline" onClick={() => setOpen(false)} className="rounded-xl">Cancel</Button>

                    <Button data-testid="tpl-save-btn" onClick={create} className="btn-primary">Save</Button>

                  </DialogFooter>

                </DialogContent>

              </Dialog>

            )}

          </div>



          <div className="flex-1 min-h-0 overflow-y-auto scrollbar-thin p-2">

            {templates.length === 0 ? (

              <div className="py-10 text-center text-ui-sm text-text-muted">No templates yet</div>

            ) : (

              <ul className="space-y-2">

                {templates.map((t) => {

                  const meta = getCategoryMeta(t.category);

                  return (

                    <li

                      key={t.id}

                      data-testid={`template-row-${t.id}`}

                      className="rounded-xl border border-white/55 bg-white/40 px-3 py-2.5 flex items-start gap-3 hover:bg-white/55 transition-colors"

                    >

                      <div className="flex-1 min-w-0">

                        <div className="flex items-center gap-2 flex-wrap">

                          <span className="text-ui-sm font-medium text-[#022C22]">{t.name}</span>

                          <StatusPill status={meta.status} label={meta.label} />

                        </div>

                        <p className="text-ui-caption text-text-secondary mt-1.5 whitespace-pre-wrap leading-relaxed">

                          {highlightBody(t.body)}

                        </p>

                      </div>

                      {canEdit && (

                        <button

                          type="button"

                          onClick={() => remove(t.id)}

                          className="h-8 w-8 rounded-xl flex items-center justify-center text-text-muted hover:text-red-600 hover:bg-white/60 shrink-0 mt-0.5"

                          aria-label={`Delete ${t.name}`}

                        >

                          <Trash className="w-4 h-4" />

                        </button>

                      )}

                    </li>

                  );

                })}

              </ul>

            )}

          </div>

        </GlassCard>

      </div>

    </SettingsLayout>

  );

};



export default TemplatesPage;

