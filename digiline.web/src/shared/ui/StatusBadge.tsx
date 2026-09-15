import type { QueueTokenStatus, SessionStatus } from "../types/domain";
import "./design-system.css";

type Status = QueueTokenStatus | SessionStatus;
const labels: Record<Status, string> = {
  Scheduled: "Scheduled",
  BookingOpen: "Booking open",
  Running: "Running",
  Paused: "Paused",
  Closed: "Completed",
  Waiting: "Waiting",
  Called: "Called",
  InConsultation: "In consultation",
  Completed: "Completed",
  Skipped: "Skipped",
  Late: "Late",
  Cancelled: "Cancelled",
  NoShow: "No show",
};
const tones: Record<Status, string> = {
  Scheduled: "neutral",
  BookingOpen: "info",
  Running: "success",
  Paused: "warning",
  Closed: "neutral",
  Waiting: "info",
  Called: "success",
  InConsultation: "success",
  Completed: "neutral",
  Skipped: "warning",
  Late: "danger",
  Cancelled: "danger",
  NoShow: "danger",
};
export function StatusBadge({ status }: { status: Status }) {
  return (
    <span className={`status-badge status-${tones[status]}`}>
      <span aria-hidden="true" />
      {labels[status]}
    </span>
  );
}
