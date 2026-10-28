import {
  test,
  expect,
  type Page,
  type APIRequestContext,
} from "@playwright/test";
import { mkdir, readFile } from "node:fs/promises";
const evidence = "../.audit-run/ui-v2/office-pending";
let authenticatedState: Awaited<ReturnType<APIRequestContext["storageState"]>>;
test.beforeAll(async ({ request }) => {
  if (!process.env.UI_V2_PASSWORD || !process.env.UI_V2_OFFICE_FIXTURE) return;
  // One genuine Identity login; keep cookies in memory, never in artifacts.
  // Each test still gets an isolated browser/sessionStorage and real CSRF bootstrap.
  const response = await request.post("/api/auth/login", {
    data: {
      username: process.env.UI_V2_USERNAME,
      password: process.env.UI_V2_PASSWORD,
    },
  });
  expect(response.status()).toBe(200);
  authenticatedState = await request.storageState();
});
test.beforeEach(async ({ page }) => {
  test.skip(
    !process.env.UI_V2_PASSWORD || !process.env.UI_V2_OFFICE_FIXTURE,
    "Requires real synthetic SQL/API fixture.",
  );
  await mkdir(evidence, { recursive: true });
  await page.context().addCookies(authenticatedState.cookies);
  await page.goto("/universal-doc-studio");
  await expect(
    page.getByRole("heading", { name: "Trợ lý Excel văn phòng", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Bước tiếp theo", exact: true }),
  ).toBeEnabled();
});
async function pick(page: Page) {
  await page
    .getByLabel("Mẫu Excel đầu ra", { exact: true })
    .setInputFiles(process.env.UI_V2_OFFICE_FIXTURE!);
}
async function upload(page: Page) {
  await pick(page);
  const request = page.waitForResponse(
    (r) => r.url().endsWith("/template") && r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Tải và phân tích mẫu", exact: true })
    .click();
  const response = await request;
  expect(response.status()).toBe(200);
  await expect(
    page.getByRole("status").filter({ hasText: "Đã phân tích mẫu" }),
  ).toBeVisible();
  return response.json();
}
async function next(page: Page) {
  await page
    .getByRole("button", { name: "Bước tiếp theo", exact: true })
    .click();
}
test.afterEach(async ({ page }) => {
  const id = await page
    .evaluate(() => sessionStorage.getItem("universal-doc-office-session"))
    .catch(() => null);
  if (!id) return;
  const bootstrap = await page.request.get("/api/universal-doc/bootstrap");
  if (!bootstrap.ok()) return;
  const { csrfToken } = await bootstrap.json();
  const read = await page.request.get(`/api/universal-doc/sessions/${id}`);
  if (!read.ok()) return;
  const session = await read.json();
  const removed = await page.request.delete(
    `/api/universal-doc/sessions/${id}`,
    {
      headers: { "X-Studio-CSRF": csrfToken },
      data: { expectedVersion: session.version },
    },
  );
  expect(removed.status()).toBe(204);
});
test("A selected template blocks navigation with a specific upload instruction", async ({
  page,
}) => {
  await pick(page);
  await next(page);
  await expect(
    page.getByRole("heading", { name: "1. Chọn mẫu Excel", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: `${evidence}/${process.env.OFFICE_PENDING_BASELINE ? "before" : "after"}-pending-template.png`,
    fullPage: true,
  });
  await expect(
    page.locator(".office-editing").getByRole("alert"),
  ).toContainText("Tải và phân tích mẫu");
  await expect(page.locator(".office-pending")).toHaveCount(1);
  await expect(
    page.getByText(
      "Có nội dung chưa áp dụng. Đây chưa phải dữ liệu đã lưu hoặc được xác nhận.",
      { exact: true },
    ),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Tải và phân tích mẫu", exact: true }),
  ).toBeFocused();
});
test("B confirmed upload clears pending file and permits structure step without a false warning", async ({
  page,
}) => {
  await upload(page);
  await expect(page.locator(".office-selected-file")).toHaveCount(0);
  await expect(
    page.getByLabel("Mẫu Excel đầu ra", { exact: true }),
  ).toHaveValue("");
  await expect(page.locator(".office-uploaded")).toContainText(
    "Đã tải mẫu lên máy chủ",
  );
  await next(page);
  await expect(
    page.getByRole("heading", { name: "2. Xem cấu trúc", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".office-editing>.uds-error")).toHaveCount(0);
  await page.screenshot({
    path: `${evidence}/${process.env.OFFICE_PENDING_BASELINE ? "before" : "after"}-upload-success.png`,
    fullPage: true,
  });
});
test("C real rejected XLSX upload retains the file for retry and never announces success", async ({
  page,
}) => {
  await page.getByLabel("Mẫu Excel đầu ra", { exact: true }).setInputFiles({
    name: "invalid-synthetic.xlsx",
    mimeType:
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    buffer: Buffer.from("not a valid workbook"),
  });
  const request = page.waitForResponse(
    (r) => r.url().endsWith("/template") && r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Tải và phân tích mẫu", exact: true })
    .click();
  expect((await request).ok()).toBe(false);
  await expect(page.locator(".office-selected-file")).toContainText(
    "invalid-synthetic.xlsx",
  );
  await expect(
    page.getByRole("status").filter({ hasText: "Đã phân tích mẫu" }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Tải và phân tích mẫu", exact: true }),
  ).toBeEnabled();
});
test("D pending example explains comparison and can be explicitly discarded", async ({
  page,
}) => {
  await upload(page);
  await page
    .getByLabel("Ví dụ Excel đã điền", { exact: true })
    .setInputFiles(process.env.UI_V2_OFFICE_FIXTURE!);
  await next(page);
  await expect(
    page.locator(".office-editing").getByRole("alert"),
  ).toContainText("Đối chiếu ví dụ");
  await page
    .getByRole("button", { name: "Bỏ ví dụ đã chọn", exact: true })
    .click();
  await next(page);
  await expect(
    page.getByRole("heading", { name: "2. Xem cấu trúc", exact: true }),
  ).toBeVisible();
});
test("F acknowledged example upload clears pending even if the subsequent preview request fails", async ({
  page,
}) => {
  await upload(page);
  await page
    .getByLabel("Ví dụ Excel đã điền", { exact: true })
    .setInputFiles(process.env.UI_V2_OFFICE_FIXTURE!);
  await page.route(
    "**/api/universal-doc/sessions/*/office-preview?*",
    (route) => route.abort(),
  );
  const uploaded = page.waitForResponse(
    (r) => r.url().endsWith("/example") && r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Đối chiếu ví dụ", exact: true })
    .click();
  expect((await uploaded).status()).toBe(200);
  await expect(
    page.locator(".office-editing").getByRole("alert"),
  ).toBeVisible();
  await page.screenshot({
    path: `${evidence}/${process.env.OFFICE_PENDING_BASELINE ? "before" : "after"}-acknowledged-example-preview-failure.png`,
    fullPage: true,
  });
  await expect(
    page.getByRole("button", { name: "Bỏ ví dụ đã chọn", exact: true }),
  ).toHaveCount(0);
  await next(page);
  await expect(
    page.getByRole("heading", { name: "2. Xem cấu trúc", exact: true }),
  ).toBeVisible();
});
test("E–F schema and mapping edits stay guarded until server acknowledges Apply", async ({
  page,
}) => {
  await upload(page);
  await next(page);
  await page.getByText("Chỉnh sửa chi tiết các mục và gợi ý", {exact:true}).click();
  const count = await page.getByLabel(/^Tên mục \d+$/).count();
  for (let i = 1; i <= count; i++)
    await page.getByLabel(`Vị trí mục ${i}`, { exact: true }).selectOption("0");
  await page.getByLabel("Tên mục 1", { exact: true }).fill("Số phiếu QA");
  await next(page);
  await expect(
    page.getByRole("heading", { name: "2. Xem cấu trúc", exact: true }),
  ).toBeVisible();
  await expect(
    page.locator(".office-editing").getByRole("alert"),
  ).toContainText(
    process.env.OFFICE_PENDING_BASELINE
      ? "Có thay đổi chưa áp dụng."
      : "các mục dữ liệu",
  );
  const schema = page.waitForResponse(
    (r) => r.url().endsWith("/schema") && r.request().method() === "PATCH",
  );
  await page
    .getByRole("button", { name: "Áp dụng các mục dữ liệu", exact: true })
    .click();
  expect((await schema).ok()).toBe(true);
  await expect(
    page.getByRole("status").filter({ hasText: "Đã áp dụng lên máy chủ" }),
  ).toBeVisible();
  await next(page);
  await expect(
    page.getByRole("heading", { name: "3. Chọn vị trí dữ liệu", exact: true }),
  ).toBeVisible();
  await page.getByText("Quản lý sheet và chỉnh sửa vị trí chi tiết", {exact:true}).click();
  await page.getByLabel("Vị trí HoaDon Số phiếu", { exact: true }).fill("B4");
  await next(page);
  await expect(
    page.getByRole("heading", { name: "3. Chọn vị trí dữ liệu", exact: true }),
  ).toBeVisible();
  await expect(
    page.locator(".office-editing").getByRole("alert"),
  ).toContainText(
    process.env.OFFICE_PENDING_BASELINE
      ? "Có thay đổi chưa áp dụng."
      : "vị trí Excel",
  );
  const mapping = page.waitForResponse(
    (r) =>
      r.url().endsWith("/multisheet-mapping") &&
      r.request().method() === "PATCH",
  );
  await page
    .getByRole("button", { name: "Áp dụng vị trí Excel", exact: true })
    .click();
  expect((await mapping).ok()).toBe(true);
  await expect(
    page.getByRole("status").filter({ hasText: "Đã áp dụng lên máy chủ" }),
  ).toBeVisible();
  await next(page);
  await expect(
    page.getByRole("heading", { name: "4. Nhập và kiểm tra", exact: true }),
  ).toBeVisible();
});
test("G saved draft resumes with its revision and data, without granting mapping confirmation or export", async ({
  page,
}) => {
  await upload(page);
  const savedResponse = page.waitForResponse(
    (r) => r.url().endsWith("/save-draft") && r.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Lưu bản nháp", exact: true }).click();
  const saved = await savedResponse;
  expect(saved.ok()).toBe(true);
  const snapshot = await saved.json();
  await expect(
    page
      .getByRole("status")
      .filter({ hasText: "Máy chủ đã xác nhận lưu bản nháp" }),
  ).toBeVisible();
  const resumedResponse = page.waitForResponse(
    (r) => r.url().endsWith("/resume") && r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Mở bản nháp 1", exact: true })
    .click();
  const resumed = await resumedResponse;
  expect(resumed.ok()).toBe(true);
  expect(resumed.request().postDataJSON().expectedRevision).toBe(
    snapshot.draft.revision,
  );
  const session = await resumed.json();
  expect(session.id).not.toBe(snapshot.session.id);
  expect(session.reviewState).not.toBe("Approved");
  expect(session.mappingState).not.toBe("UserConfirmed");
  expect(
    session.schemaDraft.fields.map((f: { key: string; label: string }) => [
      f.key,
      f.label,
    ]),
  ).toEqual(
    snapshot.session.schemaDraft.fields.map(
      (f: { key: string; label: string }) => [f.key, f.label],
    ),
  );
  await expect(
    page.getByRole("heading", { name: "2. Xem cấu trúc", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".office-pending")).toHaveCount(0);
  const boot = await (
    await page.request.get("/api/universal-doc/bootstrap")
  ).json();
  const denied = await page.request.post(
    `/api/universal-doc/sessions/${session.id}/export`,
    {
      headers: { "X-Studio-CSRF": boot.csrfToken },
      data: { expectedVersion: session.version },
    },
  );
  expect(denied.ok()).toBe(false);
  expect(denied.headers()["content-type"]).not.toContain("spreadsheetml");
  const original = await page.request.get(
    `/api/universal-doc/sessions/${snapshot.session.id}`,
  );
  if (original.ok()) {
    const old = await original.json();
    expect(
      (
        await page.request.delete(`/api/universal-doc/sessions/${old.id}`, {
          headers: { "X-Studio-CSRF": boot.csrfToken },
          data: { expectedVersion: old.version },
        })
      ).status(),
    ).toBe(204);
  }
});
test("B invalid session/version acknowledgement never clears the pending template or reports success", async ({
  page,
}) => {
  await pick(page);
  await page.route(
    "**/api/universal-doc/sessions/*/template",
    async (route) => {
      const current = await (
        await page.request.get(
          route
            .request()
            .url()
            .replace(/\/template$/, ""),
        )
      ).json();
      const response = await page.request.post(route.request().url(), {
        headers: {
          "X-Studio-CSRF": route.request().headers()["x-studio-csrf"],
        },
        multipart: {
          expectedVersion: String(current.version),
          file: {
            name: "synthetic-office.xlsx",
            mimeType:
              "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            buffer: await readFile(process.env.UI_V2_OFFICE_FIXTURE!),
          },
        },
      });
      const body = await response.json();
      expect(response.status(), String(body.code ?? "upload response")).toBe(
        200,
      );
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ ...body, version: 0 }),
      });
    },
  );
  await page
    .getByRole("button", { name: "Tải và phân tích mẫu", exact: true })
    .click();
  await expect(
    page.locator(".office-editing").getByRole("alert"),
  ).toContainText("Dữ liệu đã thay đổi");
  await expect(page.locator(".office-selected-file")).toContainText(
    "synthetic-office.xlsx",
  );
  await expect(
    page.getByRole("status").filter({ hasText: "Đã phân tích mẫu" }),
  ).toHaveCount(0);
});
