import React from "react";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import FormField from "./FormField";

const DoctorSelect = ({
  doctors = [],
  value,
  onChange,
  label = "Doctor",
  required = true,
  triggerClassName = "rounded-xl h-9",
}) => (
  <FormField label={label} required={required}>
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger className={triggerClassName}>
        <SelectValue placeholder="Select doctor" />
      </SelectTrigger>
      <SelectContent>
        {doctors.map((d) => (
          <SelectItem key={d.user_id} value={d.user_id}>{d.name}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  </FormField>
);

export default DoctorSelect;
