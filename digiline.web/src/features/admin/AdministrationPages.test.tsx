import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, expect, it, vi } from "vitest";
import { AdministrationPage } from "./AdministrationPages";
import { ApiError } from "../../api/httpClient";
import { ToastProvider } from "../../shared/ui";

const api = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("../../api/httpClient", async importOriginal => ({ ...await importOriginal<typeof import("../../api/httpClient")>(), request: api.request }));
const created = { id: "staff-1", name: "New staff", email: "staff@example.test", roles: [1,2], isActive: true, invitationPending: false, deliveryFailed: false };
beforeEach(() => {
  api.request.mockReset();
  api.request.mockImplementation(async (url: string, options?: { method?: string }) => {
    if (options?.method === "POST") return created;
    if (url === "/api/admin/clinic") return { name: "Clinic", slug: "clinic", timeZoneId: "Asia/Kolkata" };
    return { items: [created], page: 1, pageSize: 10, totalCount: 1 };
  });
});
function show() { render(<ToastProvider><MemoryRouter><AdministrationPage /></MemoryRouter></ToastProvider>); }
async function fill(confirm = "VerificationOnly123!") {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText(/Staff name/), "New staff");
  await user.type(screen.getByLabelText(/Staff email/), "staff@example.test");
  await user.type(screen.getByLabelText(/^Password/), "VerificationOnly123!");
  await user.type(screen.getByLabelText(/^Confirm password/), confirm);
  await user.click(screen.getByLabelText("Doctor"));
  return user;
}
it("creates staff with selected roles and password without invitation terminology or actions", async () => {
  show(); const user = await fill();
  expect(screen.getByRole("heading", { name: "Add staff" })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /invitation/i })).not.toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Create staff" }));
  await waitFor(() => expect(api.request).toHaveBeenCalledWith("/api/admin/staff", { method: "POST", body: { name: "New staff", email: "staff@example.test", roles: [1,2], password: "VerificationOnly123!" } }));
  expect(await screen.findByRole("status")).toHaveTextContent("Staff account created. They can sign in immediately.");
  expect(screen.getByLabelText(/^Password/)).toHaveValue("");
  expect(screen.getByLabelText(/^Confirm password/)).toHaveValue("");
});
it("rejects confirmation mismatch without posting a password", async () => {
  show(); const user = await fill("DifferentPassword123!");
  await user.click(screen.getByRole("button", { name: "Create staff" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Passwords do not match.");
  expect(api.request.mock.calls.filter(([, options]) => options?.method === "POST")).toHaveLength(0);
});
it("shows authoritative password-policy errors", async () => {
  show(); const user = await fill();
  api.request.mockRejectedValueOnce(new Error("Use at least 12 characters with uppercase, lowercase, number, and symbol."));
  await user.click(screen.getByRole("button", { name: "Create staff" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("uppercase, lowercase, number, and symbol");
});
it("keeps entered staff values and suppresses success when the email already exists", async () => {
  show(); const user = await fill();
  api.request.mockRejectedValueOnce(new ApiError("Conflict", 409, {
    title: "The request conflicts with the current state.",
    detail: "That email is already registered.",
  }));

  await user.click(screen.getByRole("button", { name: "Create staff" }));

  expect(await screen.findByRole("alert")).toHaveTextContent("That email is already registered.");
  expect(screen.getByLabelText(/Staff name/)).toHaveValue("New staff");
  expect(screen.getByLabelText(/Staff email/)).toHaveValue("staff@example.test");
  expect(screen.getByLabelText(/^Password/)).toHaveValue("VerificationOnly123!");
  expect(screen.queryByRole("status")).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Create staff" })).toBeEnabled();
});
it("retains resend and revoke only for legacy pending invitations", async () => {
  api.request.mockImplementation(async (url: string) => url === "/api/admin/clinic" ? { name: "Clinic", slug: "clinic", timeZoneId: "Asia/Kolkata" } : { items: [{ ...created, invitationPending: true, isActive: false }], page: 1, pageSize: 10, totalCount: 1 });
  show();
  expect(await screen.findByRole("button", { name: "Resend invitation" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Revoke invitation" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Create staff" })).toBeInTheDocument();
});
