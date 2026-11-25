import { SessionStateBadges } from "../../shared/ui/StatusBadge";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { ApiError, getApiErrorMessage } from "../../api/httpClient";
import { useQueueRealtime } from "../../realtime";
import {
  Button,
  Card,
  ConfirmationDialog,
  DataTable,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
  SelectField,
  StatusBadge,
  TextField,
  useToast,
  type TableColumn,
} from "../../shared/ui";
import type {
  DoctorSessionDetails,
  QueueToken,
  ReceptionDashboard as ReceptionData,
} from "../../shared/types/domain";
import { receptionApi } from "./receptionApi";
import { useBookingCutoffRefresh } from "../../shared/useBookingCutoffRefresh";
import { formatClinicTime } from "../../shared/clinicTime";
import { patientName, patientDetails } from "../../shared/queuePatient";
import { type QueueFilter } from "../../shared/queueView";
import "../doctor/doctor-dashboard.css";
import "./reception-dashboard.css";

interface Confirmation {
  title: string;
  message: string;
  label: string;
  key: string;
  run: () => Promise<unknown>;
}

export function ReceptionDashboard() {
  const toast = useToast();
  const [data, setData] = useState<ReceptionData | null>(null);
  const [selectedDetails, setSelectedDetails] =
    useState<DoctorSessionDetails | null>(null);
  const [sessionPage, setSessionPage] = useState(1);
  const [page, setPage] = useState(1);
  const version = useRef(0);
  const [selectedId, setSelectedId] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [age, setAge] = useState("");
  const [queueFilter, setQueueFilter] = useState<QueueFilter>("Active");
  const [busy, setBusy] = useState<string | null>(null);
  const busyRef = useRef<string | null>(null);
  const [actionError, setActionError] = useState("");
  const [issuedToken, setIssuedToken] = useState<number | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const load = useCallback(
    async (signal?: AbortSignal) => {
      const currentVersion = ++version.current;
      const next = await receptionApi.today(signal, sessionPage);
      if (signal?.aborted || currentVersion !== version.current) return;
      const id = next.sessions.some((x) => x.session.id === selectedId)
        ? selectedId
        : (next.sessions[0]?.session.id ?? "");
      const selected = id
        ? await receptionApi.getSession(
            id,
            id === selectedId ? page : 1,
            queueFilter,
            signal,
          )
        : null;
      if (signal?.aborted || currentVersion !== version.current) return;
      setData(next);
      setSelectedId(id);
      setSelectedDetails(selected);
      setPage(selected?.page ?? 1);
      setSessionPage(next.page ?? 1);
    },
    [selectedId, sessionPage, page, queueFilter],
  );
  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal)
      .catch((caught) => {
        if (!controller.signal.aborted) setError(getApiErrorMessage(caught));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [attempt, load]);
  const refresh = useCallback(async () => {
    if (!busyRef.current) {
      try {
        await load();
      } catch (caught) {
        setActionError(getApiErrorMessage(caught));
      }
    }
  }, [load]);
  useBookingCutoffRefresh(
    data?.sessions.map((item) => item.session) ?? [],
    () => {
      void refresh();
    },
  );
  const realtime = useQueueRealtime(selectedId || null, refresh);
  async function run(key: string, action: () => Promise<unknown>) {
    if (busyRef.current) return false;
    busyRef.current = key;
    setBusy(key);
    setActionError("");
    try {
      await action();
      await load();
      if (key === "walk-in")
        toast.success("Walk-in patient added successfully.");
      else if (key.startsWith("late-"))
        toast.success("Patient marked as arrived.");
      else if (key.startsWith("cancel-")) toast.success("Token cancelled.");
      else toast.success("Queue updated.");
      return true;
    } catch (caught) {
      const message = getApiErrorMessage(caught);
      if (
        caught instanceof ApiError &&
        caught.status === 409 &&
        /already/i.test(message)
      )
        toast.warning(message);
      else toast.error(message);
      return false;
    } finally {
      busyRef.current = null;
      setBusy(null);
    }
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    const clean = name.trim();
    if (
      !clean ||
      !selectedId ||
      age === "" ||
      !Number.isInteger(Number(age)) ||
      Number(age) < 0 ||
      Number(age) > 130
    )
      return;
    const succeeded = await run("walk-in", async () => {
      const result = await receptionApi.walkIn(
        selectedId,
        clean,
        phone.trim(),
        Number(age),
      );
      setIssuedToken(result.tokenNumber);
    });
    if (succeeded) {
      setName("");
      setPhone("");
      setAge("");
    }
  }
  if (loading)
    return <LoadingState message="Loading today’s Reception workspace…" />;
  if (error)
    return (
      <ErrorState
        message={error}
        onRetry={() => {
          setLoading(true);
          setError("");
          setAttempt((value) => value + 1);
        }}
      />
    );
  if (!data || data.sessions.length === 0)
    return (
      <EmptyState
        title="No sessions today"
        message="Today’s scheduled clinic sessions will appear here."
      />
    );
  const selected =
    data.sessions.find((x) => x.session.id === selectedId) ?? data.sessions[0];
  const { session, queue } =
    selectedDetails?.session.id === selected.session.id
      ? selectedDetails
      : selected;
  const current = session.queueSummary.currentToken;
  const columns: TableColumn<QueueToken>[] = [
    {
      key: "token",
      header: "Token",
      render: (x) => <strong>#{x.tokenNumber}</strong>,
    },
    {
      key: "patient",
      header: "Patient",
      render: (x) => (
        <div>
          <strong>{patientName(x)}</strong>
        </div>
      ),
    },
    { key: "age", header: "Age", render: (x) => x.patientAge ?? "—" },
    { key: "phone", header: "Phone", render: (x) => x.phoneNumber || "—" },
    {
      key: "source",
      header: "Type",
      render: (x) => (x.walkInName ? "Walk-in" : "Booked"),
    },
    {
      key: "status",
      header: "Status",
      render: (x) => (
        <>
          <StatusBadge status={x.status} />
          {x.status === "Skipped" && <small>Awaiting arrival</small>}
        </>
      ),
    },
    {
      key: "registered",
      header: "Registered at",
      render: (x) => formatClinicTime(x.createdAt, data?.timeZoneId),
    },
    {
      key: "called",
      header: "Called at",
      render: (x) =>
        x.calledAt ? formatClinicTime(x.calledAt, data?.timeZoneId) : "—",
    },
    {
      key: "late-arrival",
      header: "Late arrival",
      render: (x) =>
        x.lateArrivedAt
          ? formatClinicTime(x.lateArrivedAt, data?.timeZoneId)
          : "—",
    },
    {
      key: "completed",
      header: "Completed at",
      render: (x) =>
        x.completedAt ? formatClinicTime(x.completedAt, data?.timeZoneId) : "—",
    },
    {
      key: "actions",
      header: "Actions",
      align: "right",
      render: (x) => (
        <div className="row-actions">
          {x.status === "Skipped" && (
            <Button
              variant="ghost"
              disabled={!!busy || session.status !== "Running"}
              onClick={() =>
                setConfirmation({
                  title: `Patient arrived for token #${x.tokenNumber}?`,
                  message:
                    "This skipped patient will rejoin the callable queue after all waiting patients.",
                  label: "Patient arrived",
                  key: `late-${x.id}`,
                  run: () => receptionApi.markLate(session.id, x.id),
                })
              }
            >
              Patient arrived
            </Button>
          )}
          {x.status === "Waiting" && (
            <Button
              variant="ghost"
              disabled={!!busy || session.status === "Closed"}
              onClick={() =>
                setConfirmation({
                  title: `Cancel token #${x.tokenNumber}?`,
                  message: "The patient will be removed from the active queue.",
                  label: "Cancel token",
                  key: `cancel-${x.id}`,
                  run: () => receptionApi.cancel(session.id, x.id),
                })
              }
            >
              Cancel
            </Button>
          )}
        </div>
      ),
    },
  ];
  return (
    <div className="doctor-dashboard reception-dashboard">
      <PageHeader
        eyebrow="Reception workspace"
        title="Reception desk"
        description={
          <>
            {data.clinicName || session.clinicName} · Today’s sessions,
            arrivals, and queue support
          </>
        }
        context={
          <span className={`realtime-state realtime-${realtime}`}>
            <i aria-hidden="true" />
            {realtime === "connected" ? "Live" : realtime}
            {realtime !== "connected" && (
              <button
                type="button"
                onClick={() =>
                  window.dispatchEvent(new Event("digiline-retry-realtime"))
                }
              >
                Retry connection
              </button>
            )}
          </span>
        }
      />
      <section
        className="reception-session-toolbar"
        aria-label="Session selection"
      >
        <div className="reception-session-controls">
          <SelectField
            label="Session"
            value={selectedId}
            onChange={(event) => {
              version.current++;
              setPage(1);
              setSelectedId(event.target.value);
            }}
          >
            {data.sessions.map((x) => (
              <option key={x.session.id} value={x.session.id}>
                {x.session.doctorName} · {x.session.name} ·{" "}
                {formatClinicTime(x.session.startTime, data.timeZoneId)}
              </option>
            ))}
          </SelectField>
          <nav aria-label="Session pages">
            <Button
              aria-label="Previous sessions"
              disabled={sessionPage <= 1}
              onClick={() => setSessionPage((p) => p - 1)}
            >
              Previous
            </Button>
            <span>Page {sessionPage}</span>
            <Button
              aria-label="Next sessions"
              disabled={
                sessionPage * (data.pageSize ?? 10) >=
                (data.totalCount ?? data.sessions.length)
              }
              onClick={() => setSessionPage((p) => p + 1)}
            >
              Next
            </Button>
          </nav>
        </div>
      </section>
      {actionError && (
        <div className="action-error" role="alert">
          {actionError}
          <button aria-label="Dismiss error" onClick={() => setActionError("")}>
            ×
          </button>
        </div>
      )}
      <section
        className="reception-overview"
        aria-label="Active session and queue summary"
      >
        <Card padding="compact" className="reception-active-panel">
          <div className="reception-session-copy">
            <span className="eyebrow">Active session</span>
            <h2>{session.doctorName}</h2>
            <p>
              {session.name} ·{" "}
              {formatClinicTime(session.startTime, data.timeZoneId)} –{" "}
              {formatClinicTime(session.endTime, data.timeZoneId)} ·{" "}
              {data.timeZoneId ?? "Asia/Kolkata"}
            </p>
            <div className="reception-session-state">
              <SessionStateBadges session={session} />
              <span>
                {session.status === "Paused"
                  ? "Doctor temporarily unavailable"
                  : session.status === "Running"
                    ? "Doctor available"
                    : "Doctor not currently consulting"}
              </span>
            </div>
          </div>
          <div className="reception-current-token">
            <span className="eyebrow">Current token</span>
            <strong>{current ? `#${current.tokenNumber}` : "—"}</strong>
            <small>
              {current
                ? `${patientName(current)} · ${patientDetails(current)}`
                : "No active call"}
            </small>
          </div>
        </Card>
        <dl className="reception-queue-summary" aria-label="Queue summary">
          <div>
            <dt>Waiting</dt>
            <dd>{session.queueSummary.waitingCount}</dd>
            <dd className="summary-detail">Ready to be called</dd>
          </div>
          <div>
            <dt>Late</dt>
            <dd>{session.queueSummary.lateCount}</dd>
            <dd className="summary-detail">Needs assistance</dd>
          </div>
          <div>
            <dt>Total</dt>
            <dd>{session.queueSummary.totalCount}</dd>
            <dd className="summary-detail">Tokens issued</dd>
          </div>
        </dl>
      </section>
      <section className="queue-section">
        <div className="section-heading">
          <div>
            <span className="eyebrow">Patient assistance</span>
            <h2>Queue details</h2>
          </div>
          <span>{session.queueSummary.totalCount} tokens</span>
        </div>
        <label>
          Show tokens{" "}
          <select
            value={queueFilter}
            onChange={(event) => {
              version.current++;
              setPage(1);
              setQueueFilter(event.target.value as QueueFilter);
            }}
          >
            <option>Active</option>
            <option>Completed</option>
            <option>Other finished</option>
          </select>
        </label>
        {queue.length ? (
          <Card padding="none">
            <DataTable
              caption={`Reception queue for ${session.name}`}
              columns={columns}
              rows={queue}
              page={selectedDetails?.page ?? page}
              totalCount={selectedDetails?.totalCount ?? queue.length}
              onPageChange={setPage}
              pageKey={`${session.id}:${queueFilter}:${queueFilter === "Completed" ? queue.filter((token) => token.status === "Completed").length : ""}`}
              getRowKey={(x) => x.id}
            />
          </Card>
        ) : (
          <EmptyState
            title="The queue is clear"
            message="New booked and walk-in patients will appear here."
          />
        )}
      </section>
      <Card className="walk-in-card">
        <div>
          <span className="eyebrow">New arrival</span>
          <h2>Register a walk-in</h2>
          <p>Issue the next token for the selected session.</p>
        </div>
        <form onSubmit={submit}>
          <TextField
            label="Patient name"
            value={name}
            maxLength={200}
            onChange={(e) => setName(e.target.value)}
            required
          />
          <TextField
            label="Phone (optional)"
            type="tel"
            maxLength={20}
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
          />
          <TextField
            label="Age"
            type="number"
            min={0}
            max={130}
            step={1}
            value={age}
            onChange={(event) => setAge(event.target.value)}
            required
          />
          <Button
            busy={busy === "walk-in"}
            disabled={!!busy || !session.isBookingOpen}
          >
            Issue token
          </Button>
        </form>
        {!session.isBookingOpen && (
          <small className="booking-warning">
            Walk-in booking is closed for this session.
          </small>
        )}
      </Card>
      <ConfirmationDialog
        open={!!confirmation}
        title={confirmation?.title ?? ""}
        message={confirmation?.message ?? ""}
        confirmLabel={confirmation?.label}
        destructive={confirmation?.key.startsWith("cancel") ?? false}
        busy={busy === confirmation?.key}
        onCancel={() => {
          if (!busy) setConfirmation(null);
        }}
        onConfirm={() => {
          if (confirmation)
            void run(confirmation.key, confirmation.run).then((ok) => {
              if (ok) setConfirmation(null);
            });
        }}
      />
      {issuedToken !== null && (
        <div className="token-confirmation" role="status">
          <Card>
            <span className="eyebrow">Walk-in registered</span>
            <strong>Token #{issuedToken}</strong>
            <p>Share this number with the patient.</p>
            <Button onClick={() => setIssuedToken(null)}>Done</Button>
          </Card>
        </div>
      )}
    </div>
  );
}
