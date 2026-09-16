import { test, expect, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const currentToken = {
  id: "token-15",
  tokenNumber: 15,
  patientProfileId: null,
  patientName: null,
  patientAge: 42,
  walkInName: "Anita Desai",
  phoneNumber: "9999990015",
  status: "Called",
  createdAt: "2030-09-16T03:35:00Z",
  calledAt: "2030-09-16T04:20:00Z",
  lateArrivedAt: null,
  completedAt: null,
};

const waitingToken = {
  ...currentToken,
  id: "token-16",
  tokenNumber: 16,
  walkInName: "Vikram Rao",
  phoneNumber: "9999990016",
  patientAge: 35,
  status: "Waiting",
  createdAt: "2030-09-16T03:42:00Z",
  calledAt: null,
};

const lateToken = {
  ...waitingToken,
  id: "token-17",
  tokenNumber: 17,
  walkInName: "Meera Shah",
  phoneNumber: "9999990017",
  patientAge: 29,
  status: "Late",
  createdAt: "2030-09-16T03:50:00Z",
};

const session = {
  id: "session-1",
  clinicId: "clinic-1",
  clinicName: "DigiLine Clinic",
  doctorId: "doctor-1",
  doctorName: "Dr Rao",
  name: "Morning clinic",
  bookingOpenTime: "2030-09-16T03:00:00Z",
  startTime: "2030-09-16T03:30:00Z",
  endTime: "2030-09-16T08:30:00Z",
  lastBookingTime: "2030-09-16T07:30:00Z",
  averageConsultationMinutes: 10,
  status: "Running",
  isBookingOpen: true,
  bookingHasStopped: false,
  timeZoneId: "Asia/Kolkata",
  queueSummary: {
    currentToken,
    waitingCount: 1,
    lateCount: 1,
    totalCount: 3,
    completedCount: 0,
    notAttendedCount: 0,
    skippedCount: 0,
    unfinishedCount: 3,
  },
};

async function openMockedDashboard(page: Page) {
  const payload = Buffer.from(JSON.stringify({
    exp: Math.floor(Date.now() / 1000) + 3600,
    sub: "doctor-1",
    email: "doctor@example.test",
    name: "Dr Rao",
    role: "Doctor",
    clinic_id: "clinic-1",
  })).toString("base64url");

  await page.addInitScript((token) => {
    sessionStorage.setItem("digiline.staff-session", JSON.stringify({ token }));
  }, `header.${payload}.signature`);

  await page.route("**/api/doctor/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/active-upcoming") || url.pathname.endsWith("/sessions/session-1")) {
      await route.fulfill({ json: { session, queue: [currentToken, waitingToken, lateToken], page: 1, pageSize: 10, totalCount: 3 } });
      return;
    }
    await route.fulfill({ status: 404, json: { title: "Not found" } });
  });

  await page.goto("/doctor");
  await expect(page.getByRole("heading", { name: "Morning clinic", exact: true })).toBeVisible();
}

async function audit(page: Page) {
  const result = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();
  expect(result.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  const overflow = await page.evaluate(() => Array.from(document.querySelectorAll("body *"))
    .filter((element) => element.getBoundingClientRect().right > innerWidth + 1)
    .slice(0, 10)
    .map((element) => `${element.tagName}.${element.className}`));
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), overflow.join(", ")).toBeTruthy();
}

test("doctor dashboard keeps the current patient and next action prominent", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await openMockedDashboard(page);

  const currentPanel = page.locator(".current-patient-panel");
  await expect(currentPanel.getByRole("heading", { name: "Token #15" })).toBeVisible();
  await expect(currentPanel.getByText("Anita Desai")).toBeVisible();
  await expect(currentPanel.getByText("Called", { exact: true })).toBeVisible();
  await expect(currentPanel.getByRole("button", { name: "Complete & call next" })).toBeVisible();
  await expect(currentPanel.getByRole("button", { name: "Not here—skip" })).toBeVisible();
  await expect(page.getByText("Waiting").first()).toBeVisible();
  await expect(page.getByRole("heading", { name: "Patient queue" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Call Again/i })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Doctor dashboard", exact: true })).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("link", { name: "Sessions", exact: true })).not.toHaveAttribute("aria-current");

  const queueHeading = await page.getByRole("heading", { name: "Patient queue" }).boundingBox();
  expect(queueHeading?.y).toBeLessThan(900);
  await audit(page);
  await page.screenshot({ path: "test-results/doctor-dashboard-desktop.png", fullPage: true });
});

test("doctor dashboard remains operational at responsive widths", async ({ page }) => {
  for (const width of [320, 375, 390, 430, 768]) {
    await page.setViewportSize({ width, height: 900 });
    await openMockedDashboard(page);
    await expect(page.locator(".current-patient-panel").getByRole("heading", { name: "Token #15" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Complete & call next" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Patient queue" })).toBeVisible();
    await expect(page.getByRole("table", { name: "Ordered queue for Morning clinic" })).toBeAttached();
    await audit(page);
    if (width === 390) await page.screenshot({ path: "test-results/doctor-dashboard-mobile.png", fullPage: true });
  }
});
