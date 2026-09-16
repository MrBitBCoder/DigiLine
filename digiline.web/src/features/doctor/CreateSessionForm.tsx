import { useState, type FormEvent } from "react";
import { getApiErrorMessage } from "../../api/httpClient";
import { Button, Card, TextField, useToast } from "../../shared/ui";
import { doctorApi } from "./doctorApi";
import type { DoctorSessionSummary } from "../../shared/types/domain";
import { clinicDateTimeInput, clinicInputToUtc, defaultClinicTimeZone } from "../../shared/clinicTime";

export function CreateSessionForm({ onSaved, session, onCancel, timeZoneId = defaultClinicTimeZone }: { onSaved: (id: string) => Promise<void>; session?: DoctorSessionSummary; onCancel?: () => void; timeZoneId?: string }) {
  const toast = useToast();
  const [name, setName] = useState(session?.name ?? "");
  const [bookingOpenTime, setBookingOpenTime] = useState(session ? clinicDateTimeInput(session.bookingOpenTime, timeZoneId) : "");
  const [startTime, setStartTime] = useState(session ? clinicDateTimeInput(session.startTime, timeZoneId) : "");
  const [endTime, setEndTime] = useState(session ? clinicDateTimeInput(session.endTime, timeZoneId) : "");
  const [averageMinutes, setAverageMinutes] = useState(session?.averageConsultationMinutes ?? 10);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const hasStarted = session?.status === "Running" || session?.status === "Paused" || session?.status === "Closed";

  async function submit(event: FormEvent) {
    event.preventDefault();
    let start: Date, end: Date, bookingOpen: Date | null;
    try {
      start = new Date(session && startTime === clinicDateTimeInput(session.startTime, timeZoneId) ? session.startTime : clinicInputToUtc(startTime, timeZoneId));
      end = new Date(session && endTime === clinicDateTimeInput(session.endTime, timeZoneId) ? session.endTime : clinicInputToUtc(endTime, timeZoneId));
      bookingOpen = bookingOpenTime ? new Date(session && bookingOpenTime === clinicDateTimeInput(session.bookingOpenTime, timeZoneId) ? session.bookingOpenTime : clinicInputToUtc(bookingOpenTime, timeZoneId)) : null;
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Invalid clinic time.");
      return;
    }
    if ((!session || start.getTime() !== new Date(session.startTime).getTime()) && !hasStarted && start <= new Date()) {
      setError("Choose a future start time when rescheduling.");
      return;
    }
    if (end <= start) {
      setError("End time must be after start time.");
      return;
    }
    if (bookingOpen && (Number.isNaN(bookingOpen.getTime()) || bookingOpen >= start)) {
      setError("Booking must open before the session starts.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      const details = {
        name: name.trim(),
        startTime: session && startTime === clinicDateTimeInput(session.startTime, timeZoneId) ? session.startTime : start.toISOString(),
        endTime: session && endTime === clinicDateTimeInput(session.endTime, timeZoneId) ? session.endTime : end.toISOString(),
        averageConsultationMinutes: averageMinutes,
        bookingOpenTime: session && bookingOpenTime === clinicDateTimeInput(session.bookingOpenTime, timeZoneId) ? session.bookingOpenTime : bookingOpen?.toISOString() ?? null,
      };
      const id = session ? (await doctorApi.updateSession(session.id, details), session.id)
        : (await doctorApi.createSession(details)).id;
      await onSaved(id);
      toast.success(session ? "Session updated." : "Session created.");
      setName("");
      setBookingOpenTime("");
      setStartTime("");
      setEndTime("");
    } catch (caught) {
      toast.error(getApiErrorMessage(caught, "Could not save the session."));
    } finally {
      setBusy(false);
    }
  }

  return <Card padding="compact" className="create-session-card">
    <div className="session-form-intro"><div><span className="eyebrow">{session ? "Session details" : "Plan ahead"}</span><h2>{session ? "Edit session" : "Create session"}</h2></div><p>{session ? "Sessions can be changed while no queue tokens have been assigned." : "Leave booking-open time blank to open booking now."} {hasStarted && "Booking-open and start times are fixed because this session has started."} Times are in {timeZoneId}.</p></div>
    <form onSubmit={submit} className="form-grid create-session-form">
      <TextField label="Session name" value={name} onChange={event => setName(event.target.value)} required maxLength={120} />
      <TextField label="Booking opens (optional)" type="datetime-local" disabled={hasStarted} value={bookingOpenTime} onChange={event => setBookingOpenTime(event.target.value)} />
      <TextField label="Start time" type="datetime-local" disabled={hasStarted} value={startTime} onChange={event => setStartTime(event.target.value)} required />
      <TextField label="End time" type="datetime-local" value={endTime} onChange={event => setEndTime(event.target.value)} required />
      <TextField label="Average visit (minutes)" type="number" min={1} max={240} value={averageMinutes} onChange={event => setAverageMinutes(Number(event.target.value))} required />
      {error && <p role="alert" className="form-error">{error}</p>}
      <div className="form-actions"><Button type="submit" busy={busy} disabled={busy}>{session ? "Save changes" : "Create session"}</Button>
      {onCancel && <Button type="button" variant="ghost" onClick={onCancel}>Cancel</Button>}</div>
    </form>
  </Card>;
}
