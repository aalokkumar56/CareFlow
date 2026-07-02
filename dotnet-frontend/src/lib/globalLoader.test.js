import {
  __resetGlobalLoaderForTests,
  decrementGlobalLoader,
  getGlobalLoaderPendingCount,
  getGlobalLoaderVisible,
  incrementGlobalLoader,
  shouldSkipGlobalLoader,
} from "@/lib/globalLoader";

describe("globalLoader", () => {
  beforeEach(() => {
    jest.useFakeTimers();
    __resetGlobalLoaderForTests();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("shows loader after debounce when requests are in flight", () => {
    incrementGlobalLoader();
    expect(getGlobalLoaderVisible()).toBe(false);

    jest.advanceTimersByTime(75);
    expect(getGlobalLoaderVisible()).toBe(true);
  });

  it("hides loader when all requests complete", () => {
    incrementGlobalLoader();
    jest.advanceTimersByTime(75);
    expect(getGlobalLoaderVisible()).toBe(true);

    decrementGlobalLoader();
    expect(getGlobalLoaderVisible()).toBe(false);
  });

  it("does not flash loader for fast requests", () => {
    incrementGlobalLoader();
    decrementGlobalLoader();
    jest.advanceTimersByTime(75);
    expect(getGlobalLoaderVisible()).toBe(false);
  });

  it("tracks concurrent requests with a counter", () => {
    incrementGlobalLoader();
    incrementGlobalLoader();
    jest.advanceTimersByTime(75);
    expect(getGlobalLoaderPendingCount()).toBe(2);

    decrementGlobalLoader();
    expect(getGlobalLoaderVisible()).toBe(true);

    decrementGlobalLoader();
    expect(getGlobalLoaderVisible()).toBe(false);
  });

  it("respects skipGlobalLoader and auth paths", () => {
    expect(shouldSkipGlobalLoader({ skipGlobalLoader: true })).toBe(true);
    expect(shouldSkipGlobalLoader({ url: "/auth/login" })).toBe(true);
    expect(shouldSkipGlobalLoader({ url: "/auth/me" })).toBe(true);
    expect(shouldSkipGlobalLoader({ url: "/patients" })).toBe(false);
  });
});
