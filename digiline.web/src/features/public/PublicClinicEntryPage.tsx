import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useParams } from "react-router-dom";
import { ApiError, getApiErrorMessage } from "../../api/httpClient";
import { usePublicQueueRealtime, type RealtimeStatus } from "../../realtime";
import {
  Button,
  Card,
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
        <span className="eyebrow">{clinic.name}</span>
        <h1>Join the queue.<br />Keep your day.</h1>
        <p>Book your place in a live clinic session and follow your token from anywhere.</p>
        <p>All times: {clinic.timeZoneId ?? "Asia/Kolkata"}</p>
      </header>
      <section className="public-sessions" aria-labelledby="sessions-title">
        <div>
          <span className="eyebrow">Bookings available</span>
          <h2 id="sessions-title">Live sessions and bookings</h2>
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
                <div>
                  <span className="public-live-label"><span aria-hidden="true" /> {bookingStatusMessage(session, clinic.timeZoneId)}{session.status === "Paused" ? " · Doctor paused" : ""}</span>
                  <h3>{session.name}</h3>
                  <div className="public-session-timing">
                    <p><strong>Clinic time:</strong> {formatClinicTime(session.startTime, clinic.timeZoneId)} – {formatClinicTime(session.endTime, clinic.timeZoneId)}</p>
                    <p><strong>Booking starts:</strong> {formatClinicTime(session.bookingOpenTime, clinic.timeZoneId)}</p>
                  </div>
                </div>
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
                <Button disabled={!session.isBookingOpen} onClick={() => setSelected(session)}>Join queue</Button>
                {!session.isBookingOpen && (session.status === "Running" || session.status === "Paused") && (
                  <div className="token-recovery-action">
                    <span>Already joined this session?</span>
                    <Button variant="secondary" onClick={() => setRecoverySession(session)}>Find my token</Button>
                  </div>
                )}
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
function bookingStatusMessage(session: PublicSessionSummary, timeZoneId?: string) {
  if (session.isBookingOpen) return "Booking is currently open";
  if (Date.now() < Date.parse(session.bookingOpenTime))
    return `Booking opens at ${formatClinicTime(session.bookingOpenTime, timeZoneId)}`;
  return "Booking has closed for this session";
}

function PublicShell({
  children,
  clinicName,
  compact = false,
}: {
  children: React.ReactNode;
  clinicName?: string;
  compact?: boolean;
}) {
  return (
    <main className={`public-entry${compact ? " public-entry-home" : ""}`}>
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
  return <div className="booking-backdrop">
    <Card className="booking-panel" role="dialog" aria-modal="true" aria-labelledby="recovery-title">
      <div className="booking-heading"><div><span className="eyebrow">Already joined?</span><h2 id="recovery-title">Find my token</h2></div>
        <button type="button" aria-label="Close" onClick={onCancel}>×</button></div>
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
    </Card>
  </div>;
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
    <div className="booking-backdrop">
      <Card
        className="booking-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="booking-title"
      >
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
          <button type="button" aria-label="Close" onClick={onCancel}>
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
      </Card>
    </div>
  );
}

function ActiveTokenScreen({
  token,
  clinicName,
  timeZoneId,
  onLeave,
}: {
  token: StoredToken;
  clinicName?: string;
  timeZoneId?: string;
  onLeave: () => void;
}) {
  const [state, setState] = useState<PublicTokenState | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
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
      <PublicShell clinicName={clinicName}>
        <LoadingState message="Loading your token…" />
      </PublicShell>
    );
  if (error || !state)
    return (
      <PublicShell clinicName={clinicName}>
        <ErrorState
          message={error || "This token is unavailable."}
          onRetry={() => void refresh()}
        />
        <Button variant="ghost" onClick={onLeave}>
          Return to clinic
        </Button>
      </PublicShell>
    );
  return (
    <PublicShell clinicName={clinicName}>
      <section className="token-screen">
        <div className="token-live">
          <span className={`realtime-state realtime-${realtime}`}>
            <i aria-hidden="true" />
            {realtimeLabels[realtime]}
          </span>
          <span>Updated {formatClinicTime(state.updatedAt, timeZoneId)} · {timeZoneId ?? "Asia/Kolkata"}</span>
        </div>
        <Card className="patient-token-card">
          <span>Your token</span>
          <strong>#{state.tokenNumber}</strong>
          <StatusBadge status={state.status} />
        </Card>
        <div className="patient-metrics">
          <Card>
            <span>Now serving</span>
            <strong>
              {state.currentTokenNumber ? `#${state.currentTokenNumber}` : "—"}
            </strong>
          </Card>
          <Card>
            <span>Patients ahead</span>
            <strong>{state.patientsAhead ?? "—"}</strong>
          </Card>
          <Card>
            <span>Estimated wait</span>
            <strong>
              {state.estimatedWaitMinutes === null
                ? "—"
                : `${state.estimatedWaitMinutes} min`}
            </strong>
          </Card>
        </div>
        {state.isPaused && (
          <div className="pause-notice" role="status">
            The session is paused. Your place in the queue is safe.
          </div>
        )}
        <Button variant="ghost" onClick={onLeave}>
          View clinic sessions
        </Button>
      </section>
    </PublicShell>
  );
}
