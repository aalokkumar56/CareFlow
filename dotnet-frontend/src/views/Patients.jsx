import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "@/lib/navigation";
import AppShell from "@/components/layout/AppShell";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import StatusPill from "@/components/glass/StatusPill";
import PatientPreviewPanel from "@/components/patients/PatientPreviewPanel";
import PatientsPagination from "@/components/patients/PatientsPagination";
import {
  avatarGradient, DEPT_ICONS, formatPatientId, formatRupee,
  formatSource, getInitials, isVipPatient, relativeTime, SOURCE_OPTIONS,
} from "@/components/patients/patientUtils";
import DepartmentSelect from "@/components/forms/DepartmentSelect";
import useDepartments from "@/hooks/useDepartments";
import { api, formatPhone, STATUS_LABELS, normalizeApiError } from "@/lib/api";
import { unwrapPaged, buildPageQuery } from "@/lib/pagination";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Plus, Export, MagnifyingGlass, FileXls, UploadSimple, DownloadSimple,
  DotsThreeVertical, PencilSimple, Eye, Stethoscope,
} from "@phosphor-icons/react";
import { toast } from "sonner";
import EmptyState from "@/components/ui/EmptyState";
import { cn } from "@/lib/utils";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { useAuth } from "@/lib/auth";
import {
  formatHospitalDate,
  formatHospitalTime12h,
  resolveHospitalTimezone,
} from "@/lib/tenantTime";

const Patients = () => {
  const navigate = useNavigate();
  const { tenant } = useAuth();
  const hospitalTz = resolveHospitalTimezone(tenant);
  const { can, isAdmin } = usePermissions();
  const canCreatePatient = can(PERMISSIONS.PatientCreate);
  const { departments } = useDepartments();
  const [params, setParams] = useSearchParams();
  const [rows, setRows] = useState([]);
  const [doctors, setDoctors] = useState([]);
  const [q, setQ] = useState("");
  const [status, setStatus] = useState(() => params.get("status") || "all");
  const [dept, setDept] = useState(() => params.get("department") || "all");
  const [source, setSource] = useState(() => params.get("source") || "all");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(8);
  const [total, setTotal] = useState(0);
  const [nextApptMap, setNextApptMap] = useState({});
  const [openNew, setOpenNew] = useState(params.get("new") === "1");
  const [openImportExcel, setOpenImportExcel] = useState(false);
  const [excelFile, setExcelFile] = useState(null);
  const [importingExcel, setImportingExcel] = useState(false);
  const [importResult, setImportResult] = useState(null);
  const [selectedPatient, setSelectedPatient] = useState(null);
  const [form, setForm] = useState({
    name: "", phone: "", age: "", gender: "", department: "",
    inquiry_source: "manual", referral_doctor_id: "", tags: "", notes: "",
  });

  const fetchData = useCallback(() => {
    setLoading(true);
    const qs = buildPageQuery({
      q, status, department: dept, inquiry_source: source, page, page_size: pageSize,
    });
    api.get(`/patients?${qs}`)
      .then((r) => {
        const paged = unwrapPaged(r);
        if (paged.total > 0 && paged.items.length === 0 && page > 1) {
          setPage(1);
          return;
        }
        setRows(paged.items);
        setTotal(paged.total);
      })
      .catch((err) => {
        if (err?.code !== "ERR_CANCELED") toast.error(normalizeApiError(err, "Failed to load patients"));
      })
      .finally(() => setLoading(false));
  }, [q, status, dept, source, page, pageSize]);

  const fetchUpcomingAppointments = useCallback(() => {
    const from = new Date().toISOString();
    const to = new Date(Date.now() + 30 * 86400000).toISOString();
    api.get(`/appointments?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&page_size=100`)
      .then((r) => {
        const items = unwrapPaged(r).items || [];
        const map = {};
        items
          .filter((a) => a.patient_id && a.scheduled_at && !["cancelled", "completed"].includes(a.status))
          .sort((a, b) => new Date(a.scheduled_at) - new Date(b.scheduled_at))
          .forEach((a) => {
            if (!map[a.patient_id]) map[a.patient_id] = a;
          });
        setNextApptMap(map);
      })
      .catch(() => setNextApptMap({}));
  }, []);

  useEffect(() => {
    const urlStatus = params.get("status") || "all";
    const urlDept = params.get("department") || "all";
    const urlSource = params.get("source") || "all";
    if (urlStatus !== status) setStatus(urlStatus);
    if (urlDept !== dept) setDept(urlDept);
    if (urlSource !== source) setSource(urlSource);
    /* eslint-disable-next-line */
  }, [params]);

  const updateFilter = (key, value, setter) => {
    setter(value);
    setParams((prev) => {
      const next = new URLSearchParams(prev);
      if (value === "all") next.delete(key);
      else next.set(key, value);
      return next;
    }, { replace: true });
  };

  useEffect(() => { setPage(1); }, [q, status, dept, source, pageSize]);
  useEffect(() => { fetchData(); }, [fetchData]);
  useEffect(() => { fetchUpcomingAppointments(); }, [fetchUpcomingAppointments]);

  useEffect(() => {
    const controller = new AbortController();
    api.get("/doctors", { signal: controller.signal })
      .then((r) => setDoctors(r.data))
      .catch(() => {});
    return () => controller.abort();
  }, []);

  const openPreview = (patient) => setSelectedPatient(patient);
  const closePreview = () => setSelectedPatient(null);
  const openEdit = (patient) => navigate(`/patients/${patient.id}?edit=1`);
  const rowClickTimerRef = useRef(null);

  const openPatientDetail = useCallback((patientId) => {
    if (rowClickTimerRef.current) {
      clearTimeout(rowClickTimerRef.current);
      rowClickTimerRef.current = null;
    }
    navigate(`/patients/${patientId}`);
  }, [navigate]);

  useEffect(() => () => {
    if (rowClickTimerRef.current) clearTimeout(rowClickTimerRef.current);
  }, []);

  /** Delay single-click so a real double-click can win and open the profile. */
  const handleRowClick = (patientId) => {
    if (rowClickTimerRef.current) clearTimeout(rowClickTimerRef.current);
    rowClickTimerRef.current = setTimeout(() => {
      rowClickTimerRef.current = null;
      navigate(`/patients/${patientId}`);
    }, 280);
  };

  const handleRowDoubleClick = (e, patientId) => {
    e.preventDefault();
    e.stopPropagation();
    openPatientDetail(patientId);
  };

  const exportCsv = () => {
    if (!rows.length) {
      toast.error("No patients to export");
      return;
    }
    const header = ["Name", "Phone", "Email", "Department", "Status", "Source", "Created"];
    const lines = rows.map((p) => [
      p.name, p.phone, p.email || "", p.department || "",
      STATUS_LABELS[p.status] || p.status, p.inquiry_source || "",
      p.created_at ? new Date(p.created_at).toISOString() : "",
    ].map((c) => `"${String(c).replace(/"/g, '""')}"`).join(","));
    const blob = new Blob([[header.join(","), ...lines].join("\n")], { type: "text/csv" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "patients-export.csv";
    a.click();
    URL.revokeObjectURL(url);
    toast.success("Export downloaded");
  };

  const create = async () => {
    if (!form.name || !form.phone) {
      toast.error("Name and phone required");
      return;
    }
    try {
      const selectedDoctor = (form.referral_doctor_id && form.referral_doctor_id !== "none")
        ? doctors.find((d) => d.id === form.referral_doctor_id)
        : null;
      const payload = await api.post("/patients", {
        name: form.name,
        phone: form.phone,
        inquiry_source: form.inquiry_source || "manual",
        age: form.age ? parseInt(form.age, 10) : null,
        gender: form.gender || null,
        department: form.department || null,
        referring_doctor_id: selectedDoctor ? selectedDoctor.id : null,
        notes: form.notes,
        tags: form.tags ? form.tags.split(",").map((t) => t.trim()).filter(Boolean) : [],
      });
      if (selectedDoctor && payload?.data?.id) {
        try {
          await api.post("/referrals", {
            doctor_id: selectedDoctor.id,
            patient_id: payload.data.id,
            revenue: 0,
            notes: "Auto-logged from patient creation",
          });
        } catch { /* non-blocking */ }
      }
      toast.success("Patient added");
      setOpenNew(false);
      setForm({
        name: "", phone: "", age: "", gender: "", department: "",
        inquiry_source: "manual", referral_doctor_id: "", tags: "", notes: "",
      });
      fetchData();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed"));
    }
  };

  const handleImportExcel = async (e) => {
    e.preventDefault();
    if (!excelFile) {
      toast.error("Please select an Excel or CSV file");
      return;
    }
    setImportingExcel(true);
    setImportResult(null);
    try {
      const formData = new FormData();
      formData.append("file", excelFile);
      const res = await api.post("/patients/import-excel", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      const { inserted, skipped, blankRows, skipLog } = res.data || {};
      const ins = inserted || 0;
      const skp = skipped || 0;

      if (ins === 0 && skp === 0) {
        toast.warning("No records found in file. Check that your file has Name and Phone/Mobile columns.");
      } else if (ins === 0) {
        toast.info(`Import done: ${skp} rows skipped — see details below.`);
      } else {
        toast.success(`✅ ${ins} patients imported successfully!`);
      }

      if (skp > 0 && skipLog?.length > 0) {
        // Stay open and show skip details
        setImportResult({ inserted: ins, skipped: skp, blankRows: blankRows || 0, skipLog });
        setExcelFile(null);
      } else {
        setOpenImportExcel(false);
        setExcelFile(null);
      }
      fetchData();
    } catch (err) {
      toast.error(normalizeApiError(err, "Failed to import file"));
    } finally {
      setImportingExcel(false);
    }
  };

  const downloadSampleTemplate = () => {
    const header = "Patient Name,Mobile Number,Age,Gender,Department,Source,Tags,Notes";
    const rows = [
      "Ramesh Kumar,9876543210,45,Male,Cardiology,Referral,VIP,Follow-up required",
      "Priya Sharma,9123456789,32,Female,Gynaecology,Walk-in,,",
      "Ankit Verma,8000012345,28,Male,Orthopaedics,Online,,",
    ];
    const blob = new Blob([[header, ...rows].join("\n") + "\n"], { type: "text/csv" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "sample_patient_import.csv";
    a.click();
    URL.revokeObjectURL(url);
  };

  const uniqueSources = useMemo(() => {
    const fromApi = SOURCE_OPTIONS.filter((s) => s.value !== "all");
    return fromApi;
  }, []);

  return (
    <AppShell
      title="Patients"
      subtitle="Manage and track all your patients in one place"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
      actions={(
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            data-testid="export-patients-btn"
            onClick={exportCsv}
            className="rounded-xl h-9 px-3.5 text-[13px] border-white/60 bg-white/50 hover:bg-white/70"
          >
            <Export weight="regular" className="w-4 h-4 mr-1.5" />
            Export
          </Button>
          {isAdmin && (
            <Dialog open={openImportExcel} onOpenChange={setOpenImportExcel}>
              <DialogTrigger asChild>
                <Button
                  type="button"
                  variant="outline"
                  data-testid="import-excel-patients-btn"
                  className="rounded-xl h-9 px-3.5 text-[13px] border-emerald-500/40 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-500/20"
                >
                  <FileXls weight="bold" className="w-4 h-4 mr-1.5 text-emerald-600 dark:text-emerald-400" />
                  Import Excel
                </Button>
              </DialogTrigger>
              <DialogContent className={`rounded-xl glass-card ${importResult ? 'max-w-2xl' : 'max-w-md'}`} data-testid="import-excel-dialog">
                <DialogHeader>
                  <DialogTitle className="font-heading flex items-center gap-2">
                    <FileXls className="w-5 h-5 text-emerald-600" />
                    Import Patients from Excel
                  </DialogTitle>
                </DialogHeader>
                <form onSubmit={handleImportExcel} className="space-y-4">
                  <div className="text-xs text-text-secondary leading-relaxed space-y-1.5">
                    <p>Upload an Excel (<code className="bg-muted px-1 rounded">.xlsx</code>, <code className="bg-muted px-1 rounded">.xls</code>) or <code className="bg-muted px-1 rounded">.csv</code> file.</p>
                    <p className="text-[11px] text-text-tertiary">
                      The file must have a <strong>Name</strong> column (e.g. &quot;Patient Name&quot;, &quot;Name&quot;, &quot;Full Name&quot;) and a <strong>Phone</strong> column (e.g. &quot;Mobile Number&quot;, &quot;Phone&quot;, &quot;Contact&quot;, &quot;Mob&quot;). Other columns like Age, Gender, Department, Notes are auto-detected.
                    </p>
                  </div>

                  <div className="border-2 border-dashed border-white/40 dark:border-white/10 rounded-xl p-4 text-center hover:border-emerald-500/50 transition-colors">
                    <input
                      type="file"
                      id="excel-file-input"
                      accept=".xlsx,.xls,.csv"
                      onChange={(e) => setExcelFile(e.target.files[0] || null)}
                      className="hidden"
                    />
                    <label htmlFor="excel-file-input" className="cursor-pointer flex flex-col items-center justify-center gap-2">
                      <UploadSimple className="w-8 h-8 text-emerald-500 animate-pulse" />
                      <span className="text-xs font-medium text-text-primary">
                        {excelFile ? excelFile.name : "Click to choose Excel file"}
                      </span>
                      <span className="text-[11px] text-text-tertiary">
                        Supports .xlsx, .xls, .csv up to 10MB
                      </span>
                    </label>
                  </div>

                  <div className="flex items-center justify-between pt-1">
                    <button
                      type="button"
                      onClick={downloadSampleTemplate}
                      className="text-[12px] text-emerald-600 dark:text-emerald-400 hover:underline flex items-center gap-1 font-medium"
                    >
                      <DownloadSimple className="w-3.5 h-3.5" />
                      Download Sample Template
                    </button>
                  </div>

                  {/* Skip Log Results Panel */}
                  {importResult && (
                    <div className="space-y-2">
                      <div className="flex items-center gap-2 flex-wrap text-xs">
                        <span className="px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-700 dark:text-emerald-300 font-semibold">
                          ✅ {importResult.inserted} inserted
                        </span>
                        <span className="px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-700 dark:text-amber-300 font-semibold">
                          ⚠️ {importResult.skipped} skipped
                        </span>
                        {importResult.blankRows > 0 && (
                          <span className="px-2 py-0.5 rounded-full bg-gray-500/20 text-gray-600 dark:text-gray-400 font-semibold">
                            {importResult.blankRows} blank rows
                          </span>
                        )}
                      </div>
                      <p className="text-[11px] text-text-tertiary">Rows below were skipped. Fix them and re-upload to import the rest.</p>
                      <div className="border border-amber-500/20 rounded-lg overflow-hidden">
                        <div className="grid grid-cols-[3rem_1fr_1fr_2fr] text-[10px] font-semibold uppercase tracking-wider bg-amber-500/10 text-amber-700 dark:text-amber-300 px-2 py-1.5">
                          <span>Row</span>
                          <span>Name</span>
                          <span>Phone</span>
                          <span>Reason</span>
                        </div>
                        <div className="max-h-48 overflow-y-auto divide-y divide-white/5">
                          {importResult.skipLog.map((s, i) => (
                            <div key={i} className="grid grid-cols-[3rem_1fr_1fr_2fr] text-[11px] px-2 py-1.5 hover:bg-white/5 transition-colors">
                              <span className="text-text-tertiary font-mono">{s.rowNumber}</span>
                              <span className="text-text-secondary truncate pr-1">{s.name || <em className="opacity-40">—</em>}</span>
                              <span className="text-text-secondary truncate pr-1 font-mono">{s.phoneRaw || <em className="opacity-40">—</em>}</span>
                              <span className="text-amber-600 dark:text-amber-400 break-words">{s.reason}</span>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  )}

                  <DialogFooter className="pt-2">
                    <Button
                      type="button"
                      variant="ghost"
                      onClick={() => { setOpenImportExcel(false); setExcelFile(null); setImportResult(null); }}
                      className="rounded-xl text-[13px]"
                    >
                      {importResult ? "Close" : "Cancel"}
                    </Button>
                    {!importResult && (
                      <Button
                        type="submit"
                        disabled={!excelFile || importingExcel}
                        data-testid="submit-import-excel-btn"
                        className="btn-primary rounded-xl text-[13px]"
                      >
                        {importingExcel ? "Importing..." : "Upload & Import"}
                      </Button>
                    )}
                  </DialogFooter>
                </form>
              </DialogContent>
            </Dialog>
          )}
          {canCreatePatient && (
          <Dialog open={openNew} onOpenChange={setOpenNew}>
            <DialogTrigger asChild>
              <Button data-testid="new-patient-btn" className="btn-primary h-9 rounded-xl text-[13px]">
                <Plus weight="bold" className="w-3.5 h-3.5 mr-1.5" />
                Add Patient
              </Button>
            </DialogTrigger>
            <DialogContent className="rounded-xl glass-card max-w-lg" data-testid="new-patient-dialog">
              <DialogHeader>
                <DialogTitle className="font-heading">Add Patient</DialogTitle>
              </DialogHeader>
              <div className="grid grid-cols-2 gap-3">
                <div className="col-span-2">
                  <label className="text-[11px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Name *</label>
                  <Input data-testid="np-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="rounded-xl" />
                </div>
                <div>
                  <label className="text-[11px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Phone *</label>
                  <Input data-testid="np-phone" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} className="rounded-xl" placeholder="9876543210" />
                </div>
                <div>
                  <label className="text-[11px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Age</label>
                  <Input data-testid="np-age" type="number" value={form.age} onChange={(e) => setForm({ ...form, age: e.target.value })} className="rounded-xl" />
                </div>
                <div>
                  <label className="text-[11px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Gender</label>
                  <Select value={form.gender} onValueChange={(v) => setForm({ ...form, gender: v })}>
                    <SelectTrigger className="rounded-xl h-9"><SelectValue placeholder="—" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="male">Male</SelectItem>
                      <SelectItem value="female">Female</SelectItem>
                      <SelectItem value="other">Other</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <DepartmentSelect
                  departments={departments}
                  value={form.department}
                  onChange={(v) => setForm({ ...form, department: v })}
                  required={false}
                  label="Department"
                />
                <div className="col-span-2">
                  <label className="text-[11px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Referring Doctor</label>
                  <Select value={form.referral_doctor_id} onValueChange={(v) => setForm({ ...form, referral_doctor_id: v })}>
                    <SelectTrigger className="rounded-xl h-9" data-testid="np-doctor"><SelectValue placeholder="None / select referring doctor" /></SelectTrigger>
                    <SelectContent className="max-h-[300px]">
                      <SelectItem value="none">— None —</SelectItem>
                      {doctors.map((d) => (
                        <SelectItem key={d.id} value={d.id}>
                          {d.name} {d.specialty ? `· ${d.specialty}` : ""}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="col-span-2">
                  <label className="text-[11px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Tags (comma separated)</label>
                  <Input data-testid="np-tags" value={form.tags} onChange={(e) => setForm({ ...form, tags: e.target.value })} className="rounded-xl" placeholder="vip, diabetes" />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setOpenNew(false)} className="rounded-xl">Cancel</Button>
                <Button data-testid="np-save-btn" onClick={create} className="btn-primary rounded-xl">Save patient</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
          )}
        </div>
      )}
    >
      <PageContent wide fill flush className="flex flex-col min-h-0 gap-1.5 pb-1">
        <GlassCard padding={false} className="shrink-0 px-2.5 sm:px-3 py-2">
          <div className="flex flex-wrap items-center gap-1.5 sm:gap-2">
            <div className="relative flex-1 min-w-[160px]">
              <MagnifyingGlass weight="regular" className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-text-muted pointer-events-none" />
              <Input
                data-testid="patients-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Search name, phone, email..."
                className="pl-8 rounded-xl h-9 text-ui-base glass-input border-white/60"
              />
            </div>
            <Select value={dept} onValueChange={(v) => updateFilter("department", v, setDept)}>
              <SelectTrigger className="rounded-xl h-9 w-[130px] sm:w-[148px] text-ui-base glass-input border-white/60" data-testid="filter-dept">
                <SelectValue placeholder="Department" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Departments</SelectItem>
                {departments.map((d) => <SelectItem key={d} value={d}>{d}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={status} onValueChange={(v) => updateFilter("status", v, setStatus)}>
              <SelectTrigger className="rounded-xl h-9 w-[120px] sm:w-[136px] text-ui-base glass-input border-white/60" data-testid="filter-status">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                {Object.entries(STATUS_LABELS).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={source} onValueChange={(v) => updateFilter("source", v, setSource)}>
              <SelectTrigger className="rounded-xl h-9 w-[118px] sm:w-[132px] text-ui-base glass-input border-white/60" data-testid="filter-source">
                <SelectValue placeholder="Source" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Sources</SelectItem>
                {uniqueSources.map((s) => <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>)}
              </SelectContent>
            </Select>
            <span className="text-ui-label text-text-muted hidden sm:inline" data-testid="patients-filter-count">
              {loading ? "…" : `${total} patient${total === 1 ? "" : "s"}`}
            </span>
          </div>
        </GlassCard>

        {/* Table + preview — fills remaining viewport height */}
        <div className={cn(
          "flex-1 min-h-0 overflow-hidden flex gap-2",
          selectedPatient ? "flex-col lg:flex-row" : "flex-col",
        )}>
          <div className="flex-1 min-w-0 min-h-0 flex flex-col overflow-hidden">
            <GlassCard padding={false} className="flex-1 min-h-0 overflow-hidden flex flex-col">
              <div className="flex-1 min-h-0 overflow-auto scrollbar-thin">
                {!loading && rows.length === 0 ? (
                  <EmptyState
                    testId="patients-empty-state"
                    illustration="/design/empty-state-patients.svg"
                    title="No patients yet"
                    description={q || status !== "all" || dept !== "all" || source !== "all"
                      ? "No patients match your current filters. Try adjusting search or filters."
                      : "Add your first patient to start tracking intake, appointments, and follow-ups."}
                    action={canCreatePatient && (
                      <Button
                        data-testid="empty-add-patient-btn"
                        onClick={() => setOpenNew(true)}
                        className="btn-primary rounded-xl h-10 min-h-[44px] px-4"
                      >
                        <Plus weight="bold" className="w-4 h-4 mr-1.5" />
                        Add patient
                      </Button>
                    )}
                  />
                ) : (
                <table className="w-full min-w-[860px] table-dense">
                  <thead className="sticky top-0 z-10 bg-white/70 backdrop-blur-sm border-b border-white/60">
                    <tr className="text-left">
                      {["Patient", "Contact", "Department", "Status", "Last Activity", "Next Appointment", "Total Spent", ""].map((col) => (
                        <th key={col || "actions"} className="whitespace-nowrap">
                          {col}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {loading && (
                      <tr><td colSpan={8} className="text-center py-12 text-text-secondary text-ui-base">Loading patients…</td></tr>
                    )}
                    {!loading && rows.map((p) => {
                      const DeptIcon = DEPT_ICONS[p.department] || Stethoscope;
                      const nextAppt = nextApptMap[p.id];
                      const isSelected = selectedPatient?.id === p.id;

                      return (
                        <tr
                          key={p.id}
                          data-testid={`patient-row-${p.id}`}
                          title="Click or double-click to open patient profile"
                          onClick={() => handleRowClick(p.id)}
                          onDoubleClick={(e) => handleRowDoubleClick(e, p.id)}
                          className={cn(
                            "border-b border-white/40 last:border-0 cursor-pointer transition-colors select-none",
                            isSelected ? "bg-primary-soft hover:bg-[#064E3B]/10" : "hover:bg-white/30",
                          )}
                        >
                          <td>
                            <div className="flex items-center gap-2 min-w-[160px]">
                              <div className={cn(
                                "w-7 h-7 rounded-full bg-gradient-to-br flex items-center justify-center text-white text-ui-label font-semibold shrink-0",
                                avatarGradient(p.name),
                              )}>
                                {getInitials(p.name)}
                              </div>
                              <div className="min-w-0">
                                <div className="flex items-center gap-1.5 flex-wrap">
                                  <span className="text-ui-base font-semibold text-[#022C22] truncate">{p.name}</span>
                                  {isVipPatient(p) && (
                                    <span className="status-pill font-bold uppercase bg-violet-100 text-violet-700 border-violet-200">
                                      VIP
                                    </span>
                                  )}
                                </div>
                                <div className="text-ui-label text-text-muted font-mono">{formatPatientId(p)}</div>
                              </div>
                            </div>
                          </td>
                          <td>
                            <div className="text-ui-base font-mono text-[#022C22]">{formatPhone(p.phone)}</div>
                            <div className="text-ui-label text-text-muted truncate max-w-[140px]">{p.email || "—"}</div>
                          </td>
                          <td>
                            <div className="flex items-center gap-1.5 text-ui-base text-[#022C22]">
                              <DeptIcon weight="regular" className="w-3.5 h-3.5 text-[#6366F1] shrink-0" />
                              <span className="truncate max-w-[120px]">{p.department || "—"}</span>
                            </div>
                          </td>
                          <td>
                            <StatusPill status={p.status} label={STATUS_LABELS[p.status]} />
                          </td>
                          <td>
                            <div className="text-ui-base text-[#022C22]">{relativeTime(p.last_contact_at || p.created_at)}</div>
                            <div className="text-ui-label text-text-muted">{formatSource(p.inquiry_source)}</div>
                          </td>
                          <td>
                            {nextAppt ? (
                              <>
                                <div className="text-ui-base text-[#022C22]">
                                  {formatHospitalDate(nextAppt.scheduled_at, hospitalTz, "MMM d, yyyy")}
                                </div>
                                <div className="text-ui-label text-text-muted">
                                  {formatHospitalTime12h(nextAppt.scheduled_at, hospitalTz)}
                                </div>
                              </>
                            ) : (
                              <span className="text-ui-base text-text-muted">—</span>
                            )}
                          </td>
                          <td className="text-ui-base font-medium text-[#022C22]">
                            {formatRupee(0)}
                          </td>
                          <td className="w-8">
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <button
                                  type="button"
                                  data-testid={`patient-actions-${p.id}`}
                                  onClick={(e) => e.stopPropagation()}
                                  className="h-9 w-9 rounded-xl flex items-center justify-center hover:bg-white/70 text-text-muted hover:text-[#022C22] transition-colors"
                                  aria-label="Patient actions"
                                >
                                  <DotsThreeVertical weight="bold" className="w-3.5 h-3.5" />
                                </button>
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end" className="rounded-xl glass-card border-white/60 text-ui-base">
                                <DropdownMenuItem onClick={(e) => { e.stopPropagation(); navigate(`/patients/${p.id}`); }}>
                                  <Eye className="w-3.5 h-3.5 mr-2" /> View details
                                </DropdownMenuItem>
                                <DropdownMenuItem onClick={(e) => { e.stopPropagation(); openPreview(p); }}>
                                  <Eye className="w-3.5 h-3.5 mr-2" /> Quick preview
                                </DropdownMenuItem>
                                <DropdownMenuItem onClick={(e) => { e.stopPropagation(); openEdit(p); }}>
                                  <PencilSimple className="w-3.5 h-3.5 mr-2" /> Edit
                                </DropdownMenuItem>
                              </DropdownMenuContent>
                            </DropdownMenu>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                )}
              </div>
              {rows.length > 0 && (
              <div className="shrink-0 border-t border-[#E5E7EB] px-2 sm:px-3">
                <PatientsPagination
                  page={page}
                  pageSize={pageSize}
                  total={total}
                  onPageChange={setPage}
                  onPageSizeChange={(s) => { setPageSize(s); setPage(1); }}
                />
              </div>
              )}
            </GlassCard>
          </div>

          {selectedPatient && (
            <PatientPreviewPanel
              patient={selectedPatient}
              onClose={closePreview}
              onEdit={openEdit}
              className="shrink-0 w-full lg:w-[320px] lg:max-w-[320px] h-full min-h-0"
            />
          )}
        </div>
      </PageContent>
    </AppShell>
  );
};

export default Patients;
