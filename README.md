# DigiLine

DigiLine is a focused full-stack portfolio project for managing a live clinic queue. It provides clinic registration, staff administration, a public patient view, reception, and doctor workspaces. Patient phone verification remains simulated.

## Workflows

- Patients verify a phone number, select or create a profile, join a clinic session, and watch their live queue position.
- Receptionists register walk-ins and can cancel or mark tokens late.
- Doctors create, start, pause, resume, stop bookings, and explicitly complete sessions, and can call, skip, mark late, or complete tokens.
- SignalR updates all open views without a page refresh.
- JWT roles and clinic/doctor ownership protect staff operations.

## Stack and structure

- ASP.NET Core 9 Web API, Entity Framework Core, SQL Server, JWT, and SignalR
- React 19, TypeScript, Vite, and Vitest
- Four small backend layers: Domain, Application, Infrastructure, and API

See [ARCHITECTURE.md](ARCHITECTURE.md) for the dependency rules.

## Run the demo

Requirements: .NET 9 SDK, Node.js, and a local SQL Server reachable through the connection string in `DigiLine.API/appsettings.Development.json`.

```powershell
dotnet user-secrets set "JwtSettings:SecretKey" "DigiLine-Local-Development-Secret-Key-2026" --project DigiLine.API
dotnet ef database update --project DigiLine.Infrastructure --startup-project DigiLine.API
dotnet run --project DigiLine.API --launch-profile https
```

In a second terminal:

```powershell
cd digiline.web
npm install
npm run dev
```

Open `http://localhost:57436/c/dev-clinic` for the public view or `http://localhost:57436/login` for staff.

| Role | Email | Password |
| --- | --- | --- |
| Doctor | `doctor@dev.local` | `DevPassword123!` |
| Receptionist | `reception@dev.local` | `DevPassword123!` |

The public UI displays its development verification code, so no SMS provider is required.

## First run without development seeds

1. Run the API in Production with a SQL Server connection and a secret JWT signing key. Apply the migrations described in [operations](docs/operations.md).
2. Open `/register`. Enter clinic name, unique clinic address (slug), clinic time zone, owner name, globally unique email, and password. Select **I also practice here** for Doctor access.
3. Registration activates the clinic and signs its first administrator in immediately. There is no approval or email-activation step.
4. In **Clinic administration**, configure clinic details and use **Add staff** with name, email, roles, password, and confirmation. New accounts are active immediately and can sign in with the supplied password. Assign Admin, Doctor, and/or Receptionist roles; switch between permitted workspaces in the navigation.
5. As a doctor, create and start a session. As reception, select it and add a walk-in. Call and complete the consultation from the doctor workspace.
6. **Stop booking** permanently prevents new public and walk-in bookings. Finish the queue, then choose **Complete session** and confirm any remaining Skipped tokens as NoShow.

See [operations and migration instructions](docs/operations.md) and [verification results and limitations](docs/verification.md). The development accounts above are optional demo conveniences.

## Verification

Run .NET projects separately because the solution contains a Visual Studio JavaScript SDK project:

```powershell
dotnet test DigiLine.Domain.Tests
dotnet test DigiLine.Application.Tests
dotnet test DigiLine.Infrastructure.Tests
dotnet test DigiLine.API.Tests
```

Infrastructure tests require SQL Server because they verify transactions and concurrent token allocation.

```powershell
cd digiline.web
npm test
npm run lint
npm run build
```

## Remaining operational work

Patient verification is simulated. Deployment, secret management, backups/restore drills, monitoring, privacy/retention policy, and external mail delivery validation remain operational responsibilities. The included Compose configuration runs Development and is not a production deployment template.

The one-minute load smoke check passed. The cancelled longer run encountered errors and does not establish sustained-load readiness. Further load testing was explicitly omitted at the user's request. See the verification report for the exact coverage and unverified areas.

# Clinic time and existing-data audit

Session scheduling uses each clinic's `TimeZoneId` (an IANA time-zone ID). The
API and database store instants in UTC. The default for migrated clinics is
`ClinicTime:DefaultTimeZoneId`, normally `Asia/Kolkata`; set it through deployment
configuration before the first startup after migration. A clinic-specific value
can be set in `Clinics.TimeZoneId` after validating it with
`TimeZoneInfo.FindSystemTimeZoneById`. The doctor session form and all schedule
displays show clinic time, not the viewer's device time. Ambiguous and nonexistent
daylight-saving times are rejected by the form.

Before correcting pre-existing dates, back up the database and review samples
from `Sessions` (`BookingOpenTime`, `StartTime`, `EndTime`, `LastBookingTime`,
`CreatedAt`, `UpdatedAt`) and `QueueTokens` (`CreatedAt`, `CalledAt`,
`StartedConsultationAt`, `CompletedAt`) against source records or logs. SQL
Server `datetime2` does not preserve a time-zone marker, so the application
cannot determine whether an old value was originally UTC or local time. The
migration does **not** shift any saved instant. Correct only records whose
original zone and intended instant can be independently established; keep a
reviewed before/after report and a rollback backup. If provenance is unknown,
leave the value unchanged and flag it for manual review.

Acceptance checks: create a session in clinic time and verify its UTC API value;
view it from a browser in another zone; check the reception day around clinic
midnight and the booking cutoff around session end. Run these checks on a copy
of production data before applying any data correction.
# Staff password recovery

Set `PasswordReset__PublicBaseUrl` to the HTTPS origin serving the web UI.
Configure `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__Username`, and
`Smtp__Password` through deployment environment variables or a secrets manager.
Do not commit SMTP credentials. Development can point these settings at a local
mail-capture service. Password-reset links expire after 30 minutes, can be used
once, and are sent only to the account email. Changing or resetting a password
revokes earlier staff JWTs.

The staff-email migration enforces global uniqueness because login and password
recovery identify accounts by email alone. Before deploying, run this read-only
collision report against a backup or the target database:

```sql
SELECT UPPER(LTRIM(RTRIM(Email))) AS NormalizedEmail, COUNT(*) AS Accounts
FROM Staff
GROUP BY UPPER(LTRIM(RTRIM(Email)))
HAVING COUNT(*) > 1;
```

Resolve every collision with the affected clinic administrators. The migration
intentionally fails if any remain; it will not guess which account to keep.
# Staff queue actions

**Call Next** moves the next eligible Waiting or Late token to Called only when
there is no current patient. It never completes the current token. Use
**Complete & call next** to explicitly complete the current patient and advance
the queue in one action, or **Complete** when no immediate advance is needed.
**Not here—skip** releases that call and leaves the token visible as awaiting
arrival. Staff can select **Patient arrived** on a Skipped token while the
session is Running; this records `LateArrivedAt` in UTC and moves it to Late.
Call Next serves Waiting tokens before Late tokens. Completed tokens remain in
their separate filter. In the staff tables, **Registered at** means token
creation, not physical arrival. Historical late-arrival timestamps remain
blank because they cannot be reconstructed.
