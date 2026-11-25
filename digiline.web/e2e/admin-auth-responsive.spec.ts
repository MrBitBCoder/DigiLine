import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const staff = {
  id: "staff-1",
  name: "Dr Anika Rao",
  email: "anika@example.test",
  roles: [3, 1],
  isActive: true,
  invitationPending: false,
  deliveryFailed: false,
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
  const overflow = await page.evaluate(() =>
    Array.from(document.querySelectorAll("body *"))
      .filter(
        (element) => element.getBoundingClientRect().right > innerWidth + 1,
      )
      .slice(0, 10)
      .map((element) => `${element.tagName}.${element.className}`),
  );
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
    overflow.join(", "),
  ).toBeTruthy();
}

async function prepareAdmin(page: Page) {
  const payload = Buffer.from(
    JSON.stringify({
      exp: Math.floor(Date.now() / 1000) + 3600,
      sub: "staff-1",
      email: staff.email,
      name: staff.name,
      role: ["Admin", "Doctor"],
      clinic_id: "clinic-1",
    }),
  ).toString("base64url");
  await page.addInitScript(
    (token) =>
      sessionStorage.setItem(
        "digiline.staff-session",
        JSON.stringify({ token }),
      ),
    `header.${payload}.signature`,
  );
  await page.route("**/api/admin/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/clinic"))
      return route.fulfill({
        json: {
          name: "DigiLine Clinic",
          slug: "digiline",
          timeZoneId: "Asia/Kolkata",
        },
      });
    if (url.pathname.endsWith("/staff"))
      return route.fulfill({
        json: { items: [staff], page: 1, pageSize: 10, totalCount: 1 },
      });
    return route.fulfill({ status: 404, json: { title: "Not found" } });
  });
}

test("authentication screens stay focused and responsive", async ({ page }) => {
  for (const width of [320, 375, 390, 430, 768, 1366]) {
    await page.setViewportSize({ width, height: 900 });
    for (const route of [
      "/login",
      "/register",
      "/forgot-password",
      "/reset-password",
      "/accept-invitation",
    ]) {
      await page.goto(route);
      await expect(page.locator("h1")).toHaveCount(1);
      await expect(page.locator("h1")).toBeVisible();
      await audit(page);
    }
    if (width === 390)
      await page.screenshot({
        path: "test-results/auth-mobile.png",
        fullPage: true,
      });
    if (width === 1366)
      await page.screenshot({
        path: "test-results/auth-desktop.png",
        fullPage: true,
      });
  }
});

test("administration keeps staff and actions visible across target widths", async ({
  page,
}) => {
  await prepareAdmin(page);
  for (const width of [320, 375, 390, 430, 768, 1366]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/admin");
    await expect(
      page.getByRole("heading", { name: "Clinic administration" }),
    ).toBeVisible();
    await expect(page.getByRole("button", { name: "Add staff" })).toBeVisible();
    const table = page.getByRole("table", { name: "Clinic staff" });
    await expect(table).toBeAttached();
    await expect(table.getByText("Dr Anika Rao")).toBeVisible();
    await expect(table.getByText("Active", { exact: true })).toBeVisible();
    if (width < 768) await page.getByRole("button", { name: "Menu" }).click();
    await expect(
      page.getByRole("link", { name: "Clinic administration" }),
    ).toHaveAttribute("aria-current", "page");
    if (width < 768)
      await page.getByRole("button", { name: "Close menu" }).click();
    await audit(page);
    await page.getByRole("button", { name: "Add staff" }).click();
    await expect(
      page.getByRole("heading", { name: "Add staff" }),
    ).toBeFocused();
    await expect(page.getByLabel("Staff name")).toBeVisible();
    await expect(page.getByLabel("Confirm password")).toBeVisible();
    await audit(page);
    await page.getByRole("button", { name: "Cancel" }).click();
    if (width === 390)
      await page.screenshot({
        path: "test-results/admin-responsive-mobile.png",
        fullPage: true,
      });
    if (width === 1366)
      await page.screenshot({
        path: "test-results/admin-responsive-desktop.png",
        fullPage: true,
      });
  }
});
