import { API_BASE_URL } from "../api/httpClient";
import { useAuth } from "../auth/AuthContext";
import { useQueueConnection } from "./useQueueConnection";

export type { RealtimeStatus } from "./useQueueConnection";
const HUB_URL =
  import.meta.env.VITE_SIGNALR_URL ?? `${API_BASE_URL}/hubs/queue`;

export function useQueueRealtime(
  sessionId: string | null,
  onQueueChanged: () => void,
) {
  const { session } = useAuth();
  return useQueueConnection(
    HUB_URL,
    sessionId,
    session?.token ?? "",
    onQueueChanged,
  );
}
