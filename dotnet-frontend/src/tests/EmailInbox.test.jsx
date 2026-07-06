import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { api } from "@/lib/api";
import EmailInbox from "@/views/EmailInbox";

jest.mock("@/lib/api");

jest.mock("@/components/layout/AppShell", () => ({ children, title }) => (
  <div data-testid="app-shell">
    <h1>{title}</h1>
    {children}
  </div>
));

jest.mock("@/components/glass/PageContent", () => ({ children }) => <div>{children}</div>);
jest.mock("@/components/glass/GlassCard", () => ({ children }) => <div>{children}</div>);

jest.mock("sonner", () => ({
  toast: { error: jest.fn(), success: jest.fn() },
}));

describe("EmailInbox", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockImplementation((url) => {
      if (url === "/email/threads" || url.startsWith("/email/threads?")) {
        return Promise.resolve({
          data: {
            threads: [],
            status: {
              enabled: false,
              is_configured: false,
              message: "Email is not configured. Add SMTP settings under Settings → Integrations.",
            },
          },
        });
      }
      return Promise.resolve({ data: {} });
    });
  });

  it("shows disabled banner when email is not configured", async () => {
    render(<EmailInbox />);

    await waitFor(() => {
      expect(
        screen.getByText(
          "Email is not configured. Add SMTP settings under Settings → Integrations.",
        ),
      ).toBeInTheDocument();
    });

    expect(screen.queryByRole("button", { name: /send email/i })).not.toBeInTheDocument();
  });
});
