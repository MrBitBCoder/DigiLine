import { beforeEach, expect, it, vi } from "vitest";
import { doctorApi } from "./doctorApi";
const api = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("../../api/httpClient", () => ({ request: api.request }));
beforeEach(() => api.request.mockReset());
it.each([
    [1, "Scheduled"],
    [2, "BookingOpen"],
    [3, "Running"],
    [4, "Paused"],
    [5, "Closed"],
])(
    "maps backend session status %s to %s without using booking availability",
    async (status, expected) => {
        api.request.mockResolvedValue({
            items: [
                { status, isBookingOpen: false, queueSummary: { currentToken: null } },
            ],
            page: 1,
            pageSize: 10,
            totalCount: 1,
        });
        const result = await doctorApi.listSessions();
        expect(result.items[0].status).toBe(expected);
        expect(result.items[0].isBookingOpen).toBe(false);
    },
);

it("uses the explicit complete-and-call-next endpoint and normalizes both tokens", async () => {
    api.request.mockResolvedValue({
        completedToken: { id: "one", status: 4, tokenNumber: 1 },
        calledToken: { id: "two", status: 2, tokenNumber: 2 },
    });

    const result = await doctorApi.completeAndCallNext("session-1", "one");

    expect(api.request).toHaveBeenCalledWith(
        "/api/doctor/sessions/session-1/tokens/one/complete-and-call-next",
        { method: "POST" },
    );
    expect(result.completedToken.status).toBe("Completed");
    expect(result.calledToken?.status).toBe("Called");
});
