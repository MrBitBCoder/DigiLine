# DigiLine architecture

DigiLine keeps four backend projects so an interviewer can see clear dependency boundaries without production-oriented ceremony.

```text
API ─────► Application ─────► Domain
 │              ▲
 └────► Infrastructure ─────┘
```

- **Domain** owns clinic, session, patient, and queue-token state rules.
- **Application** implements public booking, reception, doctor queue, and session lifecycle workflows.
- **Infrastructure** implements persistence, authentication, patient access, and transactions.
- **API** provides HTTP endpoints, development seed data, and SignalR publishing.
- **Web** contains clinic administration, staff and patient experiences plus authentication and shared UI/realtime code.

## API surface

- `/api/public/*`: clinic details, simulated phone verification, profiles, booking, and token status.
- `/api/reception/*`: today’s sessions, walk-ins, recall, cancel, and late handling.
- `/api/doctor/*`: doctor-scoped session queries, lifecycle transitions, and queue operations.
- `/api/auth/*`: login, registration, invitation acceptance, and password recovery.
- `/api/admin/*`: tenant-scoped clinic details, paginated staff management, invitations, and access changes.
- `/hubs/queue` and `/hubs/public-queue`: staff and patient live updates.

Controllers derive clinic and staff scope from JWT claims. Doctor operations verify ownership before changing data. Application services enforce state transitions; repositories remain persistence-focused.

## Deliberate boundaries

Registration atomically creates an active clinic and its first administrator. Staff-role associations support multiple roles, while globally unique staff emails retain one clinic per account. Invitations use hashed, expiring single-use tokens and SMTP. Account/role changes revoke staff credentials, including realtime delivery eligibility.

Session, booking, and queue mutations acquire the session lock first inside SQL transactions. BookingStoppedAt is independent of lifecycle state. Completion checks blocking counts and atomically finalizes confirmed Skipped tokens to NoShow; historical Closed sessions are preserved. Events are published after commit and transaction disposal.

Staff list/session/queue queries use bounded server-side pages (default 10, maximum 100), tenant filters, deterministic ordering, and independent summary counts. Current-token lookup is scoped to the selected sessions. Clients discard stale responses and coalesce realtime refreshes.

Patient phone verification remains simulated; deployment is outside this change. See [operations](docs/operations.md) and [verification](docs/verification.md).
