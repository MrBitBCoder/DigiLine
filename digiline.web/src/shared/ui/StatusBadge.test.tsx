import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import {
  StatusBadge,
  BookingStatusBadge,
  SessionStateBadges,
} from "./StatusBadge";
import type { SessionStatus } from "../types/domain";

it.each<[SessionStatus, string, string]>([
  ["Scheduled", "Scheduled", "neutral"],
  ["BookingOpen", "Booking open", "info"],
  ["Running", "Running", "success"],
  ["Paused", "Paused", "warning"],
  ["Closed", "Completed", "neutral"],
])("renders authoritative %s lifecycle status", (status, label, tone) => {
  render(<StatusBadge status={status} />);
  expect(screen.getByText(label)).toHaveClass(`status-${tone}`);
});
it("replaces the BookingOpen label and tone when the session becomes Closed", () => {
  const view = render(<StatusBadge status="BookingOpen" />);
  view.rerender(<StatusBadge status="Closed" />);
  expect(screen.getByText("Completed")).not.toHaveClass("status-info");
  expect(screen.queryByText("Booking open")).not.toBeInTheDocument();
});

it.each([
  [
    "Scheduled",
    false,
    "2099-01-01T00:00:00Z",
    false,
    "Booking not open",
    "neutral",
  ],
  ["Running", true, "2020-01-01T00:00:00Z", false, "Booking open", "info"],
  [
    "Running",
    false,
    "2020-01-01T00:00:00Z",
    true,
    "Booking stopped",
    "warning",
  ],
  ["Paused", false, "2020-01-01T00:00:00Z", true, "Booking stopped", "warning"],
  [
    "Running",
    false,
    "2020-01-01T00:00:00Z",
    false,
    "Booking closed",
    "neutral",
  ],
  ["Closed", true, "2020-01-01T00:00:00Z", false, "Booking closed", "neutral"],
] as const)(
  "presents %s availability independently",
  (status, isBookingOpen, bookingOpenTime, bookingHasStopped, label, tone) => {
    render(
      <BookingStatusBadge
        session={{ status, isBookingOpen, bookingOpenTime, bookingHasStopped }}
      />,
    );
    expect(screen.getByText(label)).toHaveClass(`status-${tone}`);
  },
);
it.each(["Running", "Paused", "Scheduled"] as const)(
  "keeps %s lifecycle separate from stopped booking",
  (status) => {
    render(
      <SessionStateBadges
        session={{
          status,
          isBookingOpen: false,
          bookingHasStopped: true,
          bookingOpenTime: "2020-01-01",
        }}
      />,
    );
    expect(screen.getByText(status)).toBeInTheDocument();
    expect(screen.getByText("Booking stopped")).toBeInTheDocument();
    expect(screen.queryByText("Completed")).not.toBeInTheDocument();
  },
);
it("shows booking-open once and never after completion even with stale availability", () => {
  const session = {
    status: "BookingOpen" as const,
    isBookingOpen: true,
    bookingOpenTime: "2020-01-01",
  };
  const view = render(<SessionStateBadges session={session} />);
  expect(screen.getAllByText("Booking open")).toHaveLength(1);
  view.rerender(
    <SessionStateBadges session={{ ...session, status: "Closed" }} />,
  );
  expect(screen.getByText("Completed")).toHaveClass("status-neutral");
  expect(screen.queryByText("Booking open")).not.toBeInTheDocument();
});
it("does not let the legacy BookingOpen lifecycle override closed availability", () => {
  render(
    <SessionStateBadges
      session={{
        status: "BookingOpen",
        isBookingOpen: false,
        bookingOpenTime: "2020-01-01",
      }}
    />,
  );
  expect(screen.getByText("Booking closed")).toBeInTheDocument();
  expect(screen.queryByText("Booking open")).not.toBeInTheDocument();
});
