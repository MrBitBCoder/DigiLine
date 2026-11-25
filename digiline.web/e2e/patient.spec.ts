import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const session = {
  id: "session-1",
  name: "Morning session",
  bookingOpenTime: "2026-09-16T03:30:00Z",
  startTime: "2026-09-16T04:30:00Z",
  endTime: "2026-09-16T07:30:00Z",
  lastBookingTime: "2026-09-16T07:00:00Z",
  status: "Running",
  isBookingOpen: true,
  currentTokenNumber: 4,
  approximateQueueSize: 3,
};

async function audit(page: Page) {
  const result = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
    .analyze();
  expect(
    result.violations.filter(
      (violation) =>
        violation.impact === "critical" || violation.impact === "serious",
    ),
  ).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  ).toBeTruthy();
}

async function mockPatientApi(page: Page, bookingOpen = true) {
  await page.route("**/api/public/clinics/hope", (route) =>
    route.fulfill({
      json: {
        name: "Hope Clinic",
        slug: "hope",
        timeZoneId: "Asia/Kolkata",
        sessions: [{ ...session, isBookingOpen: bookingOpen }],
      },
    }),
  );
  await page.route(
    "**/api/public/queue/sessions/session-1/tokens/token-1",
    (route) =>
      route.fulfill({
        json: {
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
          updatedAt: "2026-09-16T05:00:00Z",
        },
      }),
  );
}

test("patient landing and join dialog remain focused and overflow-free", async ({
  page,
}) => {
  await mockPatientApi(page);
  for (const width of [320, 375, 390, 430, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/c/hope");
    await expect(
      page.getByRole("heading", { name: "Hope Clinic" }),
    ).toBeVisible();
    await expect(page.getByText("Clinic time")).toBeVisible();
    const join = page.getByRole("button", { name: "Join queue" });
    await expect(join).toBeVisible();
    expect((await join.boundingBox())?.y).toBeLessThan(900);
    await audit(page);

    await join.click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Close join queue" }),
    ).toBeFocused();
    await audit(page);
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).not.toBeVisible();
    await expect(join).toBeFocused();
  }
  await page.setViewportSize({ width: 390, height: 900 });
  await page.screenshot({
    path: "test-results/patient-landing-mobile.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.screenshot({
    path: "test-results/patient-landing-desktop.png",
    fullPage: true,
  });
});

test("booking closed prioritizes secure token recovery", async ({ page }) => {
  await mockPatientApi(page, false);
  await page.setViewportSize({ width: 390, height: 900 });
  await page.goto("/c/hope");
  await expect(
    page.getByText("Booking is closed", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Find my token" }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Join queue" })).toHaveCount(0);
  await audit(page);
  await page.screenshot({
    path: "test-results/patient-booking-closed-mobile.png",
    fullPage: true,
  });
});

test("active token keeps the patient token above the fold", async ({
  page,
}) => {
  await mockPatientApi(page);
  await page.addInitScript(() =>
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
    ),
  );

  for (const width of [320, 375, 390, 430, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/c/hope");
    await expect(
      page.getByRole("heading", { name: "You are in the queue" }),
    ).toBeVisible();
    const token = page.getByLabel("Your token #8");
    await expect(token).toBeVisible();
    expect((await token.boundingBox())?.y).toBeLessThan(500);
    await expect(
      page.getByRole("navigation", { name: "Token progress" }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Cancel token" }),
    ).toBeVisible();
    await audit(page);
  }
  await page.setViewportSize({ width: 390, height: 900 });
  await page.screenshot({
    path: "test-results/patient-token-mobile.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.screenshot({
    path: "test-results/patient-token-desktop.png",
    fullPage: true,
  });
});
