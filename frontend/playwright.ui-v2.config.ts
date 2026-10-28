import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests/ui-v2",
  timeout: 45000,
  fullyParallel: false,
  workers: 1,
  reporter: [
    ["list"],
    ["json", { outputFile: "../.audit-run/ui-v2/results.json" }],
  ],
  outputDir: "../.audit-run/ui-v2/test-results",
  use: {
    actionTimeout: 15000,
    baseURL: process.env.UI_V2_BASE_URL || "http://127.0.0.1:5174",
    headless: true,
    channel: process.platform === "win32" ? "msedge" : undefined,
    screenshot: "off",
    trace: "off",
  },
  webServer: {
    command: "npm run dev -- --host 127.0.0.1 --port 5174 --strictPort",
    url: "http://127.0.0.1:5174",
    reuseExistingServer: !process.env.CI,
    env: {
      XNK_API_TARGET: process.env.UI_V2_API_TARGET || "http://localhost:5270",
    },
    timeout: 45000,
  },
});
