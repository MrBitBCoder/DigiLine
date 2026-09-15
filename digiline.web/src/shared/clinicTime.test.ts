import { describe, expect, it } from "vitest";
import { clinicDateTimeInput, clinicInputToUtc, formatClinicTime } from "./clinicTime";

describe("clinic schedule time", () => {
  it("round trips independently of the viewer's time zone", () => {
    expect(clinicInputToUtc("2026-09-14T10:30", "Asia/Kolkata")).toBe("2026-09-14T05:00:00.000Z");
    expect(clinicDateTimeInput("2026-09-14T05:00:00Z", "Asia/Kolkata")).toBe("2026-09-14T10:30");
    expect(formatClinicTime("2026-09-14T05:00:00Z", "Asia/Kolkata")).toMatch(/10:30/);
  });

  it("rejects nonexistent and repeated daylight-saving wall times", () => {
    expect(() => clinicInputToUtc("2026-03-08T02:30", "America/New_York")).toThrow(/does not exist/);
    expect(() => clinicInputToUtc("2026-11-01T01:30", "America/New_York")).toThrow(/occurs twice/);
  });
});
