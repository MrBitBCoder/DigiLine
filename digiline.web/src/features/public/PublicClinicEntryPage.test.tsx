import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api/httpClient";
import type {
  PublicClinicDetails,
  PublicTokenState,
} from "../../shared/types/domain";
import { PublicClinicEntryPage } from "./PublicClinicEntryPage";

const api = vi.hoisted(() => ({
  getBySlug: vi.fn(),
  requestVerification: vi.fn(),
  confirmVerification: vi.fn(),
  createProfile: vi.fn(),
  book: vi.fn(),
  getToken: vi.fn(),
}));
const realtime = vi.hoisted(() => ({
  callbacks: new Set<() => void | Promise<void>>(),
}));
vi.mock("./publicClinicApi", () => ({ publicClinicApi: api }));
vi.mock("../../realtime", () => ({
  usePublicQueueRealtime: (
    _id: string,
    callback: () => void | Promise<void>,
  ) => {
    realtime.callbacks.add(callback);
    return "connected";
  },
}));

const clinic: PublicClinicDetails = {
  name: "Hope Clinic",
  slug: "hope",
  sessions: [
    {
      id: "session-1",
      name: "Morning session",
      bookingOpenTime: "2026-08-19T05:00:00Z",
      startTime: "2026-08-19T06:00:00Z",
      endTime: "2026-08-19T10:00:00Z",
      lastBookingTime: "2026-08-19T09:00:00Z",
      status: "Running",
      isBookingOpen: true,
      currentTokenNumber: 4,
      approximateQueueSize: 3,
    },
  ],
};
const tokenState: PublicTokenState = {
  sessionId: "session-1",
  tokenId: "token-1",
  tokenNumber: 8,
  status: "Waiting",
  currentTokenNumber: 4,
  position: 4,
  patientsAhead: 3,
  estimatedWaitMinutes: 30,
  isPaused: false,
  sessionStatus: "Running",
  updatedAt: "2026-08-19T06:30:00Z",
};
function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/c/hope"]}>
      <Routes>
        <Route path="/c/:clinicSlug" element={<PublicClinicEntryPage />} />
      </Routes>
    </MemoryRouter>,
  );
}
function storeToken() {
  sessionStorage.setItem(
    "digiline.active-token",
    JSON.stringify({
      sessionId: "session-1",
      tokenId: "token-1",
      tokenNumber: 8,
      wasAlreadyBooked: false,
      accessToken: "patient-access",
      accessExpiresAt: new Date(Date.now() + 60_000).toISOString(),
      clinicSlug: "hope",
    }),
  );
}

describe("PublicClinicEntryPage", () => {
  beforeEach(() => {
    sessionStorage.clear();
    realtime.callbacks.clear();
    Object.values(api).forEach((mock) => mock.mockReset());
    api.getBySlug.mockResolvedValue(clinic);
  });

  it("handles invalid clinic links without exposing data", async () => {
    api.getBySlug.mockRejectedValue(new ApiError("Missing", 404));
    renderPage();
    expect(
      await screen.findByText(
        "This clinic link is invalid or no longer available.",
      ),
    ).toBeInTheDocument();
  });

  it("verifies identity, selects a profile, books, and displays the token", async () => {
    api.requestVerification.mockResolvedValue({
      challengeId: "challenge-1",
      developmentCode: "123456",
    });
    api.confirmVerification.mockResolvedValue({
      accessToken: "patient-access",
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      profiles: [{ id: "profile-1", name: "Asha", age: 29 }],
    });
    api.book.mockResolvedValue({
      sessionId: "session-1",
      tokenId: "token-1",
      tokenNumber: 8,
      wasAlreadyBooked: false,
    });
    api.getToken.mockResolvedValue(tokenState);
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Join queue" }));
    await user.type(screen.getByLabelText(/Phone number/), "9876543210");
    await user.click(
      screen.getByRole("button", { name: "Send verification code" }),
    );
    expect(await screen.findByText(/Development code/)).toHaveTextContent(
      "123456",
    );
    await user.type(screen.getByLabelText(/6-digit code/), "123456");
    await user.click(screen.getByRole("button", { name: "Verify" }));
    await user.click(await screen.findByRole("button", { name: /Asha/ }));
    await user.click(
      screen.getByRole("button", { name: "Confirm and join queue" }),
    );
    expect(await screen.findByText("#8")).toBeInTheDocument();
    expect(
      screen.getByText("Patients ahead").nextElementSibling,
    ).toHaveTextContent("3");
    expect(sessionStorage.getItem("digiline.active-token")).toContain(
      "patient-access",
    );
  });

  it("restores a paused token and preserves its queue place", async () => {
    storeToken();
    api.getToken.mockResolvedValue({
      ...tokenState,
      isPaused: true,
      sessionStatus: "Paused",
      estimatedWaitMinutes: null,
    });
    renderPage();
    expect(
      await screen.findByText(
        "The session is paused. Your place in the queue is safe.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Estimated wait").nextElementSibling,
    ).toHaveTextContent("—");
  });

  it("keeps an issued token visible after booking closes", async () => {
    storeToken();
    api.getBySlug.mockResolvedValue({ ...clinic, sessions: [] });
    api.getToken.mockResolvedValue({ ...tokenState, sessionStatus: "Closed" });

    renderPage();

    expect(await screen.findByText("#8")).toBeInTheDocument();
    expect(screen.getByText("Patients ahead").nextElementSibling).toHaveTextContent("3");
  });

  it("refetches authoritative token state after public real-time events", async () => {
    storeToken();
    api.getToken
      .mockResolvedValueOnce(tokenState)
      .mockResolvedValue({
        ...tokenState,
        currentTokenNumber: 5,
        patientsAhead: 2,
      });
    renderPage();
    expect(await screen.findByText("#4")).toBeInTheDocument();
    await act(async () => {
      await Promise.all([...realtime.callbacks].map((callback) => callback()));
    });
    expect(await screen.findByText("#5")).toBeInTheDocument();
    expect(api.getToken).toHaveBeenCalledTimes(2);
  });

  it.each(["Running", "Paused"] as const)("keeps a %s session visible but disables joining after booking stops", async status => {
    api.getBySlug.mockResolvedValue({
      ...clinic,
      sessions: [{ ...clinic.sessions[0], status, isBookingOpen: false }],
    });
    renderPage();
    expect(await screen.findByText("Morning session")).toBeInTheDocument();
    expect(screen.getByText("#4")).toBeInTheDocument();
    expect(screen.getByText(/Booking closed/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Join queue" })).toBeDisabled();
  });

  it("shows pre-start, running, and paused sessions with open booking", async () => {
    api.getBySlug.mockResolvedValue({ ...clinic, sessions: [
      { ...clinic.sessions[0], id: "scheduled", name: "Scheduled", status: "Scheduled" },
      { ...clinic.sessions[0], id: "pre-start", name: "Pre-start session", status: "BookingOpen" },
      { ...clinic.sessions[0], id: "paused", name: "Paused session", status: "Paused" },
      { ...clinic.sessions[0], id: "closed", name: "Closed", status: "Closed", isBookingOpen: false },
      clinic.sessions[0],
    ] });
    renderPage();

    expect(await screen.findByText("Morning session")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Join queue" })).toHaveLength(3);
    expect(screen.getByText("Pre-start session")).toBeInTheDocument();
    expect(screen.getByText("Paused session")).toBeInTheDocument();
    expect(screen.getByText("Booking open · Doctor paused")).toBeInTheDocument();
    expect(screen.queryByText("Scheduled")).not.toBeInTheDocument();
    expect(screen.queryByText("Closed")).not.toBeInTheDocument();
  });

  it.each(["Running", "Paused"] as const)("restores and refreshes an existing token after booking stops in %s", async sessionStatus => {
    storeToken();
    api.getBySlug.mockResolvedValue({ ...clinic, sessions: [{ ...clinic.sessions[0], status: sessionStatus, isBookingOpen: false }] });
    api.getToken.mockResolvedValue({ ...tokenState, sessionStatus, isPaused: sessionStatus === "Paused" });
    renderPage();
    expect(await screen.findByText("#8")).toBeInTheDocument();
    expect(screen.getByText("#4")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Join queue" })).not.toBeInTheDocument();
    api.getToken.mockResolvedValue({ ...tokenState, sessionStatus, isPaused: sessionStatus === "Paused", currentTokenNumber: 5 });
    await act(async () => { await Promise.all([...realtime.callbacks].map(callback => callback())); });
    expect(await screen.findByText("#5")).toBeInTheDocument();
  });

  it("closes an in-progress joining flow when availability refresh reports stopped booking", async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Join queue" }));
    expect(screen.getByLabelText(/Phone number/)).toBeInTheDocument();
    api.getBySlug.mockResolvedValue({ ...clinic, sessions: [{ ...clinic.sessions[0], isBookingOpen: false }] });
    await act(async () => { document.dispatchEvent(new Event("visibilitychange")); });
    expect(await screen.findByText("Booking closed")).toBeInTheDocument();
    expect(screen.queryByLabelText(/Phone number/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Join queue" })).toBeDisabled();
    expect(api.book).not.toHaveBeenCalled();
  });

  it("shows a newly running session when the page becomes visible", async () => {
    api.getBySlug.mockResolvedValueOnce({ ...clinic, sessions: [] }).mockResolvedValue(clinic);
    renderPage();
    expect(await screen.findByText("No live sessions or open bookings")).toBeInTheDocument();

    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });

    expect(await screen.findByRole("button", { name: "Join queue" })).toBeInTheDocument();
  });
  it("removes a completed session from the public listing after refresh", async () => {
    renderPage();
    expect(await screen.findByText("Booking open")).toBeInTheDocument();
    api.getBySlug.mockResolvedValue({ ...clinic, sessions: [] });
    await act(async () => { document.dispatchEvent(new Event("visibilitychange")); });
    expect(await screen.findByText("No live sessions or open bookings")).toBeInTheDocument();
    expect(screen.queryByText("Booking open")).not.toBeInTheDocument();
  });

});
