import type { QueueTokenStatus, SessionStatus } from "../types/domain";
import {
  bookingPresentation,
  getBookingState,
  statusPresentation,
  type BookingAvailability,
} from "./statusPresentation";
import "./design-system.css";

export function StatusBadge({
  status,
}: {
  status: QueueTokenStatus | SessionStatus;
}) {
  const { label, tone } = statusPresentation[status];
  return (
    <span className={`status-badge status-${tone}`}>
      <span aria-hidden="true" />
      {label}
    </span>
  );
}

export function BookingStatusBadge({
  session,
}: {
  session: BookingAvailability;
}) {
  const { label, tone } = bookingPresentation[getBookingState(session)];
  return (
    <span className={`status-badge status-${tone}`}>
      <span aria-hidden="true" />
      {label}
    </span>
  );
}

export function SessionStateBadges({
  session,
}: {
  session: BookingAvailability;
}) {
  if (session.status === "BookingOpen")
    return <BookingStatusBadge session={session} />;
  return (
    <>
      <StatusBadge status={session.status} />
      {session.status !== "Closed" && <BookingStatusBadge session={session} />}
    </>
  );
}
