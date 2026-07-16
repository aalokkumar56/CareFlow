import React from "react";
import { Link } from "@/lib/navigation";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import FormField from "./FormField";

const DepartmentSelect = ({
  departments = [],
  value,
  onChange,
  label = "Department",
  required = true,
  placeholder = "Select department",
  triggerClassName = "rounded-xl h-9",
  showSettingsHint = false,
}) => {
  const options = [...departments];
  if (value && !options.includes(value)) options.push(value);

  return (
    <FormField label={label} required={required}>
      <Select value={value || undefined} onValueChange={onChange}>
        <SelectTrigger className={triggerClassName} data-testid="department-select">
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {options.length === 0 ? (
            <SelectItem value="__none__" disabled>No departments configured</SelectItem>
          ) : (
            options.map((d) => <SelectItem key={d} value={d}>{d}</SelectItem>)
          )}
        </SelectContent>
      </Select>
      {showSettingsHint && options.length === 0 && (
        <p className="text-[11px] text-text-muted mt-1">
          <Link to="/settings/hospital" className="text-[#064E3B] hover:underline">Settings → Hospital</Link>
          {" "}to add departments.
        </p>
      )}
    </FormField>
  );
};

export default DepartmentSelect;
