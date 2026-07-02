import React, { useCallback, useEffect, useMemo, useState } from "react";
import GlassCard from "@/components/glass/GlassCard";
import { api, normalizeApiError } from "@/lib/api";
import { PERMISSION_LABELS } from "@/lib/permissions";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

const PermissionsCatalogPanel = ({ fillHeight = false }) => {
  const [permissions, setPermissions] = useState([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(() => {
    setLoading(true);
    api.get("/admin/permissions")
      .then((res) => setPermissions(res.data || []))
      .catch((e) => toast.error(normalizeApiError(e, "Failed to load permissions")))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load]);

  const grouped = useMemo(() => {
    const map = new Map();
    for (const p of permissions) {
      const code = p.key || p.code;
      const category = p.category || "Other";
      if (!map.has(category)) map.set(category, []);
      map.get(category).push({
        code,
        label: p.label || PERMISSION_LABELS[code] || code,
      });
    }
    return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]));
  }, [permissions]);

  if (loading) {
    return (
      <GlassCard className={cn("text-center py-8 text-ui-sm text-text-muted", fillHeight && "flex-1 min-h-0 flex items-center justify-center")}>
        Loading permissions…
      </GlassCard>
    );
  }

  return (
    <GlassCard
      padding={false}
      className={cn("overflow-hidden flex flex-col min-h-0", fillHeight && "flex-1")}
      data-testid="permissions-catalog-panel"
    >
      <div className="px-3 py-2 border-b border-white/45 shrink-0">
        <h2 className="font-heading text-ui-base font-semibold text-[#022C22]">Permission catalog</h2>
        <p className="text-ui-caption text-text-secondary truncate">
          Reference list of all system permissions · assign via Roles
        </p>
      </div>

      <div className="flex-1 min-h-0 overflow-auto scrollbar-thin p-3 space-y-4">
        {grouped.map(([category, items]) => (
          <section key={category}>
            <h3 className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold mb-2">
              {category}
            </h3>
            <ul className="divide-y divide-white/35 rounded-xl border border-white/45 bg-white/20 overflow-hidden">
              {items.map((item) => (
                <li
                  key={item.code}
                  className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-0.5 px-3 py-2"
                  data-testid={`permission-${item.code.replace(/\./g, "-")}`}
                >
                  <span className="text-ui-sm font-medium text-[#022C22]">{item.label}</span>
                  <code className="text-[11px] text-text-muted font-mono">{item.code}</code>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>

      {permissions.length > 0 && (
        <div className="px-3 py-1 border-t border-white/35 text-[10px] text-text-muted shrink-0">
          {permissions.length} permissions · read-only reference
        </div>
      )}
    </GlassCard>
  );
};

export default PermissionsCatalogPanel;
