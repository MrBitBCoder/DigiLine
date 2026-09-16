import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api/httpClient";
import type {
  DoctorSessionDetails,
  QueueTokenStatus,
  SessionStatus,
} from "../../shared/types/domain";
import { DoctorDashboard } from "./DoctorDashboard";
import { ToastProvider } from "../../shared/ui";

const api = vi.hoisted(() => ({
  createSession: vi.fn(),
  listSessions: vi.fn(),
  updateSession: vi.fn(),
  deleteSession: vi.fn(),
  getActiveOrUpcoming: vi.fn(),
  getSession: vi.fn(),
  callNext: vi.fn(),
  skip: vi.fn(),
  complete: vi.fn(),
  completeAndCallNext: vi.fn(),
  markLate: vi.fn(),
  startSession: vi.fn(),
  pauseSession: vi.fn(),
  resumeSession: vi.fn(),
  closeSession: vi.fn(),
  completeSession: vi.fn(),
  stopBooking: vi.fn(),
}));
const realtime = vi.hoisted(() => ({
  callbacks: new Set<() => void | Promise<void>>(),
}));
vi.mock("./doctorApi", () => ({ doctorApi: api }));
vi.mock("../../realtime", () => ({
  useQueueRealtime: (
    _sessionId: string | null,
    callback: () => void | Promise<void>,
  ) => {
    realtime.callbacks.add(callback);
    return "connected";
  },
}));

function fixture(
  status: SessionStatus = "Running",
  tokenStatuses: QueueTokenStatus[] = ["Waiting", "Late"],
): DoctorSessionDetails {
  const queue = tokenStatuses.map((tokenStatus, index) => ({
    id: `token-${index + 1}`,
    tokenNumber: index + 1,
    patientProfileId: null,
    walkInName: `Patient ${index + 1}`,
    phoneNumber: null,
    status: tokenStatus,
    createdAt: `2026-08-19T0${index + 6}:00:00Z`,
    calledAt: tokenStatus === "Called" ? "2026-08-19T08:00:00Z" : null,
    completedAt: null,
  }));
  const currentToken =
    queue.find(
      (token) => token.status === "Called" || token.status === "InConsultation",
    ) ?? null;
  return {
    session: {
      id: "session-1",
      clinicId: "clinic-1",
      clinicName: "DigiLine Clinic",
      doctorId: "doctor-1",
      doctorName: "Dr Shah",
      name: "Morning clinic",
      bookingOpenTime: "2026-08-19T05:00:00Z",
      startTime: "2026-08-19T06:00:00Z",
      endTime: "2026-08-19T10:00:00Z",
      lastBookingTime: "2026-08-19T09:00:00Z",
      averageConsultationMinutes: 10,
      status,
      isBookingOpen: status !== "Closed",
      queueSummary: {
        currentToken,
        waitingCount: queue.filter((token) => token.status === "Waiting")
          .length,
        lateCount: queue.filter((token) => token.status === "Late").length,
        totalCount: queue.length,
        completedCount: queue.filter(token => token.status === "Completed").length,
        notAttendedCount: queue.filter(token => token.status === "NoShow" || token.status === "Skipped").length,
      },
    },
    queue,
  };
}

describe("DoctorDashboard", () => {
  beforeEach(() => {
    Object.values(api).forEach((mock) => mock.mockReset());
    api.listSessions.mockResolvedValue([]);
    realtime.callbacks.clear();
  });

  it("shows loading and then the no-session state", async () => {
    let resolve!: (value: undefined) => void;
    api.getActiveOrUpcoming.mockReturnValue(
      new Promise<undefined>((done) => {
        resolve = done;
      }),
    );
    render(<DoctorDashboard />);
    expect(
      screen.getByText("Loading your active session and queue…"),
    ).toBeInTheDocument();
    resolve(undefined);
    expect(
      await screen.findByText("No active or upcoming session"),
    ).toBeInTheDocument();
  });

  it("directs an empty dashboard to the sessions page", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(undefined);
    render(<DoctorDashboard />);
    await screen.findByText("No active or upcoming session");
    expect(screen.getByText(/Create your next clinic session on the Sessions page/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add session" })).not.toBeInTheDocument();
  });

  it("renders backend queue order and summary values", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture());
    render(<DoctorDashboard />);
    expect(
      await screen.findByRole("heading", { name: "Morning clinic" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Doctor dashboard · DigiLine Clinic/)).toBeInTheDocument();
    const currentCard = screen.getByText("Queue ready").closest<HTMLDivElement>(".current-patient-panel")!;
    expect(currentCard.compareDocumentPosition(screen.getByRole("heading", { name: "Patient queue" })) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(currentCard).getByRole("button", { name: "Call next" })).toBeEnabled();
    const rows = screen.getAllByRole("row").slice(1);
    expect(within(rows[0]).getByText("#1")).toBeInTheDocument();
    expect(within(rows[1]).getByText("#2")).toBeInTheDocument();
    for (const header of ["Token", "Patient", "Age", "Phone", "Type", "Status", "Registered at", "Called at", "Late arrival", "Completed at", "Actions"])
      expect(screen.getByRole("columnheader", { name: header })).toBeInTheDocument();
    expect(
      screen.getByText("Ready to call").previousElementSibling,
    ).toHaveTextContent("1");
    expect(
      screen.getByText("After waiting patients").previousElementSibling,
    ).toHaveTextContent("1");
  });

  it("shows booked patient identity, age, and phone in the doctor queue", async () => {
    const details = fixture("Running", ["Waiting"]);
    details.queue[0] = { ...details.queue[0], walkInName: null, patientName: "Asha Rao", patientAge: 34, phoneNumber: "9999999999" };
    api.getActiveOrUpcoming.mockResolvedValue(details);
    render(<DoctorDashboard />);
    const row = (await screen.findByText("Asha Rao")).closest("tr")!;
    expect(within(row).getByText("34")).toBeInTheDocument();
    expect(within(row).getByText("9999999999")).toBeInTheDocument();
    expect(screen.queryByText("Registered patient")).not.toBeInTheDocument();
  });

  it("locks Call Next and refreshes authoritative session state after success", async () => {
    const initial = fixture("Running", ["Waiting"]);
    const updated = fixture("Running", ["Called"]);
    api.getActiveOrUpcoming.mockResolvedValue(initial);
    api.callNext.mockResolvedValue(initial.queue[0]);
    api.getSession.mockResolvedValue(updated);
    const user = userEvent.setup();
    render(<DoctorDashboard />);
    const callNext = await screen.findByRole("button", { name: "Call next" });
    await user.dblClick(callNext);
    await waitFor(() => expect(api.callNext).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(api.getSession).toHaveBeenCalledWith("session-1", undefined, 1, "Active"),
    );
    expect(await screen.findByRole("heading", { name: "Token #1" })).toBeInTheDocument();
  });

  it("shows backend validation detail when an action is rejected", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Running", ["Waiting"]));
    api.callNext.mockRejectedValue(
      new ApiError("Rejected", 400, {
        detail: "Call Next is only available for a running session.",
      }),
    );
    const user = userEvent.setup();
    render(<ToastProvider><DoctorDashboard /></ToastProvider>);
    await user.click(await screen.findByRole("button", { name: "Call next" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Call Next is only available for a running session.",
    );
  });

  it("shows only valid paused-session controls", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Paused", ["Waiting"]));
    render(<DoctorDashboard />);
    expect(await screen.findByRole("button", { name: "Resume" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Complete session" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Call next" })).toBeDisabled();
    expect(
      screen.queryByRole("button", { name: "Pause" }),
    ).not.toBeInTheDocument();
  });

  it("confirms completion before mutating the current token", async () => {
    const called = fixture("Running", ["Called"]);
    const completed = fixture("Running", ["Completed"]);
    api.getActiveOrUpcoming.mockResolvedValue(called);
    api.complete.mockResolvedValue(called.queue[0]);
    api.getSession.mockResolvedValue(completed);
    const user = userEvent.setup();
    render(<DoctorDashboard />);
    expect(await screen.findByRole("heading", { name: "Token #1" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Call again/i })).not.toBeInTheDocument();
    await user.click(await screen.findByRole("button", { name: "Complete" }));
    expect(api.complete).not.toHaveBeenCalled();
    await user.click(
      screen.getByRole("button", { name: "Complete consultation" }),
    );
    await waitFor(() =>
      expect(api.complete).toHaveBeenCalledWith("session-1", "token-1"),
    );
  });

  it("shows a newly booked patient after QueueChanged without a page refresh", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Running", ["Waiting"]));
    api.getSession.mockResolvedValue(
      fixture("Running", ["Waiting", "Waiting"]),
    );
    render(<DoctorDashboard />);
    expect(await screen.findByText("#1")).toBeInTheDocument();

    await actRealtimeEvent();

    expect(await screen.findByText("#2")).toBeInTheDocument();
    expect(api.getSession).toHaveBeenCalledWith("session-1", undefined, 1, "Active");
  });

  it("keeps a skipped patient visible for the Patient arrived action", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Running", ["Skipped"]));
    api.markLate.mockResolvedValue({});
    api.getSession.mockResolvedValue(fixture("Running", ["Late"]));
    const user = userEvent.setup();
    render(<DoctorDashboard />);
    expect(await screen.findByText("Awaiting arrival")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Patient arrived" }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Patient arrived" }));
    await waitFor(() => expect(api.markLate).toHaveBeenCalledWith("session-1", "token-1"));
  });

  it("updates every connected doctor screen after an external Reception change", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Running", ["Waiting"]));
    api.getSession.mockResolvedValue(fixture("Running", ["Waiting", "Late"]));
    render(
      <>
        <DoctorDashboard />
        <DoctorDashboard />
      </>,
    );
    await waitFor(() => expect(screen.getAllByText("#1")).toHaveLength(2));

    await actRealtimeEvent();

    await waitFor(() => expect(screen.getAllByText("#2")).toHaveLength(2));
    expect(api.getSession).toHaveBeenCalledTimes(2);
  });

  it("keeps one row per token when duplicate events trigger repeated refetches", async () => {
    const state = fixture("Running", ["Waiting", "Late"]);
    api.getActiveOrUpcoming.mockResolvedValue(state);
    api.getSession.mockResolvedValue(state);
    render(<DoctorDashboard />);
    expect(await screen.findByText("#2")).toBeInTheDocument();

    await actRealtimeEvent();
    await actRealtimeEvent();

    expect(screen.getAllByText("#1")).toHaveLength(1);
    expect(screen.getAllByText("#2")).toHaveLength(1);
    expect(api.getSession).toHaveBeenCalledTimes(2);
  });
  it("replaces BookingOpen with Completed after an authoritative realtime refresh", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("BookingOpen", []));
    api.getSession.mockResolvedValue(fixture("Closed", []));
    render(<DoctorDashboard />);
    await waitFor(() => expect(document.querySelector(".session-state .status-badge")).toHaveTextContent("Booking open"));
    await act(async () => { await Promise.all([...realtime.callbacks].map(callback => callback())); });
    await waitFor(() => expect(document.querySelector(".session-state .status-badge")).toHaveTextContent("Completed"));
    expect(document.querySelector(".session-state .status-badge")).not.toHaveClass("status-info");
    expect(document.querySelector(".session-state .status-badge")).not.toHaveTextContent("Booking open");
  });
  it("keeps Running lifecycle status when booking is stopped", async () => {
    const stopped = fixture("Running", []); stopped.session.isBookingOpen = false;
    api.getActiveOrUpcoming.mockResolvedValue(stopped);
    render(<DoctorDashboard />);
    expect(await screen.findByText("Running", { exact: true })).toHaveClass("status-success");
    expect(document.querySelector(".session-state .status-badge")).not.toHaveTextContent("Completed");
  });

  it.each<[SessionStatus, boolean, boolean]>([
    ["Scheduled", false, false], ["BookingOpen", false, false],
    ["Running", false, true], ["Paused", false, true],
    ["Running", true, false], ["Closed", false, false],
  ])("Stop booking availability for %s with alreadyStopped=%s", async (status, stopped, available) => {
    const data = fixture(status, []);
    data.session.bookingHasStopped = stopped;
    data.session.isBookingOpen = !stopped && status !== "Closed";
    api.getActiveOrUpcoming.mockResolvedValue(data);
    render(<DoctorDashboard />);
    await screen.findByRole("heading", { name: "Patient queue" });
    const button = screen.queryByRole("button", { name: "Stop booking" });
    if (available) expect(button).toBeEnabled();
    else expect(button).not.toBeInTheDocument();
  });

  it("shows action feedback after stopping booking without relying on realtime refresh", async () => {
    const before = fixture("Running", []);
    before.session.isBookingOpen = true;
    before.session.bookingHasStopped = false;
    const after = fixture("Running", []);
    after.session.isBookingOpen = false;
    after.session.bookingHasStopped = true;
    api.getActiveOrUpcoming.mockResolvedValue(before);
    api.stopBooking.mockResolvedValue(undefined);
    api.getSession.mockResolvedValue(after);
    const user = userEvent.setup();
    render(<ToastProvider><DoctorDashboard /></ToastProvider>);
    await user.click(await screen.findByRole("button", { name: "Stop booking" }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Stop booking" }));
    expect(await screen.findByText("Booking stopped. No new patients can join.")).toBeInTheDocument();
  });

  it("completes the current token and immediately shows the next called patient", async () => {
    const before = fixture("Running", ["Called", "Waiting"]);
    const after = fixture("Running", ["Completed", "Called"]);
    api.getActiveOrUpcoming.mockResolvedValue(before);
    api.completeAndCallNext.mockResolvedValue({
      completedToken: { ...before.queue[0], status: "Completed", completedAt: "2026-08-19T08:10:00Z" },
      calledToken: { ...after.queue[1], status: "Called" },
    });
    api.getSession.mockResolvedValue(after);
    const user = userEvent.setup();
    render(<ToastProvider><DoctorDashboard /></ToastProvider>);

    expect(await screen.findByRole("button", { name: "Complete & call next" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Complete" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: "Call next" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Complete & call next" }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Complete & call next" }));

    await waitFor(() => expect(api.completeAndCallNext).toHaveBeenCalledWith("session-1", "token-1"));
    expect(await screen.findByRole("heading", { name: "Token #2" })).toBeInTheDocument();
    expect(screen.getByText("Token #1 completed. Token #2 called.")).toBeInTheDocument();
  });

  it("uses standalone Complete when the current patient is last", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Running", ["Called"]));
    render(<DoctorDashboard />);

    expect(await screen.findByRole("button", { name: "Complete" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: "Complete & call next" })).not.toBeInTheDocument();
  });

  it("prioritizes the current patient and one primary next action", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Running", ["Called", "Waiting"]));
    render(<DoctorDashboard />);

    const panel = (await screen.findByRole("heading", { name: "Token #1" })).closest<HTMLElement>(".current-patient-panel")!;
    expect(within(panel).getByRole("heading", { name: "Token #1" })).toBeInTheDocument();
    expect(within(panel).getByText("Patient 1")).toBeInTheDocument();
    expect(within(panel).getByText("Called")).toBeInTheDocument();
    expect(within(panel).getByRole("button", { name: "Complete & call next" })).toHaveClass("primary-queue-action");
    expect(within(panel).getByRole("button", { name: "Not here—skip" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: /Call Again/i })).not.toBeInTheDocument();
    expect(panel.compareDocumentPosition(screen.getByRole("heading", { name: "Patient queue" })) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("does not offer an unimplemented consultation action", async () => {
    api.getActiveOrUpcoming.mockResolvedValue(fixture("Running", ["Called"]));
    render(<DoctorDashboard />);
    await screen.findByRole("heading", { name: "Token #1" });
    expect(screen.queryByRole("button", { name: /Start consultation/i })).not.toBeInTheDocument();
  });

});

async function actRealtimeEvent() {
  await act(async () => {
    await Promise.all([...realtime.callbacks].map((callback) => callback()));
  });
}
