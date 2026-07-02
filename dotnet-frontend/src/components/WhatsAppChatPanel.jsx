import React, { useEffect, useRef, useState } from "react";
import { api, formatPhone, normalizeApiError, fetchAuthorizedBlob, resolveProtectedMediaPath } from "@/lib/api";
import { renderMessageTemplate } from "@/lib/messageTemplates";
import { PaperPlaneRight, Paperclip, X, FileArrowDown, FileText } from "@phosphor-icons/react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";

const statusClasses = {
  pending: "bg-gray-200 text-gray-700",
  sent: "bg-blue-100 text-blue-700",
  delivered: "bg-sky-100 text-sky-700",
  read: "bg-emerald-100 text-emerald-700",
  failed: "bg-red-100 text-red-700",
  received: "bg-gray-100 text-gray-700",
};

const formatMessageStatus = (status) => {
  if (!status) return "";
  return status.replace(/_/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
};

const formatBytes = (bytes) => {
  const n = Number(bytes);
  if (!n || Number.isNaN(n)) return "";
  if (n >= 1024 * 1024) return `${(n / (1024 * 1024)).toFixed(1)} MB`;
  if (n >= 1024) return `${(n / 1024).toFixed(0)} KB`;
  return `${n} B`;
};

const ACCEPTED_TYPES =
  "image/*,video/*,audio/*,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv";

const AuthorizedMedia = ({ url, type, caption, fileName, size, outbound }) => {
  const [blobUrl, setBlobUrl] = useState(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    let objectUrl = null;
    const path = resolveProtectedMediaPath(url);
    if (!path) {
      // Already a directly usable URL (e.g. remote https media).
      setBlobUrl(url);
      return undefined;
    }

    setBlobUrl(null);
    setFailed(false);
    fetchAuthorizedBlob(path)
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setBlobUrl(objectUrl);
      })
      .catch(() => {
        if (active) setFailed(true);
      });

    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [url]);

  if (failed) return <span className="text-xs opacity-70">Attachment unavailable</span>;
  if (!blobUrl) return <span className="text-xs opacity-70">Loading attachment…</span>;

  if (type === "image") {
    return (
      <a href={blobUrl} target="_blank" rel="noreferrer" className="block">
        <img
          src={blobUrl}
          alt={caption || fileName || "image"}
          className="w-full max-h-64 object-cover rounded-sm cursor-pointer"
        />
      </a>
    );
  }

  if (type === "video") {
    return <video src={blobUrl} controls className="w-full max-h-64 rounded-sm bg-black" />;
  }

  if (type === "audio") {
    return <audio src={blobUrl} controls className="w-full" />;
  }

  // Document / unknown — download chip.
  return (
    <a
      href={blobUrl}
      download={fileName || "attachment"}
      target="_blank"
      rel="noreferrer"
      className={`flex items-center gap-2 rounded-sm px-2.5 py-2 text-[12px] ${
        outbound ? "bg-white/15 hover:bg-white/25" : "bg-black/5 hover:bg-black/10"
      }`}
    >
      <FileText weight="fill" className="w-5 h-5 shrink-0" />
      <span className="min-w-0">
        <span className="block truncate font-medium">{fileName || "Document"}</span>
        {size ? <span className="block text-[10px] opacity-70">{formatBytes(size)}</span> : null}
      </span>
      <FileArrowDown className="w-4 h-4 shrink-0 ml-auto" />
    </a>
  );
};

const WhatsAppChatPanel = ({
  conversationId,
  patientId,
  patientName,
  templates = [],
  showQuickTemplates = true,
  whatsappDisabled = false,
  disabledMessage,
  className = "",
}) => {
  const [conv, setConv] = useState(null);
  const [msgs, setMsgs] = useState([]);
  const [patient, setPatient] = useState(null);
  const [body, setBody] = useState("");
  const [sending, setSending] = useState(false);
  const [activeId, setActiveId] = useState(conversationId || null);
  const [attachment, setAttachment] = useState(null);
  const [attachPreview, setAttachPreview] = useState(null);
  const [uploadProgress, setUploadProgress] = useState(0);
  const scrollRef = useRef(null);
  const fileInputRef = useRef(null);
  const previewUrlRef = useRef(null);

  useEffect(() => {
    setActiveId(conversationId || null);
  }, [conversationId]);

  const reload = async (id) => {
    const r = await api.get(`/conversations/${id}`);
    setMsgs(r.data.messages || []);
    setConv(r.data.conversation);
  };

  useEffect(() => {
    const load = async (skipGlobalLoader = false) => {
      const config = skipGlobalLoader ? { skipGlobalLoader: true } : {};
      try {
        let r;
        if (activeId) {
          r = await api.get(`/conversations/${activeId}`, config);
        } else if (patientId) {
          r = await api.get(`/conversations/patient/${patientId}`, config);
          setActiveId(r.data.conversation?.id || null);
        } else {
          return;
        }
        setConv(r.data.conversation);
        setMsgs(r.data.messages || []);
        setPatient(r.data.patient || (patientName ? { name: patientName } : null));
      } catch {
        toast.error("Failed to load WhatsApp conversation");
      }
    };

    if (activeId || patientId) {
      load();
      const timer = setInterval(() => load(true), 5000);
      return () => clearInterval(timer);
    }
  }, [activeId, patientId, patientName]);

  useEffect(() => {
    if (scrollRef.current) scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  }, [msgs]);

  useEffect(() => () => {
    if (previewUrlRef.current) URL.revokeObjectURL(previewUrlRef.current);
  }, []);

  const clearAttachment = () => {
    if (previewUrlRef.current) {
      URL.revokeObjectURL(previewUrlRef.current);
      previewUrlRef.current = null;
    }
    setAttachment(null);
    setAttachPreview(null);
    setUploadProgress(0);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const onPickFile = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (previewUrlRef.current) URL.revokeObjectURL(previewUrlRef.current);

    let kind = "document";
    if (file.type.startsWith("image/")) kind = "image";
    else if (file.type.startsWith("video/")) kind = "video";
    else if (file.type.startsWith("audio/")) kind = "audio";

    let url = null;
    if (kind !== "document") {
      url = URL.createObjectURL(file);
      previewUrlRef.current = url;
    }
    setAttachment(file);
    setAttachPreview({ kind, url, name: file.name, size: file.size });
  };

  const sendMedia = async () => {
    if (!attachment || !activeId) return;
    setSending(true);
    setUploadProgress(0);
    try {
      const form = new FormData();
      form.append("file", attachment);
      if (body.trim()) form.append("caption", body.trim());
      await api.post(`/conversations/${activeId}/media`, form, {
        onUploadProgress: (evt) => {
          if (evt.total) setUploadProgress(Math.round((evt.loaded / evt.total) * 100));
        },
      });
      setBody("");
      clearAttachment();
      await reload(activeId);
      toast.success("Attachment sent");
    } catch (err) {
      toast.error(normalizeApiError(err, "Failed to send attachment"));
      // The attachment may have been stored but failed to deliver — refresh to reflect status.
      try {
        await reload(activeId);
      } catch {
        /* ignore */
      }
    } finally {
      setSending(false);
      setUploadProgress(0);
    }
  };

  const send = async (e) => {
    e?.preventDefault();
    if (whatsappDisabled || sending) return;
    if (attachment) {
      await sendMedia();
      return;
    }
    if (!body.trim() || !activeId) return;
    setSending(true);
    try {
      await api.post("/conversations/messages", { conversationId: activeId, body });
      setBody("");
      await reload(activeId);
      toast.success("Message sent");
    } catch (err) {
      toast.error(normalizeApiError(err, "Failed to send message"));
    } finally {
      setSending(false);
    }
  };

  const applyTemplate = (template) => {
    const rendered = renderMessageTemplate(template.body, {
      name: patient?.name || conv?.name || patientName || "",
      doctor: patient?.referral_doctor || "",
    });
    setBody(rendered);
  };

  const visibleTemplates = templates.filter((t) => {
    const cat = (t.category || "").toLowerCase();
    if (cat === "appointment_confirmation") return false;
    if (cat === "appointment") return false;
    return !/appointment confirmation/i.test(t.name || "");
  });

  if (!activeId && !patientId) {
    return (
      <div className={`flex items-center justify-center text-text-muted text-sm p-8 ${className}`}>
        No WhatsApp conversation available
      </div>
    );
  }

  if (!conv) {
    return (
      <div className={`flex items-center justify-center text-text-muted text-sm p-8 ${className}`}>
        Loading conversation...
      </div>
    );
  }

  const headerName = patient?.name || conv.display_name || conv.name || conv.wa_phone;
  const canSend = !whatsappDisabled && !sending && (attachment || body.trim());

  return (
    <div className={`flex flex-col border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl overflow-hidden ${className}`}>
      <div className="border-b border-subtle px-4 py-3">
        <div className="font-medium text-[14px] text-[#022C22]">{headerName}</div>
        <div className="text-[11px] text-text-secondary font-mono">{formatPhone(conv.wa_phone)}</div>
      </div>

      <div ref={scrollRef} className="flex-1 min-h-[320px] max-h-[480px] overflow-y-auto scrollbar-thin px-4 py-3 space-y-3">
        {msgs.length === 0 && (
          <div className="text-center text-text-muted text-sm py-8">No messages yet</div>
        )}
        {msgs.map((m) => {
          const mediaUrl = m.media_url || m.mediaUrl;
          const isOutbound = m.direction === "outbound";
          return (
            <div key={m.id} className={`flex ${isOutbound ? "justify-end" : "justify-start"}`}>
              <div className={`max-w-[75%] px-3.5 py-2 rounded-sm ${
                isOutbound
                  ? "bg-[#064E3B] text-white rounded-br-none"
                  : "bg-secondary border border-subtle rounded-bl-none"
              }`}>
                {mediaUrl ? (
                  <div className="mb-1.5">
                    <AuthorizedMedia
                      url={mediaUrl}
                      type={m.type}
                      caption={m.caption}
                      fileName={m.file_name || m.fileName}
                      size={m.media_size || m.mediaSize}
                      outbound={isOutbound}
                    />
                  </div>
                ) : null}
                {m.body ? <div className="text-[13px] whitespace-pre-wrap">{m.body}</div> : null}
                <div className={`mt-2 flex items-center justify-between gap-3 text-[10px] ${isOutbound ? "text-white/60" : "text-text-muted"}`}>
                  <span>{new Date(m.created_at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span>
                  {isOutbound && (
                    <span className={`status-pill ${statusClasses[(m.status || "sent").toLowerCase()]}`}>
                      {formatMessageStatus(m.status)}
                    </span>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      <form onSubmit={send} className="border-t border-subtle p-3">
        {whatsappDisabled && (
          <div className="mb-2 rounded-md bg-amber-50 border border-amber-200 px-2 py-1.5 text-[11px] text-amber-900">
            {disabledMessage || "WhatsApp is disabled or not configured."}
          </div>
        )}

        {attachment && (
          <div className="mb-2 flex items-center gap-3 rounded-sm border border-subtle bg-secondary px-2.5 py-2">
            {attachPreview?.kind === "image" && attachPreview.url ? (
              <img src={attachPreview.url} alt="preview" className="w-12 h-12 object-cover rounded-sm" />
            ) : attachPreview?.kind === "video" && attachPreview.url ? (
              <video src={attachPreview.url} className="w-12 h-12 object-cover rounded-sm bg-black" />
            ) : (
              <div className="w-12 h-12 rounded-sm bg-primary-soft flex items-center justify-center">
                <FileText weight="fill" className="w-6 h-6 text-[#064E3B]" />
              </div>
            )}
            <div className="min-w-0 flex-1">
              <div className="text-[12px] font-medium truncate">{attachPreview?.name}</div>
              <div className="text-[10px] text-text-muted">{formatBytes(attachPreview?.size)}</div>
              {sending && uploadProgress > 0 && (
                <div className="mt-1 h-1 w-full bg-black/10 rounded-full overflow-hidden">
                  <div className="h-full bg-[#16A34A] transition-all" style={{ width: `${uploadProgress}%` }} />
                </div>
              )}
            </div>
            <button
              type="button"
              onClick={clearAttachment}
              disabled={sending}
              className="text-text-muted hover:text-red-600 disabled:opacity-50"
              aria-label="Remove attachment"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {showQuickTemplates && !attachment && visibleTemplates.length > 0 && (
          <div className="flex gap-2 mb-2 flex-wrap">
            {visibleTemplates.slice(0, 3).map((t) => (
              <Button
                key={t.id}
                type="button"
                size="sm"
                variant="outline"
                onClick={() => applyTemplate(t)}
                className="rounded-sm text-[11px] h-7"
              >
                {t.name}
              </Button>
            ))}
          </div>
        )}

        <div className="flex gap-2 items-end">
          <input
            ref={fileInputRef}
            type="file"
            accept={ACCEPTED_TYPES}
            className="hidden"
            onChange={onPickFile}
          />
          <Button
            type="button"
            variant="outline"
            disabled={whatsappDisabled || sending}
            onClick={() => fileInputRef.current?.click()}
            className="rounded-sm h-10 px-3 self-end"
            aria-label="Attach file"
            title="Attach image, video, audio or document"
          >
            <Paperclip weight="regular" className="w-4 h-4" />
          </Button>
          <Textarea
            value={body}
            onChange={(e) => setBody(e.target.value)}
            placeholder={
              whatsappDisabled
                ? "WhatsApp sending is disabled"
                : attachment
                ? "Add a caption (optional)…"
                : "Type a WhatsApp message..."
            }
            rows={2}
            disabled={whatsappDisabled}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                send();
              }
            }}
            className="rounded-sm text-[13px] resize-none"
          />
          <Button
            type="submit"
            disabled={!canSend}
            className="rounded-sm bg-[#064E3B] hover:bg-[#022C22] self-end h-10"
            aria-label="Send message"
          >
            <PaperPlaneRight weight="fill" className="w-4 h-4" />
          </Button>
        </div>
      </form>
    </div>
  );
};

export default WhatsAppChatPanel;
