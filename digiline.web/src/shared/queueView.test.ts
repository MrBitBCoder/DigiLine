import { expect, it } from "vitest";
import type { QueueToken } from "./types/domain";
import { visibleQueue } from "./queueView";

it("shows the actual call priority and hides completed tokens until filtered", () => {
  const token = (
    status: QueueToken["status"],
    tokenNumber: number,
    queueOrder = tokenNumber,
  ) => ({ status, tokenNumber, queueOrder }) as QueueToken;
  const queue = [
    token("Completed", 1),
    token("Late", 2, 4),
    token("Waiting", 3, 2),
    token("Called", 4),
    token("Waiting", 5, 5),
    token("Skipped", 6),
  ];
  expect(visibleQueue(queue, "Active").map((x) => x.tokenNumber)).toEqual([
    4, 3, 2, 5, 6,
  ]);
  expect(visibleQueue(queue, "Completed").map((x) => x.tokenNumber)).toEqual([
    1,
  ]);
  expect(visibleQueue(queue, "Other finished")).toEqual([]);
});

it("puts the latest active call first if more than one token is called", () => {
  const called = (tokenNumber: number, calledAt: string) =>
    ({
      status: "Called",
      tokenNumber,
      calledAt,
      createdAt: calledAt,
    }) as QueueToken;
  expect(
    visibleQueue(
      [called(1, "2026-09-14T09:00:00Z"), called(2, "2026-09-14T09:05:00Z")],
      "Active",
    ).map((token) => token.tokenNumber),
  ).toEqual([2, 1]);
});

it("puts newly completed tokens first and uses registration time for historical rows", () => {
  const completed = (
    tokenNumber: number,
    createdAt: string,
    completedAt: string | null,
  ) =>
    ({
      id: String(tokenNumber),
      status: "Completed",
      tokenNumber,
      createdAt,
      completedAt,
    }) as QueueToken;
  const rows = [
    completed(1, "2026-09-10T09:00:00Z", "2026-09-14T09:00:00Z"),
    completed(2, "2026-09-11T09:00:00Z", null),
    completed(3, "2026-09-12T09:00:00Z", "2026-09-14T10:00:00Z"),
    completed(4, "2026-09-12T09:00:00Z", "2026-09-14T10:00:00Z"),
  ];
  expect(visibleQueue(rows, "Completed").map((row) => row.tokenNumber)).toEqual(
    [4, 3, 1, 2],
  );
});

it("keeps the newest ten completed tokens on the first page", () => {
  const rows = Array.from({ length: 12 }, (_, index) => ({
    id: String(index + 1),
    status: "Completed",
    tokenNumber: index + 1,
    createdAt: "2026-09-14T08:00:00Z",
    completedAt: new Date(Date.UTC(2026, 8, 14, 8, index)).toISOString(),
  })) as QueueToken[];
  expect(
    visibleQueue(rows, "Completed")
      .slice(0, 10)
      .map((row) => row.tokenNumber),
  ).toEqual([12, 11, 10, 9, 8, 7, 6, 5, 4, 3]);
  expect(
    visibleQueue(rows, "Completed")
      .slice(10)
      .map((row) => row.tokenNumber),
  ).toEqual([2, 1]);
});
