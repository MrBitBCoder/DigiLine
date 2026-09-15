import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { configureHttpClient, request } from "../api/httpClient";
import {
  clearSession,
  createSession,
  readSession,
  saveSession,
  type AuthSession,
} from "./authSession";
import { AuthContext } from "./AuthContext";
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() =>
    readSession(),
  );
  const logout = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);
  useEffect(() => {
    configureHttpClient({
      getAccessToken: () => session?.token ?? null,
      onUnauthorized: logout,
    });
    if (!session) return;
    const timeout = window.setTimeout(
      logout,
      Math.max(0, session.expiresAt - Date.now()),
    );
    return () => window.clearTimeout(timeout);
  }, [session, logout]);
  const login = useCallback(async (email: string, password: string) => {
    const response = await request<{ token: string }>("/api/auth/login", {
      method: "POST",
      body: { email, password },
    });
    const next = createSession(response.token);
    saveSession(next);
    setSession(next);
    return next;
  }, []);
  const value = useMemo(
    () => ({ session, login, logout }),
    [session, login, logout],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
