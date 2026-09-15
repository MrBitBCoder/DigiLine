import { useState, type FormEvent } from "react";
import { ApiError } from "../../api/httpClient";
import { Button, Card, TextField } from "../../shared/ui";
import { doctorApi } from "./doctorApi";
import type { DoctorSessionSummary } from "../../shared/types/domain";
import { clinicDateTimeInput, clinicInputToUtc, defaultClinicTimeZone } from "../../shared/clinicTime";

export function CreateSessionForm({ onSaved, session, onCancel, timeZoneId = defaultClinicTimeZone }: { onSaved: (id: string) => Promise<void>; session?: DoctorSessionSummary; onCancel?: () => void; timeZoneId?: string }) {
  const [name, setName] = useState(session?.name ?? "");
  const [bookingOpenTime, setBookingOpenTime] = useState(session ? clinicDateTimeInput(session.bookingOpenTime, timeZoneId) : "");
  const [startTime, setStartTime] = useState(session ? clinicDateTimeInput(session.startTime, timeZoneId) : "");
  const [endTime, setEndTime] = useState(session ? clinicDateTimeInput(session.endTime, timeZoneId) : "");
  const [averageMinutes, setAverageMinutes] = useState(session?.averageConsultationMinutes ?? 10);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    let start: Date, end: Date, bookingOpen: Date | null;
    try {
      start = new Date(clinicInputToUtc(startTime, timeZoneId));
      end = new Date(clinicInputToUtc(endTime, timeZoneId));
      bookingOpen = bookingOpenTime ? new Date(clinicInputToUtc(bookingOpenTime, timeZoneId)) : null;
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Invalid clinic time.");
      return;
    }
    if (start <= new Date() || end <= start) {
      setError("Choose a future start time and an end time after it.");
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
        startTime: start.toISOString(),
        endTime: end.toISOString(),
        averageConsultationMinutes: averageMinutes,
        bookingOpenTime: bookingOpen?.toISOString() ?? null,
      };
      const id = session ? (await doctorApi.updateSession(session.id, details), session.id)
        : (await doctorApi.createSession(details)).id;
      await onSaved(id);
      setName("");
      setBookingOpenTime("");
      setStartTime("");
      setEndTime("");
    } catch (caught) {
      const detail = caught instanceof ApiError && typeof caught.details === "object" && caught.details && "detail" in caught.details
        ? String(caught.details.detail) : null;
      setError(detail ?? (caught instanceof Error ? caught.message : "Could not create the session."));
    } finally {
      setBusy(false);
    }
  }

  return <Card className="create-session-card">
    <div><span className="eyebrow">Plan ahead</span><h2>{session ? "Edit session" : "Create session"}</h2><p>{session ? "Only a future session without tokens can be changed." : "Leave booking-open time blank to open booking now."} Times are in {timeZoneId}.</p></div>
    <form onSubmit={submit} className="create-session-form">
      <TextField label="Session name" value={name} onChange={event => setName(event.target.value)} required maxLength={120} />
      <TextField label="Booking opens (optional)" type="datetime-local" value={bookingOpenTime} onChange={event => setBookingOpenTime(event.target.value)} />
      <TextField label="Start time" type="datetime-local" value={startTime} onChange={event => setStartTime(event.target.value)} required />
      <TextField label="End time" type="datetime-local" value={endTime} onChange={event => setEndTime(event.target.value)} required />
      <TextField label="Average visit (minutes)" type="number" min={1} max={240} value={averageMinutes} onChange={event => setAverageMinutes(Number(event.target.value))} required />
      {error && <p role="alert" className="form-error">{error}</p>}
      <Button type="submit" busy={busy} disabled={busy}>{session ? "Save changes" : "Create session"}</Button>
      {onCancel && <Button type="button" variant="ghost" onClick={onCancel}>Cancel</Button>}
    </form>
  </Card>;
}
