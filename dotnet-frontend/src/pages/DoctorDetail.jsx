import React, { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import AppShell from "@/components/layout/AppShell";
import { api, formatPhone } from "@/lib/api";
import { unwrapPaged } from "@/lib/pagination";
import { ArrowLeft, Stethoscope, CurrencyInr, UsersThree, Plus } from "@phosphor-icons/react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { toast } from "sonner";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import StatCard from "@/components/glass/StatCard";
import StatusPill from "@/components/glass/StatusPill";

const DoctorDetail = () => {
  const { id } = useParams();
  const [data, setData] = useState(null);
  const [patients, setPatients] = useState([]);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ patient_id: "", revenue: "", notes: "" });

  const load = () => api.get(`/doctors/${id}`).then((r) => setData(r.data));

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);
  useEffect(() => { api.get("/patients?page=1&page_size=300").then((r) => setPatients(unwrapPaged(r).items)); }, []);

  const addReferral = async () => {
    if (!form.patient_id) { toast.error("Select a patient"); return; }
    try {
      await api.post("/referrals", {
        doctor_id: id,
        patient_id: form.patient_id,
        revenue: parseFloat(form.revenue) || 0,
        notes: form.notes,
      });
      toast.success("Referral logged");
      setOpen(false);
      setForm({ patient_id: "", revenue: "", notes: "" });
      load();
    } catch (e) { toast.error(e.response?.data?.detail || "Failed"); }
  };

  if (!data) return <AppShell title="Loading...">{null}</AppShell>;

  const d = data.doctor;
  const needsReconnect = !d.last_contact_at
    || ((Date.now() - new Date(d.last_contact_at).getTime()) / 86400000) >= (d.reconnect_every_days || 30);

  return (
    <AppShell
      title={d.name}
      subtitle={`${d.specialty || "—"} · ${d.clinic || "—"}`}
      actions={
        <Link to="/doctors" className="text-[13px] text-text-secondary hover:text-[#064E3B] flex items-center gap-1.5 rounded-xl px-2 py-1">
          <ArrowLeft weight="regular" className="w-4 h-4" /> Back to doctors
        </Link>
      }
    >
      <PageContent>
        <Tabs defaultValue="overview" className="space-y-5">
          <TabsList className="glass-card h-auto p-1 rounded-xl bg-white/60 border border-white/50 w-full sm:w-auto flex flex-wrap gap-1">
            <TabsTrigger value="overview" className="rounded-lg data-[state=active]:bg-[#064E3B] data-[state=active]:text-white" data-testid="doctor-tab-overview">
              Overview
            </TabsTrigger>
            <TabsTrigger value="referrals" className="rounded-lg data-[state=active]:bg-[#064E3B] data-[state=active]:text-white" data-testid="doctor-tab-referrals">
              Referrals ({data.referrals?.length ?? 0})
            </TabsTrigger>
          </TabsList>

          <TabsContent value="overview" className="mt-0 space-y-5">
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
              <GlassCard className="lg:col-span-1">
                <div className="flex items-center gap-3 mb-5">
                  <div className="w-14 h-14 rounded-xl bg-[#064E3B] text-white flex items-center justify-center shrink-0">
                    <Stethoscope weight="regular" className="w-7 h-7" />
                  </div>
                  <div className="min-w-0">
                    <h2 className="font-heading text-lg font-semibold text-[#022C22] truncate" data-testid="doctor-name">{d.name}</h2>
                    <div className="text-ui-sm text-text-secondary">{d.specialty || "—"}</div>
                    <StatusPill
                      status={needsReconnect ? "follow_up" : "converted"}
                      label={needsReconnect ? "Needs reconnect" : "Recently contacted"}
                      className="mt-1.5"
                    />
                  </div>
                </div>
                <div className="space-y-3 text-[13px]">
                  <Row label="Clinic" value={d.clinic || "—"} />
                  <Row label="Category" value={d.category?.replace(/_/g, " ")} />
                  <Row label="Phone" value={d.phone ? formatPhone(d.phone) : "—"} mono />
                  <Row label="Email" value={d.email || "—"} />
                  <Row label="Reconnect every" value={`${d.reconnect_every_days} days`} />
                  <Row label="Last contact" value={d.last_contact_at ? new Date(d.last_contact_at).toLocaleDateString() : "—"} />
                </div>
                {d.tags?.length > 0 && (
                  <div className="mt-4 pt-4 border-t border-white/50">
                    <div className="text-[11px] uppercase tracking-[0.08em] text-text-muted font-semibold mb-2">Tags</div>
                    <div className="flex gap-1.5 flex-wrap">
                      {d.tags.map((t) => (
                        <span key={t} className="text-[11px] bg-white/60 text-text-secondary px-2 py-0.5 rounded-full border border-white/70">
                          {t}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
                {d.notes && (
                  <div className="mt-4 pt-4 border-t border-white/50">
                    <div className="text-[11px] uppercase tracking-[0.08em] text-text-muted font-semibold mb-1">Notes</div>
                    <div className="text-[13px] text-[#022C22]">{d.notes}</div>
                  </div>
                )}
              </GlassCard>

              <div className="lg:col-span-2 grid grid-cols-1 sm:grid-cols-2 gap-4">
                <StatCard icon={UsersThree} label="Patients Referred" value={data.patients_referred ?? 0} testId="doctor-referrals-stat" />
                <StatCard
                  icon={CurrencyInr}
                  label="Revenue Generated"
                  value={`₹${Number(data.total_revenue ?? 0).toLocaleString("en-IN")}`}
                  accent="bg-emerald-50 text-emerald-700"
                  testId="doctor-revenue-stat"
                />
              </div>
            </div>
          </TabsContent>

          <TabsContent value="referrals" className="mt-0">
            <GlassCard padding={false} className="overflow-hidden">
              <div className="px-5 py-4 border-b border-white/50 flex items-center justify-between gap-3 flex-wrap">
                <h3 className="font-heading text-[15px] font-semibold text-[#022C22]">Referral History</h3>
                <Dialog open={open} onOpenChange={setOpen}>
                  <DialogTrigger asChild>
                    <Button data-testid="log-referral-btn" size="sm" className="btn-primary h-8 text-[12px]">
                      <Plus weight="bold" className="w-3 h-3 mr-1" /> Log Referral
                    </Button>
                  </DialogTrigger>
                  <DialogContent className="rounded-2xl glass-card">
                    <DialogHeader><DialogTitle className="font-heading">Log Patient Referral</DialogTitle></DialogHeader>
                    <div className="space-y-3">
                      <div>
                        <label className="text-[11px] uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Patient *</label>
                        <Select value={form.patient_id} onValueChange={(v) => setForm({...form, patient_id: v})}>
                          <SelectTrigger className="rounded-xl h-9" data-testid="ref-patient-select"><SelectValue placeholder="Select patient" /></SelectTrigger>
                          <SelectContent className="max-h-[300px]">
                            {patients.map((p) => <SelectItem key={p.id} value={p.id}>{p.name} ({p.phone})</SelectItem>)}
                          </SelectContent>
                        </Select>
                      </div>
                      <div>
                        <label className="text-[11px] uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Revenue (₹)</label>
                        <Input type="number" value={form.revenue} onChange={(e) => setForm({...form, revenue: e.target.value})} className="rounded-xl" />
                      </div>
                      <div>
                        <label className="text-[11px] uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Notes</label>
                        <Textarea value={form.notes} onChange={(e) => setForm({...form, notes: e.target.value})} className="rounded-xl" rows={2} />
                      </div>
                    </div>
                    <DialogFooter>
                      <Button variant="outline" onClick={() => setOpen(false)} className="rounded-xl">Cancel</Button>
                      <Button data-testid="ref-save-btn" onClick={addReferral} className="btn-primary">Save</Button>
                    </DialogFooter>
                  </DialogContent>
                </Dialog>
              </div>
              <div className="divide-y divide-white/50 max-h-[500px] overflow-y-auto scrollbar-thin">
                {data.referrals.length === 0 && (
                  <div className="py-10 text-center text-text-muted text-[13px]">No referrals yet</div>
                )}
                {data.referrals.map((r) => (
                  <div key={r.id} className="px-5 py-3 flex items-center justify-between gap-3 hover:bg-white/30">
                    <div className="min-w-0">
                      <Link to={`/patients/${r.patient_id}`} className="text-[13px] text-[#022C22] font-medium hover:underline">{r.patient_name}</Link>
                      <div className="text-[11px] text-text-muted">{new Date(r.created_at).toLocaleDateString()} · {r.notes || "—"}</div>
                    </div>
                    <div className="text-[14px] font-mono text-[#022C22] shrink-0">₹{r.revenue.toLocaleString("en-IN")}</div>
                  </div>
                ))}
              </div>
            </GlassCard>
          </TabsContent>
        </Tabs>
      </PageContent>
    </AppShell>
  );
};

const Row = ({ label, value, mono }) => (
  <div className="flex items-center justify-between gap-2">
    <span className="text-[11px] uppercase tracking-[0.08em] text-text-muted font-semibold">{label}</span>
    <span className={`text-[13px] text-[#022C22] capitalize text-right ${mono ? "font-mono" : ""}`}>{value}</span>
  </div>
);

export default DoctorDetail;
