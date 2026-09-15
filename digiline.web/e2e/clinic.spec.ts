import { test, expect, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
async function audit(page: Page) {
  const result = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();
  expect(result.violations.filter(v => v.impact === "critical" || v.impact === "serious")).toEqual([]);
  const overflow = await page.evaluate(() => Array.from(document.querySelectorAll("body *")).filter(el => el.getBoundingClientRect().right > innerWidth + 1).slice(0, 12).map(el => `${el.tagName}.${el.className}`));
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), overflow.join(", ")).toBeTruthy();
}
async function signIn(page: Page) {
  await page.goto("/login"); await page.getByLabel("Email address").fill(process.env.DIGILINE_E2E_EMAIL ?? "load-0@verification.local");
  await page.getByLabel(/^Password/).fill(process.env.DIGILINE_E2E_PASSWORD ?? "VerificationOnly123!"); await page.getByRole("button", { name:"Sign in", exact:true }).click();
  await expect(page.getByRole("heading", { name:"Clinic administration" })).toBeVisible();
}
for (const width of [360,390,768]) {
  test(`accessible principal screens at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height:900 }); await page.goto("/register"); await audit(page);
    await page.screenshot({ path:`test-results/register-${width}.png`, fullPage:true });
    await signIn(page); await audit(page); await page.screenshot({ path:`test-results/admin-${width}.png`, fullPage:true });
    await page.getByRole("link", { name:"Doctor dashboard", exact:true }).click();
    await expect(page.getByRole("heading", { name:"Patient queue" })).toBeVisible(); await audit(page);
    await expect(page.getByRole("columnheader").first()).toBeAttached();
    await page.screenshot({ path:`test-results/doctor-${width}.png`, fullPage:true });
    await page.getByRole("link", { name:"Reception", exact:true }).click();
    await expect(page.getByRole("heading", { name:"Reception desk" })).toBeVisible(); await audit(page);
  });
}
test("keyboard registration, role navigation, and explicit completion", async ({ page }) => {
  const suffix = Date.now().toString(); await page.goto("/register");
  await page.getByLabel(/^Clinic name/).fill("Journey Clinic"); await page.getByLabel(/^Clinic address/).fill(`journey-${suffix}`);
  await page.getByLabel(/^Your name/).fill("Journey Owner"); await page.getByLabel(/^Email/).fill(`journey-${suffix}@verification.local`);
  await page.getByLabel(/^Password/).fill("VerificationOnly123!"); await page.getByLabel("I also practice here").check();
  await page.getByRole("button", { name:"Create clinic", exact:true }).focus(); await page.keyboard.press("Enter");
  await expect(page.getByRole("heading", { name:"Clinic administration" })).toBeVisible();
  await expect(page.getByRole("link", { name:"Doctor dashboard" })).toBeVisible();
  const token = await page.evaluate(() => JSON.parse(sessionStorage.getItem("digiline.staff-session")!).token as string);
  const auth = { Authorization:`Bearer ${token}` };
  const start = new Date(Date.now()+60000).toISOString(), end = new Date(Date.now()+3600000).toISOString();
  const created = await page.request.post("http://localhost:5017/api/doctor/sessions", { headers:auth, data:{ name:"Completion journey", startTime:start,endTime:end,averageConsultationMinutes:5 } });
  expect(created.ok()).toBeTruthy(); const { id } = await created.json();
  await page.goto(`/doctor?session=${id}`); await page.getByRole("button", { name:"Start session", exact:true }).click();
  await page.getByRole("button", { name:"Stop booking", exact:true }).click();
  await expect(page.getByRole("dialog")).toBeVisible(); await expect(page.getByRole("button", { name:"Go back" })).toBeFocused();
  await page.keyboard.press("Escape"); await expect(page.getByRole("dialog")).not.toBeVisible();
  await expect(page.getByRole("button", { name:"Stop booking", exact:true })).toBeFocused();
  await page.getByRole("button", { name:"Stop booking", exact:true }).click(); await page.getByRole("dialog").getByRole("button", { name:"Stop booking", exact:true }).click();
  await page.getByRole("button", { name:"Complete session", exact:true }).click();
  await expect(page.getByRole("dialog")).toContainText("0 remaining skipped");
  await page.getByRole("dialog").getByRole("button", { name:"Complete session", exact:true }).click();
  await expect(page.locator(".session-state .status-badge").filter({ hasText: "Completed" })).toBeVisible();
  await expect(page.getByRole("button", { name:"Call next" })).toBeDisabled(); await audit(page);
});
test("reconnects after an extended offline period", async ({ page, context }) => {
  await signIn(page); await page.getByRole("link", { name:"Doctor dashboard", exact:true }).click();
  await expect(page.getByText("Live", { exact:true })).toBeVisible();
  await context.setOffline(true); await page.waitForTimeout(22000); await context.setOffline(false);
  await expect(page.getByText("Live", { exact:true })).toBeVisible({ timeout:35000 });
});
test("doctor navigation exposes one current page and moves focus into routed content", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 900 });
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
  const summary = {
    id: "session-7", clinicId: "clinic-1", clinicName: "Clinic", doctorId: "doctor-1", doctorName: "Dr Rao",
    name: "Morning session", bookingOpenTime: "2026-09-15T05:00:00Z", startTime: "2026-09-15T06:00:00Z",
    endTime: "2026-09-15T10:00:00Z", lastBookingTime: "2026-09-15T09:00:00Z", averageConsultationMinutes: 10,
    status: "Running", isBookingOpen: true, bookingHasStopped: false,
    queueSummary: { currentToken: null, waitingCount: 0, lateCount: 0, totalCount: 0, completedCount: 0, notAttendedCount: 0, skippedCount: 0, unfinishedCount: 0 },
    timeZoneId: "Asia/Kolkata",
  };
  await page.route("**/api/doctor/**", async route => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/clinic-time-zone")) return route.fulfill({ json: { timeZoneId: "Asia/Kolkata" } });
    if (url.pathname.endsWith("/active-upcoming") || url.pathname.endsWith("/sessions/session-7"))
      return route.fulfill({ json: { session: summary, queue: [], page: 1, pageSize: 10, totalCount: 0 } });
    return route.fulfill({ json: { items: [summary], page: 1, pageSize: 10, totalCount: 1 } });
  });
  await page.goto("/doctor/sessions");
  const dashboardLink = page.getByRole("link", { name: "Doctor dashboard", exact: true });
  const sessionsLink = page.getByRole("link", { name: "Sessions", exact: true });
  await expect(sessionsLink).toHaveAttribute("aria-current", "page");
  await expect(dashboardLink).not.toHaveAttribute("aria-current");
  await expect(page.getByRole("heading", { name: "Sessions", exact: true })).toBeFocused();
  await audit(page);

  await page.getByRole("button", { name: "View queue" }).first().click();
  await expect(page).toHaveURL(/\/doctor\?session=/);
  await expect(dashboardLink).toHaveAttribute("aria-current", "page");
  await expect(sessionsLink).not.toHaveAttribute("aria-current");
  await expect(page.locator("main h1")).toBeFocused();
  await audit(page);

  const stopBooking = page.getByRole("button", { name: "Stop booking", exact: true });
  await stopBooking.click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await expect(page.getByRole("button", { name: "Go back" })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).not.toBeVisible();
  await expect(stopBooking).toBeFocused();

  await sessionsLink.focus();
  await page.keyboard.press("Enter");
  await expect(sessionsLink).toHaveAttribute("aria-current", "page");
  await expect(dashboardLink).not.toHaveAttribute("aria-current");
  await expect(page.getByRole("heading", { name: "Sessions", exact: true })).toBeFocused();
});
test("layout remains usable at 200 percent zoom", async ({ page }) => {
  await page.setViewportSize({ width:768,height:900 }); await signIn(page);
  await page.evaluate(() => { document.documentElement.style.zoom = "2"; }); await audit(page);
  await page.screenshot({ path:"test-results/admin-zoom-200.png",fullPage:true });
  await page.getByRole("link", { name:"Doctor dashboard", exact:true }).click();
  await expect(page.getByRole("heading", { name:"Patient queue" })).toBeVisible(); await audit(page);
  await page.getByRole("link", { name:"Reception", exact:true }).click();
  await expect(page.getByRole("heading", { name:"Reception desk" })).toBeVisible(); await audit(page);
  await page.goto("/register");
  await page.evaluate(() => { document.documentElement.style.zoom = "2"; }); await audit(page);
});
