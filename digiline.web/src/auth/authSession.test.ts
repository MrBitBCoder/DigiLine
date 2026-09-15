import { beforeEach, describe, expect, it } from "vitest";
import { createSession, readSession } from "./authSession";

function token(expiresAt: number) {
  const encode = (value: object) =>
    btoa(JSON.stringify(value))
      .replace(/=/g, "")
      .replace(/\+/g, "-")
      .replace(/\//g, "_");
  return `${encode({ alg: "none" })}.${encode({ exp: expiresAt, sub: "staff-1", email: "doctor@example.com", unique_name: "Dr Shah", role: "Doctor", clinic_id: "clinic-1" })}.signature`;
}

describe("auth session restoration", () => {
  beforeEach(() => sessionStorage.clear());

  it("restores a valid staff session", () => {
    const session = createSession(token(Math.floor(Date.now() / 1000) + 600));
    sessionStorage.setItem("digiline.staff-session", JSON.stringify(session));
    expect(readSession()?.user.role).toBe("Doctor");
  });

  it("rejects and removes an expired session", () => {
    const expired = { token: token(Math.floor(Date.now() / 1000) - 60) };
    sessionStorage.setItem("digiline.staff-session", JSON.stringify(expired));
    expect(readSession()).toBeNull();
    expect(sessionStorage.getItem("digiline.staff-session")).toBeNull();
  });
});
