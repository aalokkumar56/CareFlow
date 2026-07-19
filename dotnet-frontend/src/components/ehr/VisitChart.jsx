import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  api,
  downloadAuthorizedFile,
  fetchAuthorizedBlob,
  normalizeApiError,
  NOTE_TYPE_LABELS,
  openAuthorizedHtml,
} from "@/lib/api";
import {
  formatHospitalDate,
  hospitalDateStrFromIso,
} from "@/lib/tenantTime";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription,
} from "@/components/ui/dialog";
import { toast } from "sonner";
import {
  CalendarBlank, Camera, DownloadSimple, FilePdf, Heartbeat, MagnifyingGlassPlus, Note, Pill,
} from "@phosphor-icons/react";

function parseLocalDate(yyyyMmDd) {
  if (!yyyyMmDd) return undefined;
  const [y, m, d] = yyyyMmDd.split("-").map(Number);
  if (!y || !m || !d) return undefined;
  return new Date(y, m - 1, d);
}

/** Calendar day key from a DayPicker Date (local Y/M/D of the clicked cell). */
function toYyyyMmDd(date) {
  if (!(date instanceof Date) || Number.isNaN(date.getTime())) return "";
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function cardAnchorId(card) {
  if (card.visit_id) return `visit-card-${card.visit_id}`;
  return `visit-card-day-${card._hospitalDate || card.visit_date_key || hospitalDateStrFromIso(card.visit_date)}`;
}

/** Nearest ancestor that can actually scroll vertically (AppShell main content). */
function findScrollParent(el) {
  let node = el.parentElement;
  while (node && node !== document.body) {
    const style = window.getComputedStyle(node);
    const oy = style.overflowY;
    const canScroll = (oy === "auto" || oy === "scroll" || oy === "overlay")
      && node.scrollHeight > node.clientHeight + 1;
    if (canScroll) return node;
    node = node.parentElement;
  }
  return document.scrollingElement || document.documentElement;
}

/** Scroll a visit card into view inside AppShell's overflow container. */
function scrollVisitCardIntoView(anchorId) {
  const el = document.getElementById(anchorId)
    || document.querySelector(`[data-visit-anchor="${anchorId}"]`);
  if (!el) return false;

  const scroller = findScrollParent(el);
  if (scroller && scroller !== document.documentElement && scroller !== document.body) {
    const elRect = el.getBoundingClientRect();
    const scrollerRect = scroller.getBoundingClientRect();
    const nextTop = scroller.scrollTop + (elRect.top - scrollerRect.top) - 16;
    // Instant scroll — smooth + popover focus restore races and jumps back to top
    scroller.scrollTo({ top: Math.max(0, nextTop), behavior: "auto" });
  } else {
    el.scrollIntoView({ behavior: "auto", block: "start" });
  }
  return true;
}

function noteText(note) {
  const parts = [note.subjective, note.objective, note.assessment, note.plan]
    .filter((p) => p && String(p).trim());
  return parts.join("\n\n") || "—";
}

function VitalStrip({ vitals }) {
  if (!vitals?.length) {
    return <p className="text-[13px] text-text-muted">No vitals recorded</p>;
  }
  const v = vitals[0];
  const cells = [
    {
      label: "BP",
      value: v.systolic_bp != null && v.diastolic_bp != null ? `${v.systolic_bp}/${v.diastolic_bp}` : null,
      unit: "mmHg",
    },
    { label: "Pulse", value: v.heart_rate ?? null, unit: "bpm" },
    { label: "Temp", value: v.temperature != null ? Number(v.temperature).toFixed(1) : null, unit: "°F" },
    { label: "SpO2", value: v.oxygen_saturation ?? null, unit: "%" },
  ];
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-px bg-[#E5E7EB] rounded-lg overflow-hidden border border-[#E5E7EB]">
      {cells.map((c) => (
        <div key={c.label} className="bg-white px-3 py-2.5 text-center">
          <div className="text-[11px] uppercase tracking-wide text-text-muted">{c.label}</div>
          <div className="text-lg font-semibold text-[#064E3B] leading-tight">
            {c.value != null ? c.value : "—"}
          </div>
          <div className="text-[11px] text-text-muted">{c.unit}</div>
        </div>
      ))}
    </div>
  );
}

function PrescriptionBlock({ prescriptions, onPrint }) {
  if (!prescriptions?.length) {
    return <p className="text-[13px] text-text-muted">No prescription</p>;
  }
  return (
    <div className="space-y-3">
      {prescriptions.map((rx) => (
        <div key={rx.id} className="rounded-lg border border-[#E5E7EB] overflow-hidden">
          <div className="flex items-center justify-between gap-2 px-3 py-2 bg-[#F8FAF9]">
            <div className="text-[12px] text-text-secondary">
              {rx.doctor_name || "Doctor"}
              {rx.diagnosis ? ` · ${rx.diagnosis}` : ""}
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 text-[12px] text-[#064E3B]"
              onClick={() => onPrint(rx.id)}
            >
              Print
            </Button>
          </div>
          {(rx.items?.length || 0) === 0 ? (
            <p className="px-3 py-2 text-[13px] text-text-muted">No medicines listed</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-[13px]">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-text-muted border-b border-[#E5E7EB]">
                    <th className="px-3 py-2 font-medium">Medication</th>
                    <th className="px-3 py-2 font-medium">Dose</th>
                    <th className="px-3 py-2 font-medium">Frequency</th>
                    <th className="px-3 py-2 font-medium">Duration</th>
                    <th className="px-3 py-2 font-medium">Instructions</th>
                  </tr>
                </thead>
                <tbody>
                  {rx.items.map((item) => (
                    <tr key={item.id} className="border-b border-[#F3F4F6] last:border-0">
                      <td className="px-3 py-2 font-medium text-[#022C22]">{item.drug_name}</td>
                      <td className="px-3 py-2 text-text-secondary">{item.dosage || item.strength || "—"}</td>
                      <td className="px-3 py-2 text-text-secondary">{item.frequency || "—"}</td>
                      <td className="px-3 py-2 text-text-secondary">{item.duration || "—"}</td>
                      <td className="px-3 py-2 text-text-secondary">{item.patient_instructions || item.timing || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function PaperNoteThumb({ doc, onView }) {
  const [url, setUrl] = useState(null);
  const isImage = (doc.content_type || "").startsWith("image/");

  useEffect(() => {
    let revoked = false;
    let objectUrl = null;
    if (!isImage) return undefined;
    fetchAuthorizedBlob(`/patient-documents/${doc.id}/download`)
      .then((blob) => {
        if (revoked) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => {});
    return () => {
      revoked = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [doc.id, isImage]);

  return (
    <button
      type="button"
      onClick={() => onView?.(doc)}
      className="rounded-lg border border-[#E5E7EB] bg-white p-2 w-[140px] shrink-0 text-left hover:border-[#064E3B]/50 hover:shadow-sm transition-shadow"
      data-testid={`paper-note-thumb-${doc.id}`}
    >
      <div className="aspect-[3/4] rounded-md bg-[#F3F4F6] overflow-hidden flex items-center justify-center mb-2 relative">
        {isImage && url ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={url} alt={doc.title || "Paper note"} className="w-full h-full object-cover" />
        ) : (
          <FilePdf className="w-8 h-8 text-[#064E3B]" weight="duotone" />
        )}
        <span className="absolute bottom-1 right-1 rounded bg-[#022C22]/70 text-white p-1">
          <MagnifyingGlassPlus className="w-3.5 h-3.5" />
        </span>
      </div>
      <div className="text-[12px] font-medium text-[#022C22] truncate" title={doc.title}>{doc.title || "Paper note"}</div>
      <div className="text-[11px] text-[#064E3B] mt-0.5">View full screen</div>
    </button>
  );
}

function PaperNoteViewer({ doc, open, onOpenChange }) {
  const [url, setUrl] = useState(null);
  const [loading, setLoading] = useState(false);
  const isImage = doc ? (doc.content_type || "").startsWith("image/") : false;
  const isPdf = doc ? (doc.content_type || "").includes("pdf") || (doc.original_file_name || "").toLowerCase().endsWith(".pdf") : false;

  useEffect(() => {
    let revoked = false;
    let objectUrl = null;
    if (!open || !doc) {
      setUrl(null);
      return undefined;
    }
    setLoading(true);
    fetchAuthorizedBlob(`/patient-documents/${doc.id}/download`)
      .then((blob) => {
        if (revoked) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch((err) => toast.error(normalizeApiError(err, "Could not open paper note")))
      .finally(() => {
        if (!revoked) setLoading(false);
      });
    return () => {
      revoked = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [open, doc]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-[min(96vw,1100px)] w-full h-[min(92vh,900px)] p-0 gap-0 overflow-hidden flex flex-col"
        data-testid="paper-note-viewer"
      >
        <DialogHeader className="px-4 py-3 border-b border-[#E5E7EB] shrink-0 pr-12">
          <DialogTitle className="truncate">{doc?.title || "Paper note"}</DialogTitle>
          <DialogDescription className="truncate">
            {doc?.original_file_name || "Hardcopy scan"} — read here without downloading
          </DialogDescription>
        </DialogHeader>
        <div className="flex-1 min-h-0 bg-[#111827] flex items-center justify-center overflow-auto">
          {loading && <p className="text-white/80 text-sm">Loading…</p>}
          {!loading && url && isImage && (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={url}
              alt={doc?.title || "Paper note"}
              className="max-w-full max-h-full object-contain"
              data-testid="paper-note-viewer-image"
            />
          )}
          {!loading && url && isPdf && (
            <iframe
              title={doc?.title || "Paper note PDF"}
              src={url}
              className="w-full h-full border-0 bg-white"
              data-testid="paper-note-viewer-pdf"
            />
          )}
          {!loading && url && !isImage && !isPdf && (
            <p className="text-white/80 text-sm px-4 text-center">
              Preview not available for this file type. Use Download instead.
            </p>
          )}
        </div>
        <div className="px-4 py-3 border-t border-[#E5E7EB] flex justify-end gap-2 shrink-0 bg-white">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={!doc}
            onClick={() => doc && downloadAuthorizedFile(
              `/patient-documents/${doc.id}/download`,
              doc.original_file_name || `${doc.title || "paper-note"}.pdf`,
            )}
          >
            <DownloadSimple className="w-4 h-4 mr-1" /> Download
          </Button>
          <Button
            type="button"
            size="sm"
            className="bg-[#064E3B] hover:bg-[#022C22]"
            data-testid="paper-note-viewer-close"
            onClick={() => onOpenChange(false)}
          >
            Close
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function VisitCard({ card, hospitalTz, highlighted, onViewPaper }) {
  const dateLabel = formatHospitalDate(card.visit_date, hospitalTz, "MMM d, yyyy");
  const complaint = card.chief_complaint || card.diagnosis || "Visit";

  return (
    <article
      id={cardAnchorId(card)}
      data-visit-anchor={cardAnchorId(card)}
      data-visit-date={card._hospitalDate}
      data-testid={`visit-card-${card._hospitalDate}`}
      className={`visit-card scroll-mt-4 rounded-xl border bg-white/80 backdrop-blur-sm p-4 sm:p-5 space-y-4 transition-shadow ${
        highlighted ? "border-[#064E3B] shadow-[0_0_0_2px_rgba(6,78,59,0.25)]" : "border-white/60"
      }`}
    >
      <header>
        <h3 className="font-heading font-semibold text-[#022C22] text-base flex flex-wrap items-center gap-2">
          <CalendarBlank className="w-4 h-4 text-[#064E3B]" weight="duotone" />
          {dateLabel}
          <span className="text-text-muted font-normal">·</span>
          <span className="font-normal text-text-secondary">{complaint}</span>
          {card.doctor_name ? (
            <>
              <span className="text-text-muted font-normal">·</span>
              <span className="font-normal text-text-secondary">{card.doctor_name}</span>
            </>
          ) : null}
        </h3>
      </header>

      <section className="space-y-2" aria-label="Vitals">
        <h4 className="text-[13px] font-semibold text-[#022C22] flex items-center gap-1.5">
          <Heartbeat className="w-4 h-4 text-[#064E3B]" /> 1) Vitals
        </h4>
        <VitalStrip vitals={card.vitals} />
      </section>

      <section className="space-y-2" aria-label="Clinical notes">
        <h4 className="text-[13px] font-semibold text-[#022C22] flex items-center gap-1.5">
          <Note className="w-4 h-4 text-[#064E3B]" /> 2) Clinical notes
        </h4>
        {(card.notes?.length || 0) === 0 && !card.doctor_notes ? (
          <p className="text-[13px] text-text-muted">No clinical notes</p>
        ) : (
          <div className="space-y-2">
            {card.doctor_notes ? (
              <div className="rounded-lg border border-[#E5E7EB] px-3 py-2 text-[13px] text-text-secondary whitespace-pre-wrap">
                {card.doctor_notes}
              </div>
            ) : null}
            {(card.notes || []).map((n) => (
              <div key={n.id} className="rounded-lg border border-[#E5E7EB] px-3 py-2">
                <div className="text-[11px] uppercase tracking-wide text-text-muted mb-1">
                  {NOTE_TYPE_LABELS[n.note_type] || n.note_type || "Note"}
                  {n.author_name ? ` · ${n.author_name}` : ""}
                </div>
                <p className="text-[13px] text-text-secondary whitespace-pre-wrap">{noteText(n)}</p>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className="space-y-2" aria-label="Prescription">
        <h4 className="text-[13px] font-semibold text-[#022C22] flex items-center gap-1.5">
          <Pill className="w-4 h-4 text-[#064E3B]" /> 3) Prescription
        </h4>
        <PrescriptionBlock
          prescriptions={card.prescriptions}
          onPrint={(id) => openAuthorizedHtml(`/prescriptions/${id}/print`).catch((e) => toast.error(normalizeApiError(e)))}
        />
      </section>

      <section className="space-y-2" aria-label="Paper note photo">
        <h4 className="text-[13px] font-semibold text-[#022C22] flex items-center gap-1.5">
          <Camera className="w-4 h-4 text-[#064E3B]" /> 4) Paper note · photo
        </h4>
        <div className="flex flex-wrap gap-3">
          {(card.paper_notes || []).map((doc) => (
            <PaperNoteThumb key={doc.id} doc={doc} onView={onViewPaper} />
          ))}
          {(card.paper_notes?.length || 0) === 0 ? (
            <p className="text-[13px] text-text-muted self-center">
              No paper notes yet — upload from Prescriptions or Today&apos;s consultation
            </p>
          ) : null}
        </div>
      </section>
    </article>
  );
}

const VisitChart = ({ patientId, hospitalTz, enabled }) => {
  const [cards, setCards] = useState([]);
  const [loading, setLoading] = useState(false);
  const [jumpOpen, setJumpOpen] = useState(false);
  const [highlightId, setHighlightId] = useState(null);
  const [viewerDoc, setViewerDoc] = useState(null);
  const feedRef = useRef(null);

  const load = useCallback(async () => {
    if (!patientId) return;
    setLoading(true);
    try {
      const r = await api.get(`/patients/${patientId}/visit-chart`);
      const list = Array.isArray(r.data) ? r.data : [];
      setCards(list.map((c) => {
        const hospitalDate = hospitalDateStrFromIso(c.visit_date, hospitalTz) || c.visit_date_key;
        return { ...c, _hospitalDate: hospitalDate };
      }));
    } catch (err) {
      toast.error(normalizeApiError(err));
      setCards([]);
    } finally {
      setLoading(false);
    }
  }, [patientId, hospitalTz]);

  useEffect(() => {
    if (!enabled) return;
    load();
  }, [enabled, load]);

  const visitDates = useMemo(() => {
    const set = new Set();
    cards.forEach((c) => {
      if (c._hospitalDate) set.add(c._hospitalDate);
    });
    return set;
  }, [cards]);

  const visitDayMatchers = useMemo(
    () => [...visitDates].map((key) => parseLocalDate(key)).filter(Boolean),
    [visitDates],
  );

  const disabledDays = useCallback(
    (date) => !visitDates.has(toYyyyMmDd(date)),
    [visitDates],
  );

  const jumpToDate = (date) => {
    const key = toYyyyMmDd(date);
    if (!key || !visitDates.has(key)) return;

    const target = cards.find((c) => c._hospitalDate === key);
    if (!target) return;
    const id = cardAnchorId(target);

    // Close popover first so layout settles, then scroll the feed.
    setJumpOpen(false);
    window.requestAnimationFrame(() => {
      window.setTimeout(() => {
        const ok = scrollVisitCardIntoView(id);
        if (!ok) {
          toast.error("Could not find that visit on the chart");
          return;
        }
        setHighlightId(id);
        window.setTimeout(() => setHighlightId((cur) => (cur === id ? null : cur)), 2000);
      }, 120);
    });
  };

  const defaultMonth = useMemo(() => {
    const first = cards[0]?._hospitalDate;
    return parseLocalDate(first) || new Date();
  }, [cards]);

  if (!enabled) return null;

  return (
    <div className="space-y-3" data-testid="visit-chart">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="font-heading font-semibold text-[#022C22] text-lg">Visit chart</h2>
          <p className="text-[12px] text-text-muted">
            Read-only history — scroll or jump to a date. Upload paper notes from Prescriptions or Today&apos;s consultation.
          </p>
        </div>
        <Popover open={jumpOpen} onOpenChange={setJumpOpen}>
          <PopoverTrigger asChild>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-9 border-[#064E3B]/40 text-[#064E3B]"
              data-testid="visit-chart-jump-date"
              disabled={visitDates.size === 0}
            >
              <CalendarBlank className="w-4 h-4 mr-1.5" />
              Jump to date
            </Button>
          </PopoverTrigger>
          <PopoverContent
            className="w-auto p-0"
            align="end"
            onOpenAutoFocus={(e) => e.preventDefault()}
            onCloseAutoFocus={(e) => e.preventDefault()}
          >
            <Calendar
              mode="single"
              defaultMonth={defaultMonth}
              disabled={disabledDays}
              modifiers={{ visit: visitDayMatchers }}
              modifiersClassNames={{
                visit: "bg-[#064E3B]/15 text-[#064E3B] font-semibold",
              }}
              onDayClick={(d, modifiers) => {
                if (!d || modifiers?.disabled) return;
                jumpToDate(d);
              }}
              className="rounded-md"
            />
            <p className="px-3 pb-3 text-[11px] text-text-muted">
              Highlighted dates have visits — click one to jump
              {visitDates.size > 0
                ? ` (${visitDates.size} day${visitDates.size === 1 ? "" : "s"})`
                : ""}
            </p>
          </PopoverContent>
        </Popover>
      </div>

      {loading ? (
        <div className="text-[13px] text-text-muted py-10 text-center">Loading visit chart…</div>
      ) : cards.length === 0 ? (
        <div className="rounded-xl border border-dashed border-[#D1D5DB] bg-white/60 px-4 py-10 text-center text-[13px] text-text-muted">
          No visits yet. After a consultation is recorded, it will appear here as a scrollable chart.
        </div>
      ) : (
        <main ref={feedRef} className="visit-feed space-y-4" data-testid="visit-chart-feed">
          {cards.map((card) => (
            <VisitCard
              key={cardAnchorId(card)}
              card={card}
              hospitalTz={hospitalTz}
              highlighted={highlightId === cardAnchorId(card)}
              onViewPaper={setViewerDoc}
            />
          ))}
        </main>
      )}

      <PaperNoteViewer
        doc={viewerDoc}
        open={Boolean(viewerDoc)}
        onOpenChange={(v) => { if (!v) setViewerDoc(null); }}
      />
    </div>
  );
};

export default VisitChart;
