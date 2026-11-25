import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api/httpClient";
import type {
  PublicClinicDetails,
  PublicTokenState,
} from "../../shared/types/domain";
import { PublicClinicEntryPage } from "./PublicClinicEntryPage";
import { ToastProvider } from "../../shared/ui";

const api = vi.hoisted(() => ({
  getBySlug: vi.fn(),
  requestVerification: vi.fn(),
  confirmVerification: vi.fn(),
  createProfile: vi.fn(),
  book: vi.fn(),
  recover: vi.fn(),
  getToken: vi.fn(),
  cancelToken: vi.fn(),
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
    <ToastProvider>
      <MemoryRouter initialEntries={["/c/hope"]}>
        <Routes>
          <Route path="/c/:clinicSlug" element={<PublicClinicEntryPage />} />
        </Routes>
      </MemoryRouter>
    </ToastProvider>,
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
    expect(await screen.findByText("Clinic time")).toBeInTheDocument();
    expect(screen.getByText("Booking starts")).toBeInTheDocument();
    expect(screen.getByText("Booking open")).toBeInTheDocument();
    await user.click(await screen.findByRole("button", { name: "Join queue" }));
    expect(
      screen.getByRole("button", { name: "Close join queue" }),
    ).toHaveFocus();
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
    expect(
      await screen.findByRole("heading", { name: "You are in the queue" }),
    ).toBeInTheDocument();
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

  it("keeps the patient Called state and current-serving token visible", async () => {
    storeToken();
    api.getToken.mockResolvedValue({
      ...tokenState,
      status: "Called",
      currentTokenNumber: 8,
      position: null,
      patientsAhead: null,
      estimatedWaitMinutes: null,
    });

    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Doctor is calling you now" }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Your token #8")).toHaveTextContent("Called");
    expect(
      screen.getByText("Now serving").nextElementSibling,
    ).toHaveTextContent("#8");
  });

  it.each([
    ["Waiting", "You are in the queue", "Waiting"],
    ["Called", "Doctor is calling you now", "Called"],
    ["InConsultation", "Consultation in progress", "In consultation"],
    ["Completed", "Consultation completed", "Completed"],
  ] as const)(
    "presents the %s lifecycle state and progress clearly",
    async (status, heading, step) => {
      storeToken();
      api.getToken.mockResolvedValue({ ...tokenState, status });
      renderPage();

      expect(
        await screen.findByRole("heading", { name: heading }),
      ).toBeInTheDocument();
      expect(screen.getByLabelText("Your token #8")).toBeInTheDocument();
      const progress = screen.getByRole("navigation", {
        name: "Token progress",
      });
      expect(within(progress).getByText(step).closest("li")).toHaveAttribute(
        "aria-current",
        "step",
      );
    },
  );

  it("restores focus when the patient closes the join flow", async () => {
    const user = userEvent.setup();
    renderPage();
    const join = await screen.findByRole("button", { name: "Join queue" });
    await user.click(join);
    await user.click(screen.getByRole("button", { name: "Close join queue" }));
    expect(join).toHaveFocus();
  });

  it.each(["Waiting", "Late", "Skipped"] as const)(
    "allows a patient to cancel a %s token with safe initial focus",
    async (status) => {
      storeToken();
      api.getToken.mockResolvedValue({ ...tokenState, status });
      api.cancelToken.mockResolvedValue({
        ...tokenState,
        status: "Cancelled",
        position: null,
        patientsAhead: null,
        estimatedWaitMinutes: null,
      });
      const user = userEvent.setup();
      renderPage();

      await user.click(
        await screen.findByRole("button", { name: "Cancel token" }),
      );
      const dialog = screen.getByRole("dialog");
      expect(
        within(dialog).getByRole("heading", { name: "Cancel your token?" }),
      ).toBeInTheDocument();
      expect(
        screen.getByText(
          "You will leave the queue and token #8 will no longer be called.",
        ),
      ).toBeInTheDocument();
      expect(
        within(dialog).getByRole("button", { name: "Keep my token" }),
      ).toHaveFocus();
      await user.click(
        within(dialog).getByRole("button", { name: "Cancel token" }),
      );

      expect(api.cancelToken).toHaveBeenCalledWith(
        "patient-access",
        "session-1",
        "token-1",
      );
      expect(
        await screen.findByText(
          "You left the queue. Token #8 will no longer be called.",
        ),
      ).toBeInTheDocument();
      expect(screen.getByText("Token #8 was cancelled.")).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Cancel token" }),
      ).not.toBeInTheDocument();
      expect(sessionStorage.getItem("digiline.active-token")).toContain(
        "token-1",
      );
    },
  );

  it.each([
    "Called",
    "InConsultation",
    "Completed",
    "Cancelled",
    "NoShow",
  ] as const)(
    "does not offer patient cancellation for a %s token",
    async (status) => {
      storeToken();
      api.getToken.mockResolvedValue({ ...tokenState, status });
      renderPage();

      expect(await screen.findByText("#8")).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Cancel token" }),
      ).not.toBeInTheDocument();
    },
  );

  it("keeps the token and permits retry after cancellation fails", async () => {
    storeToken();
    api.getToken.mockResolvedValue(tokenState);
    api.cancelToken.mockRejectedValue(
      new ApiError("This token can no longer be cancelled.", 409, {
        detail: "This token can no longer be cancelled.",
      }),
    );
    const user = userEvent.setup();
    renderPage();

    await user.click(
      await screen.findByRole("button", { name: "Cancel token" }),
    );
    await user.click(
      within(screen.getByRole("dialog")).getByRole("button", {
        name: "Cancel token",
      }),
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "This token can no longer be cancelled.",
    );
    expect(
      within(screen.getByRole("dialog")).getByRole("button", {
        name: "Cancel token",
      }),
    ).toBeEnabled();
    expect(screen.queryByText(/was cancelled/)).not.toBeInTheDocument();
    expect(sessionStorage.getItem("digiline.active-token")).toContain(
      "token-1",
    );
  });

  it("restores a cancelled token as history without offering recovery or cancellation", async () => {
    storeToken();
    api.getToken.mockResolvedValue({
      ...tokenState,
      status: "Cancelled",
      position: null,
      patientsAhead: null,
      estimatedWaitMinutes: null,
    });

    renderPage();

    expect(
      await screen.findByText(
        "You left the queue. Token #8 will no longer be called.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Token cancelled" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Cancelled")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Cancel token" }),
    ).not.toBeInTheDocument();
    expect(api.recover).not.toHaveBeenCalled();
  });

  it("keeps an issued token visible after booking closes", async () => {
    storeToken();
    api.getBySlug.mockResolvedValue({ ...clinic, sessions: [] });
    api.getToken.mockResolvedValue({ ...tokenState, sessionStatus: "Closed" });

    renderPage();

    expect(await screen.findByText("#8")).toBeInTheDocument();
    expect(
      screen.getByText("Patients ahead").nextElementSibling,
    ).toHaveTextContent("3");
  });

  it("refetches authoritative token state after public real-time events", async () => {
    storeToken();
    api.getToken.mockResolvedValueOnce(tokenState).mockResolvedValue({
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

  it.each(["Running", "Paused"] as const)(
    "keeps a %s session visible but disables joining after booking stops",
    async (status) => {
      api.getBySlug.mockResolvedValue({
        ...clinic,
        sessions: [{ ...clinic.sessions[0], status, isBookingOpen: false }],
      });
      renderPage();
      expect(await screen.findByText("Morning session")).toBeInTheDocument();
      expect(screen.getByText("#4")).toBeInTheDocument();
      expect(screen.getByText(/Booking closed/)).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Join queue" }),
      ).not.toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: "Find my token" }),
      ).toBeEnabled();
    },
  );

  it("recovers an issued token after booking stops without creating anything", async () => {
    api.getBySlug.mockResolvedValue({
      ...clinic,
      sessions: [{ ...clinic.sessions[0], isBookingOpen: false }],
    });
    api.requestVerification.mockResolvedValue({
      challengeId: "challenge-1",
      developmentCode: "123456",
    });
    api.confirmVerification.mockResolvedValue({
      accessToken: "recovered-access",
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      profiles: [{ id: "profile-1", name: "Asha", age: 29 }],
    });
    api.recover.mockResolvedValue({
      sessionId: "session-1",
      tokenId: "token-1",
      tokenNumber: 8,
    });
    api.getToken.mockResolvedValue(tokenState);
    const user = userEvent.setup();
    renderPage();
    await user.click(
      await screen.findByRole("button", { name: "Find my token" }),
    );
    expect(
      screen.getByText(/will not create a new token/i),
    ).toBeInTheDocument();
    await user.type(screen.getByLabelText(/Phone number/), "9876543210");
    await user.click(
      screen.getByRole("button", { name: "Send verification code" }),
    );
    await user.type(await screen.findByLabelText(/6-digit code/), "123456");
    await user.click(screen.getByRole("button", { name: "Verify" }));
    await user.click(await screen.findByRole("button", { name: /Asha/ }));
    const ownToken = await screen.findByLabelText("Your token #8");
    expect(ownToken).toBeInTheDocument();
    expect(
      ownToken.compareDocumentPosition(screen.getByText("Now serving")) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(
      screen.getByText("Now serving").nextElementSibling,
    ).toHaveTextContent("#4");
    expect(api.recover).toHaveBeenCalledWith(
      "recovered-access",
      "session-1",
      "profile-1",
    );
    expect(api.book).not.toHaveBeenCalled();
    expect(api.createProfile).not.toHaveBeenCalled();
    expect(sessionStorage.getItem("digiline.active-token")).toContain(
      "recovered-access",
    );
  });

  it("does not disclose failed recovery details and remains retryable", async () => {
    api.getBySlug.mockResolvedValue({
      ...clinic,
      sessions: [{ ...clinic.sessions[0], isBookingOpen: false }],
    });
    api.requestVerification.mockResolvedValue({ challengeId: "challenge-1" });
    api.confirmVerification.mockResolvedValue({
      accessToken: "access",
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      profiles: [{ id: "profile-1", name: "Asha", age: 29 }],
    });
    api.recover.mockRejectedValue(new ApiError("Not found", 404));
    const user = userEvent.setup();
    renderPage();
    await user.click(
      await screen.findByRole("button", { name: "Find my token" }),
    );
    await user.type(screen.getByLabelText(/Phone number/), "9876543210");
    await user.click(
      screen.getByRole("button", { name: "Send verification code" }),
    );
    await user.type(await screen.findByLabelText(/6-digit code/), "123456");
    await user.click(screen.getByRole("button", { name: "Verify" }));
    await user.click(await screen.findByRole("button", { name: /Asha/ }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "We couldn't find an active token for this patient in this session.",
    );
    expect(screen.getByRole("button", { name: /Asha/ })).toBeEnabled();
    expect(api.book).not.toHaveBeenCalled();
  });

  it("shows pre-start, running, and paused sessions with open booking", async () => {
    api.getBySlug.mockResolvedValue({
      ...clinic,
      sessions: [
        {
          ...clinic.sessions[0],
          id: "scheduled",
          name: "Scheduled",
          status: "Scheduled",
        },
        {
          ...clinic.sessions[0],
          id: "pre-start",
          name: "Pre-start session",
          status: "BookingOpen",
        },
        {
          ...clinic.sessions[0],
          id: "paused",
          name: "Paused session",
          status: "Paused",
        },
        {
          ...clinic.sessions[0],
          id: "closed",
          name: "Closed",
          status: "Closed",
          isBookingOpen: false,
        },
        clinic.sessions[0],
      ],
    });
    renderPage();

    expect(await screen.findByText("Morning session")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Join queue" })).toHaveLength(
      3,
    );
    expect(screen.getByText("Pre-start session")).toBeInTheDocument();
    expect(screen.getByText("Paused session")).toBeInTheDocument();
    expect(screen.getByText("Paused")).toBeInTheDocument();
    expect(screen.getAllByText("Booking open")).toHaveLength(3);
    expect(screen.queryByText("Scheduled")).not.toBeInTheDocument();
    expect(screen.queryByText("Closed")).not.toBeInTheDocument();
  });

  it("shows an upcoming booking time in the clinic timezone", async () => {
    api.getBySlug.mockResolvedValue({
      ...clinic,
      timeZoneId: "Asia/Kolkata",
      sessions: [
        {
          ...clinic.sessions[0],
          status: "BookingOpen",
          isBookingOpen: false,
          bookingOpenTime: "2099-08-19T03:30:00Z",
          startTime: "2099-08-19T04:30:00Z",
          endTime: "2099-08-19T07:30:00Z",
        },
      ],
    });

    renderPage();

    expect(
      await screen.findByText(/Booking opens at 9:00 (?:AM|a\.m\.)/i),
    ).toBeInTheDocument();
    expect(screen.getByText("Booking starts").parentElement).toHaveTextContent(
      /9:00 (?:AM|a\.m\.)/i,
    );
    expect(screen.getByText("Clinic time").parentElement).toHaveTextContent(
      /10:00 (?:AM|a\.m\.) – 1:00 (?:PM|p\.m\.)/i,
    );
    expect(screen.getByRole("button", { name: "Join queue" })).toBeDisabled();
    expect(
      screen.queryByRole("button", { name: "Find my token" }),
    ).not.toBeInTheDocument();
  });

  it("refreshes patient timing after the doctor edits the session", async () => {
    api.getBySlug.mockResolvedValue({ ...clinic, timeZoneId: "UTC" });
    renderPage();
    expect(await screen.findByText("Clinic time")).toBeInTheDocument();

    api.getBySlug.mockResolvedValue({
      ...clinic,
      timeZoneId: "UTC",
      sessions: [
        {
          ...clinic.sessions[0],
          bookingOpenTime: "2026-08-19T05:30:00Z",
          startTime: "2026-08-19T07:00:00Z",
          endTime: "2026-08-19T11:00:00Z",
        },
      ],
    });
    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });

    expect(screen.getByText("Booking starts").parentElement).toHaveTextContent(
      /5:30 (?:AM|a\.m\.)/i,
    );
    expect(screen.getByText("Clinic time").parentElement).toHaveTextContent(
      /7:00 (?:AM|a\.m\.) – 11:00 (?:AM|a\.m\.)/i,
    );
  });

  it.each(["Running", "Paused"] as const)(
    "restores and refreshes an existing token after booking stops in %s",
    async (sessionStatus) => {
      storeToken();
      api.getBySlug.mockResolvedValue({
        ...clinic,
        sessions: [
          {
            ...clinic.sessions[0],
            status: sessionStatus,
            isBookingOpen: false,
          },
        ],
      });
      api.getToken.mockResolvedValue({
        ...tokenState,
        sessionStatus,
        isPaused: sessionStatus === "Paused",
      });
      renderPage();
      expect(await screen.findByText("#8")).toBeInTheDocument();
      expect(screen.getByText("#4")).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Join queue" }),
      ).not.toBeInTheDocument();
      api.getToken.mockResolvedValue({
        ...tokenState,
        sessionStatus,
        isPaused: sessionStatus === "Paused",
        currentTokenNumber: 5,
      });
      await act(async () => {
        await Promise.all(
          [...realtime.callbacks].map((callback) => callback()),
        );
      });
      expect(await screen.findByText("#5")).toBeInTheDocument();
    },
  );

  it("closes an in-progress joining flow when availability refresh reports stopped booking", async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Join queue" }));
    expect(screen.getByLabelText(/Phone number/)).toBeInTheDocument();
    api.getBySlug.mockResolvedValue({
      ...clinic,
      sessions: [{ ...clinic.sessions[0], isBookingOpen: false }],
    });
    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    expect(await screen.findByText("Booking closed")).toBeInTheDocument();
    expect(screen.queryByLabelText(/Phone number/)).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Join queue" }),
    ).not.toBeInTheDocument();
    expect(api.book).not.toHaveBeenCalled();
  });

  it("shows a newly running session when the page becomes visible", async () => {
    api.getBySlug
      .mockResolvedValueOnce({ ...clinic, sessions: [] })
      .mockResolvedValue(clinic);
    renderPage();
    expect(
      await screen.findByText("No live sessions or open bookings"),
    ).toBeInTheDocument();

    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });

    expect(
      await screen.findByRole("button", { name: "Join queue" }),
    ).toBeInTheDocument();
  });
  it("removes a completed session from the public listing after refresh", async () => {
    renderPage();
    expect(await screen.findByText("Booking open")).toBeInTheDocument();
    api.getBySlug.mockResolvedValue({ ...clinic, sessions: [] });
    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    expect(
      await screen.findByText("No live sessions or open bookings"),
    ).toBeInTheDocument();
    expect(screen.queryByText("Booking open")).not.toBeInTheDocument();
  });
});
