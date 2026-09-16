import { request } from "../../api/httpClient";
import type {
  PublicBookingResult,
  PublicClinicDetails,
  PublicPatientProfile,
  PublicSessionSummary,
  PublicTokenState,
  PublicTokenRecoveryResult,
  QueueTokenStatus,
  SessionStatus,
  VerifiedPatientAccess,
} from "../../shared/types/domain";

const sessionStatuses: SessionStatus[] = [
  "Scheduled",
  "Scheduled",
  "BookingOpen",
  "Running",
  "Paused",
  "Closed",
];
const tokenStatuses: QueueTokenStatus[] = [
  "Waiting",
  "Waiting",
  "Called",
  "InConsultation",
  "Completed",
  "Skipped",
  "Late",
  "Cancelled",
  "NoShow",
];
const normalizeSession = (
  session: PublicSessionSummary,
): PublicSessionSummary => ({
  ...session,
  status:
    typeof session.status === "number"
      ? sessionStatuses[session.status]
      : session.status,
});
const patientHeaders = (accessToken: string) => ({
  "X-Patient-Access": accessToken,
});

export const publicClinicApi = {
  getBySlug: async (clinicSlug: string, signal?: AbortSignal) => {
    const clinic = await request<PublicClinicDetails>(
      `/api/public/clinics/${encodeURIComponent(clinicSlug)}`,
      { signal },
    );
    return { ...clinic, sessions: clinic.sessions.map(normalizeSession) };
  },
  requestVerification: (phoneNumber: string) =>
    request<{
      challengeId: string;
      expiresAt: string;
      developmentCode?: string;
    }>("/api/public/patients/verification", {
      method: "POST",
      body: { phoneNumber },
    }),
  confirmVerification: (challengeId: string, code: string) =>
    request<VerifiedPatientAccess>(
      "/api/public/patients/verification/confirm",
      { method: "POST", body: { challengeId, code } },
    ),
  createProfile: (accessToken: string, name: string, age: number) =>
    request<PublicPatientProfile>("/api/public/patients/profiles", {
      method: "POST",
      headers: patientHeaders(accessToken),
      body: { name, age },
    }),
  book: (accessToken: string, sessionId: string, patientProfileId: string) =>
    request<PublicBookingResult>("/api/public/queue/book", {
      method: "POST",
      headers: patientHeaders(accessToken),
      body: { sessionId, patientProfileId },
    }),
  recover: (accessToken: string, sessionId: string, patientProfileId: string) =>
    request<PublicTokenRecoveryResult>("/api/public/queue/recover", {
      method: "POST",
      headers: patientHeaders(accessToken),
      body: { sessionId, patientProfileId },
    }),
  getToken: async (
    accessToken: string,
    sessionId: string,
    tokenId: string,
    signal?: AbortSignal,
  ) => {
    const state = await request<PublicTokenState>(
      `/api/public/queue/sessions/${sessionId}/tokens/${tokenId}`,
      { headers: patientHeaders(accessToken), signal },
    );
    return {
      ...state,
      status:
        typeof state.status === "number"
          ? tokenStatuses[state.status]
          : state.status,
      sessionStatus:
        typeof state.sessionStatus === "number"
          ? sessionStatuses[state.sessionStatus]
          : state.sessionStatus,
    };
  },
  cancelToken: async (
    accessToken: string,
    sessionId: string,
    tokenId: string,
  ) => {
    const state = await request<PublicTokenState>(
      `/api/public/queue/sessions/${sessionId}/tokens/${tokenId}/cancel`,
      { method: "POST", headers: patientHeaders(accessToken) },
    );
    return {
      ...state,
      status:
        typeof state.status === "number"
          ? tokenStatuses[state.status]
          : state.status,
      sessionStatus:
        typeof state.sessionStatus === "number"
          ? sessionStatuses[state.sessionStatus]
          : state.sessionStatus,
    };
  },
};
