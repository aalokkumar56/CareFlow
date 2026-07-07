"use client";

import React, { useCallback, useEffect } from "react";
import NextLink, { useLinkStatus } from "next/link";
import {
  useRouter,
  usePathname,
  useParams as useNextParams,
  useSearchParams as useNextSearchParams,
} from "next/navigation";
import { cn } from "@/lib/utils";

const isActivePath = (pathname, to, end) => {
  if (end) return pathname === to;
  if (to === "/") return pathname === "/";
  return pathname === to || pathname.startsWith(`${to}/`);
};

function NavLinkBody({ isActive, children }) {
  const { pending } = useLinkStatus();
  const content = typeof children === "function" ? children({ isActive, pending }) : children;

  return (
    <span className={cn("block", pending && "opacity-70 pointer-events-none")} aria-busy={pending || undefined}>
      {content}
    </span>
  );
}

export function Link({ to, href, children, prefetch = false, ...props }) {
  return (
    <NextLink href={to ?? href} prefetch={prefetch} {...props}>
      {children}
    </NextLink>
  );
}

export function NavLink({ to, end = false, className, children, onClick, ...props }) {
  const pathname = usePathname();
  const isActive = isActivePath(pathname, to, end);
  const resolvedClass =
    typeof className === "function" ? className({ isActive }) : className;

  return (
    <NextLink
      href={to}
      prefetch={false}
      className={resolvedClass}
      onClick={onClick}
      {...props}
    >
      <NavLinkBody isActive={isActive}>
        {children}
      </NavLinkBody>
    </NextLink>
  );
}

export function Navigate({ to, replace = false }) {
  const router = useRouter();

  useEffect(() => {
    if (replace) router.replace(to);
    else router.push(to);
  }, [to, replace, router]);

  return null;
}

export function useNavigate() {
  const router = useRouter();
  return useCallback(
    (to, options) => {
      if (options?.replace) router.replace(to);
      else router.push(to);
    },
    [router],
  );
}

export { usePathname };
export function useParams() {
  return useNextParams();
}

export function useSearchParams() {
  const params = useNextSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const setSearchParams = useCallback(
    (updater, options = {}) => {
      const current = new URLSearchParams(params.toString());
      const next =
        typeof updater === "function" ? updater(current) : new URLSearchParams(updater);
      const qs = next.toString();
      const url = qs ? `${pathname}?${qs}` : pathname;
      if (options.replace) router.replace(url);
      else router.push(url);
    },
    [params, pathname, router],
  );

  return [params, setSearchParams];
}
