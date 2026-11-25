import { statusPresentation } from "../../shared/ui/statusPresentation";
import { useCallback, useEffect, useRef, useState } from "react";
import { getApiErrorMessage } from "../../api/httpClient";
import {
  Button,
  ConfirmationDialog,
  DataTable,
  PageHeader,
  SelectField,
  StatusBadge,
  TextField,
  useToast,
  type TableColumn,
} from "../../shared/ui";
import type {
  DoctorSessionSummary,
  SessionStatus,
} from "../../shared/types/domain";
import { CreateSessionForm } from "./CreateSessionForm";
import { doctorApi } from "./doctorApi";
import {
  defaultClinicTimeZone,
  formatClinicTime,
} from "../../shared/clinicTime";

const sessionStatusValues: Record<SessionStatus, number> = {
  Scheduled: 1,
  BookingOpen: 2,
  Running: 3,
  Paused: 4,
  Closed: 5,
};

type DateFilter = "all" | "day" | "range";

function formatClinicDate(value: string, timeZoneId: string) {
  return new Intl.DateTimeFormat(undefined, {
    timeZone: timeZoneId,
    year: "numeric",
    month: "short",
    day: "numeric",
  }).format(new Date(value));
}

function formatSessionTimeRange(
  session: DoctorSessionSummary,
  fallbackTimeZone: string,
) {
  const zone = session.timeZoneId ?? fallbackTimeZone;
  const startDate = formatClinicDate(session.startTime, zone);
  const endDate = formatClinicDate(session.endTime, zone);
  return startDate === endDate
    ? `${formatClinicTime(session.startTime, zone)} – ${formatClinicTime(session.endTime, zone)}`
    : `${formatClinicTime(session.startTime, zone)} – ${formatClinicTime(session.endTime, zone, true)}`;
}

export function SessionManager({
  onSelect,
  onDeleted,
}: {
  onSelect: (id: string) => Promise<void>;
  onDeleted: () => Promise<void>;
}) {
  const toast = useToast();
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
  const query = new URLSearchParams({
    page: String(page),
    name: nameFilter,
    ...(statusFilter ? { status: statusFilter } : {}),
    ...(dateFilter === "day" && day
      ? { fromDate: day, toDate: day }
      : dateFilter === "range" && fromDate && toDate
        ? { fromDate, toDate }
        : {}),
  }).toString();
  const load = useCallback(
    async (signal?: AbortSignal) => {
      const version = ++requestVersion.current;
      const result = await doctorApi.listSessions(signal, query);
      if (signal?.aborted || version !== requestVersion.current) return;
      setSessions(result.items);
      setTotalCount(result.totalCount);
      setPage(result.page);
      setError("");
    },
    [query],
  );
  useEffect(() => {
    const controller = new AbortController();
    const refresh = () => {
      void load(controller.signal).catch((e) => {
        if (!controller.signal.aborted)
          setError(getApiErrorMessage(e, "Session action failed."));
      });
    };
    const visible = () => {
      if (document.visibilityState === "visible") refresh();
    };
    const timer = setTimeout(refresh, 200);
    const poll = setInterval(visible, 15_000);
    window.addEventListener("focus", visible);
    window.addEventListener("online", visible);
    document.addEventListener("visibilitychange", visible);
    return () => {
      controller.abort();
      clearTimeout(timer);
      clearInterval(poll);
      window.removeEventListener("focus", visible);
      window.removeEventListener("online", visible);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [load]);
  useEffect(() => {
    let active = true;
    doctorApi
      .clinicTimeZone()
      .then((value) => {
        if (active) {
          setTimeZoneId(value.timeZoneId);
          setZoneReady(true);
        }
      })
      .catch(() => {
        if (active)
          setError(
            "Could not load the clinic time zone. Session scheduling is unavailable until it loads.",
          );
      });
    return () => {
      active = false;
    };
  }, []);

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
      toast.success("Session deleted.");
    } catch (caught) {
      toast.error(getApiErrorMessage(caught, "Session action failed."));
    } finally {
      setBusy(false);
    }
  }

  const columns: TableColumn<DoctorSessionSummary>[] = [
    {
      key: "name",
      header: "Session",
      render: (session) => (
        <div className="session-name-cell">
          <strong>{session.name}</strong>
          <small>
            {session.doctorName} · {session.clinicName}
          </small>
        </div>
      ),
    },
    {
      key: "date",
      header: "Date",
      render: (session) =>
        formatClinicDate(session.startTime, session.timeZoneId ?? timeZoneId),
    },
    {
      key: "time",
      header: "Session time",
      render: (session) => formatSessionTimeRange(session, timeZoneId),
    },
    {
      key: "booking",
      header: "Booking starts",
      render: (session) => (
        <div className="session-booking-cell">
          <span>
            {formatClinicTime(
              session.bookingOpenTime,
              session.timeZoneId ?? timeZoneId,
            )}
          </span>
          <small>
            {formatClinicDate(
              session.bookingOpenTime,
              session.timeZoneId ?? timeZoneId,
            )}
          </small>
        </div>
      ),
    },
    {
      key: "status",
      header: "Status",
      render: (session) => <StatusBadge status={session.status} />,
    },
    {
      key: "attendance",
      header: "Attendance",
      render: (session) => (
        <div className="session-attendance">
          <strong>{session.queueSummary.totalCount} total</strong>
          <small>
            {session.queueSummary.completedCount} completed ·{" "}
            {session.queueSummary.notAttendedCount} not attended
          </small>
        </div>
      ),
    },
    {
      key: "actions",
      header: "Actions",
      align: "right",
      render: (session) => {
        const canManage = session.queueSummary.totalCount === 0;
        return (
          <div className="session-manager-actions">
            <Button variant="ghost" onClick={() => void onSelect(session.id)}>
              View queue
            </Button>
            <Button
              variant="secondary"
              disabled={!canManage}
              title={
                canManage
                  ? undefined
                  : "Sessions with assigned queue tokens cannot be edited"
              }
              onClick={() => {
                setEditing(session);
                setCreating(false);
              }}
            >
              Edit
            </Button>
            <Button
              variant="danger"
              disabled={!canManage}
              title={
                canManage
                  ? undefined
                  : "Sessions with assigned queue tokens cannot be deleted"
              }
              onClick={() => setDeleting(session)}
            >
              Delete
            </Button>
          </div>
        );
      },
    },
  ];

  const dateError =
    dateFilter === "day" && !day
      ? "Choose a session date."
      : dateFilter === "range" && (!fromDate || !toDate)
        ? "Choose both range dates."
        : dateFilter === "range" && toDate < fromDate
          ? "End date must be on or after start date."
          : "";

  return (
    <section className="session-manager" aria-labelledby="sessions-heading">
      <PageHeader
        eyebrow="Doctor workspace"
        title={<span id="sessions-heading">Sessions</span>}
        description={<>Manage your schedule · All times in {timeZoneId}</>}
        actions={
          <Button
            disabled={!zoneReady}
            onClick={() => {
              setCreating(true);
              setEditing(null);
            }}
          >
            Add session
          </Button>
        }
      />
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
      {creating && (
        <CreateSessionForm
          onSaved={saved}
          onCancel={() => setCreating(false)}
          timeZoneId={timeZoneId}
        />
      )}
      {editing && (
        <CreateSessionForm
          key={editing.id}
          session={editing}
          onSaved={saved}
          onCancel={() => setEditing(null)}
          timeZoneId={editing.timeZoneId ?? timeZoneId}
        />
      )}
      {
        <div
          className="session-filters"
          role="group"
          aria-label="Filter sessions"
        >
          <TextField
            label="Search by name"
            type="search"
            value={nameFilter}
            onChange={(event) => {
              setPage(1);
              setNameFilter(event.target.value);
            }}
            placeholder="Search session name"
          />
          <SelectField
            label="Status"
            value={statusFilter}
            onChange={(event) => {
              setPage(1);
              setStatusFilter(event.target.value);
            }}
          >
            <option value="">All statuses</option>
            {Object.entries(sessionStatusValues).map(([status, value]) => (
              <option key={status} value={value}>
                {statusPresentation[status as SessionStatus].label}
              </option>
            ))}
          </SelectField>
          <SelectField
            label="Date filter"
            value={dateFilter}
            disabled={!zoneReady}
            onChange={(event) => {
              setPage(1);
              setDateFilter(event.target.value as DateFilter);
            }}
          >
            <option value="all">All dates</option>
            <option value="day">Single day</option>
            <option value="range">Date range</option>
          </SelectField>
          {dateFilter === "day" && (
            <TextField
              label="Session date"
              type="date"
              value={day}
              disabled={!zoneReady}
              onChange={(event) => {
                setPage(1);
                setDay(event.target.value);
              }}
            />
          )}
          {dateFilter === "range" && (
            <>
              <TextField
                label="From date"
                type="date"
                value={fromDate}
                disabled={!zoneReady}
                onChange={(event) => {
                  setPage(1);
                  setFromDate(event.target.value);
                }}
              />
              <TextField
                label="To date"
                type="date"
                value={toDate}
                disabled={!zoneReady}
                onChange={(event) => {
                  setPage(1);
                  setToDate(event.target.value);
                }}
              />
            </>
          )}
          <Button
            variant="ghost"
            onClick={() => {
              setPage(1);
              setStatusFilter("");
              setNameFilter("");
              setDateFilter("all");
              setDay("");
              setFromDate("");
              setToDate("");
            }}
          >
            Clear filters
          </Button>
        </div>
      }
      {dateError && sessions.length > 0 && (
        <p role="alert" className="form-error">
          {dateError}
        </p>
      )}
      {dateError ? null : (
        <>
          <DataTable
            caption="Your sessions"
            columns={columns}
            rows={sessions}
            getRowKey={(session) => session.id}
            page={page}
            totalCount={totalCount}
            onPageChange={setPage}
          />
          {totalCount === 0 && (
            <p>
              No sessions match these filters. Add a session or clear the
              filters.
            </p>
          )}
        </>
      )}

      <ConfirmationDialog
        open={!!deleting}
        title={`Delete ${deleting?.name ?? "session"}?`}
        message="This permanently removes the session. This is only allowed when no queue tokens have been assigned."
        confirmLabel="Delete session"
        destructive
        busy={busy}
        onCancel={() => {
          if (!busy) setDeleting(null);
        }}
        onConfirm={() => void remove()}
      />
    </section>
  );
}
