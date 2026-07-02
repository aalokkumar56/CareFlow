import React, { createContext, useContext, useEffect, useState } from "react";
import { api } from "@/lib/api";

const AuthContext = createContext(null);

let authMeInflight = null;

/** Deduplicated /auth/me — parallel callers share one request. */
export const fetchAuthMe = () => {
  const token = localStorage.getItem("cureflow_token");
  if (!token) return Promise.reject(new Error("No token"));
  if (!authMeInflight) {
    authMeInflight = api.get("/auth/me").finally(() => {
      authMeInflight = null;
    });
  }
  return authMeInflight;
};

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(() => {
    try {
      const raw = localStorage.getItem("cureflow_user");
      return raw ? JSON.parse(raw) : null;
    } catch (e) { return null; }
  });
  const [loading, setLoading] = useState(() => {
    const token = localStorage.getItem("cureflow_token");
    const cached = localStorage.getItem("cureflow_user");
    return Boolean(token && !cached);
  });
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let active = true;

    const token = localStorage.getItem("cureflow_token");
    if (!token) {
      setLoading(false);
      setReady(true);
      return () => { active = false; };
    }

    fetchAuthMe()
      .then((res) => {
        if (!active) return;
        setUser(res.data);
        localStorage.setItem("cureflow_user", JSON.stringify(res.data));
      })
      .catch(() => {
        if (!active) return;
        localStorage.removeItem("cureflow_token");
        localStorage.removeItem("cureflow_user");
        setUser(null);
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setReady(true);
      });

    return () => { active = false; };
  }, []);

  const login = async (email, password) => {
    const res = await api.post("/auth/login", { email, password });
    localStorage.setItem("cureflow_token", res.data.access_token);
    const meRes = await fetchAuthMe();
    localStorage.setItem("cureflow_user", JSON.stringify(meRes.data));
    setUser(meRes.data);
    setReady(true);
    setLoading(false);
    return meRes.data;
  };

  const logout = () => {
    localStorage.removeItem("cureflow_token");
    localStorage.removeItem("cureflow_user");
    setUser(null);
    setReady(true);
    window.location.href = "/login";
  };

  return (
    <AuthContext.Provider value={{ user, loading, ready, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => useContext(AuthContext);
