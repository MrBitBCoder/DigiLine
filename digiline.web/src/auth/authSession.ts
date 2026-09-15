import type { StaffRole } from "../shared/types/domain";
const STORAGE_KEY = "digiline.staff-session";
export interface AuthUser {
  id: string;
  email: string;
  name: string;
  role: StaffRole;
  roles?: StaffRole[];
  clinicId: string;
}
export interface AuthSession {
  token: string;
  expiresAt: number;
  user: AuthUser;
}
type JwtPayload = {
  exp?: number;
  sub?: string;
  email?: string;
  name?: string;
  unique_name?: string;
  role?: string | string[];
  clinic_id?: string;
};
function decodePayload(token: string): JwtPayload {
  const encoded = token.split(".")[1];
  if (!encoded) throw new Error("The server returned an invalid session.");
  const normalized = encoded.replace(/-/g, "+").replace(/_/g, "/");
  const json = decodeURIComponent(
    Array.from(
      atob(normalized),
      (char) => `%${char.charCodeAt(0).toString(16).padStart(2, "0")}`,
    ).join(""),
  );
  return JSON.parse(json) as JwtPayload;
}
export function createSession(token: string): AuthSession {
  const payload = decodePayload(token);
  const roles = (Array.isArray(payload.role) ? payload.role : [payload.role]).filter((value): value is StaffRole => value === "Doctor" || value === "Receptionist" || value === "Admin");
  const role = roles[0];
  const name = payload.name ?? payload.unique_name;
  if (
    !payload.exp ||
    !payload.sub ||
    !payload.email ||
    !name ||
    !payload.clinic_id ||
    !roles.length
  )
    throw new Error("The server returned an incomplete session.");
  return {
    token,
    expiresAt: payload.exp * 1000,
    user: {
      id: payload.sub,
      email: payload.email,
      name,
      role,
      roles,
      clinicId: payload.clinic_id,
    },
  };
}
export function readSession(): AuthSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = createSession((JSON.parse(raw) as AuthSession).token);
    if (session.expiresAt <= Date.now()) {
      clearSession();
      return null;
    }
    return session;
  } catch {
    clearSession();
    return null;
  }
}
export function saveSession(session: AuthSession) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}
export function clearSession() {
  sessionStorage.removeItem(STORAGE_KEY);
}

export const userRoles = (user: AuthUser): StaffRole[] => user.roles ?? [user.role];
