import React from "react";
import { CaretLeft, CaretRight } from "@phosphor-icons/react";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

const PAGE_SIZES = [8, 16, 25, 50];

function pageNumbers(current, total) {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const pages = [];
  pages.push(1);
  if (current > 3) pages.push("…");
  for (let i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i += 1) {
    pages.push(i);
  }
  if (current < total - 2) pages.push("…");
  if (total > 1) pages.push(total);
  return pages;
}

const PatientsPagination = ({
  page,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
  className,
}) => {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);
  const pages = pageNumbers(page, totalPages);

  return (
    <div className={cn("flex flex-wrap items-center justify-between gap-2 px-0 py-1.5 text-[13px]", className)}>
      <p className="text-text-muted">
        Showing <span className="font-medium text-[#022C22]">{from}</span> to{" "}
        <span className="font-medium text-[#022C22]">{to}</span> of{" "}
        <span className="font-medium text-[#022C22]">{total.toLocaleString()}</span> patients
      </p>

      <div className="flex items-center gap-1.5 flex-wrap">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          className="h-9 px-2.5 rounded-xl text-[13px] border border-white/60 bg-white/50 hover:bg-white/70 disabled:opacity-40 disabled:pointer-events-none transition-colors flex items-center gap-1"
        >
          <CaretLeft weight="bold" className="w-3.5 h-3.5" />
          Previous
        </button>

        {pages.map((p, i) => (
          p === "…" ? (
            <span key={`ellipsis-${i}`} className="px-1 text-text-muted text-ui-sm">…</span>
          ) : (
            <button
              key={p}
              type="button"
              onClick={() => onPageChange(p)}
              className={cn(
                "min-w-[36px] h-9 px-1.5 rounded-xl text-[13px] border transition-colors",
                p === page
                  ? "bg-[#4338CA] text-white border-[#4338CA] font-semibold"
                  : "border-white/60 bg-white/50 hover:bg-white/70 text-[#022C22]",
              )}
            >
              {p}
            </button>
          )
        ))}

        <button
          type="button"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          className="h-9 px-2.5 rounded-xl text-[13px] border border-white/60 bg-white/50 hover:bg-white/70 disabled:opacity-40 disabled:pointer-events-none transition-colors flex items-center gap-1"
        >
          Next
          <CaretRight weight="bold" className="w-3.5 h-3.5" />
        </button>
      </div>

      {onPageSizeChange && (
        <div className="flex items-center gap-2">
          <span className="text-ui-sm text-text-muted">Rows per page</span>
          <Select value={String(pageSize)} onValueChange={(v) => onPageSizeChange(Number(v))}>
            <SelectTrigger className="h-9 w-[100px] rounded-xl text-[13px] glass-input border-white/60">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZES.map((s) => (
                <SelectItem key={s} value={String(s)}>{s} per page</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}
    </div>
  );
};

export default PatientsPagination;
