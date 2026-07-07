import React, { useCallback, useEffect, useState } from "react";
import { platformApi } from "@/lib/platformAuth";
import { normalizeApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "sonner";
import {
  Buildings,
  CheckCircle,
  Prohibit,
  XCircle,
  HourglassMedium,
} from "@phosphor-icons/react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

const TABS = [
  { id: "pending_approval", label: "Pending", icon: HourglassMedium },
  { id: "active", label: "Active", icon: CheckCircle },
  { id: "suspended", label: "Suspended", icon: Prohibit },
  { id: "rejected", label: "Rejected", icon: XCircle },
  { id: "", label: "All", icon: Buildings },
];

const formatDate = (value) => {
  if (!value) return "—";
  try {
    return new Date(value).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  } catch {
    return "—";
  }
};

const normalizeLifecycle = (status) =>
  String(status ?? "")
    .toLowerCase()
    .replace(/_/g, "");

const lifecycleLabel = (status) => {
  const key = normalizeLifecycle(status);
  if (key === "pendingapproval") return "pending approval";
  return key;
};

const lifecycleBadgeClass = (status) => {
  switch (normalizeLifecycle(status)) {
    case "pendingapproval":
      return "bg-amber-100 text-amber-800";
    case "active":
      return "bg-emerald-100 text-emerald-800";
    case "suspended":
      return "bg-red-100 text-red-800";
    case "rejected":
      return "bg-slate-200 text-slate-700";
    default:
      return "bg-muted text-muted-foreground";
  }
};

const PlatformTenants = () => {
  const [tenants, setTenants] = useState([]);
  const [tab, setTab] = useState("pending_approval");
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [approveTarget, setApproveTarget] = useState(null);
  const [rejectTarget, setRejectTarget] = useState(null);
  const [rejectReason, setRejectReason] = useState("");

  const loadTenants = useCallback(async () => {
    setLoading(true);
    try {
      const query = tab ? `?status=${tab}` : "";
      const res = await platformApi.get(`/platform/tenants${query}`);
      setTenants(Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      toast.error(normalizeApiError(err, "Platform API unavailable"));
      setTenants([]);
    } finally {
      setLoading(false);
    }
  }, [tab]);

  useEffect(() => {
    loadTenants();
  }, [loadTenants]);

  const runAction = async (tenant, action, body) => {
    setBusyId(tenant.id);
    try {
      await platformApi.patch(`/platform/tenants/${tenant.id}/${action}`, body);
      toast.success(`${tenant.name}: ${action}`);
      await loadTenants();
    } catch (err) {
      toast.error(normalizeApiError(err, "Action failed"));
    } finally {
      setBusyId(null);
    }
  };

  const confirmApprove = async () => {
    if (!approveTarget) return;
    await runAction(approveTarget, "approve");
    setApproveTarget(null);
  };

  const confirmReject = async () => {
    if (!rejectTarget) return;
    const reason = rejectReason.trim() || "Does not meet criteria";
    await runAction(rejectTarget, "reject", { reason });
    setRejectTarget(null);
    setRejectReason("");
  };

  return (
    <div className="p-6 sm:p-10" data-testid="platform-tenants-page">
      <div className="max-w-4xl mx-auto">
        <div className="mb-6">
          <h1 className="font-heading text-2xl font-semibold" data-testid="platform-tenants-title">
            Hospital tenants
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Review pending registrations · approve · reject · suspend
          </p>
        </div>

        <div className="flex flex-wrap gap-2 mb-6" data-testid="platform-tenants-tabs">
          {TABS.map(({ id, label, icon: Icon }) => (
            <Button
              key={id || "all"}
              size="sm"
              variant={tab === id ? "default" : "outline"}
              onClick={() => setTab(id)}
              data-testid={`platform-tab-${id || "all"}`}
            >
              <Icon className="w-4 h-4 mr-1" />
              {label}
            </Button>
          ))}
        </div>

        {loading ? (
          <p className="text-muted-foreground text-sm">Loading tenants...</p>
        ) : tenants.length === 0 ? (
          <div className="glass-card p-8 text-center" data-testid="platform-tenants-empty">
            <HourglassMedium className="w-10 h-10 text-muted-foreground mx-auto mb-3" weight="duotone" />
            <p className="text-sm font-medium mb-1">
              {tab === "pending_approval"
                ? "No hospitals awaiting approval"
                : "No tenants in this queue"}
            </p>
            {tab === "pending_approval" && (
              <p className="text-xs text-muted-foreground mb-4">
                New signups appear here. Active hospitals are under the Active or All tabs.
              </p>
            )}
            {tab === "pending_approval" && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => setTab("active")}
                data-testid="platform-empty-goto-active"
              >
                View active hospitals
              </Button>
            )}
          </div>
        ) : (
          <ul className="space-y-3" data-testid="platform-tenants-list">
            {tenants.map((tenant) => (
              <li
                key={tenant.id}
                className="glass-card p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3"
                data-testid={`platform-tenant-${tenant.slug}`}
              >
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="font-medium">{tenant.name}</p>
                    <span
                      className={`text-[10px] uppercase tracking-wide px-2 py-0.5 rounded-full font-semibold ${lifecycleBadgeClass(tenant.lifecycle_status)}`}
                    >
                      {lifecycleLabel(tenant.lifecycle_status)}
                    </span>
                  </div>
                  <p className="text-xs text-muted-foreground mt-1">
                    {tenant.slug} · {tenant.contact_email || "—"}
                  </p>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Registered {formatDate(tenant.created_at)}
                    {tenant.rejection_reason && (
                      <> · Rejected: {tenant.rejection_reason}</>
                    )}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2 shrink-0">
                  {normalizeLifecycle(tenant.lifecycle_status) === "pendingapproval" && (
                    <>
                      <Button
                        size="sm"
                        disabled={busyId === tenant.id}
                        data-testid={`platform-approve-${tenant.slug}`}
                        onClick={() => setApproveTarget(tenant)}
                      >
                        Approve
                      </Button>
                      <Button
                        size="sm"
                        variant="destructive"
                        disabled={busyId === tenant.id}
                        data-testid={`platform-reject-${tenant.slug}`}
                        onClick={() => {
                          setRejectTarget(tenant);
                          setRejectReason("");
                        }}
                      >
                        Reject
                      </Button>
                    </>
                  )}
                  {normalizeLifecycle(tenant.lifecycle_status) === "active" && (
                    <Button
                      size="sm"
                      variant="destructive"
                      disabled={busyId === tenant.id}
                      data-testid={`platform-suspend-${tenant.slug}`}
                      onClick={() => runAction(tenant, "suspend")}
                    >
                      Suspend
                    </Button>
                  )}
                  {normalizeLifecycle(tenant.lifecycle_status) === "suspended" && (
                    <Button
                      size="sm"
                      disabled={busyId === tenant.id}
                      data-testid={`platform-activate-${tenant.slug}`}
                      onClick={() => runAction(tenant, "activate")}
                    >
                      Reactivate
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <Dialog open={!!approveTarget} onOpenChange={(open) => !open && setApproveTarget(null)}>
        <DialogContent data-testid="platform-approve-dialog">
          <DialogHeader>
            <DialogTitle>Approve hospital?</DialogTitle>
            <DialogDescription>
              {approveTarget?.name} will become active and the admin can complete onboarding.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveTarget(null)}>Cancel</Button>
            <Button
              onClick={confirmApprove}
              disabled={busyId === approveTarget?.id}
              data-testid="platform-approve-confirm"
            >
              Approve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!rejectTarget} onOpenChange={(open) => !open && setRejectTarget(null)}>
        <DialogContent data-testid="platform-reject-dialog">
          <DialogHeader>
            <DialogTitle>Reject registration?</DialogTitle>
            <DialogDescription>
              {rejectTarget?.name} will not be able to use CureFlow. Optionally provide a reason for the hospital admin.
            </DialogDescription>
          </DialogHeader>
          <Input
            data-testid="platform-reject-reason"
            placeholder="Reason for rejection (optional)"
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectTarget(null)}>Cancel</Button>
            <Button
              variant="destructive"
              onClick={confirmReject}
              disabled={busyId === rejectTarget?.id}
              data-testid="platform-reject-confirm"
            >
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};

export default PlatformTenants;
