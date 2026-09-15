import { beforeEach, expect, it, vi } from "vitest";
import { doctorApi } from "./doctorApi";
const api = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("../../api/httpClient", () => ({ request: api.request }));
beforeEach(() => api.request.mockReset());
it.each([[1,"Scheduled"], [2,"BookingOpen"], [3,"Running"], [4,"Paused"], [5,"Closed"]])("maps backend session status %s to %s without using booking availability", async (status, expected) => {
  api.request.mockResolvedValue({ items: [{ status, isBookingOpen: false, queueSummary: { currentToken: null } }], page: 1, pageSize: 10, totalCount: 1 });
  const result = await doctorApi.listSessions();
  expect(result.items[0].status).toBe(expected);
  expect(result.items[0].isBookingOpen).toBe(false);
});
