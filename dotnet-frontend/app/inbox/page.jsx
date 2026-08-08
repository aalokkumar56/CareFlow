// app/inbox/page.jsx
import InboxPage from "./page.client";

// This is now a lightweight Server Component entry point.
// It instantly tells Next.js what client file to serve without waiting for dynamic client-side imports.
export default function Page() {
  return <InboxPage />;
}