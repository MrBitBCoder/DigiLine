import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
  Link,
  MemoryRouter,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { AuthContext } from "../auth/AuthContext";
import type { AuthSession } from "../auth/authSession";
import { AppLayout } from "./AppLayout";

const session: AuthSession = {
  token: "token",
  expiresAt: Date.now() + 60_000,
  user: {
    id: "doctor-1",
    clinicId: "clinic-1",
    email: "doctor@example.test",
    name: "Dr Rao",
    role: "Doctor",
    roles: ["Doctor"],
  },
};

function Location() {
  const location = useLocation();
  return (
    <output aria-label="Current location">
      {location.pathname}
      {location.search}
    </output>
  );
}

function Dashboard() {
  return (
    <>
      <h1>Doctor dashboard page</h1>
      <Location />
    </>
  );
}

function Sessions() {
  return (
    <>
      <h1>Sessions page</h1>
      <Link to="/doctor?session=session-7">View queue</Link>
      <Location />
    </>
  );
}

function renderAt(path: string) {
  return render(
    <AuthContext.Provider value={{ session, login: vi.fn(), logout: vi.fn() }}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/" element={<AppLayout />}>
            <Route path="doctor" element={<Dashboard />} />
            <Route path="doctor/sessions" element={<Sessions />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}

function doctorNavigation() {
  return {
    dashboard: screen.getByRole("link", { name: "Doctor dashboard" }),
    sessions: screen.getByRole("link", { name: "Sessions" }),
  };
}

describe("doctor navigation", () => {
  it("exposes a controlled menu and restores focus when Escape closes it", async () => {
    const user = userEvent.setup();
    renderAt("/doctor");
    const menu = screen.getByRole("button", { name: "Menu" });

    expect(menu).toHaveAttribute("aria-expanded", "false");
    expect(menu).toHaveAttribute("aria-controls");
    await user.click(menu);
    expect(screen.getByRole("button", { name: "Close menu" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );

    await user.keyboard("{Escape}");
    expect(screen.getByRole("button", { name: "Menu" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    expect(screen.getByRole("button", { name: "Menu" })).toHaveFocus();

    await user.click(screen.getByRole("button", { name: "Menu" }));
    fireEvent.pointerDown(document.body);
    expect(screen.getByRole("button", { name: "Menu" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });

  it("closes the menu after navigation while preserving destination heading focus", async () => {
    const user = userEvent.setup();
    renderAt("/doctor");
    await user.click(screen.getByRole("button", { name: "Menu" }));
    await user.click(doctorNavigation().sessions);

    expect(screen.getByRole("button", { name: "Menu" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Sessions page" }),
      ).toHaveFocus(),
    );
  });

  it("marks only Doctor Dashboard current, including with a selected-session query", async () => {
    renderAt("/doctor?session=session-7");
    const nav = doctorNavigation();
    expect(nav.dashboard).toHaveAttribute("aria-current", "page");
    expect(nav.dashboard).toHaveClass("active");
    expect(nav.sessions).not.toHaveAttribute("aria-current");
    expect(screen.getAllByRole("link", { current: "page" })).toEqual([
      nav.dashboard,
    ]);
    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Doctor dashboard page" }),
      ).toHaveFocus(),
    );
  });

  it("marks only Sessions current and moves focus to its page heading", async () => {
    renderAt("/doctor/sessions");
    const nav = doctorNavigation();
    expect(nav.sessions).toHaveAttribute("aria-current", "page");
    expect(nav.sessions).toHaveClass("active");
    expect(nav.dashboard).not.toHaveAttribute("aria-current");
    expect(screen.getAllByRole("link", { current: "page" })).toEqual([
      nav.sessions,
    ]);
    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Sessions page" }),
      ).toHaveFocus(),
    );
  });

  it("supports keyboard route changes and clears focus from the activated nav item", async () => {
    const user = userEvent.setup();
    renderAt("/doctor");
    const sessionsLink = doctorNavigation().sessions;
    sessionsLink.focus();
    await user.keyboard("{Enter}");
    expect(await screen.findByLabelText("Current location")).toHaveTextContent(
      "/doctor/sessions",
    );
    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Sessions page" }),
      ).toHaveFocus(),
    );
    expect(sessionsLink).not.toHaveFocus();
  });

  it("View queue selects the session, returns to Dashboard, and supports returning to Sessions", async () => {
    const user = userEvent.setup();
    renderAt("/doctor/sessions");
    await user.click(screen.getByRole("link", { name: "View queue" }));
    expect(await screen.findByLabelText("Current location")).toHaveTextContent(
      "/doctor?session=session-7",
    );
    expect(doctorNavigation().dashboard).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(doctorNavigation().sessions).not.toHaveAttribute("aria-current");
    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Doctor dashboard page" }),
      ).toHaveFocus(),
    );

    await user.click(doctorNavigation().sessions);
    expect(await screen.findByLabelText("Current location")).toHaveTextContent(
      "/doctor/sessions",
    );
    expect(doctorNavigation().sessions).toHaveAttribute("aria-current", "page");
    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Sessions page" }),
      ).toHaveFocus(),
    );
  });
});
