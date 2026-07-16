import React, {
  createContext,
  useContext,
  useLayoutEffect,
  useState,
} from "react";
import { api } from "@/lib/api";

const AuthContext = createContext(null);

let authMeInflight = null;

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

/** Deduplicated /auth/me — parallel callers share one request. */
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

export const AuthProvider = ({ children }) => {
  // SSR + first client paint stay identical; bootstrap from storage before paint.
  const [bootstrapped, setBootstrapped] = useState(false);
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [ready, setReady] = useState(false);

  useLayoutEffect(() => {
    let active = true;
    const token = readStorage("cureflow_token");
    const cachedUser = readCachedUser();

    if (!token) {
      setUser(null);
      setLoading(false);
      setReady(true);
      setBootstrapped(true);
      return () => { active = false; };
    }

    if (cachedUser) {
      setUser(cachedUser);
      setLoading(false);
      setReady(true);
      setBootstrapped(true);
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
        setBootstrapped(true);
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
    setBootstrapped(true);
    return meRes.data;
  };

  const logout = () => {
    localStorage.removeItem("cureflow_token");
    localStorage.removeItem("cureflow_user");
    setUser(null);
    setReady(true);
    setLoading(false);
    window.location.href = "/login";
  };

  const value = bootstrapped
    ? { user, loading, ready, login, logout }
    : { user: null, loading: true, ready: false, login, logout };

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
      loading: true,
      ready: false,
      login: async () => {
        throw new Error("AuthProvider is missing");
      },
      logout: () => {},
    };
  }
  return ctx;
};
