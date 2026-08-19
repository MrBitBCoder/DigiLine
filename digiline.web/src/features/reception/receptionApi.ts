import { request } from '../../api/httpClient'
import type { DoctorSessionDetails, QueueToken, QueueTokenStatus, ReceptionDashboard, SessionStatus } from '../../shared/types/domain'

const sessionStatuses: SessionStatus[] = ['Scheduled', 'Scheduled', 'BookingOpen', 'Running', 'Paused', 'Closed']
const tokenStatuses: QueueTokenStatus[] = ['Waiting', 'Waiting', 'Called', 'InConsultation', 'Completed', 'Skipped', 'Late', 'Cancelled', 'NoShow']
const token = (value: QueueToken): QueueToken => ({ ...value, status: typeof value.status === 'number' ? tokenStatuses[value.status] : value.status })
const details = (value: DoctorSessionDetails): DoctorSessionDetails => ({
  session: { ...value.session, status: typeof value.session.status === 'number' ? sessionStatuses[value.session.status] : value.session.status,
    queueSummary: { ...value.session.queueSummary, currentToken: value.session.queueSummary.currentToken ? token(value.session.queueSummary.currentToken) : null } },
  queue: value.queue.map(token),
})

export const receptionApi = {
  today: async (signal?: AbortSignal) => { const value = await request<ReceptionDashboard>('/api/reception/sessions/today', { signal }); return { ...value, sessions: value.sessions.map(details) } },
  walkIn: (sessionId: string, walkInName: string, phoneNumber?: string) => request<{ tokenNumber: number }>('/api/reception/queue/walk-in', { method: 'POST', body: JSON.stringify({ sessionId, walkInName, phoneNumber: phoneNumber || null }) }),
  cancel: (sessionId: string, tokenId: string) => request<QueueToken>(`/api/reception/sessions/${sessionId}/tokens/${tokenId}/cancel`, { method: 'POST' }),
  markLate: (sessionId: string, tokenId: string) => request<QueueToken>(`/api/reception/sessions/${sessionId}/tokens/${tokenId}/late`, { method: 'POST' }),
  recall: (sessionId: string, tokenId: string) => request<QueueToken>(`/api/reception/sessions/${sessionId}/tokens/${tokenId}/recall`, { method: 'POST' }),
}
