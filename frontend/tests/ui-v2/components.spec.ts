import { test, expect } from "@playwright/test";
test("upload picker retains selected synthetic XLSX and removes it explicitly", async ({
  page,
}) => {
  await page.goto("/tests/ui-v2/components.html");
  await page
    .getByLabel("Mẫu Excel đầu ra", { exact: true })
    .setInputFiles({
      name: "synthetic.xlsx",
      mimeType:
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      buffer: Buffer.from(
        "synthetic component fixture; not submitted to server",
      ),
    });
  await expect(page.getByRole("status")).toContainText("synthetic.xlsx");
  await page.getByRole("button", { name: "Gỡ tệp đã chọn" }).click();
  await expect(page.getByRole("status")).toHaveText("No file selected");
});
test("upload rejects wrong extension and oversized input without accepting it", async ({
  page,
}) => {
  await page.goto("/tests/ui-v2/components.html");
  const input = page.getByLabel("Mẫu Excel đầu ra", { exact: true });
  await input.setInputFiles({
    name: "invalid.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("synthetic"),
  });
  await expect(page.getByRole("alert")).toContainText(".xlsx");
  await expect(page.getByRole("status")).toHaveText("No file selected");
  await input.setInputFiles({
    name: "large.xlsx",
    mimeType: "application/octet-stream",
    buffer: Buffer.alloc(2 * 1024 * 1024 + 1),
  });
  await expect(page.getByRole("alert")).toContainText("2 MiB");
  await expect(page.getByRole("status")).toHaveText("No file selected");
});
