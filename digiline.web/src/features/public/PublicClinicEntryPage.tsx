import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useParams } from "react-router-dom";
import { ApiError } from "../../api/httpClient";
import { usePublicQueueRealtime, type RealtimeStatus } from "../../realtime";
import {
  Button,
  Card,
  EmptyState,
  ErrorState,
  LoadingState,
  StatusBadge,
  TextField,
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
  const availableSessions = clinic.sessions.filter(session => session.isBookingOpen && ["BookingOpen", "Running", "Paused"].includes(session.status));
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
          <h2 id="sessions-title">Open for booking</h2>
        </div>
        {availableSessions.length === 0 ? (
          <EmptyState
            title="No sessions open for booking"
            message="A session will appear here when its booking window opens. Please check back shortly."
          />
        ) : (
          <div className="session-list">
            {availableSessions.map((session) => (
              <Card key={session.id} className="public-session-card">
                <div>
                  <span className="public-live-label"><span aria-hidden="true" /> {session.status === "Paused" ? "Booking open · Doctor paused" : "Booking open"}</span>
                  <h3>{session.name}</h3>
                  <p>
                    {formatClinicTime(session.startTime, clinic.timeZoneId)} –{" "}
                    {formatClinicTime(session.endTime, clinic.timeZoneId)}
                  </p>
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
                <Button onClick={() => setSelected(session)}>Join queue</Button>
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
    </PublicShell>
  );
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
      setError(message(caught));
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
      setError(message(caught));
    } finally {
      setBusy(false);
    }
  }
  async function book() {
    if (!access || !profile) return;
    setBusy(true);
    setError("");
    try {
      onBooked(
        access,
        await publicClinicApi.book(access.accessToken, session.id, profile.id),
      );
    } catch (caught) {
      setError(message(caught));
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

function message(caught: unknown) {
  if (
    caught instanceof ApiError &&
    typeof caught.details === "object" &&
    caught.details &&
    "detail" in caught.details
  )
    return String(caught.details.detail);
  return caught instanceof Error ? caught.message : "Please try again.";
}
