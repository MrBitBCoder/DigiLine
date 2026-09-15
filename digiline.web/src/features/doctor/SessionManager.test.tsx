import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, expect, it, vi } from "vitest";
import type { DoctorSessionSummary } from "../../shared/types/domain";
import { SessionManager } from "./SessionManager";

const api = vi.hoisted(() => ({ listSessions: vi.fn(), clinicTimeZone: vi.fn(), updateSession: vi.fn(), deleteSession: vi.fn(), createSession: vi.fn() }));
vi.mock("./doctorApi", () => ({ doctorApi: api }));

const start = new Date(Date.now() + 3_600_000);
const session: DoctorSessionSummary = {
  id: "session-1", clinicId: "clinic-1", clinicName: "Clinic", doctorId: "doctor-1", doctorName: "Doctor",
  name: "Morning session", bookingOpenTime: new Date().toISOString(), startTime: start.toISOString(),
  endTime: new Date(start.getTime() + 3_600_000).toISOString(), lastBookingTime: new Date(start.getTime() + 3_600_000).toISOString(),
  averageConsultationMinutes: 10, status: "BookingOpen", isBookingOpen: true,
  queueSummary: { currentToken: null, waitingCount: 0, lateCount: 0, totalCount: 0, completedCount: 0, notAttendedCount: 0 },
};

const paged = (items: DoctorSessionSummary[]) => ({ items, page: 1, pageSize: 10, totalCount: items.length });

beforeEach(() => { Object.values(api).forEach(mock => mock.mockReset()); api.listSessions.mockResolvedValue(paged([session])); api.clinicTimeZone.mockResolvedValue({ timeZoneId: "Asia/Kolkata" }); });

it("shows aggregate attendance without patient details", async () => {
  api.listSessions.mockResolvedValue(paged([{ ...session, queueSummary: { ...session.queueSummary, totalCount: 5, completedCount: 2, notAttendedCount: 1 } }]));
  render(<SessionManager onSelect={vi.fn()} onDeleted={vi.fn()} />);

  expect(await screen.findByText("Morning session")).toBeInTheDocument();
  const row = screen.getAllByRole("row")[1];
  expect(within(row).getByText("5")).toBeInTheDocument();
  expect(within(row).getByText("2")).toBeInTheDocument();
  expect(within(row).getByText("1")).toBeInTheDocument();
  expect(screen.getByRole("table", { name: "Your sessions" })).toBeInTheDocument();
  expect(screen.queryByText(/Patient /)).not.toBeInTheDocument();
});

it("shows every doctor session in the API's newest-first order", async () => {
  api.listSessions.mockResolvedValue(paged([{ ...session, id: "later", name: "Later session" }, { ...session, id: "earlier", name: "Earlier session" }]));
  render(<SessionManager onSelect={vi.fn()} onDeleted={vi.fn()} />);

  await screen.findByText("Later session");
  const rows = screen.getAllByRole("row");
  expect(rows).toHaveLength(3);
  expect(within(rows[1]).getByText("Later session")).toBeInTheDocument();
  expect(within(rows[2]).getByText("Earlier session")).toBeInTheDocument();
});

it("sends clinic-local date and name filters to the server", async () => {
  render(<SessionManager onSelect={vi.fn()} onDeleted={vi.fn()} />);
  await screen.findByText("Morning session");
  fireEvent.change(screen.getByLabelText("Date filter"), { target: { value: "day" } });
  fireEvent.change(screen.getByLabelText("Session date"), { target: { value: "2026-09-15" } });
  fireEvent.change(screen.getByLabelText("Search by name"), { target: { value: "Morning" } });
  await waitFor(() => {
    const query = new URLSearchParams(api.listSessions.mock.calls.at(-1)?.[1]);
    expect(query.get("name")).toBe("Morning"); expect(query.get("fromDate")).toBe("2026-09-15"); expect(query.get("toDate")).toBe("2026-09-15");
  });
});
it("reports invalid date ranges and retains filter controls for empty results", async () => {
  render(<SessionManager onSelect={vi.fn()} onDeleted={vi.fn()} />);
  await screen.findByText("Morning session");
  fireEvent.change(screen.getByLabelText("Date filter"), { target: { value: "range" } });
  fireEvent.change(screen.getByLabelText("From date"), { target: { value: "2026-09-15" } });
  fireEvent.change(screen.getByLabelText("To date"), { target: { value: "2026-09-14" } });
  expect(screen.getByRole("alert")).toHaveTextContent("End date must be on or after start date.");
  api.listSessions.mockResolvedValue(paged([]));
  fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
  await screen.findByText(/No sessions match/);
  expect(screen.getByLabelText("Search by name")).toBeInTheDocument();
});
it("requests the next server page and resets to page one on filtering", async () => {
  api.listSessions.mockResolvedValue({ ...paged([session]), totalCount: 12 });
  render(<SessionManager onSelect={vi.fn()} onDeleted={vi.fn()} />);
  await screen.findByText("Morning session");
  api.listSessions.mockResolvedValue({ ...paged([{ ...session, id: "second", name: "Second page" }]), page: 2, totalCount: 12 });
  fireEvent.click(screen.getByRole("button", { name: "Next 10" }));
  await screen.findByText("Second page");
  expect(new URLSearchParams(api.listSessions.mock.calls.at(-1)?.[1]).get("page")).toBe("2");
  api.listSessions.mockResolvedValue(paged([session]));
  fireEvent.change(screen.getByLabelText("Search by name"), { target: { value: "Morning" } });
  await screen.findByText("Morning session");
  expect(new URLSearchParams(api.listSessions.mock.calls.at(-1)?.[1]).get("page")).toBe("1");
});

it("creates a session and opens its queue", async () => {
  api.listSessions.mockResolvedValue(paged([]));
  api.createSession.mockResolvedValue({ id: "new-session" });
  const onSelect = vi.fn().mockResolvedValue(undefined);
  const user = userEvent.setup();
  render(<SessionManager onSelect={onSelect} onDeleted={vi.fn()} />);

  await user.click(await screen.findByRole("button", { name: "Add session" }));
  await user.type(screen.getByLabelText(/Session name/), "Afternoon clinic");
  const localValue = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}T${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
  fireEvent.change(screen.getByLabelText(/Start time/), { target: { value: localValue(new Date(Date.now() + 7_200_000)) } });
  fireEvent.change(screen.getByLabelText(/End time/), { target: { value: localValue(new Date(Date.now() + 14_400_000)) } });
  await user.click(screen.getByRole("button", { name: "Create session" }));
  await waitFor(() => expect(api.createSession).toHaveBeenCalled());
  expect(api.createSession).toHaveBeenCalledWith(expect.objectContaining({ bookingOpenTime: null }));
  await waitFor(() => expect(onSelect).toHaveBeenCalledWith("new-session"));
});

it("sends a scheduled booking-open time in UTC", async () => {
  api.listSessions.mockResolvedValue(paged([]));
  api.createSession.mockResolvedValue({ id: "new-session" });
  const user = userEvent.setup();
  render(<SessionManager onSelect={vi.fn().mockResolvedValue(undefined)} onDeleted={vi.fn()} />);
  await user.click(await screen.findByRole("button", { name: "Add session" }));
  const localValue = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}T${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
  const opening = localValue(new Date(Date.now() + 3_600_000));
  fireEvent.change(screen.getByLabelText(/Session name/), { target: { value: "Later clinic" } });
  fireEvent.change(screen.getByLabelText(/Booking opens/), { target: { value: opening } });
  fireEvent.change(screen.getByLabelText(/Start time/), { target: { value: localValue(new Date(Date.now() + 7_200_000)) } });
  fireEvent.change(screen.getByLabelText(/End time/), { target: { value: localValue(new Date(Date.now() + 14_400_000)) } });
  await user.click(screen.getByRole("button", { name: "Create session" }));

  await waitFor(() => expect(api.createSession).toHaveBeenCalledWith(expect.objectContaining({ bookingOpenTime: new Date(opening).toISOString() })));
});

it("rejects a booking-open time after session start", async () => {
  api.listSessions.mockResolvedValue(paged([]));
  const user = userEvent.setup();
  render(<SessionManager onSelect={vi.fn()} onDeleted={vi.fn()} />);
  await user.click(await screen.findByRole("button", { name: "Add session" }));
  const localValue = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}T${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
  const startValue = localValue(new Date(Date.now() + 7_200_000));
  fireEvent.change(screen.getByLabelText(/Session name/), { target: { value: "Later clinic" } });
  fireEvent.change(screen.getByLabelText(/Booking opens/), { target: { value: localValue(new Date(Date.now() + 10_800_000)) } });
  fireEvent.change(screen.getByLabelText(/Start time/), { target: { value: startValue } });
  fireEvent.change(screen.getByLabelText(/End time/), { target: { value: localValue(new Date(Date.now() + 14_400_000)) } });
  await user.click(screen.getByRole("button", { name: "Create session" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Booking must open before the session starts.");
  expect(api.createSession).not.toHaveBeenCalled();
});

it("lists, edits, and deletes an unbooked future session", async () => {
  const user = userEvent.setup();
  api.updateSession.mockResolvedValue(undefined);
  api.deleteSession.mockResolvedValue(undefined);
  const onSelect = vi.fn().mockResolvedValue(undefined);
  const onDeleted = vi.fn().mockResolvedValue(undefined);
  render(<SessionManager onSelect={onSelect} onDeleted={onDeleted} />);

  expect(await screen.findByText("Morning session")).toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Edit" }));
  fireEvent.change(screen.getByLabelText(/Session name/), { target: { value: "Updated session" } });
  fireEvent.change(screen.getByLabelText(/Booking opens/), { target: { value: "" } });
  await user.click(screen.getByRole("button", { name: "Save changes" }));
  await waitFor(() => expect(api.updateSession).toHaveBeenCalledWith("session-1", expect.objectContaining({ name: "Updated session", bookingOpenTime: null })));
  await waitFor(() => expect(onSelect).toHaveBeenCalledWith("session-1"));

  await user.click(screen.getByRole("button", { name: "Delete" }));
  await user.click(screen.getByRole("button", { name: "Delete session" }));
  await waitFor(() => expect(api.deleteSession).toHaveBeenCalledWith("session-1"));
  await waitFor(() => expect(onDeleted).toHaveBeenCalled());
});
