import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { StatusBadge } from "./StatusBadge";
import type { SessionStatus } from "../types/domain";

it.each<[SessionStatus, string, string]>([
  ["Scheduled", "Scheduled", "neutral"], ["BookingOpen", "Booking open", "info"],
  ["Running", "Running", "success"], ["Paused", "Paused", "warning"], ["Closed", "Completed", "neutral"],
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
