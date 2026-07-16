import React, { useEffect, useState } from "react";
import { useNavigate } from "@/lib/navigation";
import {
  CommandDialog, CommandInput, CommandList,
  CommandEmpty, CommandGroup, CommandItem,
} from "@/components/ui/command";
import {
  House, ChatCircleDots, UsersThree, CalendarBlank,
  ListChecks, GearSix, Plus, MagnifyingGlass,
} from "@phosphor-icons/react";
import { api } from "@/lib/api";
import { unwrapPaged } from "@/lib/pagination";

const CommandPalette = ({ open, setOpen }) => {
  const nav = useNavigate();
  const [query, setQuery] = useState("");
  const [patients, setPatients] = useState([]);

  useEffect(() => {
    const handler = (e) => {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        setOpen((v) => !v);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [setOpen]);

  useEffect(() => {
    if (!open || !query) { setPatients([]); return; }
    const t = setTimeout(() => {
      api.get(`/patients?q=${encodeURIComponent(query)}&page=1&page_size=8`)
        .then((r) => setPatients(unwrapPaged(r).items))
        .catch(() => setPatients([]));
    }, 200);
    return () => clearTimeout(t);
  }, [query, open]);

  const go = (path) => { setOpen(false); nav(path); };

  return (
    <CommandDialog open={open} onOpenChange={setOpen}>
      <CommandInput
        placeholder="Search patients, navigate..."
        value={query}
        onValueChange={setQuery}
        data-testid="cmd-palette-input"
      />
      <CommandList className="max-h-[400px]">
        <CommandEmpty>No results.</CommandEmpty>
        {patients.length > 0 && (
          <CommandGroup heading="Patients">
            {patients.map((p) => (
              <CommandItem
                key={p.id}
                value={`${p.name} ${p.phone || ""}`}
                onSelect={() => go(`/patients/${p.id}`)}
              >
                <UsersThree weight="regular" className="mr-2 w-4 h-4" />
                <span className="flex-1">{p.name}</span>
                <span className="text-xs text-text-muted">{p.phone}</span>
              </CommandItem>
            ))}
          </CommandGroup>
        )}
        <CommandGroup heading="Navigate">
          <CommandItem onSelect={() => go("/")}><House weight="regular" className="mr-2 w-4 h-4" />Daily Operations Dashboard</CommandItem>
          <CommandItem onSelect={() => go("/inbox")}><ChatCircleDots weight="regular" className="mr-2 w-4 h-4" />WhatsApp Inbox</CommandItem>
          <CommandItem onSelect={() => go("/email-inbox")}><ChatCircleDots weight="regular" className="mr-2 w-4 h-4" />Email Inbox</CommandItem>
          <CommandItem onSelect={() => go("/patients")}><UsersThree weight="regular" className="mr-2 w-4 h-4" />All Patients</CommandItem>
          <CommandItem onSelect={() => go("/appointments")}><CalendarBlank weight="regular" className="mr-2 w-4 h-4" />Appointments</CommandItem>
          <CommandItem onSelect={() => go("/tasks")}><ListChecks weight="regular" className="mr-2 w-4 h-4" />Follow-ups</CommandItem>
        </CommandGroup>
        <CommandGroup heading="Quick Actions">
          <CommandItem onSelect={() => go("/patients?new=1")}><Plus weight="regular" className="mr-2 w-4 h-4" />New Patient</CommandItem>
          <CommandItem onSelect={() => go("/appointments?new=1")}><Plus weight="regular" className="mr-2 w-4 h-4" />New Appointment</CommandItem>
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  );
};

export default CommandPalette;
