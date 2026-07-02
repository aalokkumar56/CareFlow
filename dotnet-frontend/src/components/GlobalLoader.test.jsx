import React from "react";
import { render, screen, act } from "@testing-library/react";
import GlobalLoader from "@/components/GlobalLoader";
import {
  __resetGlobalLoaderForTests,
  incrementGlobalLoader,
} from "@/lib/globalLoader";

describe("GlobalLoader", () => {
  beforeEach(() => {
    jest.useFakeTimers();
    __resetGlobalLoaderForTests();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("renders when global loader becomes visible", () => {
    render(<GlobalLoader />);
    expect(screen.queryByTestId("global-loader")).not.toBeInTheDocument();

    act(() => {
      incrementGlobalLoader();
      jest.advanceTimersByTime(75);
    });

    expect(screen.getByTestId("global-loader")).toBeInTheDocument();
    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });
});
