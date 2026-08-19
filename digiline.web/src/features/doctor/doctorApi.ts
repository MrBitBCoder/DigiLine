import { request } from '../../api/httpClient'
import type { DoctorSessionDetails, DoctorSessionSummary, QueueToken, QueueTokenStatus, SessionStatus } from '../../shared/types/domain'

const sessionStatuses: SessionStatus[] = ['Scheduled', 'Scheduled', 'BookingOpen', 'Running', 'Paused', 'Closed']
const tokenStatuses: QueueTokenStatus[] = ['Waiting', 'Waiting', 'Called', 'InConsultation', 'Completed', 'Skipped', 'Late', 'Cancelled', 'NoShow']
const sessionStatus = (value: SessionStatus | number) => typeof value === 'number' ? sessionStatuses[value] : value
const tokenStatus = (value: QueueTokenStatus | number) => typeof value === 'number' ? tokenStatuses[value] : value
const normalizeToken = (token: QueueToken): QueueToken => ({ ...token, status: tokenStatus(token.status) })
const normalizeSummary = (session: DoctorSessionSummary): DoctorSessionSummary => ({
  ...session,
  status: sessionStatus(session.status),
  queueSummary: { ...session.queueSummary, currentToken: session.queueSummary.currentToken ? normalizeToken(session.queueSummary.currentToken) : null },
})
const normalizeDetails = (details: DoctorSessionDetails): DoctorSessionDetails => ({ session: normalizeSummary(details.session), queue: details.queue.map(normalizeToken) })

export const doctorApi = {
  listSessions: async (signal?: AbortSignal) => (await request<DoctorSessionSummary[]>('/api/sessions', { signal })).map(normalizeSummary),
  getSession: async (sessionId: string, signal?: AbortSignal) => normalizeDetails(await request<DoctorSessionDetails>(`/api/sessions/${sessionId}`, { signal })),
  getActiveOrUpcoming: async (signal?: AbortSignal) => { const result = await request<DoctorSessionDetails | undefined>('/api/sessions/active-upcoming', { signal }); return result ? normalizeDetails(result) : undefined },
  callNext: (sessionId: string) => request<QueueToken>(`/api/queue/session/${sessionId}/call-next`, { method: 'POST' }),
  recall: (sessionId: string, tokenId: string) => request<QueueToken>(`/api/queue/session/${sessionId}/token/${tokenId}/recall`, { method: 'POST' }),
  skip: (sessionId: string, tokenId: string) => request<QueueToken>(`/api/queue/session/${sessionId}/token/${tokenId}/skip`, { method: 'POST' }),
  complete: (sessionId: string, tokenId: string) => request<QueueToken>(`/api/queue/session/${sessionId}/token/${tokenId}/complete`, { method: 'POST' }),
  markLate: (sessionId: string, tokenId: string) => request<QueueToken>(`/api/queue/session/${sessionId}/token/${tokenId}/late`, { method: 'POST' }),
  startSession: (sessionId: string) => request(`/api/sessions/${sessionId}/start`, { method: 'POST' }),
  pauseSession: (sessionId: string) => request(`/api/sessions/${sessionId}/pause`, { method: 'POST' }),
  resumeSession: (sessionId: string) => request(`/api/sessions/${sessionId}/resume`, { method: 'POST' }),
  closeSession: (sessionId: string) => request(`/api/sessions/${sessionId}/close`, { method: 'POST' }),
}
