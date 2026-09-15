import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type {
  DoctorSessionDetails,
  ReceptionDashboard as Data,
} from "../../shared/types/domain";
import { ReceptionDashboard } from "./ReceptionDashboard";
import { ApiError } from "../../api/httpClient";

const api = vi.hoisted(() => ({
  today: vi.fn(),
  getSession: vi.fn(),
  walkIn: vi.fn(),
  cancel: vi.fn(),
  markLate: vi.fn(),
  recall: vi.fn(),
}));
const realtime = vi.hoisted(() => ({
  callback: null as null | (() => Promise<void> | void),
}));
vi.mock("./receptionApi", () => ({ receptionApi: api }));
vi.mock("../../realtime", () => ({
  useQueueRealtime: (
    _id: string | null,
    callback: () => Promise<void> | void,
  ) => {
    realtime.callback = callback;
    return "connected";
  },
}));

function fixture(statuses = ["Waiting", "Skipped", "Called"] as const): Data {
  const queue = statuses.map((status, i) => ({
    id: `token-${i}`,
    tokenNumber: i + 1,
    patientProfileId: null,
    walkInName: `Patient ${i + 1}`,
    phoneNumber: i ? null : "9999999999",
    status,
    createdAt: "2026-08-19T08:00:00Z",
    calledAt: status === "Called" ? "2026-08-19T08:05:00Z" : null,
    completedAt: null,
  }));
  const details: DoctorSessionDetails = {
    session: {
      id: "session-1",
      clinicId: "clinic-1",
      clinicName: "Clinic",
      doctorId: "doctor-1",
      doctorName: "Dr Rao",
      name: "Morning",
      bookingOpenTime: "2026-08-19T06:00:00Z",
      startTime: "2026-08-19T07:00:00Z",
      endTime: "2026-08-19T12:00:00Z",
      lastBookingTime: "2026-08-19T11:00:00Z",
      averageConsultationMinutes: 10,
      status: "Running",
      isBookingOpen: true,
      queueSummary: {
        currentToken: queue.find((x) => x.status === "Called") ?? null,
        waitingCount: queue.filter((x) => x.status === "Waiting").length,
        lateCount: 0,
        totalCount: queue.length,
        completedCount: 0,
        notAttendedCount: queue.filter(token => token.status === "Skipped").length,
      },
    },
    queue,
  };
  return { clinicName: "Clinic", date: "2026-08-19", sessions: [details] };
}

describe("ReceptionDashboard", () => {
  beforeEach(() => {
    Object.values(api).forEach((mock) => mock.mockReset());
    realtime.callback = null;
    api.getSession.mockImplementation(async (id: string) => {
      const data = await api.today.mock.results.at(-1)?.value as Data;
      return data.sessions.find(x => x.session.id === id);
    });
  });
  it("renders today summary and doctor availability", async () => {
    api.today.mockResolvedValue(fixture());
    render(<ReceptionDashboard />);
    expect(await screen.findByText("Reception desk")).toBeInTheDocument();
    expect(screen.getByText("Doctor available")).toBeInTheDocument();
    expect(screen.getByText("9999999999")).toBeInTheDocument();
    for (const header of ["Token", "Patient", "Age", "Phone", "Type", "Status", "Registered at", "Last called", "Late arrival", "Completed at", "Actions"])
      expect(screen.getByRole("columnheader", { name: header })).toBeInTheDocument();
  });
  it("shows booked patient identity, age, and phone in queue details", async () => {
    const data = fixture();
    data.sessions[0].queue[0] = { ...data.sessions[0].queue[0], walkInName: null, patientName: "Asha Rao", patientAge: 34, phoneNumber: "9999999999" };
    api.today.mockResolvedValue(data);
    render(<ReceptionDashboard />);
    const row = (await screen.findByText("Asha Rao")).closest("tr")!;
    expect(within(row).getByText("34")).toBeInTheDocument();
    expect(within(row).getByText("9999999999")).toBeInTheDocument();
  });
  it("registers one walk-in despite a double click and shows its token", async () => {
    api.today.mockResolvedValue(fixture());
    api.walkIn.mockResolvedValue({ tokenNumber: 12 });
    const user = userEvent.setup();
    render(<ReceptionDashboard />);
    await user.type(await screen.findByLabelText(/Patient name/), "Asha");
    await user.type(screen.getByLabelText(/^Age/), "34");
    const button = screen.getByRole("button", { name: "Issue token" });
    await user.dblClick(button);
    await waitFor(() => expect(api.walkIn).toHaveBeenCalledTimes(1));
    expect(await screen.findByText("Token #12")).toBeInTheDocument();
  });
  it("registers a walk-in with an optional phone number", async () => {
    api.today.mockResolvedValue(fixture());
    api.walkIn.mockResolvedValue({ tokenNumber: 13 });
    const user = userEvent.setup();
    render(<ReceptionDashboard />);
    await user.type(await screen.findByLabelText(/Patient name/), "Asha");
    await user.type(screen.getByLabelText(/^Age/), "34");
    await user.type(screen.getByLabelText(/Phone \(optional\)/), "9876543210");
    await user.click(screen.getByRole("button", { name: "Issue token" }));
    await waitFor(() => expect(api.walkIn).toHaveBeenCalledWith("session-1", "Asha", "9876543210", 34));
    expect(await screen.findByText("Token #13")).toBeInTheDocument();
  });
  it("shows server validation errors without issuing a token", async () => {
    api.today.mockResolvedValue(fixture());
    api.walkIn.mockRejectedValue(new ApiError("One or more validation errors occurred.", 400, { errors: { WalkInName: ["Name is invalid."] } }));
    const user = userEvent.setup();
    render(<ReceptionDashboard />);
    await user.type(await screen.findByLabelText(/Patient name/), "Asha");
    await user.type(screen.getByLabelText(/^Age/), "34");
    await user.click(screen.getByRole("button", { name: "Issue token" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Name is invalid.");
    expect(screen.queryByText(/Token #/)).not.toBeInTheDocument();
  });
  it("confirms cancellation before changing the queue", async () => {
    api.today.mockResolvedValue(fixture());
    api.cancel.mockResolvedValue({});
    const user = userEvent.setup();
    render(<ReceptionDashboard />);
    await user.click(await screen.findByRole("button", { name: "Cancel" }));
    expect(api.cancel).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Cancel token" }));
    await waitFor(() =>
      expect(api.cancel).toHaveBeenCalledWith("session-1", "token-0"),
    );
  });
  it("refreshes after a realtime event", async () => {
    api.today.mockResolvedValue(fixture());
    render(<ReceptionDashboard />);
    await screen.findByText("Reception desk");
    await act(async () => {
      await realtime.callback?.();
    });
    expect(api.today).toHaveBeenCalledTimes(3);
  });
  it("shows skipped patients in the active queue and records their arrival", async () => {
    api.today.mockResolvedValue(fixture());
    api.markLate.mockResolvedValue({});
    const user = userEvent.setup();
    render(<ReceptionDashboard />);
    expect(await screen.findByText("Awaiting arrival")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Patient arrived" }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Patient arrived" }));
    await waitFor(() => expect(api.markLate).toHaveBeenCalledWith("session-1", "token-1"));
  });
  it("shows Completed after the selected session closes and realtime refreshes", async () => {
    const before = fixture(); before.sessions[0].session.status = "BookingOpen";
    api.today.mockResolvedValue(before);
    render(<ReceptionDashboard />);
    await waitFor(() => expect(document.querySelector(".reception-session-strip .status-badge")).toHaveTextContent("Booking open"));
    const after = fixture(); after.sessions[0].session.status = "Closed"; after.sessions[0].session.isBookingOpen = false;
    api.today.mockResolvedValue(after);
    await act(async () => { await realtime.callback?.(); });
    await waitFor(() => expect(document.querySelector(".reception-session-strip .status-badge")).toHaveTextContent("Completed"));
    expect(document.querySelector(".reception-session-strip .status-badge")).not.toHaveClass("status-info");
  });

});
