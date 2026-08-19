import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { API_BASE_URL } from '../api/httpClient'
import { useAuth } from '../auth/AuthContext'

export type RealtimeStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'
interface QueueChangedEvent { sessionId: string }
const HUB_URL = import.meta.env.VITE_SIGNALR_URL ?? `${API_BASE_URL}/hubs/queue`

export function useQueueRealtime(sessionId: string | null, onQueueChanged: () => void) {
  const { session } = useAuth()
  const callbackRef = useRef(onQueueChanged)
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  useEffect(() => { callbackRef.current = onQueueChanged }, [onQueueChanged])
  useEffect(() => {
    if (!sessionId || !session?.token) return
    let active = true
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => session.token })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build()
    connection.on('QueueChanged', (event: QueueChangedEvent) => {
      if (event.sessionId.toLowerCase() === sessionId.toLowerCase()) void callbackRef.current()
    })
    connection.onreconnecting(() => { if (active) setStatus('reconnecting') })
    connection.onreconnected(async () => {
      if (!active) return
      try { await connection.invoke('JoinSession', sessionId); setStatus('connected'); await callbackRef.current() }
      catch { if (active) setStatus('disconnected') }
    })
    connection.onclose(() => { if (active) setStatus('disconnected') })
    queueMicrotask(() => { if (active) setStatus('connecting') })
    void connection.start()
      .then(() => connection.invoke('JoinSession', sessionId))
      .then(() => { if (active) setStatus('connected') })
      .catch(() => { if (active) setStatus('disconnected') })
    return () => {
      active = false
      if (connection.state === HubConnectionState.Connected) {
        void connection.invoke('LeaveSession', sessionId).finally(() => connection.stop())
      } else {
        void connection.stop()
      }
    }
  }, [sessionId, session?.token])
  return sessionId ? status : 'disconnected'
}
