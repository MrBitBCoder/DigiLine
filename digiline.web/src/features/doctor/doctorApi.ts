import { request } from "../../api/httpClient";
import type {
    PageResult,
    DoctorSessionDetails,
    DoctorSessionSummary,
    QueueToken,
    CompleteAndCallNextResult,
    QueueTokenStatus,
    SessionStatus,
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
const sessionStatus = (value: SessionStatus | number) =>
    typeof value === "number" ? sessionStatuses[value] : value;
const tokenStatus = (value: QueueTokenStatus | number) =>
    typeof value === "number" ? tokenStatuses[value] : value;
const normalizeToken = (token: QueueToken): QueueToken => ({
    ...token,
    status: tokenStatus(token.status),
});
const normalizeSummary = (
    session: DoctorSessionSummary,
): DoctorSessionSummary => ({
    ...session,
    status: sessionStatus(session.status),
    queueSummary: {
        ...session.queueSummary,
        currentToken: session.queueSummary.currentToken
            ? normalizeToken(session.queueSummary.currentToken)
            : null,
    },
});
const normalizeDetails = (
    details: DoctorSessionDetails,
): DoctorSessionDetails => ({
    ...details,
    session: normalizeSummary(details.session),
    queue: details.queue.map(normalizeToken),
});

export const doctorApi = {
    clinicTimeZone: () =>
        request<{ timeZoneId: string }>("/api/doctor/clinic-time-zone"),
    createSession: (details: {
        name: string;
        startTime: string;
        endTime: string;
        averageConsultationMinutes: number;
        bookingOpenTime: string | null;
    }) =>
        request<{ id: string }>("/api/doctor/sessions", {
            method: "POST",
            body: details,
        }),
    updateSession: (
        id: string,
        details: {
            name: string;
            startTime: string;
            endTime: string;
            averageConsultationMinutes: number;
            bookingOpenTime: string | null;
        },
    ) =>
        request<void>(`/api/doctor/sessions/${id}`, {
            method: "PUT",
            body: details,
        }),
    deleteSession: (id: string) =>
        request<void>(`/api/doctor/sessions/${id}`, { method: "DELETE" }),
    listSessions: async (signal?: AbortSignal, query = "") => {
        const page = await request<PageResult<DoctorSessionSummary>>(
            `/api/doctor/sessions?${query}`,
            { signal },
        );
        return { ...page, items: page.items.map(normalizeSummary) };
    },
    getSession: async (
        sessionId: string,
        signal?: AbortSignal,
        page = 1,
        filter = "Active",
    ) =>
        normalizeDetails(
            await request<DoctorSessionDetails>(
                `/api/doctor/sessions/${sessionId}?page=${page}&filter=${encodeURIComponent(filter)}`,
                { signal },
            ),
        ),
    getActiveOrUpcoming: async (signal?: AbortSignal) => {
        const result = await request<DoctorSessionDetails | undefined>(
            "/api/doctor/sessions/active-upcoming",
            { signal },
        );
        return result ? normalizeDetails(result) : undefined;
    },
    callNext: (sessionId: string) =>
        request<QueueToken>(`/api/doctor/sessions/${sessionId}/queue/call-next`, {
            method: "POST",
        }),
    skip: (sessionId: string, tokenId: string) =>
        request<QueueToken>(
            `/api/doctor/sessions/${sessionId}/tokens/${tokenId}/skip`,
            { method: "POST" },
        ),
    complete: (sessionId: string, tokenId: string) =>
        request<QueueToken>(
            `/api/doctor/sessions/${sessionId}/tokens/${tokenId}/complete`,
            { method: "POST" },
        ),
    completeAndCallNext: async (sessionId: string, tokenId: string) => {
        const result = await request<CompleteAndCallNextResult>(
            `/api/doctor/sessions/${sessionId}/tokens/${tokenId}/complete-and-call-next`,
            { method: "POST" },
        );
        return {
            completedToken: normalizeToken(result.completedToken),
            calledToken: result.calledToken
                ? normalizeToken(result.calledToken)
                : null,
        };
    },
    markLate: (sessionId: string, tokenId: string) =>
        request<QueueToken>(
            `/api/doctor/sessions/${sessionId}/tokens/${tokenId}/late`,
            { method: "POST" },
        ),
    startSession: (sessionId: string) =>
        request(`/api/doctor/sessions/${sessionId}/start`, { method: "POST" }),
    pauseSession: (sessionId: string) =>
        request(`/api/doctor/sessions/${sessionId}/pause`, { method: "POST" }),
    resumeSession: (sessionId: string) =>
        request(`/api/doctor/sessions/${sessionId}/resume`, { method: "POST" }),
    stopBooking: (sessionId: string) =>
        request(`/api/doctor/sessions/${sessionId}/stop-booking`, {
            method: "POST",
        }),
    completeSession: (sessionId: string) =>
        request(`/api/doctor/sessions/${sessionId}/complete`, {
            method: "POST",
            body: { confirmSkipped: true },
        }),
    closeSession: (sessionId: string) =>
        request(`/api/doctor/sessions/${sessionId}/close`, { method: "POST" }),
};
