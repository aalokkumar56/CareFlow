import "@testing-library/jest-dom";

jest.mock("@/lib/utils", () => ({
  cn: (...classes) => classes.filter(Boolean).join(" "),
}));

jest.mock("@/lib/messageTemplates", () => ({
  renderMessageTemplate: (body) => body,
}));

jest.mock("@/components/ui/button", () => {
  const mockReact = require("react");
  return {
    Button: mockReact.forwardRef(({ children, ...props }, ref) =>
      mockReact.createElement(
        "button",
        { ref, type: props.type || "button", ...props },
        children,
      ),
    ),
  };
});

jest.mock("@/components/ui/textarea", () => ({
  Textarea: (props) => require("react").createElement("textarea", props),
}));

jest.mock("@/components/ui/input", () => ({
  Input: (props) => require("react").createElement("input", props),
}));

jest.mock("@/components/ui/switch", () => ({
  Switch: ({ checked, onCheckedChange, ...props }) =>
    require("react").createElement("input", {
      type: "checkbox",
      checked,
      onChange: (e) => onCheckedChange?.(e.target.checked),
      ...props,
    }),
}));

jest.mock("@/components/ui/dialog", () => {
  const mockReact = require("react");
  return {
    Dialog: ({ children, open }) =>
      open ? mockReact.createElement("div", { "data-testid": "dialog" }, children) : null,
    DialogContent: ({ children }) => mockReact.createElement("div", null, children),
    DialogHeader: ({ children }) => mockReact.createElement("div", null, children),
    DialogTitle: ({ children }) => mockReact.createElement("h2", null, children),
    DialogFooter: ({ children }) => mockReact.createElement("div", null, children),
  };
});

jest.mock("@/components/ui/select", () => {
  const mockReact = require("react");
  return {
    Select: ({ children, value }) =>
      mockReact.createElement("div", { "data-value": value }, children),
    SelectTrigger: ({ children }) =>
      mockReact.createElement("button", { type: "button" }, children),
    SelectValue: () => null,
    SelectContent: ({ children }) => mockReact.createElement("div", null, children),
    SelectItem: ({ children, value }) =>
      mockReact.createElement("option", { value }, children),
  };
});
