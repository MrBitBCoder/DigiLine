import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { request, ApiError } from "../../api/httpClient";
import { useAuth } from "../../auth/AuthContext";
import { Button, Card, TextField, DataTable } from "../../shared/ui";
import type { StaffRole, PageResult } from "../../shared/types/domain";
import "./administration.css";

function message(error: unknown) {
  if (error instanceof ApiError && error.details && typeof error.details === "object" && "detail" in error.details) return String(error.details.detail);
  return error instanceof Error ? error.message : "The request failed.";
}
const passwordHelp = "Use at least 12 characters with uppercase, lowercase, number, and symbol.";
export function RegistrationPage() {
  const { login } = useAuth(); const navigate = useNavigate();
  const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const values = new FormData(event.currentTarget); setBusy(true); setError("");
    const body = Object.fromEntries(values); const email = String(body.email); const password = String(body.password);
    try { await request("/api/auth/register", { method: "POST", body: { ...body, alsoDoctor: values.has("alsoDoctor") } }); await login(email, password); navigate("/admin"); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <main className="onboarding"><Card><h1>Register your clinic</h1><p>Your clinic becomes active immediately.</p>
    <form onSubmit={submit}><TextField label="Clinic name" name="clinicName" required maxLength={200} />
    <TextField label="Clinic address" name="slug" required maxLength={100} pattern="[a-z0-9]+(-[a-z0-9]+)*" hint="Your public page: /c/your-clinic. Use lowercase letters, numbers, and hyphens." />
    <TextField label="Clinic time zone" name="timeZoneId" defaultValue="Asia/Kolkata" required hint="Use an IANA time zone, for example Asia/Kolkata." />
    <TextField label="Your name" name="name" required maxLength={200} autoComplete="name" />
    <TextField label="Email" name="email" required type="email" autoComplete="email" />
    <TextField label="Password" name="password" required type="password" minLength={12} autoComplete="new-password" hint={passwordHelp} />
    <label className="check"><input type="checkbox" name="alsoDoctor" />I also practice here</label>
    {error && <p role="alert">{error}</p>}<Button busy={busy}>Create clinic</Button><Link to="/login">Sign in instead</Link></form></Card></main>;
}
export function AcceptInvitationPage() {
  const [token] = useState(() => new URLSearchParams(location.hash.slice(1)).get("token") ?? "");
  const [error, setError] = useState(""); const [busy, setBusy] = useState(false); const [done, setDone] = useState(false);
  useEffect(() => { history.replaceState(null, "", location.pathname); }, []);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget); setBusy(true); setError("");
    try { if (data.get("password") !== data.get("confirm")) throw new Error("Passwords do not match.");
      await request("/api/auth/accept-invitation", { method: "POST", body: { token, password: data.get("password") } }); setDone(true);
    } catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <main className="onboarding"><Card><h1>Join your clinic</h1>{done ? <p role="status">Your account is active. <Link to="/login">Sign in</Link></p> : <form onSubmit={submit}>
    <TextField label="Password" name="password" type="password" required minLength={12} autoComplete="new-password" hint={passwordHelp} />
    <TextField label="Confirm password" name="confirm" type="password" required autoComplete="new-password" />
    {error && <p role="alert">{error}</p>}<Button busy={busy} disabled={!token}>Activate account</Button>{!token && <p role="alert">Open a valid invitation link from your email.</p>}</form>}</Card></main>;
}
interface Staff { id: string; name: string; email: string; roles: StaffRole[]; isActive: boolean; invitationPending: boolean; deliveryFailed: boolean; }
interface Clinic { name: string; slug: string; timeZoneId: string; }
const roles: StaffRole[] = ["Admin", "Doctor", "Receptionist"];
const roleNames = ["", "Doctor", "Receptionist", "Admin"];
function normalize(staff: Staff): Staff { return { ...staff, roles: staff.roles.map(r => typeof r === "number" ? roleNames[r] as StaffRole : r) }; }
export function AdministrationPage() {
  const [clinic, setClinic] = useState<Clinic | null>(null); const [staff, setStaff] = useState<PageResult<Staff> | null>(null);
  const [page, setPage] = useState(1); const [editing, setEditing] = useState<Staff | null>(null);
  const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [busy, setBusy] = useState(false);
  const load = useCallback(async (signal?: AbortSignal) => {
    const [c, s] = await Promise.all([request<Clinic>("/api/admin/clinic", { signal }), request<PageResult<Staff>>(`/api/admin/staff?page=${page}`, { signal })]);
    if (!signal?.aborted) { setClinic(c); setStaff({ ...s, items: s.items.map(normalize) }); }
  }, [page]);
  useEffect(() => { const controller = new AbortController(); queueMicrotask(() => { if (!controller.signal.aborted) void load(controller.signal).catch(e => { if (!controller.signal.aborted) setError(message(e)); }); }); return () => controller.abort(); }, [load]);
  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true); setError(""); setNotice(""); try { await action(); await load(); setNotice(success); } catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <div className="administration"><h1>Clinic administration</h1>{error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {clinic && <Card><h2>Clinic details</h2><p><Link to={`/c/${clinic.slug}`}>Open public clinic page</Link></p><form key={`${clinic.name}:${clinic.timeZoneId}`} onSubmit={e => { e.preventDefault(); const data = Object.fromEntries(new FormData(e.currentTarget)); void run(() => request("/api/admin/clinic", { method: "PUT", body: data }), "Clinic details saved."); }}>
      <TextField label="Clinic name" name="name" defaultValue={clinic.name} required maxLength={200} /><TextField label="Time zone" name="timeZoneId" defaultValue={clinic.timeZoneId} required /><Button busy={busy}>Save clinic</Button></form></Card>}
    <Card><h2>{editing ? `Edit ${editing.name}` : "Invite staff"}</h2><form key={editing?.id ?? "invite"} onSubmit={e => {
      e.preventDefault(); const form = e.currentTarget; const data = new FormData(form); const selected = data.getAll("roles").map(String);
      void run(async () => { const result = await request<Staff>(editing ? `/api/admin/staff/${editing.id}` : "/api/admin/staff", { method: editing ? "PUT" : "POST", body: { name: data.get("name"), email: data.get("email"), roles: selected.map(r => roles.indexOf(r as StaffRole) === 0 ? 3 : r === "Doctor" ? 1 : 2), isActive: data.has("isActive") } });
        setEditing(null); form.reset(); if (result?.deliveryFailed) throw new Error("Invitation saved, but email delivery failed. Use Resend invitation to retry.");
      }, editing ? "Staff updated. Changed access requires a new sign-in." : "Invitation sent.");
    }}><TextField label="Staff name" name="name" defaultValue={editing?.name} required maxLength={200} />
    {!editing && <TextField label="Staff email" name="email" type="email" required />}
    <fieldset><legend>Roles</legend>{roles.map(role => <label className="check" key={role}><input type="checkbox" name="roles" value={role} defaultChecked={editing ? editing.roles.includes(role) : role === "Receptionist"} />{role}</label>)}</fieldset>
    {editing && <label className="check"><input type="checkbox" name="isActive" defaultChecked={editing.isActive} disabled={editing.invitationPending} />Active account</label>}
    <Button busy={busy}>{editing ? "Save staff" : "Send invitation"}</Button>{editing && <Button type="button" variant="secondary" onClick={() => setEditing(null)}>Cancel edit</Button>}</form></Card>
    <Card><h2>Staff</h2>{staff && <DataTable caption="Clinic staff" rows={staff.items} getRowKey={s => s.id} page={page} totalCount={staff.totalCount} onPageChange={setPage} columns={[
      { key: "name", header: "Name", render: s => s.name }, { key: "email", header: "Email", render: s => s.email }, { key: "roles", header: "Roles", render: s => s.roles.join(", ") },
      { key: "status", header: "Status", render: s => s.invitationPending ? s.deliveryFailed ? "Invitation delivery failed" : "Invitation pending" : s.isActive ? "Active" : "Inactive" },
      { key: "actions", header: "Actions", render: s => <div className="row-actions"><Button disabled={busy} onClick={() => setEditing(s)}>Edit</Button>{s.invitationPending && <><Button disabled={busy} onClick={() => void run(async () => { const result = await request<{ delivered: boolean }>(`/api/admin/staff/${s.id}/resend`, { method: "POST" }); if (!result.delivered) throw new Error("Email delivery failed. Check SMTP configuration and retry."); }, "Invitation resent; previous links are invalid.")}>Resend invitation</Button><Button variant="danger" disabled={busy} onClick={() => void run(() => request(`/api/admin/staff/${s.id}/revoke`, { method: "POST" }), "Invitation revoked.")}>Revoke invitation</Button></>}</div> }
    ]} />}</Card></div>;
}
