"use client";

import React, {
  createContext,
  useCallback,
  useContext,
  useLayoutEffect,
  useMemo,
  useState,
} from "react";
import { api } from "@/lib/api";
import { lifecycleRouteForTenant } from "@/lib/tenantBranding";

const AuthContext = createContext(null);

let authMeInflight = null;
let authSessionInflight = null;

const readStorage = (key) => {
  if (typeof window === "undefined") return null;
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
};

const readCachedUser = () => {
  try {
    const raw = readStorage("cureflow_user");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
};

const readCachedTenant = () => {
  try {
    const raw = readStorage("cureflow_tenant");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
};

export const fetchAuthMe = () => {
  const token = readStorage("cureflow_token");
  if (!token) return Promise.reject(new Error("No token"));
  if (!authMeInflight) {
    authMeInflight = api.get("/auth/me").finally(() => {
      authMeInflight = null;
    });
  }
  return authMeInflight;
};

export const fetchAuthSession = (source = "unknown") => {
  const token = readStorage("cureflow_token");
  if (!token) return Promise.reject(new Error("No token"));
  // #region agent log
  fetch('http://127.0.0.1:7396/ingest/71a493aa-be86-4272-b3f4-088f0dfe3f3f',{method:'POST',headers:{'Content-Type':'application/json','X-Debug-Session-Id':'a6e1ca'},body:JSON.stringify({sessionId:'a6e1ca',location:'auth.jsx:fetchAuthSession',message:'session_fetch',data:{source,inflight:!!authSessionInflight},timestamp:Date.now(),hypothesisId:'H5'})}).catch(()=>{});
  // #endregion
  if (!authSessionInflight) {
    authSessionInflight = api.get("/auth/session").finally(() => {
      authSessionInflight = null;
    });
  }
  return authSessionInflight;
};

export const AuthProvider = ({ children }) => {
  const [bootstrapped, setBootstrapped] = useState(false);
  const [user, setUser] = useState(null);
  const [tenant, setTenant] = useState(null);
  const [loading, setLoading] = useState(true);
  const [ready, setReady] = useState(false);

  useLayoutEffect(() => {
    let active = true;
    const token = readStorage("cureflow_token");
    const cachedUser = readCachedUser();
    const cachedTenant = readCachedTenant();

    if (!token) {
      setUser(null);
      setTenant(null);
      setLoading(false);
      setReady(true);
      setBootstrapped(true);
      return () => { active = false; };
    }

    if (cachedUser) {
      setUser(cachedUser);
      setTenant(cachedTenant);
      setLoading(false);
      setReady(true);
      setBootstrapped(true);
    }

    fetchAuthSession("bootstrap")
      .then((res) => {
        if (!active) return;
        setUser(res.data.user);
        setTenant(res.data.tenant);
        localStorage.setItem("cureflow_user", JSON.stringify(res.data.user));
        localStorage.setItem("cureflow_tenant", JSON.stringify(res.data.tenant));
      })
      .catch(() => {
        if (!active) return;
        localStorage.removeItem("cureflow_token");
        localStorage.removeItem("cureflow_user");
        localStorage.removeItem("cureflow_tenant");
        setUser(null);
        setTenant(null);
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setReady(true);
        setBootstrapped(true);
      });

    return () => { active = false; };
  }, []);

  const login = useCallback(async (email, password) => {
    const res = await api.post("/auth/login", { email, password });
    localStorage.setItem("cureflow_token", res.data.access_token);
    localStorage.setItem("cureflow_user", JSON.stringify(res.data.user));
    localStorage.setItem("cureflow_tenant", JSON.stringify(res.data.tenant));
    setUser(res.data.user);
    setTenant(res.data.tenant);
    setReady(true);
    setLoading(false);
    setBootstrapped(true);
    return { user: res.data.user, tenant: res.data.tenant };
  }, []);

  const refreshSession = useCallback(async () => {
    const res = await fetchAuthSession("refreshSession");
    setUser(res.data.user);
    setTenant(res.data.tenant);
    localStorage.setItem("cureflow_user", JSON.stringify(res.data.user));
    localStorage.setItem("cureflow_tenant", JSON.stringify(res.data.tenant));
    return res.data;
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem("cureflow_token");
    localStorage.removeItem("cureflow_user");
    localStorage.removeItem("cureflow_tenant");
    setUser(null);
    setTenant(null);
    setReady(true);
    setLoading(false);
    window.location.href = "/login";
  }, []);

  const postLoginRoute = useCallback((t) => lifecycleRouteForTenant(t), []);

  const value = useMemo(
    () =>
      bootstrapped
        ? { user, tenant, loading, ready, login, logout, refreshSession, postLoginRoute }
        : {
            user: null,
            tenant: null,
            loading: true,
            ready: false,
            login,
            logout,
            refreshSession,
            postLoginRoute,
          },
    [bootstrapped, user, tenant, loading, ready, login, logout, refreshSession, postLoginRoute],
  );

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    return {
      user: null,
      tenant: null,
      loading: true,
      ready: false,
      login: async () => { throw new Error("AuthProvider is missing"); },
      logout: () => {},
      refreshSession: async () => { throw new Error("AuthProvider is missing"); },
      postLoginRoute: () => "/",
    };
  }
  return ctx;
};
