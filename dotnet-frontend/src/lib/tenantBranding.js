import { api } from "@/lib/api";

const PLATFORM_HOST_SUFFIXES = [
  "vercel.app",
  "localhost",
  "onrender.com",
  "127.0.0.1",
];

const PLATFORM_SUBS = new Set(["www", "app", "api", "localhost", "care-flow", "cureflow"]);

const readSlugFromHost = () => {
  if (typeof window === "undefined") return null;
  const host = window.location.hostname.toLowerCase();

  // Platform / preview hosts are not hospital tenant subdomains.
  if (PLATFORM_HOST_SUFFIXES.some((suffix) => host === suffix || host.endsWith(`.${suffix}`))) {
    return null;
  }

  const parts = host.split(".");
  if (parts.length < 3) return null;
  const sub = parts[0];
  if (PLATFORM_SUBS.has(sub)) return null;
  return sub.endsWith("-local") ? sub.slice(0, -6) : sub;
};

export const resolveTenantBranding = async () => {
  const slug = readSlugFromHost();
  if (!slug) {
    try {
      const ctx = await api.get("/public/tenant-context", {
        skipGlobalLoader: true,
        timeout: 8000,
      });
      if (ctx.data?.resolved) return ctx.data;
    } catch {
      /* ignore */
    }
    return null;
  }

  try {
    const res = await api.get(`/public/tenant/${slug}`, {
      skipGlobalLoader: true,
      timeout: 8000,
    });
    return { resolved: true, slug: res.data.slug, name: res.data.name };
  } catch {
    return null;
  }
};

export const lifecycleRouteForTenant = (tenant) => {
  if (!tenant) return "/";
  const raw = tenant.lifecycle_status ?? tenant.lifecycleStatus ?? "";
  const status = String(raw).toLowerCase().replace(/_/g, "");
  if (status === "pendingapproval") return "/pending-approval";
  if (status === "active" && !(tenant.onboarding_complete ?? tenant.onboardingComplete)) return "/onboarding";
  return "/";
};

/** Routes reachable while onboarding is incomplete (configure hospital, integrations, team). */
export const ONBOARDING_SETUP_PATHS = [
  "/onboarding",
  "/settings/hospital",
  "/settings/integrations",
  "/settings/users",
];

export const isOnboardingSetupPath = (pathname) =>
  ONBOARDING_SETUP_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`));
