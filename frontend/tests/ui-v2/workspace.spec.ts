import { test, expect } from "@playwright/test";
import { mkdir } from "node:fs/promises";
const evidence = "../.audit-run/ui-v2/screenshots";
test.beforeAll(async () => {
  await mkdir(evidence, { recursive: true });
});
for (const [width, height] of [
  [1440, 900],
  [1366, 768],
  [1024, 768],
  [390, 844],
  [360, 800],
]) {
  test(`login responsive ${width}x${height}`, async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.name));
    await page.setViewportSize({ width, height });
    await page.goto("/");
    await expect(
      page.getByRole("heading", { name: "Đăng nhập", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByLabel("Tên đăng nhập", { exact: true }),
    ).toBeVisible();
    await expect(page.getByLabel("Mật khẩu", { exact: true })).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Đăng nhập", exact: true }),
    ).toBeInViewport();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `${evidence}/login-${width}.png`,
      fullPage: true,
    });
    expect(errors).toEqual([]);
  });
}
test("login native required fields, reveal control and server rejection", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  expect(
    await page
      .getByLabel("Tên đăng nhập", { exact: true })
      .evaluate((el: HTMLInputElement) => el.validity.valueMissing),
  ).toBe(true);
  await page
    .getByLabel("Tên đăng nhập", { exact: true })
    .fill("ui-v2-nonexistent-synthetic-user");
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill("Synthetic_Invalid_Only!");
  await page
    .getByRole("button", { name: "Hiện mật khẩu", exact: true })
    .click();
  await expect(page.getByLabel("Mật khẩu", { exact: true })).toHaveAttribute(
    "type",
    "text",
  );
  await page.getByRole("button", { name: "Ẩn mật khẩu", exact: true }).click();
  await expect(page.getByLabel("Mật khẩu", { exact: true })).toHaveAttribute(
    "type",
    "password",
  );
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.locator(".signin-error")).toBeVisible();
});
test("keyboard focus, reduced motion and 200 percent equivalent viewport", async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 720, height: 450 });
  await page.goto("/");
  await expect(page.getByLabel("Tên đăng nhập", { exact: true })).toBeVisible();
  await page.keyboard.press("Tab");
  await expect(page.getByLabel("Tên đăng nhập", { exact: true })).toBeFocused();
  expect(
    await page
      .getByLabel("Tên đăng nhập", { exact: true })
      .evaluate((el) => parseFloat(getComputedStyle(el).outlineWidth)),
  ).toBeGreaterThanOrEqual(2);
  await page
    .getByRole("button", { name: "Đăng nhập", exact: true })
    .scrollIntoViewIfNeeded();
  await expect(
    page.getByRole("button", { name: "Đăng nhập", exact: true }),
  ).toBeInViewport();
});
test("authenticated workspace, all authorized destinations, mobile menu and wizard", async ({
  page,
}) => {
  test.setTimeout(120000);
  const serverFailures: string[] = [];
  const runtimeErrors: string[] = [];
  page.on('pageerror', error => runtimeErrors.push(error.name));
  page.on("response", (response) => {
    if (response.url().includes("/api/") && response.status() >= 500)
      serverFailures.push(new URL(response.url()).pathname);
  });
  test.skip(
    !process.env.UI_V2_PASSWORD,
    "Requires an explicitly supplied synthetic local credential; no mock authentication.",
  );
  await page.goto("/");
  await page
    .getByLabel("Tên đăng nhập", { exact: true })
    .fill(process.env.UI_V2_USERNAME || "localadmin");
  await page
    .getByLabel("Mật khẩu", { exact: true })
    .fill(process.env.UI_V2_PASSWORD!);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(
    page.getByRole("navigation", { name: "Điều hướng chính", exact: true }),
  ).toBeVisible();
  const navigation = page.getByRole("navigation", {
    name: "Điều hướng chính",
    exact: true,
  });
  const labels = await navigation.getByRole("button").allTextContents();
  for (const label of labels.filter(
    (label) =>
      !label.includes("Trợ lý Excel") && !label.includes("Quét phiếu kho"),
  )) {
    await navigation
      .getByRole("button", { name: label.trim(), exact: true })
      .click();
    await expect(page.locator("#workspace-main")).toBeVisible();
    await expect(
      page.getByText("Không thể hiển thị nội dung", { exact: true }),
    ).toHaveCount(0);
    await expect(page.locator(".ant-spin-spinning")).toHaveCount(0);
    if (label.trim() === "Invoice & Packing List") {
      await expect(page.locator(".driver-overlay")).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(page.locator(".driver-overlay")).toHaveCount(0);
    }
    for (const [width, height] of [
      [1440, 900],
      [1366, 768],
      [1024, 768],
      [390, 844],
      [360, 800],
    ]) {
      await page.setViewportSize({ width, height });
      await page.screenshot({
        path: `${evidence}/page-${labels.indexOf(label)}-${width}.png`,
        fullPage: true,
      });
      const overflow = await page.locator("#workspace-main").evaluate((el) => ({
        fits: el.scrollWidth <= el.clientWidth + 1,
        offenders: Array.from(el.querySelectorAll("*"))
          .filter(
            (child) => child.getBoundingClientRect().width > el.clientWidth,
          )
          .slice(0, 12)
          .map((child) => ({
            tag: child.tagName,
            className: child.getAttribute("class"),
            width: Math.round(child.getBoundingClientRect().width),
          })),
      }));
      expect(
        overflow.fits,
        `page ${label} at ${width}: ${JSON.stringify(overflow.offenders)}`,
      ).toBe(true);
    }
    await page.setViewportSize({ width: 1440, height: 900 });
  }
  await navigation
    .getByRole("button", { name: "Product Master", exact: true })
    .click();
  await page.getByRole("button", { name: /Thêm mã hàng/ }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  for (const [width, height] of [
    [1440, 900],
    [390, 844],
    [360, 800],
  ]) {
    await page.setViewportSize({ width, height });
    await page.getByPlaceholder("42072-030").fill("SYNTH-UI-V2");
    await expect(page.getByPlaceholder("42072-030")).toHaveValue("SYNTH-UI-V2");
    await expect(
      page
        .getByRole("dialog")
        .getByRole("button", { name: "Tạo mới", exact: true }),
    ).toBeInViewport();
    await page.screenshot({
      path: `${evidence}/product-modal-${width}.png`,
      fullPage: true,
    });
  }
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Hủy", exact: true })
    .click();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole("button", { name: "Mở menu điều hướng" }).click();
  await expect(
    page.getByRole("dialog", { name: "Điều hướng ShoeDocX" }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await page.goto("/universal-doc-studio");
  await expect(
    page.getByRole("heading", { name: "Trợ lý Excel văn phòng", exact: true }),
  ).toBeVisible();
  const steps = page
    .getByRole("navigation", { name: "Các bước xử lý Excel" })
    .getByRole("button");
  await expect(steps).toHaveCount(6);
  await page.getByLabel("Mẫu Excel đầu ra", { exact: true }).setInputFiles({
    name: "invalid.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("synthetic"),
  });
  await expect(page.getByRole("alert")).toContainText(".xlsx");
  for (let index = 0; index < 6; index++) {
    await steps.nth(index).click();
    await page.screenshot({
      path: `${evidence}/office-empty-step-${index + 1}.png`,
      fullPage: true,
    });
  }
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect(serverFailures).toEqual([]);
  expect(runtimeErrors).toEqual([]);
  await page
    .getByRole("button", { name: "Synthetic Demo / kỹ thuật", exact: true })
    .click();
  await expect(
    page.getByRole("heading", {
      name: "Universal Document Studio",
      exact: true,
    }),
  ).toBeVisible();
  for (const [width, height] of [
    [1440, 900],
    [1366, 768],
    [1024, 768],
    [390, 844],
    [360, 800],
  ]) {
    await page.setViewportSize({ width, height });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.screenshot({
      path: `${evidence}/studio-technical-${width}.png`,
      fullPage: true,
    });
  }
});
