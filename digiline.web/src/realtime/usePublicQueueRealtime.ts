import { API_BASE_URL } from "../api/httpClient";
import { useQueueConnection } from "./useQueueConnection";

const HUB_URL =
  import.meta.env.VITE_PUBLIC_SIGNALR_URL ??
  `${API_BASE_URL}/hubs/public-queue`;
export function usePublicQueueRealtime(
  sessionId: string | null,
  onChanged: () => void,
) {
  return useQueueConnection(HUB_URL, sessionId, undefined, onChanged);
}
