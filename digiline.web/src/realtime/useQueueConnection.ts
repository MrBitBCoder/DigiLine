import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";
export type RealtimeStatus = "connecting" | "connected" | "reconnecting" | "disconnected";
export function useQueueConnection(hubUrl: string, sessionId: string | null, accessToken: string | undefined, onChanged: () => void | Promise<void>) {
  const callback = useRef(onChanged);
  const [status, setStatus] = useState<RealtimeStatus>("connecting");
  useEffect(() => { callback.current = onChanged; }, [onChanged]);
  useEffect(() => {
    if (!sessionId || accessToken === "") return;
    let active = true, starting = false, failures = 0, refreshing = false, dirty = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    let refreshTimer: ReturnType<typeof setTimeout> | undefined;
    const builder = new HubConnectionBuilder();
    const connection = (accessToken ? builder.withUrl(hubUrl, { accessTokenFactory: () => accessToken }) : builder.withUrl(hubUrl))
      .withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(LogLevel.Warning).build();
    async function refresh() {
      if (!active) return;
      if (refreshing) { dirty = true; return; }
      refreshing = true;
      try { await callback.current(); } finally {
        refreshing = false;
        if (dirty && active) { dirty = false; scheduleRefresh(); }
      }
    }
    function scheduleRefresh() {
      if (!active || refreshTimer) return;
      refreshTimer = setTimeout(() => { refreshTimer = undefined; void refresh(); }, 100);
    }
    function scheduleRetry() {
      if (!active || retryTimer) return;
      retryTimer = setTimeout(() => { retryTimer = undefined; void start(); }, Math.min(30000, 1000 * 2 ** Math.min(failures++, 5)));
    }
    async function joined() {
      await connection.invoke("JoinSession", sessionId);
      if (!active) return;
      failures = 0; setStatus("connected"); await refresh();
    }
    async function start() {
      if (!active || starting) return;
      starting = true;
      try { setStatus(failures ? "reconnecting" : "connecting"); await connection.start(); await joined(); }
      catch { if (active) { setStatus("disconnected"); await connection.stop(); scheduleRetry(); } }
      finally { starting = false; }
    }
    connection.on("QueueChanged", (event: { sessionId: string }) => { if (event.sessionId.toLowerCase() === sessionId.toLowerCase()) scheduleRefresh(); });
    connection.onreconnecting(() => { if (active) setStatus("reconnecting"); });
    connection.onreconnected(async () => { try { await joined(); } catch { if (active) { setStatus("disconnected"); await connection.stop(); scheduleRetry(); } } });
    connection.onclose(() => { if (active) { setStatus("disconnected"); scheduleRetry(); } });
    const retry = () => {
      if (!active || starting) return;
      if (connection.state === HubConnectionState.Connected) { scheduleRefresh(); return; }
      clearTimeout(retryTimer); retryTimer = undefined; void start();
    };
    window.addEventListener("online", retry);
    window.addEventListener("digiline-retry-realtime", retry);
    queueMicrotask(() => { if (active) void start(); });
    return () => {
      active = false; clearTimeout(retryTimer); clearTimeout(refreshTimer);
      window.removeEventListener("online", retry); window.removeEventListener("digiline-retry-realtime", retry);
      if (connection.state === HubConnectionState.Connected) void connection.invoke("LeaveSession", sessionId).finally(() => connection.stop());
      else void connection.stop();
    };
  }, [accessToken, hubUrl, sessionId]);
  return sessionId ? status : "disconnected";
}
