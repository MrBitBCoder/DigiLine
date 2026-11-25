import { act, render } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { useBookingCutoffRefresh } from "./useBookingCutoffRefresh";

afterEach(() => vi.useRealTimers());

it("refreshes at the earlier booking cutoff", () => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-09-14T08:00:00Z"));
  const refresh = vi.fn();
  function Probe() {
    useBookingCutoffRefresh(
      [
        {
          bookingOpenTime: "2026-09-14T07:00:00Z",
          endTime: "2026-09-14T09:00:00Z",
          lastBookingTime: "2026-09-14T08:30:00Z",
          isBookingOpen: true,
          status: "BookingOpen",
        },
      ],
      refresh,
    );
    return null;
  }
  render(<Probe />);

  act(() => vi.advanceTimersByTime(29 * 60 * 1000));
  expect(refresh).not.toHaveBeenCalled();
  act(() => vi.advanceTimersByTime(60 * 1000));
  expect(refresh).toHaveBeenCalledTimes(1);
});

it("refreshes when scheduled booking opens", () => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-09-14T08:00:00Z"));
  const refresh = vi.fn();
  function Probe() {
    useBookingCutoffRefresh(
      [
        {
          bookingOpenTime: "2026-09-14T08:30:00Z",
          endTime: "2026-09-14T10:00:00Z",
          lastBookingTime: "2026-09-14T10:00:00Z",
          isBookingOpen: false,
          status: "BookingOpen",
        },
      ],
      refresh,
    );
    return null;
  }
  render(<Probe />);

  act(() => vi.advanceTimersByTime(29 * 60 * 1000));
  expect(refresh).not.toHaveBeenCalled();
  act(() => vi.advanceTimersByTime(60 * 1000));
  expect(refresh).toHaveBeenCalledTimes(1);
});
