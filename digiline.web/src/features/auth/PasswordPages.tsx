import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { request, getApiErrorMessage } from "../../api/httpClient";
import { useAuth } from "../../auth/AuthContext";
import { Button, Card, PageHeader, TextField, useToast } from "../../shared/ui";
import "./login-page.css";

export function ChangePasswordPage() {
  const toast = useToast();
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [currentPassword, setCurrent] = useState("");
  const [newPassword, setNext] = useState("");
  const [confirmPassword, setConfirm] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (newPassword !== confirmPassword) { setError("Passwords do not match."); return; }
    setBusy(true); setError("");
    try {
      await request<void>("/api/auth/change-password", { method: "POST", body: { currentPassword, newPassword, confirmPassword } });
      toast.success("Password changed successfully. Sign in with your new password.");
      logout(); navigate("/login", { replace: true });
    } catch (caught) { toast.error(getApiErrorMessage(caught)); }
    finally { setBusy(false); }
  }
  return <section className="account-security"><PageHeader eyebrow="Account security" title="Change password" description="Use at least 12 characters with uppercase, lowercase, a number, and a symbol." />
    <Card className="form-page-card">
    <form onSubmit={submit}>
      <TextField label="Current password" type="password" autoComplete="current-password" value={currentPassword} onChange={event => setCurrent(event.target.value)} required />
      <TextField label="New password" type="password" autoComplete="new-password" minLength={12} value={newPassword} onChange={event => setNext(event.target.value)} required />
      <TextField label="Confirm new password" type="password" autoComplete="new-password" value={confirmPassword} onChange={event => setConfirm(event.target.value)} required />
      {error && <p role="alert" className="form-error">{error}</p>}
      <Button type="submit" busy={busy}>Change password</Button>
    </form></Card></section>;
}

export function ForgotPasswordPage() {
  const toast = useToast();
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError("");
    try { await request("/api/auth/forgot-password", { method: "POST", body: { email } }); setSent(true); toast.info("If that email is registered, a reset link has been sent."); }
    catch (caught) { toast.error(getApiErrorMessage(caught)); }
    finally { setBusy(false); }
  }
  return <main className="auth-page auth-page-single"><Card className="auth-card"><span className="eyebrow">Account recovery</span><h1>Forgot password</h1><p>Enter your staff email to request a secure reset link.</p>
    {sent ? <p>If that email is registered, a reset link has been sent.</p> : <form onSubmit={submit}>
      <TextField label="Email address" type="email" autoComplete="email" value={email} onChange={event => setEmail(event.target.value)} required />
      {error && <p role="alert" className="form-error">{error}</p>}
      <Button type="submit" busy={busy}>Send reset link</Button>
    </form>}
    <Link to="/login">Back to sign in</Link>
  </Card></main>;
}

export function ResetPasswordPage() {
  const toast = useToast();
  const [token] = useState(() => {
    return new URLSearchParams(window.location.hash.slice(1)).get("token");
  });
  useEffect(() => { window.history.replaceState(window.history.state, "", window.location.pathname); }, []);
  const [newPassword, setNext] = useState("");
  const [confirmPassword, setConfirm] = useState("");
  const [done, setDone] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (newPassword !== confirmPassword) { setError("Passwords do not match."); return; }
    setBusy(true); setError("");
    try { await request<void>("/api/auth/reset-password", { method: "POST", body: { token, newPassword, confirmPassword } }); setDone(true); toast.success("Password reset successfully."); }
    catch (caught) { toast.error(getApiErrorMessage(caught)); }
    finally { setBusy(false); }
  }
  return <main className="auth-page auth-page-single"><Card className="auth-card"><span className="eyebrow">Account recovery</span><h1>Reset password</h1><p>Choose a new password for your staff account.</p>
    {!token ? <p role="alert">This reset link is invalid.</p> : done ? <p>Password changed. You can sign in now.</p> : <form onSubmit={submit}>
      <TextField label="New password" type="password" autoComplete="new-password" minLength={12} value={newPassword} onChange={event => setNext(event.target.value)} required />
      <TextField label="Confirm new password" type="password" autoComplete="new-password" value={confirmPassword} onChange={event => setConfirm(event.target.value)} required />
      {error && <p role="alert" className="form-error">{error}</p>}
      <Button type="submit" busy={busy}>Reset password</Button>
    </form>}
    <Link to="/login">Back to sign in</Link>
  </Card></main>;
}
