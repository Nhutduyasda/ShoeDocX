# Production readiness audit — main e939086

No business workflow, calculation or frontend redesign is part of this change.

| Area | Verified finding | Required action |
| --- | --- | --- |
| Backend | ASP.NET Core 8 | Preserve provider/runtime; monitor .NET support lifecycle |
| Database | EF SQL Server; historical SQLite migrations excluded | Keep SQL Server and current SQL migrations |
| Migration | Startup `MigrateAsync`, CLI `--migrate-only` | Single migration writer; production SQL integration test still required |
| Transactions | Many explicit transactions plus `EnableRetryOnFailure` | Disable automatic operation retries; don't replay file/business side effects |
| Docker | Backend image contains stale SQLite connection default | Remove it; runtime SQL configuration only |
| Frontend | React 19 / Vite 8; default `/api`; UI uses internal state tabs | Root Dockerfile copies SPA to wwwroot; fallback/API 404 |
| Auth | HttpOnly/Secure/Strict production cookie, revocation/security-stamp checks | Same-origin; stable runtime JWT key; bootstrap disabled after setup |
| CORS | Explicit localhost allowlist for development | No wildcard credentials or production cross-origin changes needed |
| Customs | Original files written in data/customs, legacy Uploads/Customs | R2 storage and reference metadata; retain local legacy migration tool |
| Templates | Uploaded xlsx written into Templates; same files used by exports | R2 upload/read; read-only bundled fallback only when no configured template |
| Export | Excel/ZIP/settlement files use memory streams | Preserve calculations; use expandable stream for editing OpenXML |
| OCR | OpenAI key required by implementation, no README demo fallback | Optional funded key; no key in image/SPA |
| Source data | Historical SQLite backup is tracked upstream | Exclude from Docker; do not delete or treat as imported production data |
| Tests | One fixture test hardcodes author's Windows path | Resolve from published test fixture directory for Linux CI |

Unfinished blockers: Container Apps cost-risk approval, private registry/pull credential, R2 account/bucket/secure credentials, managed identity SQL permissions, real SQL migration and full production verification. SQL provisioning alone is not application readiness.

## Updated hosting decision (2026-09-30)

The owner selected Azure App Service Free F1 with Code/.NET 8 deployment instead of Container Apps. The same-origin publish artifact and R2 abstraction remain applicable. CI deploy now uses webapps-deploy with OIDC and refuses non-F1 plans. Runtime secrets, SQL managed identity permissions, R2 configuration, bootstrap and live acceptance remain blockers; no production success is claimed.
