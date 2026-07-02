import React from "react";

const FormField = ({ label, required, children, className = "" }) => (
  <div className={className}>
    {label && (
      <label className="text-[11px] uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">
        {label}{required ? " *" : ""}
      </label>
    )}
    {children}
  </div>
);

export default FormField;
