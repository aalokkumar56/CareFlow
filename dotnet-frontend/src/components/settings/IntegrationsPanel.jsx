import React, { useEffect, useState } from "react";
import GlassCard from "@/components/glass/GlassCard";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { api, BACKEND_URL } from "@/lib/api";
import {
  WhatsappLogo, ChatCircle, EnvelopeSimple, CheckCircle, WarningCircle, Gear,
} from "@phosphor-icons/react";
import { toast } from "sonner";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const DEFAULT_WHATSBIZ_URL = "https://whatsbizapi.com/api/wpbox/";

const IntegrationsPanel = ({ fillHeight = false }) => {
  const { can } = usePermissions();
  const canEdit = can(PERMISSIONS.SettingsEdit);
  const [waSettings, setWaSettings] = useState({});
  const [smsSettings, setSmsSettings] = useState({});
  const [emailSettings, setEmailSettings] = useState({});
  const [waOpen, setWaOpen] = useState(false);
  const [smsOpen, setSmsOpen] = useState(false);
  const [emailOpen, setEmailOpen] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get("/settings/whatsapp").then((r) => r.data || {}).catch(() => ({})),
      api.get("/settings/sms").then((r) => r.data || {}).catch(() => ({})),
      api.get("/settings/email").then((r) => r.data || {}).catch(() => ({})),
    ]).then(([wa, sms, email]) => {
      setWaSettings(wa);
      setSmsSettings(sms);
      setEmailSettings(email);
    }).finally(() => setLoading(false));
  }, []);

  const waProvider = waSettings.provider || "MetaCloud";
  const waConfigured = waProvider === "WhatsBiz"
    ? !!(waSettings.has_api_token || waSettings.api_token)
    : !!(waSettings.phone_number_id && (waSettings.has_access_token || waSettings.access_token));
  const waConnected = !!(waSettings.enabled && waConfigured);

  const smsConfigured = !!(smsSettings.gateway_url && (smsSettings.has_api_key || smsSettings.api_key));
  const smsConnected = !!(smsSettings.enabled && smsConfigured);

  const emailConfigured = !!(emailSettings.smtp_host && emailSettings.from_email);
  const emailConnected = !!(emailSettings.enabled && emailConfigured);

  const saveWa = async () => {
    try {
      await api.post("/settings/whatsapp", {
        provider: waSettings.provider || "MetaCloud",
        phoneNumberId: waSettings.phone_number_id || null,
        wabaId: waSettings.waba_id || null,
        accessToken: shouldSendSecret(waSettings.access_token) ? waSettings.access_token : null,
        apiToken: shouldSendSecret(waSettings.api_token) ? waSettings.api_token : null,
        whatsBizBaseUrl: waSettings.whats_biz_base_url || DEFAULT_WHATSBIZ_URL,
        verifyToken: waSettings.verify_token || null,
        appSecret: shouldSendSecret(waSettings.app_secret) ? waSettings.app_secret : null,
        businessName: waSettings.business_name || "Cure & Care Hospital",
        enabled: waSettings.enabled || false,
      });
      toast.success("WhatsApp settings saved");
      const r = await api.get("/settings/whatsapp");
      setWaSettings(r.data);
      setWaOpen(false);
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed to save");
    }
  };

  const saveSms = async () => {
    try {
      await api.post("/settings/sms", {
        gatewayUrl: smsSettings.gateway_url || null,
        apiKey: shouldSendSecret(smsSettings.api_key) ? smsSettings.api_key : null,
        senderId: smsSettings.sender_id || null,
        enabled: smsSettings.enabled || false,
      });
      toast.success("SMS settings saved");
      const r = await api.get("/settings/sms");
      setSmsSettings(r.data);
      setSmsOpen(false);
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed to save SMS settings");
    }
  };

  const saveEmail = async () => {
    try {
      await api.post("/settings/email", {
        smtpHost: emailSettings.smtp_host || null,
        smtpPort: emailSettings.smtp_port ? parseInt(emailSettings.smtp_port, 10) : 587,
        smtpUsername: emailSettings.smtp_username || null,
        smtpPassword: shouldSendSecret(emailSettings.smtp_password) ? emailSettings.smtp_password : null,
        useSsl: emailSettings.use_ssl !== false,
        fromEmail: emailSettings.from_email || null,
        fromName: emailSettings.from_name || null,
        enabled: emailSettings.enabled || false,
        sendWithWhatsApp: emailSettings.send_with_whatsapp !== false,
      });
      toast.success("Email settings saved");
      const r = await api.get("/settings/email");
      setEmailSettings(r.data);
      setEmailOpen(false);
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed to save email settings");
    }
  };

  if (loading) {
    return (
      <GlassCard className={cn("text-center py-6 text-ui-sm text-text-muted", fillHeight && "flex-1 min-h-0 flex items-center justify-center")}>
        Loading integrations…
      </GlassCard>
    );
  }

  return (
    <>
      <GlassCard
        padding={false}
        className={cn("overflow-hidden shrink-0", fillHeight && "flex-1 min-h-0 flex flex-col")}
        data-testid="integrations-panel"
      >
        <div className="px-3 py-2 border-b border-white/45 shrink-0">
          <h2 className="font-heading text-ui-base font-semibold text-[#022C22]">Integrations</h2>
          <p className="text-ui-caption text-text-secondary">Configure WhatsApp, SMS, and email from this screen.</p>
        </div>

        <div className={cn("p-2 flex flex-col gap-2", fillHeight && "flex-1 min-h-0 overflow-y-auto scrollbar-thin")}>
          <IntegrationRow
            connected={waConnected}
            icon={<WhatsappLogo weight="fill" className="w-4 h-4 text-[#25D366]" />}
            iconBg="bg-[#25D366]/15"
            title="WhatsApp Business"
            description="Source of truth for WhatsApp — credentials stored here, not in appsettings."
            connectedClass="bg-emerald-50/60 border-emerald-200/60"
            extra={waConfigured && (
              <div className="mt-1 space-y-0.5 text-[10px] text-text-secondary">
                <div>Provider: <span className="font-mono">{waProvider}</span></div>
                {waSettings.phone_number_id && <div>Phone ID: <span className="font-mono">{waSettings.phone_number_id}</span></div>}
              </div>
            )}
            statusIcon={waConnected
              ? <CheckCircle weight="fill" className="w-3.5 h-3.5 text-emerald-600" />
              : <WarningCircle weight="fill" className="w-3.5 h-3.5 text-amber-500" />}
            statusLabel={waConnected ? "Active" : waSettings.enabled ? "Configured but inactive" : "Not connected"}
            action={(
              <Button variant="outline" size="sm" className="rounded-lg h-7 text-ui-caption border-[#4338CA]/30 text-[#4338CA] px-2" onClick={() => setWaOpen(true)} disabled={!canEdit}>
                <Gear weight="regular" className="w-3 h-3 mr-0.5" />
                Configure
              </Button>
            )}
          />

          <IntegrationRow
            connected={smsConnected}
            icon={<ChatCircle weight="duotone" className="w-4 h-4 text-sky-600" />}
            iconBg="bg-sky-50"
            title="SMS Gateway"
            description="Transactional SMS — active only when enabled and credentials are set."
            statusIcon={smsConnected
              ? <CheckCircle weight="fill" className="w-3.5 h-3.5 text-emerald-600" />
              : <WarningCircle weight="fill" className="w-3.5 h-3.5 text-amber-500" />}
            statusLabel={smsConnected ? "Active" : smsSettings.enabled ? "Incomplete config" : "Not connected"}
            action={(
              <Button variant="outline" size="sm" className="rounded-lg h-7 text-ui-caption border-[#4338CA]/30 text-[#4338CA] px-2" onClick={() => setSmsOpen(true)} disabled={!canEdit}>
                <Gear weight="regular" className="w-3 h-3 mr-0.5" />
                Configure
              </Button>
            )}
          />

          <IntegrationRow
            connected={emailConnected}
            icon={<EnvelopeSimple weight="duotone" className="w-4 h-4 text-violet-600" />}
            iconBg="bg-violet-50"
            title="Email (SMTP)"
            description="Email inbox and appointment notifications alongside WhatsApp when enabled."
            statusIcon={emailConnected
              ? <CheckCircle weight="fill" className="w-3.5 h-3.5 text-emerald-600" />
              : <WarningCircle weight="fill" className="w-3.5 h-3.5 text-amber-500" />}
            statusLabel={emailConnected ? "Active" : emailSettings.enabled ? "Incomplete config" : "Not connected"}
            action={(
              <Button variant="outline" size="sm" className="rounded-lg h-7 text-ui-caption border-[#4338CA]/30 text-[#4338CA] px-2" onClick={() => setEmailOpen(true)} disabled={!canEdit}>
                <Gear weight="regular" className="w-3 h-3 mr-0.5" />
                Configure
              </Button>
            )}
          />
        </div>
      </GlassCard>

      {/* WhatsApp dialog */}
      <Dialog open={waOpen} onOpenChange={setWaOpen}>
        <DialogContent className="rounded-2xl glass-card max-w-lg max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="font-heading flex items-center gap-2">
              <WhatsappLogo weight="fill" className="w-4 h-4 text-[#25D366]" />
              WhatsApp Business
            </DialogTitle>
          </DialogHeader>
          <p className="text-ui-caption text-text-secondary -mt-2">
            Webhook URL (must be public HTTPS — use ngrok for local dev):
            <code className="block font-mono bg-white/60 px-2 py-1 rounded text-[10px] mt-1 break-all">{BACKEND_URL}/api/whatsapp/webhook</code>
          </p>
          {waProvider === "WhatsBiz" && (
            <div className="rounded-xl border border-amber-200/70 bg-amber-50/50 px-3 py-2 text-[11px] text-amber-900 space-y-1.5">
              <p className="font-semibold">WhatsBiz Webhook Relay setup</p>
              <ol className="list-decimal list-inside space-y-0.5 text-amber-800">
                <li>In WhatsBiz dashboard → Webhook Relay, paste the URL above (HTTPS only).</li>
                <li>Set the same Verify Token here and in WhatsBiz if prompted.</li>
                <li>Enable WhatsApp and save — inbound messages arrive via webhook only.</li>
                <li>Localhost cannot receive webhooks; run <code className="font-mono bg-white/60 px-0.5 rounded">ngrok http 5180</code> and use the ngrok URL.</li>
              </ol>
            </div>
          )}
          <FormField label="Provider">
            <Select value={waProvider} onValueChange={(v) => setWaSettings({ ...waSettings, provider: v })}>
              <SelectTrigger className="rounded-lg h-8"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="MetaCloud">Meta Cloud API</SelectItem>
                <SelectItem value="WhatsBiz">WhatsBiz / WPBox</SelectItem>
              </SelectContent>
            </Select>
          </FormField>
          {waProvider === "MetaCloud" ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
              <Field label="Phone Number ID" value={waSettings.phone_number_id} onChange={(v) => setWaSettings({ ...waSettings, phone_number_id: v })} />
              <Field label="WABA ID" value={waSettings.waba_id} onChange={(v) => setWaSettings({ ...waSettings, waba_id: v })} />
              <Field label="Access Token" type="password" value={waSettings.access_token} onChange={(v) => setWaSettings({ ...waSettings, access_token: v })} placeholder={waSettings.has_access_token ? "Leave blank to keep existing" : ""} />
              <Field label="Verify Token" value={waSettings.verify_token} onChange={(v) => setWaSettings({ ...waSettings, verify_token: v })} />
              <Field label="App Secret" type="password" value={waSettings.app_secret} onChange={(v) => setWaSettings({ ...waSettings, app_secret: v })} placeholder={waSettings.has_app_secret ? "Leave blank to keep existing" : ""} />
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-2">
              <Field label="API Token" type="password" value={waSettings.api_token} onChange={(v) => setWaSettings({ ...waSettings, api_token: v })} placeholder={waSettings.has_api_token ? "Leave blank to keep existing" : ""} />
              <Field label="WhatsBiz Base URL" value={waSettings.whats_biz_base_url || DEFAULT_WHATSBIZ_URL} onChange={(v) => setWaSettings({ ...waSettings, whats_biz_base_url: v })} />
              <Field label="Verify Token" value={waSettings.verify_token} onChange={(v) => setWaSettings({ ...waSettings, verify_token: v })} />
            </div>
          )}
          <Field label="Business Name" value={waSettings.business_name} onChange={(v) => setWaSettings({ ...waSettings, business_name: v })} />
          <ToggleRow label="Active — enable WhatsApp messaging" checked={waSettings.enabled || false} onCheckedChange={(c) => setWaSettings({ ...waSettings, enabled: c })} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setWaOpen(false)} className="rounded-xl">Cancel</Button>
            <Button onClick={saveWa} className="btn-primary" disabled={!canEdit}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* SMS dialog */}
      <Dialog open={smsOpen} onOpenChange={setSmsOpen}>
        <DialogContent className="rounded-2xl glass-card max-w-md">
          <DialogHeader><DialogTitle>SMS Gateway</DialogTitle></DialogHeader>
          <Field label="Gateway URL" value={smsSettings.gateway_url} onChange={(v) => setSmsSettings({ ...smsSettings, gateway_url: v })} />
          <Field label="API Key" type="password" value={smsSettings.api_key} onChange={(v) => setSmsSettings({ ...smsSettings, api_key: v })} placeholder={smsSettings.has_api_key ? "Leave blank to keep existing" : ""} />
          <Field label="Sender ID" value={smsSettings.sender_id} onChange={(v) => setSmsSettings({ ...smsSettings, sender_id: v })} />
          <ToggleRow label="Active — enable SMS sending" checked={smsSettings.enabled || false} onCheckedChange={(c) => setSmsSettings({ ...smsSettings, enabled: c })} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setSmsOpen(false)} className="rounded-xl">Cancel</Button>
            <Button onClick={saveSms} className="btn-primary" disabled={!canEdit}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Email dialog */}
      <Dialog open={emailOpen} onOpenChange={setEmailOpen}>
        <DialogContent className="rounded-2xl glass-card max-w-lg max-h-[85vh] overflow-y-auto">
          <DialogHeader><DialogTitle>Email (SMTP)</DialogTitle></DialogHeader>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <Field label="SMTP Host" value={emailSettings.smtp_host} onChange={(v) => setEmailSettings({ ...emailSettings, smtp_host: v })} />
            <Field label="SMTP Port" value={emailSettings.smtp_port ?? 587} onChange={(v) => setEmailSettings({ ...emailSettings, smtp_port: v })} />
            <Field label="Username" value={emailSettings.smtp_username} onChange={(v) => setEmailSettings({ ...emailSettings, smtp_username: v })} />
            <Field label="Password" type="password" value={emailSettings.smtp_password} onChange={(v) => setEmailSettings({ ...emailSettings, smtp_password: v })} placeholder={emailSettings.has_smtp_password ? "Leave blank to keep existing" : ""} />
            <Field label="From Email" value={emailSettings.from_email} onChange={(v) => setEmailSettings({ ...emailSettings, from_email: v })} />
            <Field label="From Name" value={emailSettings.from_name} onChange={(v) => setEmailSettings({ ...emailSettings, from_name: v })} />
          </div>
          <ToggleRow label="Use SSL/TLS" checked={emailSettings.use_ssl !== false} onCheckedChange={(c) => setEmailSettings({ ...emailSettings, use_ssl: c })} />
          <ToggleRow label="Active — enable email sending" checked={emailSettings.enabled || false} onCheckedChange={(c) => setEmailSettings({ ...emailSettings, enabled: c })} />
          <ToggleRow label="Send email with WhatsApp for appointments" checked={emailSettings.send_with_whatsapp !== false} onCheckedChange={(c) => setEmailSettings({ ...emailSettings, send_with_whatsapp: c })} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setEmailOpen(false)} className="rounded-xl">Cancel</Button>
            <Button onClick={saveEmail} className="btn-primary" disabled={!canEdit}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
};

const shouldSendSecret = (value) => value && !value.includes("...") && value !== "********";

const IntegrationRow = ({ connected, icon, iconBg, title, description, connectedClass, extra, statusIcon, statusLabel, action }) => (
  <div className={cn("rounded-lg border p-2.5 flex flex-col sm:flex-row sm:items-center gap-2", connected ? connectedClass : "bg-white/45 border-white/60")}>
    <div className="flex items-start gap-2 flex-1 min-w-0">
      <div className={cn("w-8 h-8 rounded-lg flex items-center justify-center shrink-0", iconBg)}>{icon}</div>
      <div className="min-w-0">
        <div className="flex items-center gap-1.5 flex-wrap">
          <span className="font-semibold text-ui-sm text-[#022C22]">{title}</span>
          <span className={cn("text-[10px] px-1.5 py-0.5 rounded-full font-medium", connected ? "bg-emerald-100 text-emerald-700" : "bg-slate-100 text-slate-600")}>{statusLabel}</span>
        </div>
        <p className="text-ui-caption text-text-secondary mt-0.5 line-clamp-2">{description}</p>
        {extra}
      </div>
    </div>
    <div className="flex sm:flex-col items-center sm:items-end gap-1 shrink-0 text-[10px] text-text-secondary">
      <div className="flex items-center gap-1">{statusIcon}<span>{statusLabel}</span></div>
      <div className="flex items-center gap-1">{action}</div>
    </div>
  </div>
);

const Field = ({ label, value, onChange, type, placeholder }) => (
  <div>
    <label className="text-ui-caption uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-0.5">{label}</label>
    <Input type={type || "text"} value={value ?? ""} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} className="rounded-lg h-8 text-ui-sm" />
  </div>
);

const FormField = ({ label, children }) => (
  <div className="mb-2">
    <label className="text-ui-caption uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-0.5">{label}</label>
    {children}
  </div>
);

const ToggleRow = ({ label, checked, onCheckedChange }) => (
  <label className="flex items-center gap-2 text-ui-sm mt-2">
    <Switch checked={checked} onCheckedChange={onCheckedChange} />
    {label}
  </label>
);

export default IntegrationsPanel;
