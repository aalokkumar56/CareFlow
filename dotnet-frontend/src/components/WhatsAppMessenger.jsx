import React, { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api } from "@/lib/api";
import { toast } from "sonner";
import { WhatsappLogo, File, Image as ImageIcon, Video, MusicNote } from "@phosphor-icons/react";

const MEDIA_TYPES = [
  { id: "image", label: "Image", icon: ImageIcon, accept: "image/*" },
  { id: "video", label: "Video", icon: Video, accept: "video/*" },
  { id: "document", label: "Document", icon: File, accept: ".pdf,.doc,.docx,.txt" },
  { id: "audio", label: "Audio", icon: MusicNote, accept: "audio/*" },
];

export const WhatsAppMessenger = ({ patients = [], onSent }) => {
  const [open, setOpen] = useState(false);
  const [selectedPatients, setSelectedPatients] = useState([]);
  const [loading, setLoading] = useState(false);
  const [templates, setTemplates] = useState([]);
  const [messageType, setMessageType] = useState("text");
  const [mediaFile, setMediaFile] = useState(null);
  const [mediaPreview, setMediaPreview] = useState("");
  const [formData, setFormData] = useState({
    text: "",
    templateName: "",
    templateLanguage: "en_US",
    mediaType: "image",
    mediaUrl: "",
    caption: "",
  });

  useEffect(() => {
    if (!open) return;

    const controller = new AbortController();
    api.get("/whatsapp/templates", { signal: controller.signal })
      .then((res) => {
        if (!controller.signal.aborted) {
          setTemplates(res.data?.templates || []);
        }
      })
      .catch((err) => {
        if (err?.code !== "ERR_CANCELED") {
          console.error("Failed to fetch templates:", err);
        }
      });

    return () => controller.abort();
  }, [open]);

  useEffect(() => {
    if (!mediaFile) {
      setMediaPreview("");
      return;
    }

    const objectUrl = URL.createObjectURL(mediaFile);
    setMediaPreview(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [mediaFile]);

  const handleSelectPatient = (patientId) => {
    setSelectedPatients((prev) =>
      prev.includes(patientId)
        ? prev.filter((id) => id !== patientId)
        : [...prev, patientId]
    );
  };

  const sendMessages = async () => {
    if (selectedPatients.length === 0) {
      toast.error("Please select at least one patient");
      return;
    }

    if (messageType === "text" && !formData.text.trim()) {
      toast.error("Please enter a message");
      return;
    }

    if (messageType === "template" && !formData.templateName) {
      toast.error("Please select a template");
      return;
    }

    if (messageType === "media" && !formData.mediaUrl && !mediaFile) {
      toast.error("Please upload a file or enter a media URL");
      return;
    }

    setLoading(true);
    const results = { success: 0, failed: 0 };

    try {
      for (const patientId of selectedPatients) {
        const patient = patients.find((p) => p.id === patientId);
        if (!patient) continue;

        try {
          let response;
          if (messageType === "text") {
            response = await api.post("/whatsapp/sendmessage", {
              phone: patient.phone,
              message: formData.text,
            });
          } else if (messageType === "template") {
            response = await api.post("/whatsapp/sendtemplatemessage", {
              phone: patient.phone,
              templateName: formData.templateName,
              templateLanguage: formData.templateLanguage,
              components: [],
            });
          } else if (messageType === "media") {
            if (mediaFile) {
              const form = new FormData();
              form.append("phone", patient.phone);
              form.append("mediaType", formData.mediaType);
              form.append("caption", formData.caption || "");
              form.append("mediaFile", mediaFile);
              response = await api.post("/whatsapp/sendmedia", form);
            } else {
              response = await api.post("/whatsapp/sendmedia", {
                phone: patient.phone,
                mediaType: formData.mediaType,
                mediaUrl: formData.mediaUrl,
                caption: formData.caption,
              });
            }
          }

          const success = response?.data?.Success ?? response?.data?.success ?? false;
          if (success) {
            results.success++;
          } else {
            results.failed++;
          }
        } catch (err) {
          results.failed++;
          console.error(`Failed to send to ${patient.phone}:`, err);
        }
      }

      toast.success(`Sent ${results.success} message(s). Failed: ${results.failed}`);

      if (results.success > 0) {
        setSelectedPatients([]);
        setMediaFile(null);
        setFormData({
          text: "",
          templateName: "",
          templateLanguage: "en_US",
          mediaType: "image",
          mediaUrl: "",
          caption: "",
        });
        setOpen(false);
        onSent?.();
      }
    } catch (err) {
      toast.error("Error sending messages");
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm" className="gap-2">
          <WhatsappLogo size={16} />
          Send WhatsApp
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Send WhatsApp Messages</DialogTitle>
        </DialogHeader>

        <div className="space-y-6">
          {/* Patient Selection */}
          <div>
            <label className="text-sm font-medium mb-3 block">
              Select Patients ({selectedPatients.length} selected)
            </label>
            <div className="border rounded-lg p-4 space-y-2 max-h-48 overflow-y-auto">
              {patients && patients.length > 0 ? (
                patients.map((patient, index) => (
                  <label
                    key={patient.id ?? `patient-${index}`}
                    className="flex items-center gap-3 p-2 hover:bg-gray-50 rounded cursor-pointer"
                  >
                    <input
                      type="checkbox"
                      checked={selectedPatients.includes(patient.id)}
                      onChange={() => handleSelectPatient(patient.id)}
                      className="w-4 h-4"
                    />
                    <div className="flex-1">
                      <p className="font-medium text-sm">{patient.name}</p>
                      <p className="text-xs text-gray-500">{patient.phone}</p>
                    </div>
                  </label>
                ))
              ) : (
                <p className="text-sm text-gray-500">No patients available</p>
              )}
            </div>
          </div>

          {/* Message Type Selection */}
          <div>
            <label className="text-sm font-medium mb-3 block">
              Message Type
            </label>
            <Tabs value={messageType} onValueChange={setMessageType}>
              <TabsList className="grid w-full grid-cols-3">
                <TabsTrigger value="text">Text</TabsTrigger>
                <TabsTrigger value="template">Template</TabsTrigger>
                <TabsTrigger value="media">Media</TabsTrigger>
              </TabsList>

              <TabsContent value="text" className="space-y-4">
                <Textarea
                  placeholder="Enter your message..."
                  value={formData.text}
                  onChange={(e) =>
                    setFormData({ ...formData, text: e.target.value })
                  }
                  className="min-h-24"
                />
              </TabsContent>

              <TabsContent value="template" className="space-y-4">
                <div>
                  <label className="text-sm font-medium block mb-2">
                    Template
                  </label>
                  <Select
                    value={formData.templateName}
                    onValueChange={(value) =>
                      setFormData({ ...formData, templateName: value })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select a template" />
                    </SelectTrigger>
                    <SelectContent>
                      {templates.map((t, index) => (
                        <SelectItem
                          key={t.id ? `wa-tpl-${t.id}` : `wa-tpl-${t.name}-${t.language}-${index}`}
                          value={t.name}
                        >
                          {t.name} ({t.language})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <label className="text-sm font-medium block mb-2">
                    Language
                  </label>
                  <Select
                    value={formData.templateLanguage}
                    onValueChange={(value) =>
                      setFormData({ ...formData, templateLanguage: value })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="en_US">English (US)</SelectItem>
                      <SelectItem value="hi_IN">Hindi (IN)</SelectItem>
                      <SelectItem value="es_ES">Spanish (ES)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </TabsContent>

              <TabsContent value="media" className="space-y-4">
                <div>
                  <label className="text-sm font-medium block mb-2">
                    Media Type
                  </label>
                  <Select
                    value={formData.mediaType}
                    onValueChange={(value) => {
                      setFormData({ ...formData, mediaType: value });
                      setMediaFile(null);
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {MEDIA_TYPES.map((type) => (
                        <SelectItem key={type.id} value={type.id}>
                          {type.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <label className="text-sm font-medium block mb-2">
                    Attach file or media URL
                  </label>
                  <div className="flex flex-col gap-2">
                    <label className="flex items-center gap-2 text-sm cursor-pointer text-[#064E3B]">
                      <input
                        type="file"
                        accept={MEDIA_TYPES.find((t) => t.id === formData.mediaType)?.accept}
                        className="hidden"
                        onChange={(e) => {
                          const file = e.target.files?.[0] || null;
                          setMediaFile(file);
                          setFormData({ ...formData, mediaUrl: file ? "" : formData.mediaUrl });
                        }}
                      />
                      <span className="underline">Choose file</span>
                      {mediaFile && <span className="text-text-secondary text-xs">{mediaFile.name}</span>}
                    </label>
                    <span className="text-[11px] text-text-muted">OR</span>
                    <Input
                      placeholder="https://example.com/image.jpg"
                      value={formData.mediaUrl}
                      onChange={(e) => {
                        setFormData({ ...formData, mediaUrl: e.target.value });
                        if (mediaFile) setMediaFile(null);
                      }}
                    />
                  </div>
                </div>
                {mediaPreview && (
                  <div className="rounded-sm border border-subtle overflow-hidden">
                    {formData.mediaType === "image" ? (
                      <img src={mediaPreview} alt="preview" className="w-full object-cover max-h-72" />
                    ) : formData.mediaType === "video" ? (
                      <video controls src={mediaPreview} className="w-full max-h-72" />
                    ) : formData.mediaType === "audio" ? (
                      <audio controls src={mediaPreview} className="w-full" />
                    ) : (
                      <div className="p-3 text-sm text-text-muted">Selected file: {mediaFile?.name}</div>
                    )}
                  </div>
                )}
                <div>
                  <label className="text-sm font-medium block mb-2">
                    Caption (Optional)
                  </label>
                  <Textarea
                    placeholder="Add a caption..."
                    value={formData.caption}
                    onChange={(e) =>
                      setFormData({ ...formData, caption: e.target.value })
                    }
                    className="min-h-20"
                  />
                </div>
              </TabsContent>
            </Tabs>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)}>
            Cancel
          </Button>
          <Button
            onClick={sendMessages}
            disabled={loading || selectedPatients.length === 0}
            className="gap-2"
          >
            {loading ? "Sending..." : "Send Messages"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default WhatsAppMessenger;
