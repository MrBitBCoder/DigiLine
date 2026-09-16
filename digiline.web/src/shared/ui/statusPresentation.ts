import type { QueueTokenStatus, SessionStatus } from "../types/domain";

export type StatusTone = "neutral" | "info" | "success" | "warning" | "danger";
export const statusPresentation: Record<SessionStatus | QueueTokenStatus, { label: string; tone: StatusTone }> = {
  Scheduled: { label: "Scheduled", tone: "neutral" },
  BookingOpen: { label: "Booking open", tone: "info" },
  Running: { label: "Running", tone: "success" },
  Paused: { label: "Paused", tone: "warning" },
  Closed: { label: "Completed", tone: "neutral" },
  Waiting: { label: "Waiting", tone: "info" },
  Called: { label: "Called", tone: "success" },
  InConsultation: { label: "In consultation", tone: "success" },
  Completed: { label: "Completed", tone: "neutral" },
  Skipped: { label: "Skipped", tone: "warning" },
  Late: { label: "Late", tone: "danger" },
  Cancelled: { label: "Cancelled", tone: "danger" },
  NoShow: { label: "No show", tone: "danger" },
};

export interface BookingAvailability {
  status: SessionStatus;
  isBookingOpen: boolean;
  bookingOpenTime: string;
  bookingStoppedAt?: string | null;
  bookingHasStopped?: boolean;
}
export type BookingState = "notOpen" | "open" | "stopped" | "closed";
export const bookingPresentation: Record<BookingState, { label: string; tone: StatusTone }> = {
  notOpen: { label: "Booking not open", tone: "neutral" },
  open: { label: "Booking open", tone: "info" },
  stopped: { label: "Booking stopped", tone: "warning" },
  closed: { label: "Booking closed", tone: "neutral" },
};

// Availability comes from the API. Time only explains a negative availability;
// it never opens booking or advances the session lifecycle.
export function getBookingState(session: BookingAvailability, now = Date.now()): BookingState {
  if (session.status === "Closed") return "closed";
  if (session.bookingStoppedAt || session.bookingHasStopped) return "stopped";
  if (session.isBookingOpen) return "open";
  if (now < Date.parse(session.bookingOpenTime)) return "notOpen";
  return "closed";
}
