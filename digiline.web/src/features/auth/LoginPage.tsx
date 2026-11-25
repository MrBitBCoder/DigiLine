import { userRoles } from "../../auth/authSession";
import { useState, type FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { ApiError } from "../../api/httpClient";
import { useAuth } from "../../auth/AuthContext";
import { Button, Card, TextField, useToast } from "../../shared/ui";
import "./login-page.css";
export function LoginPage() {
  const { session, login } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  if (session)
    return (
      <Navigate
        to={
          userRoles(session.user).includes("Doctor")
            ? "/doctor"
            : userRoles(session.user).includes("Admin")
              ? "/admin"
              : "/staff"
        }
        replace
      />
    );
  async function submit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    try {
      const next = await login(email.trim(), password);
      const requested = (location.state as { from?: string } | null)?.from;
      navigate(
        requested ??
          (userRoles(next.user).includes("Doctor")
            ? "/doctor"
            : userRoles(next.user).includes("Admin")
              ? "/admin"
              : "/staff"),
        { replace: true },
      );
    } catch (caught) {
      toast.error(
        caught instanceof ApiError && caught.status === 401
          ? "The email or password is incorrect."
          : caught instanceof Error
            ? caught.message
            : "Unable to sign in right now.",
      );
    } finally {
      setSubmitting(false);
    }
  }
  return (
    <main className="auth-page login-page">
      <div className="auth-shell">
        <section
          className="auth-brand-context"
          aria-label="DigiLine staff access"
        >
          <span className="brand-mark" aria-hidden="true">
            D
          </span>
          <span className="eyebrow">DigiLine staff</span>
          <h2>Clinic queue workspace</h2>
          <p>Manage today’s sessions and keep patients moving.</p>
        </section>
        <Card className="auth-card login-card" aria-labelledby="login-title">
          <div>
            <span className="eyebrow">Welcome back</span>
            <h1 id="login-title">Sign in to your workspace</h1>
          </div>
          <form onSubmit={submit}>
            <TextField
              label="Email address"
              autoComplete="username"
              inputMode="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
              autoFocus
            />
            <TextField
              label="Password"
              autoComplete="current-password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
            <Button className="login-button" busy={submitting} type="submit">
              {submitting ? "Signing in…" : "Sign in"}
            </Button>
            <div className="auth-links">
              <Link to="/register">Register a clinic</Link>
              <Link to="/forgot-password">Forgot password?</Link>
            </div>
          </form>
        </Card>
      </div>
    </main>
  );
}
