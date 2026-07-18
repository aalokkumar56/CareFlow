import axios from "axios";
import { API_BASE } from "@/lib/api";

export const platformApi = axios.create({
  baseURL: API_BASE,
  headers: { "Content-Type": "application/json" },
});

platformApi.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("cureflow_platform_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

const persistSession = (data) => {
  localStorage.setItem("cureflow_platform_token", data.access_token);
  return data;
};

export const fetchPlatformSetupStatus = async () => {
  const res = await platformApi.get("/platform/auth/setup-status");
  return res.data;
};

export const platformBootstrap = async ({ name, email, password }) => {
  const res = await platformApi.post("/platform/auth/bootstrap", { name, email, password });
  return persistSession(res.data);
};

export const platformLogin = async (email, password) => {
  const res = await platformApi.post("/platform/auth/login", { email, password });
  return persistSession(res.data);
};

export const platformLogout = () => {
  localStorage.removeItem("cureflow_platform_token");
  window.location.href = "/platform/login";
};

export const isPlatformAuthenticated = () =>
  typeof window !== "undefined" && !!localStorage.getItem("cureflow_platform_token");
