import { getBookingState } from "../../shared/ui/statusPresentation";
import { SessionStateBadges } from "../../shared/ui/StatusBadge";
import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { useParams } from "react-router-dom";
import { ApiError, getApiErrorMessage } from "../../api/httpClient";
import { usePublicQueueRealtime, type RealtimeStatus } from "../../realtime";
import {
  Button,
  Card,
  ConfirmationDialog,
  EmptyState,
  ErrorState,
  LoadingState,
  StatusBadge,
  TextField,
  useToast,
} from "../../shared/ui";
import type {
  PublicBookingResult,
  PublicClinicDetails,
  PublicPatientProfile,
  PublicSessionSummary,
  PublicTokenState,
  VerifiedPatientAccess,
} from "../../shared/types/domain";
import { publicClinicApi } from "./publicClinicApi";
import { useBookingCutoffRefresh } from "../../shared/useBookingCutoffRefresh";
import { formatClinicTime } from "../../shared/clinicTime";
import "./public-clinic.css";

const TOKEN_STORAGE = "digiline.active-token";
interface StoredToken extends PublicBookingResult {
  accessToken: string;
  accessExpiresAt: string;
  clinicSlug: string;
}
const realtimeLabels: Record<RealtimeStatus, string> = {
  connecting: "Connecting",
  connected: "Live",
  reconnecting: "Reconnecting",
  disconnected: "Offline",
};
function readStored(slug: string): StoredToken | null {
  try {
    const value = JSON.parse(
      sessionStorage.getItem(TOKEN_STORAGE) ?? "null",
    ) as StoredToken | null;
    return value?.clinicSlug === slug &&
      new Date(value.accessExpiresAt).getTime() > Date.now()
      ? value
      : null;
  } catch {
    return null;
  }
}

export function PublicClinicEntryPage() {
  const { clinicSlug = "" } = useParams();
  const [clinic, setClinic] = useState<PublicClinicDetails | null>(null);
  const [selected, setSelected] = useState<PublicSessionSummary | null>(null);
  const [recoverySession, setRecoverySession] = useState<PublicSessionSummary | null>(null);
  const [storedToken, setStoredToken] = useState<StoredToken | null>(() =>
    readStored(clinicSlug),
  );
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const refreshAvailability = useCallback(() => {
    publicClinicApi.getBySlug(clinicSlug).then(next => {
      setClinic(next);
      setSelected(current => current && next.sessions.some(session => session.id === current.id && session.isBookingOpen && ["BookingOpen", "Running", "Paused"].includes(session.status)) ? current : null);
    }).catch(() => undefined);
  }, [clinicSlug]);
  useBookingCutoffRefresh(clinic?.sessions ?? [], refreshAvailability);
  useEffect(() => {
    if (storedToken) return;
    const timer = window.setInterval(refreshAvailability, 30_000);
    const onVisible = () => { if (document.visibilityState === "visible") refreshAvailability(); };
    document.addEventListener("visibilitychange", onVisible);
    return () => { window.clearInterval(timer); document.removeEventListener("visibilitychange", onVisible); };
  }, [refreshAvailability, storedToken]);
  useEffect(() => {
    const controller = new AbortController();
    publicClinicApi
      .getBySlug(clinicSlug, controller.signal)
      .then(setClinic)
      .catch((caught) => {
        if (!controller.signal.aborted)
          setError(
            caught instanceof ApiError && caught.status === 404
              ? "Clinic not found"
              : "We could not load this clinic.",
          );
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [clinicSlug, attempt]);
  if (storedToken)
    return (
      <ActiveTokenScreen
        token={storedToken}
        clinicName={clinic?.name}
        timeZoneId={clinic?.timeZoneId}
        session={clinic?.sessions.find((session) => session.id === storedToken.sessionId)}
        onLeave={() => {
          sessionStorage.removeItem(TOKEN_STORAGE);
          setStoredToken(null);
        }}
      />
    );
  if (loading)
    return (
      <PublicShell>
        <LoadingState message="Finding clinic sessions…" />
      </PublicShell>
    );
  if (error === "Clinic not found")
    return (
      <PublicShell>
        <ErrorState message="This clinic link is invalid or no longer available." />
      </PublicShell>
    );
  if (error || !clinic)
    return (
      <PublicShell>
        <ErrorState
          message={error || "We could not load this clinic."}
          onRetry={() => {
            setLoading(true);
            setError("");
            setAttempt((value) => value + 1);
          }}
        />
      </PublicShell>
    );
  const visibleSessions = clinic.sessions.filter(session => session.status === "Running" || session.status === "Paused" || session.status === "BookingOpen");
  return (
    <PublicShell clinicName={clinic.name} compact>
      <div className="public-home-content">
      <header className="public-hero">
        <span className="eyebrow">Digital queue</span>
        <h1>{clinic.name}</h1>
        <p>Join a clinic session and follow your token from anywhere.</p>
        <p className="public-time-zone">All times shown in {clinic.timeZoneId ?? "Asia/Kolkata"}</p>
      </header>
      <section className="public-sessions" aria-labelledby="sessions-title">
        <div>
          <span className="eyebrow">Today’s queue</span>
          <h2 id="sessions-title">Choose a session</h2>
          <p>Join an open queue or find a token you already received.</p>
        </div>
        {visibleSessions.length === 0 ? (
          <EmptyState
            title="No live sessions or open bookings"
            message="A session will appear here when booking opens or the session starts. Please check back shortly."
          />
        ) : (
          <div className="session-list">
            {visibleSessions.map((session) => (
              <Card key={session.id} className="public-session-card">
                <div className="public-session-heading">
                  <div className="session-badges"><SessionStateBadges session={session} /></div>
                  <h3>{session.name}</h3>
                  {getBookingState(session) === "notOpen" && <p>Booking opens at {formatClinicTime(session.bookingOpenTime, clinic.timeZoneId)}</p>}
                </div>
                <dl className="public-session-timing">
                  <div><dt>Clinic time</dt><dd>{formatClinicTime(session.startTime, clinic.timeZoneId)} – {formatClinicTime(session.endTime, clinic.timeZoneId)}</dd></div>
                  <div><dt>Booking starts</dt><dd>{formatClinicTime(session.bookingOpenTime, clinic.timeZoneId)}</dd></div>
                </dl>
                <dl>
                  <div>
                    <dt>Current token</dt>
                    <dd>
                      {session.currentTokenNumber
                        ? `#${session.currentTokenNumber}`
                        : "—"}
                    </dd>
                  </div>
                  <div>
                    <dt>Approx. queue</dt>
                    <dd>{session.approximateQueueSize}</dd>
                  </div>
                </dl>
                {session.isBookingOpen ? <Button onClick={() => setSelected(session)}>Join queue</Button>
                  : (session.status === "Running" || session.status === "Paused") ? (
                  <div className="booking-closed-action">
                    <div><strong>Already joined?</strong><span>New patients can no longer join. You can still check your existing token.</span></div>
                    <Button variant="secondary" onClick={() => setRecoverySession(session)}>Find my token</Button>
                  </div>
                ) : <Button disabled>Join queue</Button>}
              </Card>
            ))}
          </div>
        )}
      </section>
      </div>
      {selected && (
        <BookingPanel
          session={selected}
          timeZoneId={clinic.timeZoneId}
          onCancel={() => setSelected(null)}
          onBooked={(patientAccess, booking) => {
            const value = {
              ...booking,
              accessToken: patientAccess.accessToken,
              accessExpiresAt: patientAccess.expiresAt,
              clinicSlug,
            };
            sessionStorage.setItem(TOKEN_STORAGE, JSON.stringify(value));
            setStoredToken(value);
          }}
        />
      )}
      {recoverySession && (
        <RecoveryPanel session={recoverySession} onCancel={() => setRecoverySession(null)}
          onRecovered={(patientAccess, recovered) => {
            const value: StoredToken = { ...recovered, wasAlreadyBooked: true, accessToken: patientAccess.accessToken,
              accessExpiresAt: patientAccess.expiresAt, clinicSlug };
            sessionStorage.setItem(TOKEN_STORAGE, JSON.stringify(value));
            setStoredToken(value);
          }} />
      )}
    </PublicShell>
  );
}

function PublicShell({
  children,
  clinicName,
  compact = false,
  patient = false,
}: {
  children: React.ReactNode;
  clinicName?: string;
  compact?: boolean;
  patient?: boolean;
}) {
  return (
    <main className={`public-entry${compact ? " public-entry-home" : ""}${patient ? " public-entry-patient" : ""}`}>
      <div className="public-brand">
        <span className="brand-mark" aria-hidden="true">
          D
        </span>
        <span>{clinicName ?? "DigiLine"}</span>
      </div>
      {children}
      <footer>
        Private by design. Patient details are visible only after phone
        verification.
      </footer>
    </main>
  );
}

function RecoveryPanel({ session, onCancel, onRecovered }: {
  session: PublicSessionSummary;
  onCancel: () => void;
  onRecovered: (access: VerifiedPatientAccess, result: { sessionId: string; tokenId: string; tokenNumber: number }) => void;
}) {
  const toast = useToast();
  const [stage, setStage] = useState<"phone" | "code" | "profile">("phone");
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [challengeId, setChallengeId] = useState("");
  const [developmentCode, setDevelopmentCode] = useState("");
  const [access, setAccess] = useState<VerifiedPatientAccess | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function requestCode(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError("");
    try {
      const result = await publicClinicApi.requestVerification(phone);
      setChallengeId(result.challengeId); setDevelopmentCode(result.developmentCode ?? ""); setStage("code");
    } catch (caught) { toast.error(getApiErrorMessage(caught)); }
    finally { setBusy(false); }
  }
  async function verify(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError("");
    try { setAccess(await publicClinicApi.confirmVerification(challengeId, code)); setStage("profile"); }
    catch { setError("The verification code is invalid or expired."); }
    finally { setBusy(false); }
  }
  async function recover(profile: PublicPatientProfile) {
    if (!access) return;
    setBusy(true); setError("");
    try {
      const recovered = await publicClinicApi.recover(access.accessToken, session.id, profile.id);
      onRecovered(access, recovered);
      toast.success(`Token #${recovered.tokenNumber} restored.`);
    }
    catch (caught) { toast.error(caught instanceof ApiError && caught.status === 404
      ? "We couldn't find an active token for this patient in this session."
      : getApiErrorMessage(caught)); }
    finally { setBusy(false); }
  }
  return <PatientDialog labelledBy="recovery-title" onClose={onCancel}>
      <div className="booking-heading"><div><span className="eyebrow">Already joined?</span><h2 id="recovery-title">Find my token</h2></div>
        <button type="button" aria-label="Close find my token" autoFocus onClick={onCancel}>×</button></div>
      <p>This only finds a token already issued for {session.name}. It will not create a new token.</p>
      {stage === "phone" && <form onSubmit={requestCode}><TextField label="Phone number" type="tel" autoComplete="tel" value={phone} onChange={event => setPhone(event.target.value)} required />
        <Button busy={busy}>Send verification code</Button></form>}
      {stage === "code" && <form onSubmit={verify}><TextField label="6-digit code" inputMode="numeric" autoComplete="one-time-code" value={code} onChange={event => setCode(event.target.value)} required />
        {developmentCode && <p className="dev-code">Development code: <strong>{developmentCode}</strong></p>}<Button busy={busy}>Verify</Button></form>}
      {stage === "profile" && <div className="profile-step">
        {access?.profiles.length ? access.profiles.map(profile => <button type="button" disabled={busy} className="profile-choice" key={profile.id} onClick={() => void recover(profile)}>
          <strong>{profile.name}</strong><span>Age {profile.age}</span></button>) : <p>No patient profiles are registered for this phone number.</p>}
      </div>}
      {error && <p className="form-error" role="alert">{error}</p>}
  </PatientDialog>;
}

function BookingPanel({
  session,
  timeZoneId,
  onCancel,
  onBooked,
}: {
  session: PublicSessionSummary;
  timeZoneId?: string;
  onCancel: () => void;
  onBooked: (
    access: VerifiedPatientAccess,
    result: PublicBookingResult,
  ) => void;
}) {
  const toast = useToast();
  const [stage, setStage] = useState<"phone" | "code" | "profile" | "confirm">(
    "phone",
  );
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [challengeId, setChallengeId] = useState("");
  const [developmentCode, setDevelopmentCode] = useState("");
  const [access, setAccess] = useState<VerifiedPatientAccess | null>(null);
  const [profile, setProfile] = useState<PublicPatientProfile | null>(null);
  const [name, setName] = useState("");
  const [age, setAge] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submitPhone(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await publicClinicApi.requestVerification(phone);
      setChallengeId(result.challengeId);
      setDevelopmentCode(result.developmentCode ?? "");
      setStage("code");
    } catch (caught) {
      setError(getApiErrorMessage(caught));
    } finally {
      setBusy(false);
    }
  }
  async function submitCode(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await publicClinicApi.confirmVerification(
        challengeId,
        code,
      );
      setAccess(result);
      setStage("profile");
    } catch {
      setError("The verification code is invalid or expired.");
    } finally {
      setBusy(false);
    }
  }
  async function createProfile(event: FormEvent) {
    event.preventDefault();
    if (!access) return;
    setBusy(true);
    setError("");
    try {
      const created = await publicClinicApi.createProfile(
        access.accessToken,
        name,
        Number(age),
      );
      setProfile(created);
      setStage("confirm");
    } catch (caught) {
      setError(getApiErrorMessage(caught));
    } finally {
      setBusy(false);
    }
  }
  async function book() {
    if (!access || !profile) return;
    setBusy(true);
    setError("");
    try {
      const booking =
        await publicClinicApi.book(access.accessToken, session.id, profile.id);
      onBooked(
        access,
        booking,
      );
      toast.success(booking.wasAlreadyBooked ? "Your existing token was restored." : `You joined the queue with token #${booking.tokenNumber}.`);
    } catch (caught) {
      toast.error(getApiErrorMessage(caught));
      setBusy(false);
    }
  }
  return (
    <PatientDialog labelledBy="booking-title" onClose={onCancel}>
        <div className="booking-heading">
          <div>
            <span className="eyebrow">Join {session.name}</span>
            <h2 id="booking-title">
              {stage === "phone"
                ? "Verify your phone"
                : stage === "code"
                  ? "Enter your code"
                  : stage === "profile"
                    ? "Who is visiting?"
                    : "Confirm your place"}
            </h2>
          </div>
          <button type="button" aria-label="Close join queue" autoFocus onClick={onCancel}>
            ×
          </button>
        </div>
        {stage === "phone" && (
          <form onSubmit={submitPhone}>
            <TextField
              label="Phone number"
              type="tel"
              autoComplete="tel"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              required
              hint="We use this only to find your patient profiles."
            />
            <Button busy={busy}>Send verification code</Button>
          </form>
        )}
        {stage === "code" && (
          <form onSubmit={submitCode}>
            <TextField
              label="6-digit code"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              required
            />
            {developmentCode && (
              <p className="dev-code">
                Development code: <strong>{developmentCode}</strong>
              </p>
            )}
            <Button busy={busy}>Verify</Button>
          </form>
        )}
        {stage === "profile" && (
          <div className="profile-step">
            {access?.profiles.map((item) => (
              <button
                className="profile-choice"
                key={item.id}
                onClick={() => {
                  setProfile(item);
                  setStage("confirm");
                }}
              >
                <strong>{item.name}</strong>
                <span>Age {item.age}</span>
              </button>
            ))}
            <div className="profile-divider">
              <span>or create a profile</span>
            </div>
            <form onSubmit={createProfile}>
              <TextField
                label="Patient name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                required
              />
              <TextField
                label="Age"
                type="number"
                min="0"
                max="130"
                value={age}
                onChange={(event) => setAge(event.target.value)}
                required
              />
              <Button busy={busy}>Create and continue</Button>
            </form>
          </div>
        )}
        {stage === "confirm" && profile && (
          <div className="booking-confirm">
            <dl>
              <div>
                <dt>Patient</dt>
                <dd>{profile.name}</dd>
              </div>
              <div>
                <dt>Session</dt>
                <dd>{session.name}</dd>
              </div>
              <div>
                <dt>Time</dt>
                <dd>
                  {formatClinicTime(session.startTime, timeZoneId)} –{" "}
                  {formatClinicTime(session.endTime, timeZoneId)}
                </dd>
              </div>
            </dl>
            <Button busy={busy} onClick={() => void book()}>
              Confirm and join queue
            </Button>
          </div>
        )}
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
    </PatientDialog>
  );
}

const tokenProgress = ["Waiting", "Called", "InConsultation", "Completed"] as const;
function tokenStatusMessage(status: PublicTokenState["status"]) {
  switch (status) {
    case "Called": return { title: "Doctor is calling you now", message: "Please proceed to the consultation area." };
    case "InConsultation": return { title: "Consultation in progress", message: "Your token is currently with the doctor." };
    case "Completed": return { title: "Consultation completed", message: "Thank you for visiting." };
    case "Cancelled": return { title: "Token cancelled", message: "You have left the queue." };
    case "NoShow": return { title: "Token marked as no show", message: "This token is no longer active in the queue." };
    case "Late": return { title: "You are still in the queue", message: "Your token is marked late and remains available to staff." };
    case "Skipped": return { title: "Your token was skipped", message: "You can still cancel while waiting for staff to call you again." };
    default: return { title: "You are in the queue", message: "Keep this page open for live updates." };
  }
}

function ActiveTokenScreen({
  token,
  clinicName,
  timeZoneId,
  session,
  onLeave,
}: {
  token: StoredToken;
  clinicName?: string;
  timeZoneId?: string;
  session?: PublicSessionSummary;
  onLeave: () => void;
}) {
  const toast = useToast();
  const [state, setState] = useState<PublicTokenState | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [confirmCancellation, setConfirmCancellation] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const refresh = useCallback(async () => {
    try {
      setState(
        await publicClinicApi.getToken(
          token.accessToken,
          token.sessionId,
          token.tokenId,
        ),
      );
      setError("");
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.status === 401
          ? "Your secure patient session has expired."
          : "We could not refresh this token.",
      );
    } finally {
      setLoading(false);
    }
  }, [token]);
  const realtime = usePublicQueueRealtime(token.sessionId, refresh);
  useEffect(() => {
    const controller = new AbortController();
    publicClinicApi
      .getToken(
        token.accessToken,
        token.sessionId,
        token.tokenId,
        controller.signal,
      )
      .then(setState)
      .catch((caught) => {
        if (!controller.signal.aborted)
          setError(
            caught instanceof ApiError && caught.status === 401
              ? "Your secure patient session has expired."
              : "This token is no longer available.",
          );
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [token]);
  if (loading)
    return (
      <PublicShell clinicName={clinicName} patient>
        <LoadingState message="Loading your token…" />
      </PublicShell>
    );
  if (error || !state)
    return (
      <PublicShell clinicName={clinicName} patient>
        <ErrorState
          message={error || "This token is unavailable."}
          onRetry={() => void refresh()}
        />
        <Button variant="ghost" onClick={onLeave}>
          Return to clinic
        </Button>
      </PublicShell>
    );
  const canCancel = ["Waiting", "Late", "Skipped"].includes(state.status);
  async function cancelToken() {
    setCancelling(true);
    try {
      const cancelled = await publicClinicApi.cancelToken(
        token.accessToken,
        token.sessionId,
        token.tokenId,
      );
      setState(cancelled);
      setConfirmCancellation(false);
      toast.success(`Token #${cancelled.tokenNumber} was cancelled.`);
    } catch (caught) {
      toast.error(getApiErrorMessage(caught));
    } finally {
      setCancelling(false);
    }
  }
  const statusMessage = tokenStatusMessage(state.status);
  return (
    <PublicShell clinicName={clinicName} patient>
      <section className={`token-screen token-status-${state.status.toLowerCase()}`} aria-labelledby="patient-status-title">
        <header className="patient-status-heading" role="status" aria-live="polite">
          <span className="eyebrow">Your queue status</span>
          <h1 id="patient-status-title">{statusMessage.title}</h1>
          <p>{statusMessage.message}</p>
        </header>
        <Card className="patient-token-card" aria-label={`Your token #${state.tokenNumber}`}>
          <span>Your token number</span>
          <strong className="patient-token-number">#{state.tokenNumber}</strong>
          <StatusBadge status={state.status} />
        </Card>
        <div className="token-live">
          <span className={`realtime-state realtime-${realtime}`}>
            <i aria-hidden="true" />
            {realtimeLabels[realtime]}
          </span>
          <span>Updated {formatClinicTime(state.updatedAt, timeZoneId)} · {timeZoneId ?? "Asia/Kolkata"}</span>
        </div>
        <div className={`patient-metrics${["Waiting", "Late", "Skipped"].includes(state.status) ? " patient-metrics-waiting" : ""}`}>
          <Card className="now-serving-card">
            <span>Now serving</span>
            <strong>
              {state.currentTokenNumber ? `#${state.currentTokenNumber}` : "—"}
            </strong>
          </Card>
          {["Waiting", "Late", "Skipped"].includes(state.status) && <Card>
            <span>Patients ahead</span>
            <strong>{state.patientsAhead ?? "—"}</strong>
          </Card>}
          {["Waiting", "Late", "Skipped"].includes(state.status) && <Card>
            <span>Estimated wait</span>
            <strong>
              {state.estimatedWaitMinutes === null
                ? "—"
                : `${state.estimatedWaitMinutes} min`}
            </strong>
          </Card>}
        </div>
        {tokenProgress.includes(state.status as typeof tokenProgress[number]) && <nav className="token-progress" aria-label="Token progress">
          <ol>{tokenProgress.map((step, index) => {
            const current = tokenProgress.indexOf(state.status as typeof tokenProgress[number]);
            return <li key={step} className={index <= current ? "is-reached" : ""} aria-current={index === current ? "step" : undefined}>
              <span aria-hidden="true">{index + 1}</span>{step === "InConsultation" ? "In consultation" : step}
            </li>;
          })}</ol>
        </nav>}
        {session && <Card className="patient-session-details" padding="compact">
          <div><span>Session</span><strong>{session.name}</strong></div>
          <div><span>Clinic time</span><strong>{formatClinicTime(session.startTime, timeZoneId)} – {formatClinicTime(session.endTime, timeZoneId)}</strong></div>
          <div><span>Booking starts</span><strong>{formatClinicTime(session.bookingOpenTime, timeZoneId)}</strong></div>
        </Card>}
        {state.isPaused && (
          <div className="pause-notice" role="status">
            The session is paused. Your place in the queue is safe.
          </div>
        )}
        {state.status === "Cancelled" && (
          <div className="token-cancelled-notice" role="status">
            You left the queue. Token #{state.tokenNumber} will no longer be called.
          </div>
        )}
        <div className="token-actions">
          <Button variant="ghost" onClick={onLeave}>
            View clinic sessions
          </Button>
          {canCancel && (
            <Button variant="danger" onClick={() => setConfirmCancellation(true)}>
              Cancel token
            </Button>
          )}
        </div>
      </section>
      <ConfirmationDialog
        open={confirmCancellation}
        title="Cancel your token?"
        message={`You will leave the queue and token #${state.tokenNumber} will no longer be called.`}
        confirmLabel="Cancel token"
        cancelLabel="Keep my token"
        destructive
        busy={cancelling}
        onConfirm={() => void cancelToken()}
        onCancel={() => setConfirmCancellation(false)}
      />
    </PublicShell>
  );
}

function PatientDialog({ labelledBy, onClose, children }: { labelledBy: string; onClose: () => void; children: ReactNode }) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const returnFocusRef = useRef<HTMLElement | null>(document.activeElement instanceof HTMLElement ? document.activeElement : null);
  useEffect(() => {
    const dialog = dialogRef.current;
    const returnFocus = returnFocusRef.current;
    if (dialog && !dialog.open) dialog.showModal();
    return () => {
      if (dialog?.open) dialog.close();
      if (returnFocus?.isConnected) returnFocus.focus();
    };
  }, []);
  return <dialog ref={dialogRef} className="booking-dialog" aria-labelledby={labelledBy}
    onCancel={event => { event.preventDefault(); onClose(); }}>
    <Card className="booking-panel">{children}</Card>
  </dialog>;
}
