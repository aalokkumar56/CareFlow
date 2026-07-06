import { useCallback, useEffect, useState } from "react";
import { apiGet, apiPatch, apiPost, toastApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";

const POLL_MS = 30_000;

export const useNotifications = () => {
  const { user } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);

  const refreshCount = useCallback(async (signal) => {
    if (!user) return;
    try {
      const res = await apiGet("/notifications/unread-count", {
        signal,
        skipGlobalLoader: true,
      });
      setUnreadCount(Number(res?.count) || 0);
    } catch (error) {
      if (error?.code === "ERR_CANCELED") return;
      // Silent for badge polling — dropdown load shows errors
    }
  }, [user]);

  const refreshList = useCallback(async (signal) => {
    if (!user) return;
    setLoading(true);
    try {
      const list = await apiGet("/notifications?limit=50", { signal });
      setItems(Array.isArray(list) ? list : []);
    } catch (error) {
      if (error?.code === "ERR_CANCELED") return;
      toastApiError(error, "Failed to load notifications");
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    if (!user) {
      setUnreadCount(0);
      return;
    }

    const controller = new AbortController();
    refreshCount(controller.signal);
    const interval = setInterval(() => refreshCount(controller.signal), POLL_MS);
    return () => {
      controller.abort();
      clearInterval(interval);
    };
  }, [user, refreshCount]);

  const markRead = useCallback(async (id) => {
    await apiPatch(`/notifications/${id}/read`);
    setItems((prev) => prev.map((n) => (n.id === id ? { ...n, is_read: true } : n)));
    setUnreadCount((c) => Math.max(0, c - 1));
  }, []);

  const markAllRead = useCallback(async () => {
    await apiPost("/notifications/mark-all-read");
    setItems((prev) => prev.map((n) => ({ ...n, is_read: true })));
    setUnreadCount(0);
  }, []);

  return {
    unreadCount,
    items,
    loading,
    refreshCount,
    refreshList,
    markRead,
    markAllRead,
  };
};

export default useNotifications;
