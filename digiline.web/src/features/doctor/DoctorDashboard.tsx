import { SessionStateBadges } from "../../shared/ui/StatusBadge";
import { useCallback, useEffect, useRef, useState } from "react";

import { ApiError, getApiErrorMessage } from "../../api/httpClient";

import {

  Button,

  Card,

  ConfirmationDialog,

  DataTable,

  EmptyState,

  ErrorState,

  LoadingState,

  PageHeader,

  StatusBadge,

  useToast,

  type TableColumn,

} from "../../shared/ui";

import type {

  DoctorSessionDetails,

  CompleteAndCallNextResult,

  QueueToken,

} from "../../shared/types/domain";

import { doctorApi } from "./doctorApi";

import { useQueueRealtime, type RealtimeStatus } from "../../realtime";

import { useBookingCutoffRefresh } from "../../shared/useBookingCutoffRefresh";

import { formatClinicTime } from "../../shared/clinicTime";

import { patientName, patientDetails } from "../../shared/queuePatient";

import { type QueueFilter } from "../../shared/queueView";

import "./doctor-dashboard.css";



function formatRange(start: string, end: string, timeZoneId?: string) {

  return `${formatClinicTime(start, timeZoneId)} – ${formatClinicTime(end, timeZoneId)}`;

}

function errorMessage(error: unknown) {

  if (error instanceof ApiError && error.status === 403)

    return "Your account cannot access this doctor workspace.";

  if (error instanceof ApiError && error.status >= 500)

    return "The clinic service is temporarily unavailable.";

  return "We could not load the latest session. Check your connection and try again.";

}

function actionErrorMessage(error: unknown) {
  return getApiErrorMessage(error, "The queue action could not be completed.");
}

function actionSuccessMessage(actionKey: string, result?: unknown) {
  if (actionKey === "start-session") return "Session started.";
  if (actionKey === "pause-session") return "Session paused.";
  if (actionKey === "resume-session") return "Session resumed.";
  if (actionKey === "stop-booking") return "Booking stopped. No new patients can join.";
  if (actionKey === "complete-session") return "Session completed.";
  if (actionKey === "call-next") return "Next patient called.";
  if (actionKey.startsWith("skip-")) return "Patient skipped.";
  if (actionKey.startsWith("complete-next-") && result && typeof result === "object") {
    const value = result as CompleteAndCallNextResult;
    return value.calledToken
      ? `Token #${value.completedToken.tokenNumber} completed. Token #${value.calledToken.tokenNumber} called.`
      : `Token #${value.completedToken.tokenNumber} completed. No patients are waiting.`;
  }
  if (actionKey.startsWith("complete-")) return "Consultation completed.";
  if (actionKey.startsWith("late-")) return "Patient marked as arrived.";
  return "Queue updated.";
}

const realtimeLabels: Record<RealtimeStatus, string> = {

  connecting: "Connecting…",

  connected: "Live",

  reconnecting: "Reconnecting…",

  disconnected: "Offline",

};

interface Confirmation {

  title: string;

  message: string;

  label: string;

  actionKey: string;

  run: () => Promise<unknown>;

}



export function DoctorDashboard({ initialSessionId }: { initialSessionId?: string | null } = {}) {

  const toast = useToast();

  const [details, setDetails] = useState<DoctorSessionDetails | null>(null);

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState("");

  const [attempt, setAttempt] = useState(0);

  const [pendingAction, setPendingAction] = useState<string | null>(null);

  const pendingActionRef = useRef<string | null>(null);

  const [actionError, setActionError] = useState("");

  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);

  const [queueFilter, setQueueFilter] = useState<QueueFilter>("Active");

  const [page, setPage] = useState(1);

  const refreshVersion = useRef(0);

  const retry = useCallback(() => {

    setLoading(true);

    setError("");

    setAttempt((value) => value + 1);

  }, []);

  const refreshFromRealtime = useCallback(async () => {

    const sessionId = details?.session.id;

    if (!sessionId || pendingActionRef.current) return;

    try {

      const version = ++refreshVersion.current;

      const next = await doctorApi.getSession(sessionId, undefined, page, queueFilter);

      if (version !== refreshVersion.current) return;

      setDetails(next); setPage(next.page ?? 1);

      setActionError("");

    } catch (caught) {

      setActionError(errorMessage(caught));

    }

  }, [details?.session.id, page, queueFilter]);

  const previousPage = useRef(`${page}:${queueFilter}`);
  useEffect(() => {
    const key = `${page}:${queueFilter}`;
    if (key === previousPage.current) return;
    previousPage.current = key;
    let active = true;
    queueMicrotask(() => { if (active) void refreshFromRealtime(); });
    return () => { active = false; };
  }, [page, queueFilter, refreshFromRealtime]);

  const realtimeStatus = useQueueRealtime(

    details?.session.id ?? null,

    refreshFromRealtime,

  );

  useBookingCutoffRefresh(details ? [details.session] : [], () => { void refreshFromRealtime(); });



  useEffect(() => {

    const controller = new AbortController();

    (initialSessionId

      ? doctorApi.getSession(initialSessionId, controller.signal)

      : doctorApi.getActiveOrUpcoming(controller.signal))

      .then((result) => setDetails(result ?? null))

      .catch((caught) => {

        if (!controller.signal.aborted) setError(errorMessage(caught));

      })

      .finally(() => {

        if (!controller.signal.aborted) setLoading(false);

      });

    return () => controller.abort();

  }, [attempt, initialSessionId]);



  if (loading)

    return <LoadingState message="Loading your active session and queue…" />;

  if (error) return <ErrorState message={error} onRetry={retry} />;

  if (!details)

    return (

      <div className="doctor-dashboard">

        <EmptyState title="No active or upcoming session" message="Create your next clinic session on the Sessions page." />

      </div>

    );



  const { session, queue } = details;

  const current = session.queueSummary.currentToken;

  const running = session.status === "Running";



  async function runAction(actionKey: string, action: () => Promise<unknown>) {

    if (pendingActionRef.current) return false;

    pendingActionRef.current = actionKey;

    setPendingAction(actionKey);

    setActionError("");

    try {

      const result = await action();

      refreshVersion.current++;

      const next = await doctorApi.getSession(session.id, undefined, page, queueFilter);

      setDetails(next); setPage(next.page ?? 1);

      toast.success(actionSuccessMessage(actionKey, result));

      return true;

    } catch (caught) {

      const message = actionErrorMessage(caught);

      if (caught instanceof ApiError && caught.status === 409 && /already/i.test(message)) toast.warning(message);

      else toast.error(message);

      return false;

    } finally {

      pendingActionRef.current = null;

      setPendingAction(null);

    }

  }



  function confirm(config: Confirmation) {

    if (!pendingActionRef.current) setConfirmation(config);

  }

  const bookingsStopped = !!session.bookingStoppedAt || session.bookingHasStopped === true;

  const unfinished = session.queueSummary.unfinishedCount ?? (session.queueSummary.waitingCount + session.queueSummary.lateCount + (current ? 1 : 0));

  const skipped = session.queueSummary.skippedCount ?? 0;
  const hasCallablePatient = session.queueSummary.waitingCount + session.queueSummary.lateCount > 0;

  const lifecycleControl = <div className="lifecycle-buttons">

    {session.status === "BookingOpen" && <Button disabled={!!pendingAction} onClick={() => void runAction("start-session", () => doctorApi.startSession(session.id))}>Start session</Button>}

    {running && <Button disabled={!!pendingAction} variant="secondary" onClick={() => void runAction("pause-session", () => doctorApi.pauseSession(session.id))}>Pause</Button>}

    {session.status === "Paused" && <Button disabled={!!pendingAction} onClick={() => void runAction("resume-session", () => doctorApi.resumeSession(session.id))}>Resume</Button>}

    {(running || session.status === "Paused") && !bookingsStopped && <Button disabled={!!pendingAction} variant="danger" onClick={() => confirm({ title: "Stop booking?", message: "No more public or walk-in bookings will be accepted. Continue serving the existing queue. Booking cannot reopen for this session.", label: "Stop booking", actionKey: "stop-booking", run: () => doctorApi.stopBooking(session.id) })}>Stop booking</Button>}

    {(running || session.status === "Paused") && <Button disabled={!!pendingAction || !bookingsStopped || unfinished > 0} title={!bookingsStopped ? "Stop booking first" : unfinished ? `Resolve ${unfinished} unfinished tokens first` : undefined} onClick={() => confirm({ title: `Complete ${session.name}?`, message: `This completes the session permanently. ${skipped} remaining skipped patients will be marked NoShow.`, label: "Complete session", actionKey: "complete-session", run: () => doctorApi.completeSession(session.id) })}>Complete session</Button>}

    {unfinished > 0 && session.status !== "Closed" && <span>{unfinished} tokens to resolve</span>}

  </div>;

  const queueColumns: TableColumn<QueueToken>[] = [

    {

      key: "token",

      header: "Token",

      render: (token) => (

        <strong className="token-number">#{token.tokenNumber}</strong>

      ),

    },

    {

      key: "patient",

      header: "Patient",

      render: (token) => <strong>{patientName(token)}</strong>,

    },

    {

      key: "age",

      header: "Age",

      render: (token) => token.patientAge ?? "—",

    },

    {

      key: "phone",

      header: "Phone",

      render: (token) => token.phoneNumber || "—",

    },

    {

      key: "source",

      header: "Type",

      render: (token) => (token.walkInName ? "Walk-in" : "Booked"),

    },

    {

      key: "status",

      header: "Status",

      render: (token) => <><StatusBadge status={token.status} />{token.status === "Skipped" && <small>Awaiting arrival</small>}</>,

    },

    {

      key: "registered",

      header: "Registered at",

      render: (token) => formatClinicTime(token.createdAt, session.timeZoneId),

    },

    { key: "called", header: "Called at", render: token => token.calledAt ? formatClinicTime(token.calledAt, session.timeZoneId) : "—" },

    { key: "late-arrival", header: "Late arrival", render: token => token.lateArrivedAt ? formatClinicTime(token.lateArrivedAt, session.timeZoneId) : "—" },

    { key: "completed", header: "Completed at", render: token => token.completedAt ? formatClinicTime(token.completedAt, session.timeZoneId) : "—" },

    {

      key: "actions",

      header: "Actions",

      align: "right",

      render: (token) => <div className="row-actions">

        {token.status === "Skipped" && (

          <Button

            variant="ghost"

            disabled={!running || !!pendingAction}

            onClick={() =>

              confirm({

                title: `Patient arrived for token #${token.tokenNumber}?`,

                message:

                  "This skipped patient will rejoin the callable queue after all waiting patients.",

                label: "Patient arrived",

                actionKey: `late-${token.id}`,

                run: () => doctorApi.markLate(session.id, token.id),

              })

            }

          >

            Patient arrived

          </Button>

        )}

        {token.id === current?.id && <span className="no-action">Current patient</span>}

        {token.status !== "Skipped" && token.id !== current?.id && <span className="no-action">—</span>}

      </div>,

    },

  ];

  return (

    <div className="doctor-dashboard">

      <PageHeader
        eyebrow={<>Doctor dashboard · {session.clinicName}</>}
        title={session.name}
        description={<>{formatRange(session.startTime, session.endTime, session.timeZoneId)} · {session.timeZoneId ?? "Asia/Kolkata"}</>}
        context={<div className="session-state compact-session-state"><SessionStateBadges session={session} /></div>}
        actions={lifecycleControl}
      />

      {actionError && (

        <div className="action-error" role="alert">

          <strong>Action not completed.</strong>

          <span>{actionError}</span>

          <button

            type="button"

            aria-label="Dismiss error"

            onClick={() => setActionError("")}

          >

            ×

          </button>

        </div>

      )}

      <section className="doctor-operations" aria-label="Current queue activity">
        <Card className={`current-patient-panel${current ? " has-current-patient" : ""}`}>
          <div className="current-patient-identity">
            <span className="eyebrow">{current ? "Current patient" : "Queue ready"}</span>
            <div className="current-patient-heading">
              <h2>{current ? `Token #${current.tokenNumber}` : "No patient called"}</h2>
              {current && <StatusBadge status={current.status} />}
            </div>
            <p>
              {current
                ? <><strong>{patientName(current)}</strong><span> · {patientDetails(current)}</span></>
                : hasCallablePatient
                  ? "Call the next eligible patient when you are ready."
                  : "No waiting or late patients are ready to call."}
            </p>
          </div>

          <div className="current-patient-actions" aria-label="Current patient actions">
            {!current && <Button
              className="primary-queue-action"
              busy={pendingAction === "call-next"}
              disabled={!running || !!pendingAction || !hasCallablePatient}
              onClick={() => void runAction("call-next", () => doctorApi.callNext(session.id))}
            >Call next</Button>}

            {current && (current.status === "Called" || current.status === "InConsultation") && <>
              {hasCallablePatient && <Button
                className="primary-queue-action"
                disabled={!!pendingAction}
                onClick={() => confirm({
                  title: `Complete token #${current.tokenNumber} and call next?`,
                  message: "This completes the current consultation and calls the next eligible patient in queue order.",
                  label: "Complete & call next",
                  actionKey: `complete-next-${current.id}`,
                  run: () => doctorApi.completeAndCallNext(session.id, current.id),
                })}
              >Complete &amp; call next</Button>}

              {!hasCallablePatient && <Button
                className="primary-queue-action"
                disabled={!!pendingAction}
                onClick={() => confirm({
                  title: `Complete token #${current.tokenNumber}?`,
                  message: "This marks the consultation complete and removes the patient from the active queue.",
                  label: "Complete consultation",
                  actionKey: `complete-${current.id}`,
                  run: () => doctorApi.complete(session.id, current.id),
                })}
              >Complete</Button>}

              {current.status === "Called" && <Button
                variant="secondary"
                disabled={!!pendingAction}
                onClick={() => confirm({
                  title: `Patient not here for token #${current.tokenNumber}?`,
                  message: "The patient will leave the active call. If they arrive later, use Patient arrived to place them after waiting patients.",
                  label: "Not here—skip",
                  actionKey: `skip-${current.id}`,
                  run: () => doctorApi.skip(session.id, current.id),
                })}
              >Not here—skip</Button>}

              {hasCallablePatient && <Button
                variant="secondary"
                disabled={!!pendingAction}
                onClick={() => confirm({
                  title: `Complete token #${current.tokenNumber}?`,
                  message: "This marks the consultation complete and removes the patient from the active queue.",
                  label: "Complete consultation",
                  actionKey: `complete-${current.id}`,
                  run: () => doctorApi.complete(session.id, current.id),
                })}
              >Complete</Button>}
            </>}
          </div>
        </Card>

        <dl className="queue-summary-strip" aria-label="Queue summary">
          <div><dt>Waiting</dt><dd>{session.queueSummary.waitingCount}</dd><dd className="summary-detail">Ready to call</dd></div>
          <div><dt>Late</dt><dd>{session.queueSummary.lateCount}</dd><dd className="summary-detail">After waiting patients</dd></div>
          <div><dt>Total queue</dt><dd>{session.queueSummary.totalCount}</dd><dd className="summary-detail">{session.averageConsultationMinutes} min average visit</dd></div>
        </dl>
      </section>

      <section className="queue-section" aria-labelledby="queue-title">

        <div className="section-heading">

          <div>

            <span className="eyebrow">Live order</span>

            <h2 id="queue-title">Patient queue</h2>

          </div>

          <div className="queue-meta">

            <span>

              {session.queueSummary.totalCount} {session.queueSummary.totalCount === 1 ? "token" : "tokens"}

            </span>

            <span className={`realtime-state realtime-${realtimeStatus}`}>

              <i aria-hidden="true" />

              {realtimeLabels[realtimeStatus]}

              {realtimeStatus !== "connected" && <button type="button" onClick={() => window.dispatchEvent(new Event("digiline-retry-realtime"))}>Retry connection</button>}

            </span>

          </div>

        </div>

        <label>Show tokens <select value={queueFilter} onChange={event => { refreshVersion.current++; setPage(1); setQueueFilter(event.target.value as QueueFilter); }}><option>Active</option><option>Completed</option><option>Other finished</option></select></label>

        {queue.length === 0 ? (

          <EmptyState

            title="The queue is clear"

            message="Booked and walk-in patients will appear here in token order."

          />

        ) : (

          <Card padding="none">

            <DataTable

              caption={`Ordered queue for ${session.name}`}

              columns={queueColumns}

              rows={queue}

              page={details.page ?? page} totalCount={details.totalCount ?? queue.length} onPageChange={setPage}

              pageKey={`${session.id}:${queueFilter}:${queueFilter === "Completed" ? queue.filter(token => token.status === "Completed").length : ""}`}

              getRowKey={(token) => token.id}

            />

          </Card>

        )}

      </section>

      <ConfirmationDialog

        open={!!confirmation}

        title={confirmation?.title ?? ""}

        message={confirmation?.message ?? ""}

        confirmLabel={confirmation?.label}

        destructive={

          confirmation

            ? confirmation.actionKey.startsWith("skip-") ||

              (confirmation.actionKey === "complete-session" || confirmation.actionKey === "stop-booking")

            : false

        }

        busy={pendingAction === confirmation?.actionKey}

        onCancel={() => {

          if (!pendingAction) setConfirmation(null);

        }}

        onConfirm={() => {

          if (confirmation)

            void runAction(confirmation.actionKey, confirmation.run).then(

              (succeeded) => {

                if (succeeded) setConfirmation(null);

              },

            );

        }}

      />

    </div>

  );

}
