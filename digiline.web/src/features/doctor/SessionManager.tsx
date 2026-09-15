import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../../api/httpClient";
import { Button, ConfirmationDialog, DataTable, StatusBadge, type TableColumn } from "../../shared/ui";
import type { DoctorSessionSummary } from "../../shared/types/domain";
import { CreateSessionForm } from "./CreateSessionForm";
import { doctorApi } from "./doctorApi";
import { defaultClinicTimeZone, formatClinicTime } from "../../shared/clinicTime";

type DateFilter = "all" | "day" | "range";

function errorMessage(error: unknown) {
  if (error instanceof ApiError && typeof error.details === "object" && error.details && "detail" in error.details)
    return String(error.details.detail);
  return error instanceof Error ? error.message : "Session action failed.";
}

export function SessionManager({ onSelect, onDeleted }: { onSelect: (id: string) => Promise<void>; onDeleted: () => Promise<void> }) {
  const [sessions, setSessions] = useState<DoctorSessionSummary[]>([]);
  const [editing, setEditing] = useState<DoctorSessionSummary | null>(null);
  const [creating, setCreating] = useState(false);
  const [deleting, setDeleting] = useState<DoctorSessionSummary | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [timeZoneId, setTimeZoneId] = useState(defaultClinicTimeZone);
  const [zoneReady, setZoneReady] = useState(false);
  const [nameFilter, setNameFilter] = useState("");
  const [dateFilter, setDateFilter] = useState<DateFilter>("all");
  const [day, setDay] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [statusFilter, setStatusFilter] = useState("");
  const requestVersion = useRef(0);
  const query = new URLSearchParams({ page: String(page), name: nameFilter, ...(statusFilter ? { status: statusFilter } : {}),
    ...(dateFilter === "day" && day ? { fromDate: day, toDate: day } : dateFilter === "range" && fromDate && toDate ? { fromDate, toDate } : {}) }).toString();
  const load = useCallback(async (signal?: AbortSignal) => {
    const version = ++requestVersion.current;
    const result = await doctorApi.listSessions(signal, query);
    if (signal?.aborted || version !== requestVersion.current) return;
    setSessions(result.items); setTotalCount(result.totalCount); setPage(result.page); setError("");
  }, [query]);
  useEffect(() => { const controller = new AbortController(); const timer = setTimeout(() => { void load(controller.signal).catch(e => { if (!controller.signal.aborted) setError(errorMessage(e)); }); }, 200); return () => { clearTimeout(timer); controller.abort(); }; }, [load]);
  useEffect(() => { let active = true; doctorApi.clinicTimeZone().then(value => { if (active) { setTimeZoneId(value.timeZoneId); setZoneReady(true); } }).catch(() => { if (active) setError("Could not load the clinic time zone. Session scheduling is unavailable until it loads."); }); return () => { active = false; }; }, []);

  async function saved(id: string) {
    await load();
    setCreating(false);
    setEditing(null);
    await onSelect(id);
  }

  async function remove() {
    if (!deleting) return;
    setBusy(true);
    setError("");
    try {
      await doctorApi.deleteSession(deleting.id);
      setDeleting(null);
      await load();
      await onDeleted();
    } catch (caught) { setError(errorMessage(caught)); }
    finally { setBusy(false); }
  }

  const columns: TableColumn<DoctorSessionSummary>[] = [
    { key: "name", header: "Session", render: session => <strong>{session.name}</strong> },
    { key: "booking", header: "Booking opens", render: session => formatClinicTime(session.bookingOpenTime, session.timeZoneId ?? timeZoneId, true) },
    { key: "start", header: "Start", render: session => formatClinicTime(session.startTime, session.timeZoneId ?? timeZoneId, true) },
    { key: "end", header: "End", render: session => formatClinicTime(session.endTime, session.timeZoneId ?? timeZoneId, true) },
    { key: "status", header: "Status", render: session => <StatusBadge status={session.status} /> },
    { key: "total", header: "Total tokens", render: session => session.queueSummary.totalCount },
    { key: "completed", header: "Completed", render: session => session.queueSummary.completedCount },
    { key: "not-attended", header: "Not attended", render: session => session.queueSummary.notAttendedCount },
    { key: "actions", header: "Actions", align: "right", render: session => {
      const canManage = (session.status === "Scheduled" || session.status === "BookingOpen") && session.queueSummary.totalCount === 0 && new Date(session.startTime) > new Date();
      return <div className="session-manager-actions"><Button variant="ghost" onClick={() => void onSelect(session.id)}>View queue</Button><Button variant="secondary" disabled={!canManage} title={canManage ? undefined : "Only future sessions without tokens can be edited"} onClick={() => { setEditing(session); setCreating(false); }}>Edit</Button><Button variant="danger" disabled={!canManage} title={canManage ? undefined : "Only future sessions without tokens can be deleted"} onClick={() => setDeleting(session)}>Delete</Button></div>;
    } },
  ];

  const dateError = dateFilter === "day" && !day
    ? "Choose a session date."
    : dateFilter === "range" && (!fromDate || !toDate)
      ? "Choose both range dates."
      : dateFilter === "range" && toDate < fromDate
        ? "End date must be on or after start date."
        : "";

  return <section className="session-manager" aria-labelledby="sessions-heading">
    <div className="section-heading"><div><span className="eyebrow">Schedule</span><h2 id="sessions-heading">Your sessions</h2><p>All times: {timeZoneId}</p></div><Button variant="secondary" disabled={!zoneReady} onClick={() => { setCreating(true); setEditing(null); }}>Add session</Button></div>
    {error && <p role="alert" className="form-error">{error}</p>}
    {creating && <CreateSessionForm onSaved={saved} onCancel={() => setCreating(false)} timeZoneId={timeZoneId} />}
    {editing && <CreateSessionForm key={editing.id} session={editing} onSaved={saved} onCancel={() => setEditing(null)} timeZoneId={editing.timeZoneId ?? timeZoneId} />}
    {<div className="session-filters" role="group" aria-label="Filter sessions">
      <label>Search by name<input type="search" value={nameFilter} onChange={event => { setPage(1); setNameFilter(event.target.value); }} placeholder="Search session name" /></label>
      <label>Status<select value={statusFilter} onChange={event => { setPage(1); setStatusFilter(event.target.value); }}><option value="">All statuses</option>{["Scheduled", "BookingOpen", "Running", "Paused", "Closed"].map((status, index) => <option key={status} value={index + 1}>{status === "Closed" ? "Completed" : status}</option>)}</select></label>
      <label>Date filter<select value={dateFilter} disabled={!zoneReady} onChange={event => { setPage(1); setDateFilter(event.target.value as DateFilter); }}><option value="all">All dates</option><option value="day">Single day</option><option value="range">Date range</option></select></label>
      {dateFilter === "day" && <label>Session date<input type="date" value={day} disabled={!zoneReady} onChange={event => { setPage(1); setDay(event.target.value); }} /></label>}
      {dateFilter === "range" && <><label>From date<input type="date" value={fromDate} disabled={!zoneReady} onChange={event => { setPage(1); setFromDate(event.target.value); }} /></label><label>To date<input type="date" value={toDate} disabled={!zoneReady} onChange={event => { setPage(1); setToDate(event.target.value); }} /></label></>}
      <Button variant="ghost" onClick={() => { setPage(1); setStatusFilter(""); setNameFilter(""); setDateFilter("all"); setDay(""); setFromDate(""); setToDate(""); }}>Clear filters</Button>
    </div>}
    {dateError && sessions.length > 0 && <p role="alert" className="form-error">{dateError}</p>}
    {dateError ? null : <><DataTable caption="Your sessions" columns={columns} rows={sessions} getRowKey={session => session.id} page={page} totalCount={totalCount} onPageChange={setPage} />{totalCount === 0 && <p>No sessions match these filters. Add a session or clear the filters.</p>}</>}

    <ConfirmationDialog open={!!deleting} title={`Delete ${deleting?.name ?? "session"}?`} message="This permanently removes the session. This is only allowed before it starts and before any tokens are issued." confirmLabel="Delete session" destructive busy={busy} onCancel={() => { if (!busy) setDeleting(null); }} onConfirm={() => void remove()} />
  </section>;
}
