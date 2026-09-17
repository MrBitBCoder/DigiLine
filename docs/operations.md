# Clinic operations and migrations

## Configuration and first run

Set `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DigiLine`, a strong secret `JwtSettings__SecretKey`, and the frontend origin in `Cors__AllowedOrigins__0`. Configure the frontend `VITE_API_BASE_URL` for that API. Serve the application over HTTPS. Do not commit credentials. Production registration requires no development seeds: visit `/register`, enter clinic details and owner credentials, and optionally select **I also practice here**. The active owner is signed in immediately.

Clinic time zones govern scheduling and date filters; timestamps are stored in UTC. Existing clinics default to `ClinicTime__DefaultTimeZoneId` (normally Asia/Kolkata). Review the [existing-time audit](local-development.md#clinic-time-and-existing-data-audit) before correcting historical timestamps.

## Staff creation and legacy invitations

Configure `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__Username`, and `Smtp__Password`. Set `PasswordReset__PublicBaseUrl` to the HTTPS frontend origin; both invitation and recovery links use it. The sender enables TLS, so the SMTP endpoint or local test sink must support STARTTLS. Verify delivery before inviting real staff.

In **Clinic administration**, use **Add staff** with name, email, roles, password, and matching confirmation. The backend enforces the existing password policy and stores an Identity password hash. Accounts are active immediately; new staff creation does not need SMTP or send email.

Previously issued invitation links remain supported and expire after 48 hours; each works once. Acceptance sets the password and activates the account. Resend invalidates the old link; revoke prevents acceptance. Delivery failures appear in administration with a retry/resend action. Password-reset links separately expire after 30 minutes.

Accounts belong to one clinic and emails are globally unique. Admin, Doctor, and Receptionist roles can be combined. Navigation displays permitted workspaces. Edit names/roles or deactivate/reactivate accounts from the staff table; historical staff records are retained. Access changes invalidate existing credentials. Staff must sign in again after role changes. The last active administrator cannot be removed or deactivated. A doctor with unfinished sessions cannot lose Doctor access or be deactivated. Administrative changes produce audit records.

## Stop booking and complete a session

The owning doctor can permanently **Stop booking** only after starting a session, while it is Running or Paused. Public and walk-in bookings then fail, while pause/resume and queue work remain available. Automatic booking cutoffs also stop admission; neither the cutoff nor scheduled end completes a session.

Completion requires a Running or Paused session whose bookings have stopped manually or reached their cutoff. Resolve every Waiting, Called, InConsultation, and Late token first. Call Next retains Waiting-before-Late ordering. Skipped tokens can be marked arrived while Running and served as Late. The completion dialog shows the remaining Skipped count; explicit confirmation turns those tokens into NoShow and completes the session atomically. NoShow finalization does not set a completed-consultation timestamp. Completed sessions are read-only. Repeated completion/stop requests are safe; the legacy `/close` endpoint uses the same guards.

## Schema rollout and historical review

Back up the database and test the migration chain on a copy first. Resolve globally normalized staff-email collisions using the [staff-email collision report](local-development.md#staff-password-recovery). Apply all pending migrations with:

```powershell
dotnet ef database update --project DigiLine.Infrastructure --startup-project DigiLine.API
```

Startup also applies pending migrations; ensure the deployment identity and rollout strategy permit this. The latest migration is `20260914180835_AddClinicAdministrationAndSessionCompletion`. It adds staff-role associations/invitations/audit records, account status, booking/completion/finalization timestamps, and query indexes. Existing Doctor/Receptionist assignments are preserved. Existing clinics do not automatically gain an administrator: an authorized operator must provision an appropriate administrator through a reviewed data migration and revoke that staff member's earlier credentials. No operator bootstrap UI is included.

Persisted session `Closed = 5` is retained and displayed as **Completed**. Historical closed sessions and unresolved tokens are not rewritten or assigned guessed outcomes. Run [closed-session-anomalies.sql](sql/closed-session-anomalies.sql) read-only and review each result separately. Token statuses 1/2/3/5/6 mean Waiting/Called/InConsultation/Skipped/Late. Preserve findings with the migration record.

Rollback of the new schema can lose role/invitation/audit and lifecycle data; use a tested backup/restore plan rather than treating a down migration as a lossless rollback. No deployment was performed as part of this work.
