import type { QueueToken } from "./types/domain";

export type QueueFilter = "Active" | "Completed" | "Other finished";
const rank: Record<string, number> = {
  Called: 0,
  InConsultation: 0,
  Waiting: 1,
  Late: 1,
  Skipped: 2,
};

export function visibleQueue(
  queue: QueueToken[],
  filter: QueueFilter,
): QueueToken[] {
  return queue
    .filter((token) =>
      filter === "Completed"
        ? token.status === "Completed"
        : filter === "Other finished"
          ? ["NoShow", "Cancelled"].includes(token.status)
          : ["Called", "InConsultation", "Waiting", "Late", "Skipped"].includes(
              token.status,
            ),
    )
    .sort((a, b) =>
      filter === "Completed"
        ? Date.parse(b.completedAt ?? b.createdAt) -
            Date.parse(a.completedAt ?? a.createdAt) ||
          b.tokenNumber - a.tokenNumber
        : (rank[a.status] ?? 3) - (rank[b.status] ?? 3) ||
          (rank[a.status] === 0 && rank[b.status] === 0
            ? Date.parse(b.calledAt ?? b.createdAt) -
              Date.parse(a.calledAt ?? a.createdAt)
            : 0) ||
          (a.queueOrder ?? a.tokenNumber) - (b.queueOrder ?? b.tokenNumber) ||
          a.tokenNumber - b.tokenNumber,
    );
}
