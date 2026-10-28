import { test, expect, type Page } from "@playwright/test";
async function login(page: Page) {
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
}
test.beforeEach(() => {
  test.skip(
    !process.env.UI_V2_PASSWORD,
    "Requires a real synthetic authenticated full-API fixture.",
  );
});
test("Product Master required validation, create and server-persisted search", async ({
  page,
}) => {
  await login(page);
  await page
    .getByRole("navigation", { name: "Điều hướng chính", exact: true })
    .getByRole("button", { name: "Product Master", exact: true })
    .click();
  await page.getByRole("button", { name: /Thêm mã hàng/ }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: "Tạo mới", exact: true }).click();
  await expect(
    dialog.locator(".ant-form-item-explain-error").first(),
  ).toBeVisible();
  await page.getByPlaceholder("42072-030").fill("SYNTH-UI-V2-001");
  await page
    .getByPlaceholder(/Giày thể thao nữ/)
    .fill("Synthetic UI product — no company data");
  const response = page.waitForResponse(
    (r) =>
      r.url().includes("/api/product-masters") &&
      r.request().method() === "POST",
  );
  await dialog.getByRole("button", { name: "Tạo mới", exact: true }).click();
  expect((await response).ok()).toBe(true);
  await expect(dialog).toBeHidden();
  await expect(
    page.getByText("SYNTH-UI-V2-001", { exact: true }).first(),
  ).toBeVisible();
  await page.screenshot({
    path: "../.audit-run/ui-v2/screenshots/product-created.png",
    fullPage: true,
  });
});
test("Materials create, search and edit preserve real backend persistence", async ({
  page,
}) => {
  await login(page);
  await page
    .getByRole("navigation", { name: "Điều hướng chính", exact: true })
    .getByRole("button", { name: "Danh mục vật tư", exact: true })
    .click();
  await page.getByRole("button", { name: /Thêm Vật tư/ }).click();
  await page.getByLabel("Mã Vật tư", { exact: true }).fill("SYNTH-MAT-001");
  await page.getByLabel("Tên NPL", { exact: true }).fill("Synthetic material");
  await page.getByLabel("Đơn vị tính", { exact: true }).fill("đôi");
  await page.getByLabel("Tồn kho", { exact: true }).fill("100");
  let response = page.waitForResponse(
    (r) =>
      r.url().includes("/api/materials") && r.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Lưu vật tư", exact: true }).click();
  expect((await response).ok()).toBe(true);
  await expect(page.getByRole("dialog")).toBeHidden();
  await page
    .getByPlaceholder("Tìm theo mã hoặc tên vật tư...")
    .fill("SYNTH-MAT-001");
  await expect(
    page.getByText("Synthetic material", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Sửa SYNTH-MAT-001", exact: true })
    .click();
  await page
    .getByLabel("Tên NPL", { exact: true })
    .fill("Synthetic material updated");
  response = page.waitForResponse(
    (r) =>
      r.url().includes("/api/materials/") && r.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "Lưu vật tư", exact: true }).click();
  expect((await response).ok()).toBe(true);
  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(
    page.getByText("Synthetic material updated", { exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: "../.audit-run/ui-v2/screenshots/material-updated.png",
    fullPage: true,
  });
});
