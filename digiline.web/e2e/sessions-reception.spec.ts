import { test, expect, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const currentToken = {
  id: "token-21", tokenNumber: 21, patientProfileId: null, walkInName: "Anita Desai",
  phoneNumber: "9999990021", patientAge: 42, status: "Called",
  createdAt: "2030-09-16T03:40:00Z", calledAt: "2030-09-16T04:10:00Z",
  lateArrivedAt: null, completedAt: null,
};
const waitingToken = {
  ...currentToken, id: "token-22", tokenNumber: 22, walkInName: "Vikram Rao",
  phoneNumber: "9999990022", patientAge: 35, status: "Waiting", calledAt: null,
};
const skippedToken = {
  ...waitingToken, id: "token-23", tokenNumber: 23, walkInName: "Meera Shah",
  phoneNumber: "9999990023", patientAge: 29, status: "Skipped",
};

const activeSession = {
  id: "session-1", clinicId: "clinic-1", clinicName: "DigiLine Clinic", doctorId: "doctor-1",
  doctorName: "Dr Rao", name: "Morning clinic", bookingOpenTime: "2030-09-16T03:00:00Z",
  startTime: "2030-09-16T03:30:00Z", endTime: "2030-09-16T08:30:00Z",
  lastBookingTime: "2030-09-16T07:30:00Z", averageConsultationMinutes: 10,
  status: "Running", isBookingOpen: true, bookingHasStopped: false, timeZoneId: "Asia/Kolkata",
  queueSummary: { currentToken, waitingCount: 1, lateCount: 0, totalCount: 3, completedCount: 0, notAttendedCount: 1, skippedCount: 1, unfinishedCount: 3 },
};
const upcomingSession = {
  ...activeSession, id: "session-2", name: "Evening clinic", status: "BookingOpen",
  startTime: "2030-09-16T11:30:00Z", endTime: "2030-09-16T14:30:00Z",
  bookingOpenTime: "2030-09-16T09:30:00Z", lastBookingTime: "2030-09-16T13:30:00Z",
  queueSummary: { currentToken: null, waitingCount: 0, lateCount: 0, totalCount: 0, completedCount: 0, notAttendedCount: 0, skippedCount: 0, unfinishedCount: 0 },
};

async function prepare(page: Page) {
  const payload = Buffer.from(JSON.stringify({
    exp: Math.floor(Date.now() / 1000) + 3600,
    sub: "staff-1", email: "staff@example.test", name: "Dr Rao",
    role: ["Doctor", "Receptionist"], clinic_id: "clinic-1",
  })).toString("base64url");
  await page.addInitScript((token) => sessionStorage.setItem("digiline.staff-session", JSON.stringify({ token })), `header.${payload}.signature`);

  await page.route("**/api/doctor/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/clinic-time-zone")) return route.fulfill({ json: { timeZoneId: "Asia/Kolkata" } });
    if (url.pathname.endsWith("/sessions")) return route.fulfill({ json: { items: [activeSession, upcomingSession], page: 1, pageSize: 10, totalCount: 2 } });
    return route.fulfill({ status: 404, json: { title: "Not found" } });
  });
  await page.route("**/api/reception/**", async (route) => {
    const url = new URL(route.request().url());
    const details = { session: activeSession, queue: [currentToken, waitingToken, skippedToken], page: 1, pageSize: 10, totalCount: 3 };
    if (url.pathname.endsWith("/sessions/today")) return route.fulfill({ json: { clinicName: "DigiLine Clinic", timeZoneId: "Asia/Kolkata", date: "2030-09-16", sessions: [details], page: 1, pageSize: 10, totalCount: 1 } });
    if (url.pathname.endsWith("/sessions/session-1")) return route.fulfill({ json: details });
    return route.fulfill({ status: 404, json: { title: "Not found" } });
  });
}

async function audit(page: Page) {
  const result = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();
  expect(result.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
}

async function expectCurrentNavigation(page: Page, width: number, current: "Sessions" | "Reception", other: "Sessions" | "Reception") {
  if (width < 768) await page.getByRole("button", { name: "Menu" }).click();
  await expect(page.getByRole("link", { name: current, exact: true })).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("link", { name: other, exact: true })).not.toHaveAttribute("aria-current");
  if (width < 768) await page.getByRole("button", { name: "Close menu" }).click();
}

test("Sessions stays compact and responsive", async ({ page }) => {
  await prepare(page);
  for (const width of [320, 375, 390, 430, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/doctor/sessions");
    await expect(page.getByRole("heading", { name: "Sessions", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Add session" })).toBeVisible();
    await expect(page.getByRole("table", { name: "Your sessions" })).toBeAttached();
    await expect(page.getByText("Morning clinic")).toBeVisible();
    await expect(page.getByText("Evening clinic")).toBeVisible();
    await expectCurrentNavigation(page, width, "Sessions", "Reception");
    await audit(page);
    if (width <= 640) {
      await expect(page.locator('td[data-column-key="attendance"]').first()).toBeHidden();
      await page.getByRole("button", { name: "Add session" }).click();
      await expect(page.getByRole("heading", { name: "Create session" })).toBeVisible();
      await expect(page.getByLabel("Booking opens (optional)")).toBeVisible();
      await expect(page.getByLabel("Average visit (minutes)")).toBeVisible();
      await audit(page);
      await page.getByRole("button", { name: "Cancel" }).click();
    }
    if (width === 390) await page.screenshot({ path: "test-results/sessions-mobile.png", fullPage: true });
    if (width === 1440) {
      const table = await page.getByRole("table", { name: "Your sessions" }).boundingBox();
      expect(table?.y).toBeLessThan(500);
      await page.screenshot({ path: "test-results/sessions-desktop.png", fullPage: true });
    }
  }
});

test("Reception keeps the active session, current token, and queue responsive", async ({ page }) => {
  await prepare(page);
  for (const width of [320, 375, 390, 430, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/staff");
    await expect(page.getByRole("heading", { name: "Reception desk" })).toBeVisible();
    const overview = page.getByRole("region", { name: "Active session and queue summary" });
    await expect(overview.getByText("Dr Rao")).toBeVisible();
    await expect(overview.getByText("#21")).toBeVisible();
    await expect(overview.getByText("Anita Desai")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Queue details" })).toBeVisible();
    await expect(page.getByRole("table", { name: "Reception queue for Morning clinic" })).toBeAttached();
    await expect(page.getByRole("button", { name: "Issue token" })).toBeVisible();
    await expectCurrentNavigation(page, width, "Reception", "Sessions");
    await audit(page);
    if (width <= 640) await expect(page.locator('td[data-column-key="phone"]').first()).toBeHidden();
    if (width === 390) await page.screenshot({ path: "test-results/reception-mobile.png", fullPage: true });
    if (width === 1440) {
      const queue = await page.getByRole("heading", { name: "Queue details" }).boundingBox();
      expect(queue?.y).toBeLessThan(650);
      await page.screenshot({ path: "test-results/reception-desktop.png", fullPage: true });
    }
  }
});
