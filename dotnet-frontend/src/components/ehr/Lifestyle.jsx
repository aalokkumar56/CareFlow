import React, { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { Heartbeat, Leaf } from "@phosphor-icons/react";

const Section = ({ title, children }) => (
  <div className="space-y-3">
    <div className="text-[11px] uppercase tracking-[0.15em] text-[#064E3B] font-semibold border-b border-subtle pb-1">
      {title}
    </div>
    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">{children}</div>
  </div>
);

const Field = ({ label, children }) => (
  <div>
    <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">
      {label}
    </label>
    {children}
  </div>
);

const T = ({ value, onChange, ...rest }) => (
  <Input value={value ?? ""} onChange={(e) => onChange(e.target.value)} className="rounded-sm h-9" {...rest} />
);

const N = ({ value, onChange, ...rest }) => (
  <Input
    type="number"
    step="0.01"
    value={value ?? ""}
    onChange={(e) => onChange(e.target.value === "" ? null : Number(e.target.value))}
    className="rounded-sm h-9"
    {...rest}
  />
);

const Bool = ({ label, value, onChange }) => (
  <label className="flex items-center gap-2 text-[13px] text-[#022C22] cursor-pointer">
    <input
      type="checkbox"
      checked={!!value}
      onChange={(e) => onChange(e.target.checked)}
    />
    {label}
  </label>
);

const Lifestyle = ({ patientId }) => {
  const [data, setData] = useState(null);

  useEffect(() => {
    api.get(`/patients/${patientId}/lifestyle`).then((r) => setData(r.data || { patient_id: patientId, smoking_status: "unknown", alcohol_consumption: "unknown" }));
  }, [patientId]);

  const set = (patch) => setData((d) => ({ ...d, ...patch }));

  const save = async () => {
    try {
      await api.put(`/patients/${patientId}/lifestyle`, data);
      toast.success("Lifestyle profile saved");
    } catch { toast.error("Save failed"); }
  };

  if (!data) return <div className="text-text-muted text-sm p-4">Loading lifestyle profile...</div>;

  return (
    <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl" data-testid="ehr-lifestyle">
      <div className="px-5 py-3 border-b border-white/60 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Leaf weight="fill" className="w-4 h-4 text-green-600" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">
            Lifestyle Profile <span className="text-text-muted text-[11px] font-normal">(holistic personalised care)</span>
          </h3>
        </div>
        <Button onClick={save} className="rounded-sm h-7 text-[12px] bg-[#064E3B] hover:bg-[#022C22]" data-testid="lifestyle-save">
          Save profile
        </Button>
      </div>

      <div className="p-5 space-y-6 max-h-[700px] overflow-y-auto scrollbar-thin">
        <Section title="Sleep">
          <Field label="Avg sleep hrs"><N value={data.average_sleep_hours} onChange={(v) => set({ average_sleep_hours: v })} /></Field>
          <Field label="Sleep quality"><T value={data.sleep_quality} onChange={(v) => set({ sleep_quality: v })} placeholder="good / poor / interrupted" /></Field>
          <Field label="Wake up time"><T value={data.wake_up_time} onChange={(v) => set({ wake_up_time: v })} placeholder="06:00" /></Field>
          <Field label="Bed time"><T value={data.bed_time} onChange={(v) => set({ bed_time: v })} placeholder="22:30" /></Field>
        </Section>

        <Section title="Hydration & Meals">
          <Field label="Water (L/day)"><N value={data.water_intake_liters_per_day} onChange={(v) => set({ water_intake_liters_per_day: v })} /></Field>
          <Field label="Meals per day"><N value={data.meals_per_day} onChange={(v) => set({ meals_per_day: v })} /></Field>
          <Field label="Meal timings"><T value={data.meal_timings} onChange={(v) => set({ meal_timings: v })} placeholder="8am, 1pm, 8pm" /></Field>
          <div className="flex items-center pt-6">
            <Bool label="Skips breakfast" value={data.skips_breakfast} onChange={(v) => set({ skips_breakfast: v })} />
          </div>
        </Section>

        <Section title="Diet">
          <Field label="Diet type">
            <Select value={data.diet_type || ""} onValueChange={(v) => set({ diet_type: v })}>
              <SelectTrigger className="rounded-sm h-9"><SelectValue placeholder="Select" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="vegetarian">Vegetarian</SelectItem>
                <SelectItem value="non_vegetarian">Non-vegetarian</SelectItem>
                <SelectItem value="vegan">Vegan</SelectItem>
                <SelectItem value="jain">Jain</SelectItem>
                <SelectItem value="eggetarian">Eggetarian</SelectItem>
              </SelectContent>
            </Select>
          </Field>
          <Field label="Cuisine preferences"><T value={data.cuisine_preferences} onChange={(v) => set({ cuisine_preferences: v })} /></Field>
          <Field label="Food allergies (text)"><T value={data.food_allergies_text} onChange={(v) => set({ food_allergies_text: v })} /></Field>
          <Field label="Dietary restrictions"><T value={data.dietary_restrictions} onChange={(v) => set({ dietary_restrictions: v })} /></Field>
          <Field label="Caffeine cups/day"><N value={data.caffeine_cups_per_day} onChange={(v) => set({ caffeine_cups_per_day: v })} /></Field>
          <div className="space-y-2 pt-6">
            <Bool label="Eats processed food regularly" value={data.consumes_processed_food} onChange={(v) => set({ consumes_processed_food: v })} />
            <Bool label="Sugary drinks" value={data.consumes_sugary_drinks} onChange={(v) => set({ consumes_sugary_drinks: v })} />
          </div>
        </Section>

        <Section title="Exercise & Activity">
          <div className="flex items-center pt-6">
            <Bool label="Exercises regularly" value={data.exercises_regularly} onChange={(v) => set({ exercises_regularly: v })} />
          </div>
          <Field label="Minutes/week"><N value={data.exercise_minutes_per_week} onChange={(v) => set({ exercise_minutes_per_week: v })} /></Field>
          <Field label="Exercise type"><T value={data.exercise_type} onChange={(v) => set({ exercise_type: v })} placeholder="walking / gym / yoga" /></Field>
          <Field label="Activity level">
            <Select value={data.physical_activity_level || ""} onValueChange={(v) => set({ physical_activity_level: v })}>
              <SelectTrigger className="rounded-sm h-9"><SelectValue placeholder="Select" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="sedentary">Sedentary</SelectItem>
                <SelectItem value="light">Light</SelectItem>
                <SelectItem value="moderate">Moderate</SelectItem>
                <SelectItem value="active">Active</SelectItem>
              </SelectContent>
            </Select>
          </Field>
        </Section>

        <Section title="Substances">
          <Field label="Smoking">
            <Select value={data.smoking_status || "unknown"} onValueChange={(v) => set({ smoking_status: v })}>
              <SelectTrigger className="rounded-sm h-9" data-testid="lifestyle-smoking"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="never">Never</SelectItem>
                <SelectItem value="former">Former</SelectItem>
                <SelectItem value="current">Occasional / Current</SelectItem>
                <SelectItem value="unknown">Unknown</SelectItem>
              </SelectContent>
            </Select>
          </Field>
          <Field label="Cigarettes/day"><N value={data.cigarettes_per_day} onChange={(v) => set({ cigarettes_per_day: v })} /></Field>
          <Field label="Alcohol">
            <Select value={data.alcohol_consumption || "unknown"} onValueChange={(v) => set({ alcohol_consumption: v })}>
              <SelectTrigger className="rounded-sm h-9"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="never">Never</SelectItem>
                <SelectItem value="occasional">Occasional / Social</SelectItem>
                <SelectItem value="regular">Regular</SelectItem>
                <SelectItem value="heavy">Heavy</SelectItem>
                <SelectItem value="unknown">Unknown</SelectItem>
              </SelectContent>
            </Select>
          </Field>
          <Field label="Alcohol details"><T value={data.alcohol_details} onChange={(v) => set({ alcohol_details: v })} /></Field>
          <div className="pt-6">
            <Bool label="Chews tobacco / paan" value={data.chews_tobacco_or_paan} onChange={(v) => set({ chews_tobacco_or_paan: v })} />
          </div>
          <Field label="Other substances"><T value={data.other_substances} onChange={(v) => set({ other_substances: v })} /></Field>
        </Section>

        <Section title="Work & Stress">
          <Field label="Occupation"><T value={data.occupation} onChange={(v) => set({ occupation: v })} /></Field>
          <Field label="Work schedule"><T value={data.work_schedule} onChange={(v) => set({ work_schedule: v })} placeholder="9-5 / shift / night" /></Field>
          <Field label="Work hours/day"><N value={data.work_hours_per_day} onChange={(v) => set({ work_hours_per_day: v })} /></Field>
          <Field label="Stress level">
            <Select value={data.stress_level || ""} onValueChange={(v) => set({ stress_level: v })}>
              <SelectTrigger className="rounded-sm h-9"><SelectValue placeholder="Select" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="low">Low</SelectItem>
                <SelectItem value="moderate">Moderate</SelectItem>
                <SelectItem value="high">High</SelectItem>
              </SelectContent>
            </Select>
          </Field>
          <Field label="Stress management"><T value={data.stress_management_methods} onChange={(v) => set({ stress_management_methods: v })} placeholder="meditation, walking..." /></Field>
          <Field label="Hobbies"><T value={data.hobbies_and_interests} onChange={(v) => set({ hobbies_and_interests: v })} /></Field>
        </Section>

        <Section title="Mental Wellness">
          <Field label="Mental health concerns"><T value={data.mental_health_concerns} onChange={(v) => set({ mental_health_concerns: v })} /></Field>
          <div className="pt-6">
            <Bool label="Has anxiety or depression" value={data.has_anxiety_or_depression} onChange={(v) => set({ has_anxiety_or_depression: v })} />
          </div>
          <Field label="Current treatment"><T value={data.current_mental_health_treatment} onChange={(v) => set({ current_mental_health_treatment: v })} /></Field>
        </Section>

        <Section title="Environment">
          <Field label="Living environment"><T value={data.living_environment} onChange={(v) => set({ living_environment: v })} placeholder="urban / rural / industrial" /></Field>
          <div className="pt-6">
            <Bool label="Pollution exposure" value={data.exposure_to_pollution} onChange={(v) => set({ exposure_to_pollution: v })} />
          </div>
          <Field label="Pet exposure"><T value={data.pet_exposure} onChange={(v) => set({ pet_exposure: v })} /></Field>
        </Section>

        <Field label="Additional notes">
          <Textarea
            value={data.additional_lifestyle_notes ?? ""}
            onChange={(e) => set({ additional_lifestyle_notes: e.target.value })}
            rows={3}
            className="rounded-sm"
          />
        </Field>

        <div className="text-[11px] text-text-muted flex items-center gap-1.5">
          <Heartbeat weight="bold" className="w-3 h-3" />
          Last updated: {data.last_updated_at ? new Date(data.last_updated_at).toLocaleString() : "—"}
        </div>
      </div>
    </div>
  );
};

export default Lifestyle;
