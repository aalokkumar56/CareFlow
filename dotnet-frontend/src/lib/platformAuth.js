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

export const platformLogin = async (email, password) => {
  const res = await platformApi.post("/platform/auth/login", { email, password });
  localStorage.setItem("cureflow_platform_token", res.data.access_token);
  return res.data;
};

export const platformLogout = () => {
  localStorage.removeItem("cureflow_platform_token");
  window.location.href = "/platform/login";
};

export const isPlatformAuthenticated = () =>
  typeof window !== "undefined" && !!localStorage.getItem("cureflow_platform_token");

export const PLATFORM_OPS_EMAIL = "ops@cureflow.in";
export const PLATFORM_OPS_PASSWORD = "OpsAdmin123!";
