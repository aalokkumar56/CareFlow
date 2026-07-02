import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { api } from "@/lib/api";
import WhatsAppChatPanel from "@/components/WhatsAppChatPanel";

jest.mock("@/lib/api");

jest.mock("sonner", () => ({
  toast: { error: jest.fn(), success: jest.fn() },
}));

describe("WhatsAppChatPanel", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockImplementation((url) => {
      if (String(url).includes("/conversations/")) {
        return Promise.resolve({
          data: {
            conversation: {
              id: "conv-1",
              name: "Test Patient",
              wa_phone: "919876543210",
              display_name: "Test Patient",
            },
            messages: [],
            patient: { name: "Test Patient" },
          },
        });
      }
      return Promise.resolve({ data: {} });
    });
  });

  it("renders disabled state with custom message when whatsappDisabled is true", async () => {
    render(
      <WhatsAppChatPanel
        conversationId="conv-1"
        whatsappDisabled
        disabledMessage="You do not have permission to send WhatsApp messages."
      />,
    );

    await waitFor(() => {
      expect(screen.getByText("Test Patient")).toBeInTheDocument();
    });

    expect(
      screen.getByText("You do not have permission to send WhatsApp messages."),
    ).toBeInTheDocument();

    expect(screen.getByPlaceholderText("WhatsApp sending is disabled")).toBeDisabled();
    expect(screen.getByRole("button", { name: "Attach file" })).toBeDisabled();
  });

  it("shows default disabled message when none provided", async () => {
    render(<WhatsAppChatPanel conversationId="conv-1" whatsappDisabled />);

    await waitFor(() => {
      expect(
        screen.getByText("WhatsApp is disabled or not configured."),
      ).toBeInTheDocument();
    });
  });

  it("shows empty state when no conversation or patient is provided", () => {
    render(<WhatsAppChatPanel whatsappDisabled />);
    expect(screen.getByText("No WhatsApp conversation available")).toBeInTheDocument();
  });
});
