import React from "react";
import ReactDOM from "react-dom/client";
import "@/index.css";
import { initClientLogger } from "@/lib/logger";
import App from "@/App";

initClientLogger();

const root = ReactDOM.createRoot(document.getElementById("root"));
// StrictMode double-mounts effects in development, which duplicates API calls.
// Keep it in dev to surface unsafe lifecycles; production renders once.
const app = <App />;
root.render(
  process.env.NODE_ENV === "development" ? <React.StrictMode>{app}</React.StrictMode> : app,
);
