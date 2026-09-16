import { afterEach, describe, expect, it, vi } from "vitest";
import { publicClinicApi } from "./publicClinicApi";

describe("publicClinicApi.cancelToken", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("uses the patient access header and patient cancellation route", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      sessionId: "session-1",
      tokenId: "token-1",
      tokenNumber: 15,
      status: 7,
      currentTokenNumber: 4,
      position: null,
      patientsAhead: null,
      estimatedWaitMinutes: null,
      isPaused: false,
      sessionStatus: 3,
      updatedAt: "2026-09-16T08:30:00Z",
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await publicClinicApi.cancelToken("patient-access", "session-1", "token-1");

    const [url, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toMatch(/\/api\/public\/queue\/sessions\/session-1\/tokens\/token-1\/cancel$/);
    expect(options.method).toBe("POST");
    expect(new Headers(options.headers).get("X-Patient-Access")).toBe("patient-access");
    expect(result.status).toBe("Cancelled");
    expect(result.sessionStatus).toBe("Running");
  });
});
