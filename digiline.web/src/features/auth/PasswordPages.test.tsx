import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { StrictMode } from "react";
import { afterEach, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import {
  ChangePasswordPage,
  ForgotPasswordPage,
  ResetPasswordPage,
} from "./PasswordPages";

afterEach(() => vi.unstubAllGlobals());

it("offers email recovery without revealing whether the address exists", async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(
      new Response(
        JSON.stringify({
          message: "If that email is registered, a reset link has been sent.",
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
  vi.stubGlobal("fetch", fetchMock);
  const user = userEvent.setup();
  render(
    <MemoryRouter>
      <ForgotPasswordPage />
    </MemoryRouter>,
  );
  await user.type(
    screen.getByLabelText(/Email address/),
    "doctor@example.test",
  );
  await user.click(screen.getByRole("button", { name: "Send reset link" }));
  expect(
    await screen.findByText(
      "If that email is registered, a reset link has been sent.",
    ),
  ).toBeInTheDocument();
  expect(fetchMock).toHaveBeenCalledTimes(1);
});

it("checks confirmation before submitting a password change", async () => {
  const fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  const user = userEvent.setup();
  render(
    <AuthContext.Provider
      value={{ session: null, login: vi.fn(), logout: vi.fn() }}
    >
      <MemoryRouter>
        <ChangePasswordPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  );
  await user.type(screen.getByLabelText(/Current password/), "OldPassword123!");
  await user.type(screen.getByLabelText(/^New password/), "Replacement123!");
  await user.type(
    screen.getByLabelText(/Confirm new password/),
    "different123!",
  );
  await user.click(screen.getByRole("button", { name: "Change password" }));
  expect(screen.getByRole("alert")).toHaveTextContent(
    "Passwords do not match.",
  );
  expect(fetchMock).not.toHaveBeenCalled();
});

it("reads a reset secret from the fragment and removes it from the address", async () => {
  window.history.replaceState(null, "", "/reset-password#token=secret-token");
  render(
    <StrictMode>
      <MemoryRouter>
        <ResetPasswordPage />
      </MemoryRouter>
    </StrictMode>,
  );
  expect(screen.getByLabelText(/^New password/)).toBeInTheDocument();
  await waitFor(() => expect(window.location.hash).toBe(""));
});
