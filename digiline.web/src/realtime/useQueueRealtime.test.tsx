import type { ReactNode } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AuthContext, type AuthContextValue } from "../auth/AuthContext";
import { useQueueRealtime } from "./useQueueRealtime";

const signalr = vi.hoisted(() => {
  const handlers = new Map<string, (event: { sessionId: string }) => void>();
  const connection = {
    state: "Connected",
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    invoke: vi.fn().mockResolvedValue(undefined),
    on: vi.fn(
      (name: string, callback: (event: { sessionId: string }) => void) =>
        handlers.set(name, callback),
    ),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
  };
  const builder = {
    withUrl: vi.fn(),
    withAutomaticReconnect: vi.fn(),
    configureLogging: vi.fn(),
    build: vi.fn(() => connection),
  };
  builder.withUrl.mockReturnValue(builder);
  builder.withAutomaticReconnect.mockReturnValue(builder);
  builder.configureLogging.mockReturnValue(builder);
  return { connection, builder, handlers };
});

vi.mock("@microsoft/signalr", () => ({
  HubConnectionBuilder: function HubConnectionBuilder() {
    return signalr.builder;
  },
  HubConnectionState: { Connected: "Connected" },
  LogLevel: { Warning: 3 },
}));

const authValue: AuthContextValue = {
  session: {
    token: "jwt-token",
    expiresAt: Date.now() + 60_000,
    user: {
      id: "doctor-1",
      email: "doctor@example.com",
      name: "Doctor",
      role: "Doctor",
      clinicId: "clinic-1",
    },
  },
  login: vi.fn(),
  logout: vi.fn(),
};
function Wrapper({ children }: { children: ReactNode }) {
  return (
    <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>
  );
}

describe("useQueueRealtime", () => {
  it("authenticates, joins, refetches matching events, and leaves the old group", async () => {
    const changed = vi.fn();
    const { result, rerender, unmount } = renderHook(
      ({ sessionId }) => useQueueRealtime(sessionId, changed),
      { wrapper: Wrapper, initialProps: { sessionId: "session-1" } },
    );
    await waitFor(() => expect(result.current).toBe("connected"));
    expect(signalr.builder.withUrl).toHaveBeenCalledWith(
      expect.stringContaining("/hubs/queue"),
      expect.objectContaining({ accessTokenFactory: expect.any(Function) }),
    );
    expect(signalr.connection.invoke).toHaveBeenCalledWith(
      "JoinSession",
      "session-1",
    );

    act(() => signalr.handlers.get("QueueChanged")?.({ sessionId: "session-1" }));
    await waitFor(() => expect(changed).toHaveBeenCalledTimes(2));
    expect(changed).toHaveBeenCalledTimes(2);
    act(() =>
      signalr.handlers.get("QueueChanged")?.({ sessionId: "another-session" }),
    );
    expect(changed).toHaveBeenCalledTimes(2);

    rerender({ sessionId: "session-2" });
    await waitFor(() =>
      expect(signalr.connection.invoke).toHaveBeenCalledWith(
        "LeaveSession",
        "session-1",
      ),
    );
    await waitFor(() =>
      expect(signalr.connection.invoke).toHaveBeenCalledWith(
        "JoinSession",
        "session-2",
      ),
    );
    unmount();
  });

  it("shows reconnecting and rejoins before refetching after reconnect", async () => {
    const changed = vi.fn();
    const { result } = renderHook(
      () => useQueueRealtime("session-1", changed),
      { wrapper: Wrapper },
    );
    await waitFor(() => expect(result.current).toBe("connected"));
    act(() => {
      const callback = signalr.connection.onreconnecting.mock.calls.at(-1)?.[0];
      callback?.();
    });
    expect(result.current).toBe("reconnecting");
    await act(async () => {
      const callback = signalr.connection.onreconnected.mock.calls.at(-1)?.[0];
      await callback?.();
    });
    expect(result.current).toBe("connected");
    expect(changed).toHaveBeenCalledTimes(2);
  });
});
