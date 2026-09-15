import type { QueueToken } from "./types/domain";

export function patientName(token: QueueToken) {
  return token.patientName || token.walkInName || "Patient details unavailable";
}

export function patientDetails(token: QueueToken) {
  return `Age: ${token.patientAge ?? "—"} · Phone: ${token.phoneNumber || "—"}`;
}
