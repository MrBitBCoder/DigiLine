import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, getApiErrorMessage, request } from "./httpClient";

describe("API error messages", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("prefers validation errors, detail, then title", () => {
    expect(getApiErrorMessage(new ApiError("failed", 400, { errors: { email: ["Enter a valid email."] }, detail: "detail" }))).toBe("Enter a valid email.");
    expect(getApiErrorMessage(new ApiError("failed", 409, { detail: "That email is already registered.", title: "Conflict" }))).toBe("That email is already registered.");
    expect(getApiErrorMessage(new ApiError("failed", 404, { title: "Staff member was not found." }))).toBe("Staff member was not found.");
  });

  it("provides permission and not-found fallbacks", () => {
    expect(getApiErrorMessage(new ApiError("failed", 403))).toBe("You do not have permission to perform this action.");
    expect(getApiErrorMessage(new ApiError("failed", 404))).toBe("The requested record was not found.");
  });

  it("surfaces expected lifecycle and booking conflicts", () => {
    expect(getApiErrorMessage(new ApiError("failed", 409, { detail: "Call Next is only available for a running session." })))
      .toBe("Call Next is only available for a running session.");
    expect(getApiErrorMessage(new ApiError("failed", 409, { detail: "Booking is not currently open for this session." })))
      .toBe("Booking is not currently open for this session.");
  });

  it("never exposes server details for an unexpected failure", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
      title: "SqlException",
      detail: "Violation of UNIQUE KEY constraint IX_Staff_NormalizedEmail",
    }), { status: 500, headers: { "Content-Type": "application/problem+json" } })));

    const error = await request("/api/failure").catch(caught => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(getApiErrorMessage(error)).toBe("The service is temporarily unavailable. Please try again.");
    expect((error as ApiError).details).toBeUndefined();
    expect((error as Error).message).not.toContain("constraint");
  });
});
