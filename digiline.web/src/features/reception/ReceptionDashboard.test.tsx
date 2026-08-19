import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { DoctorSessionDetails, ReceptionDashboard as Data } from '../../shared/types/domain'
import { ReceptionDashboard } from './ReceptionDashboard'

const api = vi.hoisted(() => ({ today: vi.fn(), walkIn: vi.fn(), cancel: vi.fn(), markLate: vi.fn(), recall: vi.fn() }))
const realtime = vi.hoisted(() => ({ callback: null as null | (() => Promise<void> | void) }))
vi.mock('./receptionApi', () => ({ receptionApi: api }))
vi.mock('../../realtime', () => ({ useQueueRealtime: (_id: string | null, callback: () => Promise<void> | void) => { realtime.callback = callback; return 'connected' } }))

function fixture(statuses = ['Waiting', 'Skipped', 'Called'] as const): Data {
  const queue = statuses.map((status, i) => ({ id: `token-${i}`, tokenNumber: i + 1, patientProfileId: null, walkInName: `Patient ${i + 1}`, phoneNumber: i ? null : '9999999999', status, createdAt: '2026-08-19T08:00:00Z', calledAt: status === 'Called' ? '2026-08-19T08:05:00Z' : null, completedAt: null }))
  const details: DoctorSessionDetails = { session: { id: 'session-1', clinicId: 'clinic-1', clinicName: 'Clinic', doctorId: 'doctor-1', doctorName: 'Dr Rao', name: 'Morning', bookingOpenTime: '2026-08-19T06:00:00Z', startTime: '2026-08-19T07:00:00Z', endTime: '2026-08-19T12:00:00Z', lastBookingTime: '2026-08-19T11:00:00Z', averageConsultationMinutes: 10, status: 'Running', isBookingOpen: true, queueSummary: { currentToken: queue.find(x => x.status === 'Called') ?? null, waitingCount: queue.filter(x => x.status === 'Waiting').length, lateCount: 0, totalCount: queue.length } }, queue }
  return { clinicName: 'Clinic', date: '2026-08-19', sessions: [details] }
}

describe('ReceptionDashboard', () => {
  beforeEach(() => { Object.values(api).forEach(mock => mock.mockReset()); realtime.callback = null })
  it('renders today summary and doctor availability', async () => { api.today.mockResolvedValue(fixture()); render(<ReceptionDashboard />); expect(await screen.findByText('Reception desk')).toBeInTheDocument(); expect(screen.getByText('Doctor available')).toBeInTheDocument(); expect(screen.getByText('9999999999')).toBeInTheDocument() })
  it('registers one walk-in despite a double click and shows its token', async () => { api.today.mockResolvedValue(fixture()); api.walkIn.mockResolvedValue({ tokenNumber: 12 }); const user = userEvent.setup(); render(<ReceptionDashboard />); await user.type(await screen.findByLabelText(/Patient name/), 'Asha'); const button = screen.getByRole('button', { name: 'Issue token' }); await user.dblClick(button); await waitFor(() => expect(api.walkIn).toHaveBeenCalledTimes(1)); expect(await screen.findByText('Token #12')).toBeInTheDocument() })
  it('confirms cancellation before changing the queue', async () => { api.today.mockResolvedValue(fixture()); api.cancel.mockResolvedValue({}); const user = userEvent.setup(); render(<ReceptionDashboard />); await user.click(await screen.findByRole('button', { name: 'Cancel' })); expect(api.cancel).not.toHaveBeenCalled(); await user.click(screen.getByRole('button', { name: 'Cancel token' })); await waitFor(() => expect(api.cancel).toHaveBeenCalledWith('session-1', 'token-0')) })
  it('refreshes after a realtime event', async () => { api.today.mockResolvedValue(fixture()); render(<ReceptionDashboard />); await screen.findByText('Reception desk'); await act(async () => { await realtime.callback?.() }); expect(api.today).toHaveBeenCalledTimes(2) })
})
