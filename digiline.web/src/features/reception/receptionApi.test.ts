import { afterEach, expect, it, vi } from "vitest";
import { receptionApi } from "./receptionApi";

afterEach(() => vi.unstubAllGlobals());

it.each([
  [undefined, null],
  ["9876543210", "9876543210"],
])(
  "sends a walk-in as a JSON object with optional phone",
  async (phone, expectedPhone) => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ tokenNumber: 12 }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      receptionApi.walkIn("session-1", "Asha", phone, 34),
    ).resolves.toEqual({ tokenNumber: 12 });

    const [url, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("/api/reception/queue/walk-in");
    expect(JSON.parse(options.body as string)).toEqual({
      sessionId: "session-1",
      walkInName: "Asha",
      phoneNumber: expectedPhone,
      age: 34,
    });
  },
);
