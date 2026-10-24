# Gate 6A — runtime and dependency security

Status: **PARTIAL / NOT READY**. Verification in progress; no production readiness claim.

## Scope and provenance

Accepted baseline: `3d07a4f1db7e95cccb1f36e7de23569e5f57c2c5` (merged Gate 5D PR #18).
Branch: `codex/universal-doc-engine-gate-6a-runtime-security`.
The exact feature SHA is recorded by `VERIFIED_HEAD` in each CI summary; do not confuse a documentation commit with a tested binary.
PRs #4, #13 and #14 are untouched. No cloud, deployment, customer data, production migration or key rotation.

Official Microsoft release metadata fetched 2026-10-24:
https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
SDK **10.0.402**, runtime/ASP.NET Core **10.0.13**. Local Linux SDK archive matched publisher SHA-512 `1904a98dda2db34ba78ba3c4fb1b224455329af91c341d83547843c1839e112b04ae699ee92fa2449a8f62249196a938723078c0c60b9f65c88539d5687729c2` before extraction.

## Implementation

All seven csproj targets move to net10.0; EF/Identity/JwtBearer/OpenApi packages align on 10.0.13. Swashbuckle 10.3.1 uses OpenAPI's document-bound security reference API. Pkcs explicitly pins 10.0.13 because SqlClient 6.1.7 otherwise resolves affected 9.0.11. Package restore and audit must independently verify these graph changes.

Docker builds SDK 10.0.402 and runtime 10.0.13, runs as `app`, and removes the incompatible SQLite default. No operator settings are overwritten. Container execution remains unverified until a real build/run is recorded.

Production startup validates SQL Server connection structure, nonempty database/server, encryption enabled and certificate validation enabled, explicit HTTPS origins and absolute data-protection key path. Development remains compatible. Unexpected request failures log exception type/trace ID, not exception objects or arbitrary messages. Production startup does not apply migrations unless explicitly configured or invoked with a migration maintenance argument. Pending migrations fail closed. Default initialization still performs legacy seed/integrity operations; deployment credentials and seed policy require operator review.

Shared browser launchers derive paths from each host csproj; historical gate workflows/verifier entry points remain for audit. Gate 6A installs both SDKs and runs same-run baseline/feature inventories. Inherited failures require the same test identity and normalized error hash; resolved failures are recorded, not silently discarded.

## Dependencies

Current local npm baseline: 2 High package entries (`follow-redirects`, Vite), 3 Moderate entries in the Tailwind 3 selector chain. Compatible lockfile updates remove the two High entries; current complete audit has 3 Moderate entries. These are not three independent vulnerabilities: Tailwind/postcss-nested inherit GHSA-rj75-hqrm-r3gf from postcss-selector-parser <7.1.6. No forced transitive major override or audit suppression. Tailwind remains build-only and requires trusted repository CSS, private builds with finite CI timeout, no customer-supplied CSS and no public dev server. A Tailwind 4 visual migration is deferred pending adequate comparison evidence. This is an explicit unresolved build risk, not a fixed finding.

NuGet scans use the official vulnerability feed. Per-project resolved graphs include dependencies and native/runtime-target assets. npm complete and production-only scans are separate. Machine-readable before/after inventories are generated as sanitized CI metadata. New/remaining High or Critical findings fail verification. Zero package findings alone do not prove complete embedded-native advisory coverage.

## EF/database compatibility review

Review sources:
- https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/breaking-changes
- https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/breaking-changes

EF9 introduces pending-model validation and migration locking; EF10 changes parameterized collection translation, SQLite temporal behavior and optional Azure SQL JSON mappings. Keep UseSqlServer, do not select UseAzureSql or change compatibility levels. Existing SQL migrations are not rewritten. Existing Gate5C raw-SQL migration and its missing snapshot require verification before future migrations; do not invent snapshot parity. SQL rowversion, applocks, trailing-space identity, encryption/key rotation, rollback/reapply and process restart remain mandatory inherited tests. No database evidence is claimed from a successful build.

## Verification and blockers

Local API/test compilation on 10.0.402 succeeds. Full engine, real SQL, Windows/Ubuntu, browsers and independent XLSX verification are pending CI. Gate6A summary must report actual results and missing evidence. Mandatory checks are never treated as passing when unevaluated.

Remaining handoff items: current EF snapshots, real production-host auth/CRUD, container startup/permissions, production TLS/key storage and SQL least privilege. Gate 6B must not start until review and separate owner authorization.

## Operational configuration and rollback

Supply ConnectionStrings__DefaultConnection (SQL Server, Encrypt=True, TrustServerCertificate=False); Jwt__Key (>=32 cryptographically random bytes), issuer/audience; explicit Xnk__AllowedOrigins; DataProtection__KeyPath on restricted persistent storage. Encrypt the data-protection key ring at rest using operator-managed storage/certificate controls; a configured directory alone is not encryption. Supply UniversalDocDrafts stable active key ID and 32-byte AES keys, retain old IDs until all dependent seven-day drafts expire, protect keys separately from SQL backups. If draft connection differs, enforce the same SQL TLS policy. Restrict source/temp/customs paths and mount ownership to the app user. Do not enable sensitive EF logging or HTTP body logging. Termination must allow hosted cleanup; process-local sessions do not survive restart, encrypted drafts do.

Run migrations only as an explicit maintenance action against a backed-up, verified target with separate DDL credentials; runtime users should have only required DML/applock permissions. No configuration in this PR provisions SQL or storage. Test unavailable DB and key storage before pilot.

Rollback: stop traffic, preserve encrypted DB and keys, restore previous accepted application image/configuration. This gate does not authorize schema changes or destructive database rollback. Verify the old application against backups/disposable copies before using it with data written under upgraded dependencies. Never discard ciphertext or old keys to make startup pass.
