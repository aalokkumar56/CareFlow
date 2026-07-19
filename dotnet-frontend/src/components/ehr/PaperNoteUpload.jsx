import React, { useRef, useState } from "react";
import { api, normalizeApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "sonner";
import { Camera, Plus } from "@phosphor-icons/react";

/**
 * Upload hardcopy doctor notes / prescription scans for a patient (optional visit link).
 */
export default function PaperNoteUpload({
  patientId,
  visitId,
  onUploaded,
  compact = false,
  testIdPrefix = "paper-note",
}) {
  const fileRef = useRef(null);
  const [title, setTitle] = useState("");
  const [uploading, setUploading] = useState(false);

  const upload = async (file) => {
    if (!file || !patientId) return;
    setUploading(true);
    try {
      const form = new FormData();
      form.append("file", file);
      if (visitId) form.append("visitId", visitId);
      if (title.trim()) form.append("title", title.trim());
      form.append("documentType", "paper_note");
      await api.post(`/patients/${patientId}/documents`, form);
      toast.success("Paper note added");
      setTitle("");
      onUploaded?.();
    } catch (err) {
      toast.error(normalizeApiError(err, "Failed to upload paper note"));
    } finally {
      setUploading(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  return (
    <div
      className={compact
        ? "flex flex-wrap items-end gap-2"
        : "rounded-xl border border-[#E5E7EB] bg-white/50 p-3 space-y-2"}
      data-testid={`${testIdPrefix}-upload`}
    >
      {!compact && (
        <div className="flex items-center gap-1.5 text-[13px] font-semibold text-[#022C22]">
          <Camera className="w-4 h-4 text-[#064E3B]" />
          Scan paper note
        </div>
      )}
      <div className={`flex flex-wrap items-end gap-2 ${compact ? "" : ""}`}>
        <div className="min-w-[160px] flex-1">
          {!compact && <label className="text-[11px] text-text-muted">Label (optional)</label>}
          <Input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="e.g. Dr. paper prescription"
            className="h-9"
            data-testid={`${testIdPrefix}-title`}
          />
        </div>
        <input
          ref={fileRef}
          type="file"
          accept="image/*,application/pdf,.pdf,.jpg,.jpeg,.png"
          capture="environment"
          className="hidden"
          data-testid={`${testIdPrefix}-input`}
          onChange={(e) => upload(e.target.files?.[0])}
        />
        <Button
          type="button"
          size="sm"
          className="h-9 bg-[#064E3B] hover:bg-[#022C22] text-white"
          disabled={uploading || !patientId}
          data-testid={`${testIdPrefix}-add-btn`}
          onClick={() => fileRef.current?.click()}
        >
          <Plus className="w-4 h-4 mr-1" />
          {uploading ? "Uploading…" : "Add scanned note"}
        </Button>
      </div>
      {!compact && (
        <p className="text-[11px] text-text-muted">
          Photo or PDF of hardcopy notes / prescription (max 10 MB). Appears on Visit chart for later review.
        </p>
      )}
    </div>
  );
}
