import React from "react";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import FormField from "./FormField";

const PatientSelect = ({
  patients = [],
  value,
  onChange,
  label = "Patient",
  required = true,
  testId,
  triggerClassName = "rounded-xl h-9",
}) => (
  <FormField label={label} required={required}>
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger className={triggerClassName} data-testid={testId}>
        <SelectValue placeholder="Select patient" />
      </SelectTrigger>
      <SelectContent className="max-h-[300px]">
        {patients.map((p) => (
          <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  </FormField>
);

export default PatientSelect;
