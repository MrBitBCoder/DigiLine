import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/httpClient'
import { Button, Card, ConfirmationDialog, DataTable, EmptyState, ErrorState, LoadingState, StatusBadge, type TableColumn } from '../../shared/ui'
import type { DoctorSessionDetails, QueueToken } from '../../shared/types/domain'
import { doctorApi } from './doctorApi'
import { useQueueRealtime, type RealtimeStatus } from '../../realtime'
import './doctor-dashboard.css'

function formatTime(value: string) { return new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(value)) }
function formatRange(start: string, end: string) { return `${formatTime(start)} – ${formatTime(end)}` }
function errorMessage(error: unknown) { if (error instanceof ApiError && error.status === 403) return 'Your account cannot access this doctor workspace.'; if (error instanceof ApiError && error.status >= 500) return 'The clinic service is temporarily unavailable.'; return 'We could not load the latest session. Check your connection and try again.' }
function actionErrorMessage(error: unknown) { if (error instanceof ApiError && typeof error.details === 'object' && error.details && 'detail' in error.details) return String(error.details.detail); if (error instanceof ApiError && error.status === 403) return 'You do not have permission to perform this action.'; return error instanceof Error ? error.message : 'The queue action could not be completed.' }
const realtimeLabels: Record<RealtimeStatus, string> = { connecting: 'Connecting…', connected: 'Live', reconnecting: 'Reconnecting…', disconnected: 'Offline' }
interface Confirmation { title: string; message: string; label: string; actionKey: string; run: () => Promise<unknown> }

export function DoctorDashboard() {
  const [details, setDetails] = useState<DoctorSessionDetails | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [pendingAction, setPendingAction] = useState<string | null>(null)
  const pendingActionRef = useRef<string | null>(null)
  const [actionError, setActionError] = useState('')
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null)
  const retry = useCallback(() => {
    setLoading(true)
    setError('')
    setAttempt(value => value + 1)
  }, [])
  const refreshFromRealtime = useCallback(async () => {
    const sessionId = details?.session.id
    if (!sessionId || pendingActionRef.current) return
    try { setDetails(await doctorApi.getSession(sessionId)); setActionError('') }
    catch (caught) { setActionError(errorMessage(caught)) }
  }, [details?.session.id])
  const realtimeStatus = useQueueRealtime(
    details?.session.id ?? null,
    refreshFromRealtime,
  )

  useEffect(() => {
    const controller = new AbortController()
    doctorApi.getActiveOrUpcoming(controller.signal)
      .then(result => setDetails(result ?? null))
      .catch(caught => { if (!controller.signal.aborted) setError(errorMessage(caught)) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [attempt])

  if (loading) return <LoadingState message="Loading your active session and queue…" />
  if (error) return <ErrorState message={error} onRetry={retry} />
  if (!details) return <EmptyState title="No active or upcoming session" message="Your next scheduled clinic session will appear here when one is available." />

  const { session, queue } = details
  const current = session.queueSummary.currentToken
  const running = session.status === 'Running'

  async function runAction(actionKey: string, action: () => Promise<unknown>) {
    if (pendingActionRef.current) return false
    pendingActionRef.current = actionKey
    setPendingAction(actionKey); setActionError('')
    try {
      await action()
      setDetails(await doctorApi.getSession(session.id))
      return true
    } catch (caught) {
      setActionError(actionErrorMessage(caught))
      return false
    } finally { pendingActionRef.current = null; setPendingAction(null) }
  }

  function confirm(config: Confirmation) { if (!pendingActionRef.current) setConfirmation(config) }
  const lifecycleControl = session.status === 'BookingOpen'
    ? <Button busy={pendingAction === 'start-session'} disabled={!!pendingAction} onClick={() => void runAction('start-session', () => doctorApi.startSession(session.id))}>Start session</Button>
    : session.status === 'Running'
      ? <div className="lifecycle-buttons"><Button variant="secondary" busy={pendingAction === 'pause-session'} disabled={!!pendingAction} onClick={() => void runAction('pause-session', () => doctorApi.pauseSession(session.id))}>Pause</Button><Button variant="danger" disabled={!!pendingAction} onClick={() => confirm({ title: `Close ${session.name}?`, message: 'Closing the session is final. No further queue actions or bookings will be available.', label: 'Close session', actionKey: 'close-session', run: () => doctorApi.closeSession(session.id) })}>Close</Button></div>
      : session.status === 'Paused'
        ? <div className="lifecycle-buttons"><Button busy={pendingAction === 'resume-session'} disabled={!!pendingAction} onClick={() => void runAction('resume-session', () => doctorApi.resumeSession(session.id))}>Resume</Button><Button variant="danger" disabled title="Resume the session before closing it">Close</Button></div>
        : <Button disabled title="Booking must be open before this session can start">Start session</Button>
  const queueColumns: TableColumn<QueueToken>[] = [
    { key: 'token', header: 'Token', render: token => <strong className="token-number">#{token.tokenNumber}</strong> },
    { key: 'patient', header: 'Patient', render: token => token.walkInName ?? 'Registered patient' },
    { key: 'source', header: 'Type', render: token => token.walkInName ? 'Walk-in' : 'Booked' },
    { key: 'status', header: 'Status', render: token => <StatusBadge status={token.status} /> },
    { key: 'arrival', header: 'Arrived', render: token => formatTime(token.createdAt) },
    { key: 'actions', header: 'Actions', align: 'right', render: token => token.status === 'Skipped' ? <Button variant="ghost" disabled={!running || !!pendingAction} onClick={() => confirm({ title: `Mark token #${token.tokenNumber} late?`, message: 'The patient will move into the late queue and can be called again after waiting patients.', label: 'Mark late', actionKey: `late-${token.id}`, run: () => doctorApi.markLate(session.id, token.id) })}>Mark late</Button> : <span className="no-action">—</span> },
  ]
  return <div className="doctor-dashboard">
    <header className="dashboard-heading"><div><span className="eyebrow">{session.clinicName}</span><h1>Good to see you, {session.doctorName}.</h1><p>{session.name} · {formatRange(session.startTime, session.endTime)}</p></div><div className="session-state"><StatusBadge status={session.status} /><span className={session.isBookingOpen ? 'booking-open' : 'booking-closed'}>{session.isBookingOpen ? 'Booking window open' : 'Booking window closed'}</span><div className="header-actions">{lifecycleControl}<Button busy={pendingAction === 'call-next'} disabled={!running || !!pendingAction || session.queueSummary.waitingCount + session.queueSummary.lateCount === 0} onClick={() => void runAction('call-next', () => doctorApi.callNext(session.id))}>Call next</Button></div></div></header>
    {actionError && <div className="action-error" role="alert"><strong>Action not completed.</strong><span>{actionError}</span><button type="button" aria-label="Dismiss error" onClick={() => setActionError('')}>×</button></div>}
    <section className="dashboard-metrics" aria-label="Queue summary">
      <Card className="current-token-card"><span className="metric-label">Current token</span><strong>{current ? `#${current.tokenNumber}` : '—'}</strong><small>{current ? (current.walkInName ?? 'Registered patient') : 'No patient is currently called'}</small></Card>
      <Card><span className="metric-label">Waiting</span><strong>{session.queueSummary.waitingCount}</strong><small>Ready to be called</small></Card>
      <Card><span className="metric-label">Late</span><strong>{session.queueSummary.lateCount}</strong><small>Patients to review</small></Card>
      <Card><span className="metric-label">Total queue</span><strong>{session.queueSummary.totalCount}</strong><small>{session.averageConsultationMinutes} min average visit</small></Card>
    </section>
    {current && (current.status === 'Called' || current.status === 'InConsultation') && <Card className="current-actions"><div><span className="eyebrow">Now serving</span><h2>Token #{current.tokenNumber}</h2><p>{current.walkInName ?? 'Registered patient'}</p></div><div className="action-buttons">{current.status === 'Called' && <Button variant="secondary" busy={pendingAction === `recall-${current.id}`} disabled={!!pendingAction} onClick={() => void runAction(`recall-${current.id}`, () => doctorApi.recall(session.id, current.id))}>Recall</Button>}<Button variant="secondary" disabled={!!pendingAction || current.status !== 'Called'} onClick={() => confirm({ title: `Skip token #${current.tokenNumber}?`, message: 'The patient will leave the active call and can then be marked late.', label: 'Skip token', actionKey: `skip-${current.id}`, run: () => doctorApi.skip(session.id, current.id) })}>Skip</Button><Button disabled={!!pendingAction} onClick={() => confirm({ title: `Complete token #${current.tokenNumber}?`, message: 'This marks the consultation complete and removes the patient from the active queue.', label: 'Complete consultation', actionKey: `complete-${current.id}`, run: () => doctorApi.complete(session.id, current.id) })}>Complete</Button></div></Card>}
    <section className="queue-section" aria-labelledby="queue-title"><div className="section-heading"><div><span className="eyebrow">Live order</span><h2 id="queue-title">Patient queue</h2></div><div className="queue-meta"><span>{queue.length} {queue.length === 1 ? 'token' : 'tokens'}</span><span className={`realtime-state realtime-${realtimeStatus}`}><i aria-hidden="true" />{realtimeLabels[realtimeStatus]}</span></div></div>
      {queue.length === 0 ? <EmptyState title="The queue is clear" message="Booked and walk-in patients will appear here in token order." /> : <Card padding="none"><DataTable caption={`Ordered queue for ${session.name}`} columns={queueColumns} rows={queue} getRowKey={token => token.id} /></Card>}
    </section>
    <ConfirmationDialog open={!!confirmation} title={confirmation?.title ?? ''} message={confirmation?.message ?? ''} confirmLabel={confirmation?.label} destructive={confirmation ? confirmation.actionKey.startsWith('skip-') || confirmation.actionKey === 'close-session' : false} busy={pendingAction === confirmation?.actionKey} onCancel={() => { if (!pendingAction) setConfirmation(null) }} onConfirm={() => { if (confirmation) void runAction(confirmation.actionKey, confirmation.run).then(succeeded => { if (succeeded) setConfirmation(null) }) }} />
  </div>
}
