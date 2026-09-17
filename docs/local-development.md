# Local development and data review

This guide covers the local demo and the detailed data/SMTP notes. See [operations](operations.md) for production configuration and migrations, and [verification](verification.md) for evidence and limits.

## Start the local demo

Install the .NET 9 SDK, Node.js compatible with the frontend dependencies, SQL Server, and the EF Core 9 CLI tools. From the repository root, configure local secrets, apply migrations, and start the API:

```powershell
dotnet user-secrets set "ConnectionStrings:DigiLine" "<your local SQL Server connection string>" --project DigiLine.API
dotnet user-secrets set "JwtSettings:SecretKey" "<a strong random local signing secret>" --project DigiLine.API
dotnet ef database update --project DigiLine.Infrastructure --startup-project DigiLine.API
dotnet run --project DigiLine.API --launch-profile https
```

Trust the local HTTPS development certificate if required by your machine. In a second terminal:

```powershell
cd digiline.web
npm ci
npm run dev
```

The HTTPS profile serves the API at `https://localhost:7201`; the frontend defaults to that address. Open `http://localhost:57436/register` to create a clinic and administrator without relying on seeded accounts. Select **I also practice here** for Doctor access. Use **Add staff** to create a Receptionist, then create/start a doctor session and add a walk-in from Reception.

Development also supplies these optional demo accounts:

| Role | Email | Password |
| --- | --- | --- |
| Doctor | `doctor@dev.local` | `DevPassword123!` |
| Receptionist | `reception@dev.local` | `DevPassword123!` |

Their public clinic page is `/c/dev-clinic`. These are development-only fixture credentials, not deployed account credentials. Patient verification displays a development code and sends no SMS.
## Clinic time and existing-data audit

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
## Staff password recovery

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
## Staff queue actions

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

