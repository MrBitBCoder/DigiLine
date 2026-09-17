# Verification records
## Portfolio snapshot — 17 September 2026

The status-standardization frontend run passed 165 tests across 21 files, ESLint, and the TypeScript/Vite production build. This is the latest recorded frontend result, not a new backend certification.

The preceding responsive browser run on 16 September passed 10 deterministic tests covering authentication, administration, patient, doctor, Sessions, Reception, and navigation. Nine live cases failed at login/registration before reaching their target screens. API/realtime fetch failures were observed; the live environment was not fully diagnosed. Those cases remain unverified for that run. The historical successful live checks below are retained with their original date and scope.

Patient phone verification remains simulated. Development exposes the challenge code and no SMS provider proves phone ownership. Challenges and access tickets live in memory; restarting the API invalidates them. These limitations also apply to recovery/cancellation identity assurance.

## Historical record — 15 September 2026

## Final mobile and frontend checks

- Playwright/Edge: six browser checks passed for registration, administration, doctor and reception views at 360, 390 and 768 pixels; keyboard registration/role navigation/explicit session completion; recovery after a 22-second offline period; and 200% CSS zoom. The zoom check covers administration, doctor, reception and registration.
- Axe WCAG 2 A/AA and 2.1 AA scans reported no critical or serious findings in the exercised screens. Document-wide horizontal overflow assertions passed. Table column headers remain in the accessibility tree with explicit table roles. Confirmation checks cover initial cancel-button focus, Escape dismissal, focus restoration and explicit confirmation.
- Visual review covered the mobile doctor view and zoomed administration form/table. Navigation remains available; session controls wrap; tables stack when their available width is narrow, including under zoom. Fixed a reception status-badge contrast override and scoped the ambiguous Completed selector to the session status badge.
- Frontend: 55 tests across 12 files passed; ESLint and TypeScript/Vite production build passed.

Evidence logs are in `.codex-build/browser-mobile-final.log`, `frontend-final.log`, `lint-final.log`, and `build-final.log`; browser artifacts are generated under `digiline.web/test-results/`.

## Earlier backend verification retained

Domain: 22 passed. Application: 115 passed. API: 25 passed, including real Production-mode registration and staff invitation acceptance against isolated SQL databases without development seeds. The eight targeted SQL Server production-workflow tests passed, covering invitation delivery failures/retry/expiry/replay, tenant isolation, role/account protections and revocation, competing registrations/last-admin changes, booking versus stop, late arrival versus completion, repeated completion and post-commit publication.

The complete Infrastructure test suite was not verified in this run. Invitation delivery used a captured test sender; this does not verify a deployed SMTP provider or a STARTTLS mail sink end to end. Browser coverage is targeted, not exhaustive: initial and exhausted reconnect retries, missed-event convergence, session-switch races and revoked authentication are not all established by the single offline browser scenario. Manual screen-reader testing and physical-device testing were not performed. Zoom was simulated with CSS zoom, not browser chrome zoom. No full execution-plan certification or deployment was performed.

## Load decision and limits

No further load testing was run during the mobile/documentation finish. The user explicitly cancelled the longer run and requested that no additional load testing be added or restarted.

The earlier one-minute smoke check passed with 100 clinics, 100,000 historical tokens per clinic, and 500 connected clients: 7,800 requests (6,000 reads, 1,800 writes), zero unexpected errors, read p95 48.2 ms and write p95 394.3 ms. It observed 9,000 realtime events. Hardware: Windows, Intel Core i7-13620H, 16 logical CPUs, 16 GiB RAM, with local SQL Server/API/browser workload. Source: `.codex-build/load-smoke.json`.

The attempted 30-minute run encountered errors before cancellation. One recorded progress sample showed 31,242 requests and 2,812 errors with 1,440 seconds remaining. It did not finish and produced no final sustained-load result. Its failures remain unresolved; the successful smoke check does **not** establish sustained-load readiness or satisfy the original 30-minute acceptance target.

## Reproduce targeted frontend checks

```powershell
cd digiline.web
npm test
npm run lint
npm run build
npm run test:browser
```

Browser checks expect the web app at localhost:57436 and API at localhost:5017, with Edge installed. Set `DIGILINE_E2E_EMAIL` and `DIGILINE_E2E_PASSWORD` to a disposable active account with Admin, Doctor and Receptionist roles and a session visible in doctor/reception. The default account is the existing local verification fixture. Registration checks create disposable clinics. Do not point these tests at production patient data. Running browser checks does not run the load script.
