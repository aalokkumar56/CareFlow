import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { api } from "@/lib/api";
import IntegrationsPanel from "@/components/settings/IntegrationsPanel";

jest.mock("@/lib/api");

jest.mock("@/hooks/usePermissions", () => () => ({
  can: () => true,
}));

jest.mock("@/components/glass/GlassCard", () => ({ children, ...props }) => (
  <div data-testid={props["data-testid"] || "glass-card"}>{children}</div>
));

jest.mock("sonner", () => ({
  toast: { error: jest.fn(), success: jest.fn() },
}));

describe("IntegrationsPanel", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockImplementation((url) => {
      if (url === "/settings/whatsapp") {
        return Promise.resolve({
          data: {
            provider: "MetaCloud",
            phone_number_id: "",
            enabled: false,
          },
        });
      }
      if (url === "/settings/sms") return Promise.resolve({ data: {} });
      if (url === "/settings/email") return Promise.resolve({ data: {} });
      return Promise.resolve({ data: {} });
    });
  });

  it("renders WhatsApp section and config fields in dialog", async () => {
    const user = userEvent.setup();
    render(<IntegrationsPanel />);

    await waitFor(() => {
      expect(screen.getByText("WhatsApp Business")).toBeInTheDocument();
    });

    const configureButtons = screen.getAllByRole("button", { name: /configure/i });
    await user.click(configureButtons[0]);

    await waitFor(() => {
      expect(screen.getByText("Phone Number ID")).toBeInTheDocument();
    });

    expect(screen.getByText("WABA ID")).toBeInTheDocument();
    expect(screen.getByText("Access Token")).toBeInTheDocument();
    expect(screen.getByText("Active — enable WhatsApp messaging")).toBeInTheDocument();
  });
});
