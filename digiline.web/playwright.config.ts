import { defineConfig } from "@playwright/test";
export default defineConfig({ testDir: "./e2e", timeout: 60000, workers: 1, use: { baseURL: "http://localhost:57436", channel: "msedge", headless: true, trace: "retain-on-failure" }, reporter: [["list"], ["json", { outputFile: "test-results/browser.json" }]] });
