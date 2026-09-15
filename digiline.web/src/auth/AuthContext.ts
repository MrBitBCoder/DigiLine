import { createContext, useContext } from "react";
import type { AuthSession } from "./authSession";

export interface AuthContextValue {
  session: AuthSession | null;
  login: (email: string, password: string) => Promise<AuthSession>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside AuthProvider.");
  return context;
}
