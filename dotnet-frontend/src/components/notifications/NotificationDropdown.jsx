import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Bell, Check, Gear } from "@phosphor-icons/react";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import useNotifications from "@/hooks/useNotifications";
import { cn } from "@/lib/utils";
import { Link } from "react-router-dom";

const formatTime = (iso) => {
  if (!iso) return "";
  const d = new Date(iso);
  const now = new Date();
  const diffMs = now - d;
  if (diffMs < 60_000) return "Just now";
  if (diffMs < 3_600_000) return `${Math.floor(diffMs / 60_000)}m ago`;
  if (diffMs < 86_400_000) return `${Math.floor(diffMs / 3_600_000)}h ago`;
  return d.toLocaleDateString("en-IN", { day: "numeric", month: "short" });
};

const NotificationList = ({ items, loading, onItemClick, onMarkAll, compact }) => (
  <div className={cn("flex flex-col", compact ? "max-h-[70vh]" : "max-h-[28rem]")} data-testid="notification-dropdown">
    <div className="flex items-center justify-between px-3 py-2 border-b border-white/40 shrink-0">
      <span className="text-sm font-semibold text-[#022C22]">Notifications</span>
      <div className="flex items-center gap-2">
        {items.some((n) => !n.is_read) && (
          <button
            type="button"
            onClick={onMarkAll}
            className="text-[11px] text-[#4338CA] hover:underline flex items-center gap-1"
            data-testid="notification-mark-all-read"
          >
            <Check className="w-3.5 h-3.5" />
            Mark all read
          </button>
        )}
      </div>
    </div>
    <div
      data-testid="notification-list-scroll"
      className="overflow-y-auto flex-1 scrollbar-thin max-h-96"
    >
      {loading && items.length === 0 ? (
        <p className="text-sm text-text-muted px-4 py-6 text-center">Loading…</p>
      ) : items.length === 0 ? (
        <p className="text-sm text-text-muted px-4 py-6 text-center" data-testid="notification-empty">
          No notifications yet
        </p>
      ) : (
        items.map((n) => (
          <button
            key={n.id}
            type="button"
            data-testid={`notification-item-${n.id}`}
            data-read={n.is_read ? "true" : "false"}
            onClick={() => onItemClick(n)}
            className={cn(
              "w-full text-left px-3 py-2.5 border-b border-white/30 hover:bg-white/40 transition-colors",
              !n.is_read ? "bg-sky-50/70" : "bg-transparent opacity-80",
            )}
          >
            <div className="flex items-start gap-2">
              {!n.is_read ? (
                <span
                  className="mt-1.5 w-2 h-2 rounded-full bg-[#4338CA] shrink-0"
                  aria-hidden
                  data-testid="notification-unread-dot"
                />
              ) : (
                <span className="mt-1.5 w-2 h-2 shrink-0" aria-hidden />
              )}
              <div className="min-w-0 flex-1">
                <p
                  className={cn(
                    "text-[13px] truncate",
                    n.is_read ? "font-normal text-text-secondary" : "font-semibold text-[#022C22]",
                  )}
                >
                  {n.title}
                </p>
                {n.body && (
                  <p
                    className={cn(
                      "text-[12px] line-clamp-2 mt-0.5",
                      n.is_read ? "text-text-muted" : "text-text-secondary",
                    )}
                  >
                    {n.body}
                  </p>
                )}
                <p className="text-[10px] text-text-muted mt-1">{formatTime(n.created_at)}</p>
              </div>
            </div>
          </button>
        ))
      )}
    </div>
    <div className="shrink-0 px-3 py-2 border-t border-white/40 flex items-center justify-between">
      <span className="text-[10px] text-text-muted">
        Showing latest {items.length}
        {items.length >= 50 ? " (max 50)" : ""}
      </span>
      <Link
        to="/notifications/preferences"
        data-testid="notification-preferences-link"
        className="text-[11px] text-[#64748B] hover:text-[#4338CA] flex items-center gap-1"
      >
        <Gear className="w-3.5 h-3.5" />
        Preferences
      </Link>
    </div>
  </div>
);

const NotificationDropdown = ({ className, iconClassName, compact }) => {
  const navigate = useNavigate();
  const { unreadCount, items, loading, refreshList, markRead, markAllRead } = useNotifications();
  const [open, setOpen] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  const handleOpen = (next) => {
    setOpen(next);
    if (next) refreshList();
  };

  const handleItemClick = async (n) => {
    if (!n.is_read) await markRead(n.id);
    setOpen(false);
    setMobileOpen(false);
    if (n.action_url) navigate(n.action_url);
  };

  const bellButton = (
    <button
      type="button"
      data-testid="notification-bell-desktop"
      className={cn(
        "relative h-9 w-9 flex items-center justify-center rounded-xl glass-input hover:bg-white/50 transition-colors",
        className,
      )}
      aria-label="Notifications"
    >
      <Bell weight="regular" className={cn("w-[18px] h-[18px] text-[#4338CA]", iconClassName)} />
      {unreadCount > 0 && (
        <span
          data-testid="notification-badge"
          className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 text-white text-[10px] font-bold flex items-center justify-center"
        >
          {unreadCount > 99 ? "99+" : unreadCount}
        </span>
      )}
    </button>
  );

  return (
    <>
      <div className="hidden sm:block">
        <Popover open={open} onOpenChange={handleOpen}>
          <PopoverTrigger asChild>{bellButton}</PopoverTrigger>
          <PopoverContent align="end" className="w-[360px] p-0 glass-panel border-white/60">
            <NotificationList
              items={items}
              loading={loading}
              onItemClick={handleItemClick}
              onMarkAll={markAllRead}
              compact={compact}
            />
          </PopoverContent>
        </Popover>
      </div>
      <div className="sm:hidden">
        <button
          type="button"
          data-testid="notification-bell-mobile"
          onClick={() => {
            setMobileOpen(true);
            refreshList();
          }}
          className={cn(
            "relative h-9 w-9 flex items-center justify-center rounded-xl glass-input hover:bg-white/50 transition-colors",
            className,
          )}
          aria-label="Notifications"
        >
          <Bell weight="regular" className={cn("w-[18px] h-[18px] text-[#4338CA]", iconClassName)} />
          {unreadCount > 0 && (
            <span
              data-testid="notification-badge-mobile"
              className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 text-white text-[10px] font-bold flex items-center justify-center"
            >
              {unreadCount > 99 ? "99+" : unreadCount}
            </span>
          )}
        </button>
        <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
          <SheetContent side="right" className="w-full sm:max-w-md p-0 glass-panel">
            <SheetHeader className="sr-only">
              <SheetTitle>Notifications</SheetTitle>
            </SheetHeader>
            <NotificationList
              items={items}
              loading={loading}
              onItemClick={handleItemClick}
              onMarkAll={markAllRead}
              compact
            />
          </SheetContent>
        </Sheet>
      </div>
    </>
  );
};

export default NotificationDropdown;
