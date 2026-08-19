import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { API_BASE_URL } from '../api/httpClient'
import type { RealtimeStatus } from './useQueueRealtime'

const HUB_URL = import.meta.env.VITE_PUBLIC_SIGNALR_URL ?? `${API_BASE_URL}/hubs/public-queue`
export function usePublicQueueRealtime(sessionId: string | null, onChanged: () => void) {
  const callback = useRef(onChanged); const [status, setStatus] = useState<RealtimeStatus>('connecting')
  useEffect(() => { callback.current = onChanged }, [onChanged])
  useEffect(() => {
    if (!sessionId) return
    let active = true
    const connection = new HubConnectionBuilder().withUrl(HUB_URL).withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(LogLevel.Warning).build()
    connection.on('QueueChanged', (event: { sessionId: string }) => { if (event.sessionId.toLowerCase() === sessionId.toLowerCase()) void callback.current() })
    connection.onreconnecting(() => { if (active) setStatus('reconnecting') })
    connection.onreconnected(async () => { if (!active) return; try { await connection.invoke('JoinSession', sessionId); setStatus('connected'); await callback.current() } catch { setStatus('disconnected') } })
    connection.onclose(() => { if (active) setStatus('disconnected') })
    void connection.start().then(() => connection.invoke('JoinSession', sessionId)).then(() => { if (active) setStatus('connected') }).catch(() => { if (active) setStatus('disconnected') })
    return () => { active = false; if (connection.state === HubConnectionState.Connected) void connection.invoke('LeaveSession', sessionId).finally(() => connection.stop()); else void connection.stop() }
  }, [sessionId])
  return sessionId ? status : 'disconnected'
}
