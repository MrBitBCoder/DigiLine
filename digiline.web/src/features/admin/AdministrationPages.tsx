import {
    useCallback,
    useEffect,
    useRef,
    useState,
    type FormEvent,
} from "react";
import { Link, useNavigate } from "react-router-dom";
import { request, getApiErrorMessage } from "../../api/httpClient";
import { useAuth } from "../../auth/AuthContext";
import {
    Button,
    Card,
    ConfirmationDialog,
    TextField,
    DataTable,
    PageHeader,
    useToast,
} from "../../shared/ui";
import type { StaffRole, PageResult } from "../../shared/types/domain";
import "./administration.css";
import "../auth/login-page.css";

const passwordHelp =
    "Use at least 12 characters with uppercase, lowercase, number, and symbol.";
export function RegistrationPage() {
    const { login } = useAuth();
    const navigate = useNavigate();
    const toast = useToast();
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    async function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const values = new FormData(event.currentTarget);
        setBusy(true);
        setError("");
        const body = Object.fromEntries(values);
        const email = String(body.email);
        const password = String(body.password);
        try {
            await request("/api/auth/register", {
                method: "POST",
                body: { ...body, alsoDoctor: values.has("alsoDoctor") },
            });
            await login(email, password);
            toast.success("Clinic registered successfully.");
            navigate("/admin");
        } catch (e) {
            const message = getApiErrorMessage(e);
            setError(message);
            toast.error(message);
        } finally {
            setBusy(false);
        }
    }
    return (
        <main className="auth-page auth-page-single onboarding">
            <Card className="auth-card onboarding-card">
                <div className="auth-card-heading">
                    <span className="eyebrow">New clinic</span>
                    <h1>Register your clinic</h1>
                    <p>
                        Your clinic and administrator account become active immediately.
                    </p>
                </div>
                <form className="form-grid onboarding-form" onSubmit={submit}>
                    <TextField
                        label="Clinic name"
                        name="clinicName"
                        required
                        maxLength={200}
                    />
                    <TextField
                        label="Clinic address"
                        name="slug"
                        required
                        maxLength={100}
                        pattern="[a-z0-9]+(-[a-z0-9]+)*"
                        hint="Your public page: /c/your-clinic. Use lowercase letters, numbers, and hyphens."
                    />
                    <TextField
                        label="Clinic time zone"
                        name="timeZoneId"
                        defaultValue="Asia/Kolkata"
                        required
                        hint="Use an IANA time zone, for example Asia/Kolkata."
                    />
                    <TextField
                        label="Your name"
                        name="name"
                        required
                        maxLength={200}
                        autoComplete="name"
                    />
                    <TextField
                        label="Email"
                        name="email"
                        required
                        type="email"
                        autoComplete="email"
                    />
                    <TextField
                        label="Password"
                        name="password"
                        required
                        type="password"
                        minLength={12}
                        autoComplete="new-password"
                        hint={passwordHelp}
                    />
                    <label className="check">
                        <input type="checkbox" name="alsoDoctor" />I also practice here
                    </label>
                    {error && (
                        <p role="alert" className="form-error">
                            {error}
                        </p>
                    )}
                    <div className="form-actions onboarding-actions">
                        <Button busy={busy}>Create clinic</Button>
                        <Link to="/login">Sign in instead</Link>
                    </div>
                </form>
            </Card>
        </main>
    );
}
export function AcceptInvitationPage() {
    const toast = useToast();
    const [token] = useState(
        () => new URLSearchParams(location.hash.slice(1)).get("token") ?? "",
    );
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const [done, setDone] = useState(false);
    useEffect(() => {
        history.replaceState(null, "", location.pathname);
    }, []);
    async function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        setBusy(true);
        setError("");
        try {
            if (data.get("password") !== data.get("confirm"))
                throw new Error("Passwords do not match.");
            await request("/api/auth/accept-invitation", {
                method: "POST",
                body: { token, password: data.get("password") },
            });
            setDone(true);
            toast.success("Your account is active.");
        } catch (e) {
            const message = getApiErrorMessage(e);
            setError(message);
            toast.error(message);
        } finally {
            setBusy(false);
        }
    }
    return (
        <main className="auth-page auth-page-single onboarding">
            <Card className="auth-card onboarding-card">
                <div className="auth-card-heading">
                    <span className="eyebrow">Staff account</span>
                    <h1>Join your clinic</h1>
                    <p>Set a secure password to activate your account.</p>
                </div>
                {done ? (
                    <p role="status">
                        Your account is active. <Link to="/login">Sign in</Link>
                    </p>
                ) : (
                    <form onSubmit={submit}>
                        <TextField
                            label="Password"
                            name="password"
                            type="password"
                            required
                            minLength={12}
                            autoComplete="new-password"
                            hint={passwordHelp}
                        />
                        <TextField
                            label="Confirm password"
                            name="confirm"
                            type="password"
                            required
                            autoComplete="new-password"
                        />
                        {error && (
                            <p role="alert" className="form-error">
                                {error}
                            </p>
                        )}
                        <Button busy={busy} disabled={!token}>
                            Activate account
                        </Button>
                        {!token && (
                            <p role="alert" className="form-error">
                                Open a valid invitation link from your email.
                            </p>
                        )}
                    </form>
                )}
            </Card>
        </main>
    );
}
interface Staff {
    id: string;
    name: string;
    email: string;
    roles: StaffRole[];
    isActive: boolean;
    invitationPending: boolean;
    deliveryFailed: boolean;
}
interface Clinic {
    name: string;
    slug: string;
    timeZoneId: string;
}
const roles: StaffRole[] = ["Admin", "Doctor", "Receptionist"];
const roleNames = ["", "Doctor", "Receptionist", "Admin"];
function normalize(staff: Staff): Staff {
    return {
        ...staff,
        roles: staff.roles.map((r) =>
            typeof r === "number" ? (roleNames[r] as StaffRole) : r,
        ),
    };
}
function StaffState({ staff }: { staff: Staff }) {
    const label = staff.invitationPending
        ? staff.deliveryFailed
            ? "Invitation delivery failed"
            : "Invitation pending"
        : staff.isActive
            ? "Active"
            : "Inactive";
    const tone = staff.invitationPending
        ? staff.deliveryFailed
            ? "danger"
            : "warning"
        : staff.isActive
            ? "success"
            : "neutral";
    return (
        <span className={`status-badge status-${tone}`}>
            <span aria-hidden="true" />
            {label}
        </span>
    );
}
interface AdminConfirmation {
    title: string;
    message: string;
    confirmLabel: string;
    action: () => Promise<boolean>;
}
export function AdministrationPage() {
    const toast = useToast();
    const [clinic, setClinic] = useState<Clinic | null>(null);
    const [staff, setStaff] = useState<PageResult<Staff> | null>(null);
    const [page, setPage] = useState(1);
    const [editing, setEditing] = useState<Staff | null>(null);
    const [creating, setCreating] = useState(false);
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const [confirmation, setConfirmation] = useState<AdminConfirmation | null>(
        null,
    );
    const editorHeading = useRef<HTMLHeadingElement>(null);
    const addStaffButton = useRef<HTMLButtonElement>(null);
    const load = useCallback(
        async (signal?: AbortSignal) => {
            const [c, s] = await Promise.all([
                request<Clinic>("/api/admin/clinic", { signal }),
                request<PageResult<Staff>>(`/api/admin/staff?page=${page}`, { signal }),
            ]);
            if (!signal?.aborted) {
                setClinic(c);
                setStaff({ ...s, items: s.items.map(normalize) });
            }
        },
        [page],
    );
    useEffect(() => {
        const controller = new AbortController();
        queueMicrotask(() => {
            if (!controller.signal.aborted)
                void load(controller.signal).catch((e) => {
                    if (!controller.signal.aborted) setError(getApiErrorMessage(e));
                });
        });
        return () => controller.abort();
    }, [load]);
    useEffect(() => {
        if (creating || editing) editorHeading.current?.focus();
    }, [creating, editing]);
    async function run(action: () => Promise<unknown>, success: string) {
        setBusy(true);
        setError("");
        try {
            await action();
            await load();
            toast.success(success);
            return true;
        } catch (e) {
            toast.error(getApiErrorMessage(e));
            return false;
        } finally {
            setBusy(false);
        }
    }
    function closeEditor() {
        setCreating(false);
        setEditing(null);
        requestAnimationFrame(() => addStaffButton.current?.focus());
    }
    return (
        <div className="administration">
            <PageHeader
                eyebrow="Clinic workspace"
                title="Clinic administration"
                description="Manage staff access and clinic settings."
                actions={
                    <Button
                        ref={addStaffButton}
                        onClick={() => {
                            setEditing(null);
                            setCreating(true);
                        }}
                    >
                        Add staff
                    </Button>
                }
            />
            {error && (
                <p role="alert" className="form-error">
                    {error}
                </p>
            )}
            {(creating || editing) && (
                <Card className="staff-editor" padding="compact">
                    <div className="staff-editor-heading">
                        <div>
                            <span className="eyebrow">Account access</span>
                            <h2 ref={editorHeading} tabIndex={-1}>
                                {editing ? `Edit ${editing.name}` : "Add staff"}
                            </h2>
                        </div>
                        <p>
                            {editing
                                ? "Update roles and account access."
                                : "Create an active account that can sign in immediately."}
                        </p>
                    </div>
                    <form
                        className="form-grid"
                        key={editing?.id ?? "create"}
                        onSubmit={(e) => {
                            e.preventDefault();
                            const form = e.currentTarget;
                            const data = new FormData(form);
                            const selected = data.getAll("roles").map(String);
                            if (!editing && data.get("password") !== data.get("confirm")) {
                                setError("Passwords do not match.");
                                return;
                            }
                            const save = async () => {
                                const saved = await run(
                                    () =>
                                        request<Staff>(
                                            editing
                                                ? `/api/admin/staff/${editing.id}`
                                                : "/api/admin/staff",
                                            {
                                                method: editing ? "PUT" : "POST",
                                                body: {
                                                    name: data.get("name"),
                                                    email: data.get("email"),
                                                    roles: selected.map((r) =>
                                                        roles.indexOf(r as StaffRole) === 0
                                                            ? 3
                                                            : r === "Doctor"
                                                                ? 1
                                                                : 2,
                                                    ),
                                                    ...(editing
                                                        ? { isActive: data.has("isActive") }
                                                        : { password: data.get("password") }),
                                                },
                                            },
                                        ),
                                    editing
                                        ? "Staff updated. Changed access requires a new sign-in."
                                        : "Staff account created. They can sign in immediately.",
                                );
                                if (saved) {
                                    setEditing(null);
                                    setCreating(false);
                                    form.reset();
                                }
                                return saved;
                            };
                            if (editing?.isActive && !data.has("isActive"))
                                setConfirmation({
                                    title: `Deactivate ${editing.name}?`,
                                    message:
                                        "They will be signed out and will not be able to access the clinic until reactivated.",
                                    confirmLabel: "Deactivate account",
                                    action: save,
                                });
                            else void save();
                        }}
                    >
                        <TextField
                            label="Staff name"
                            name="name"
                            defaultValue={editing?.name}
                            required
                            maxLength={200}
                        />
                        {!editing && (
                            <TextField
                                label="Staff email"
                                name="email"
                                type="email"
                                required
                            />
                        )}
                        <fieldset>
                            <legend>Roles</legend>
                            {roles.map((role) => (
                                <label className="check" key={role}>
                                    <input
                                        type="checkbox"
                                        name="roles"
                                        value={role}
                                        defaultChecked={
                                            editing
                                                ? editing.roles.includes(role)
                                                : role === "Receptionist"
                                        }
                                    />
                                    {role}
                                </label>
                            ))}
                        </fieldset>
                        {!editing && (
                            <>
                                <TextField
                                    label="Password"
                                    name="password"
                                    type="password"
                                    required
                                    minLength={12}
                                    autoComplete="new-password"
                                    hint={passwordHelp}
                                />
                                <TextField
                                    label="Confirm password"
                                    name="confirm"
                                    type="password"
                                    required
                                    autoComplete="new-password"
                                />
                            </>
                        )}
                        {editing && (
                            <label className="check">
                                <input
                                    type="checkbox"
                                    name="isActive"
                                    defaultChecked={editing.isActive}
                                    disabled={editing.invitationPending}
                                />
                                Active account
                            </label>
                        )}
                        <div className="form-actions admin-form-actions">
                            <Button busy={busy}>
                                {editing ? "Save staff" : "Create staff"}
                            </Button>
                            <Button type="button" variant="secondary" onClick={closeEditor}>
                                Cancel
                            </Button>
                        </div>
                    </form>
                </Card>
            )}
            <Card className="staff-list" padding="none">
                <div className="card-header admin-card-heading">
                    <div>
                        <span className="eyebrow">People and access</span>
                        <h2>Staff</h2>
                    </div>
                    <span>{staff?.totalCount ?? 0} accounts</span>
                </div>
                {staff && (
                    <DataTable
                        caption="Clinic staff"
                        rows={staff.items}
                        getRowKey={(s) => s.id}
                        page={page}
                        totalCount={staff.totalCount}
                        onPageChange={setPage}
                        columns={[
                            {
                                key: "staff",
                                header: "Staff member",
                                render: (s) => (
                                    <div className="staff-identity">
                                        <strong>{s.name}</strong>
                                        <span>{s.email}</span>
                                    </div>
                                ),
                            },
                            {
                                key: "roles",
                                header: "Roles",
                                render: (s) => (
                                    <div className="role-list">
                                        {s.roles.map((role) => (
                                            <span key={role}>{role}</span>
                                        ))}
                                    </div>
                                ),
                            },
                            {
                                key: "status",
                                header: "Status",
                                render: (s) => <StaffState staff={s} />,
                            },
                            {
                                key: "actions",
                                header: "Actions",
                                render: (s) => (
                                    <div className="row-actions">
                                        <Button
                                            variant="secondary"
                                            disabled={busy}
                                            onClick={() => {
                                                setCreating(false);
                                                setEditing(s);
                                            }}
                                        >
                                            Edit
                                        </Button>
                                        {s.invitationPending && (
                                            <>
                                                <Button
                                                    variant="secondary"
                                                    disabled={busy}
                                                    onClick={() =>
                                                        void run(async () => {
                                                            const result = await request<{
                                                                delivered: boolean;
                                                            }>(`/api/admin/staff/${s.id}/resend`, {
                                                                method: "POST",
                                                            });
                                                            if (!result.delivered)
                                                                throw new Error(
                                                                    "Email delivery failed. Check SMTP configuration and retry.",
                                                                );
                                                        }, "Invitation resent; previous links are invalid.")
                                                    }
                                                >
                                                    Resend invitation
                                                </Button>
                                                <Button
                                                    variant="danger"
                                                    disabled={busy}
                                                    onClick={() =>
                                                        setConfirmation({
                                                            title: `Revoke invitation for ${s.name}?`,
                                                            message:
                                                                "The invitation link will stop working. The historical staff record will be preserved.",
                                                            confirmLabel: "Revoke invitation",
                                                            action: () =>
                                                                run(
                                                                    () =>
                                                                        request(`/api/admin/staff/${s.id}/revoke`, {
                                                                            method: "POST",
                                                                        }),
                                                                    "Invitation revoked.",
                                                                ),
                                                        })
                                                    }
                                                >
                                                    Revoke invitation
                                                </Button>
                                            </>
                                        )}
                                    </div>
                                ),
                            },
                        ]}
                    />
                )}
            </Card>
            {clinic && (
                <Card className="clinic-settings" padding="none">
                    <div className="card-header admin-card-heading">
                        <div>
                            <span className="eyebrow">Clinic profile</span>
                            <h2>Clinic settings</h2>
                        </div>
                        <Link to={`/c/${clinic.slug}`}>Open public page</Link>
                    </div>
                    <form
                        className="form-grid"
                        key={`${clinic.name}:${clinic.timeZoneId}`}
                        onSubmit={(e) => {
                            e.preventDefault();
                            const data = Object.fromEntries(new FormData(e.currentTarget));
                            void run(
                                () =>
                                    request("/api/admin/clinic", { method: "PUT", body: data }),
                                "Clinic details saved.",
                            );
                        }}
                    >
                        <TextField
                            label="Clinic name"
                            name="name"
                            defaultValue={clinic.name}
                            required
                            maxLength={200}
                        />
                        <TextField
                            label="Time zone"
                            name="timeZoneId"
                            defaultValue={clinic.timeZoneId}
                            required
                        />
                        <Button busy={busy}>Save clinic</Button>
                    </form>
                </Card>
            )}
            <ConfirmationDialog
                open={confirmation !== null}
                title={confirmation?.title ?? "Confirm change"}
                message={confirmation?.message ?? ""}
                confirmLabel={confirmation?.confirmLabel}
                destructive
                busy={busy}
                onCancel={() => setConfirmation(null)}
                onConfirm={() => {
                    if (confirmation)
                        void confirmation.action().then((saved) => {
                            if (saved) setConfirmation(null);
                        });
                }}
            />
        </div>
    );
}
