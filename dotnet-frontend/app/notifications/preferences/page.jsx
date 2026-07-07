"use client";

import { createClientRoute } from "@/components/createClientRoute";

export default createClientRoute(() => import("./page.client"));
