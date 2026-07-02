import React from "react";
import { Button } from "@/components/ui/button";
import { CaretLeft, CaretRight } from "@phosphor-icons/react";

const ListPagination = ({ page, pageSize, total, onPageChange }) => {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages <= 1) return null;

  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  return (
    <div className="flex items-center justify-between gap-4 mt-4 px-1">
      <p className="text-[12px] text-text-muted">
        Showing {from}–{to} of {total}
      </p>
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          className="rounded-sm h-8 text-[12px]"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          <CaretLeft weight="bold" className="w-3.5 h-3.5 mr-1" />
          Previous
        </Button>
        <span className="text-[12px] text-text-secondary tabular-nums">
          Page {page} of {totalPages}
        </span>
        <Button
          variant="outline"
          size="sm"
          className="rounded-sm h-8 text-[12px]"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Next
          <CaretRight weight="bold" className="w-3.5 h-3.5 ml-1" />
        </Button>
      </div>
    </div>
  );
};

export default ListPagination;
