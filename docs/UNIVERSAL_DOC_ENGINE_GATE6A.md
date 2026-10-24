# Gate 6A — runtime and dependency security

Technical verification status is determined by the latest exact-HEAD Gate 6A CI summary attached to PR #19. The delivery recommendation remains Draft / NOT READY while the inherited Moderate build advisory remains unresolved, following the requested delivery condition; a green technical CI is not automatic security acceptance. A build or an older successful run is insufficient for acceptance. No production readiness claim.

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

Docker builds SDK 10.0.402 and runtime 10.0.13, runs as `app`, and removes the incompatible SQLite default. No operator settings are overwritten. CI builds backend and frontend images without deploying/running containers and inspects the backend runtime user. Frontend build uses Node 22 and npm ci. Build-context ignore files exclude host bin/obj/node_modules/dist, local environment files, production/development operator settings and private key/certificate files; this prevents host native dependencies from overwriting the Alpine install and keeps operator secrets out of image COPY operations. Actual production-container startup is outside this gate and remains a pilot prerequisite.

Production startup validates SQL Server connection structure, nonempty database/server, encryption enabled and certificate validation enabled, explicit HTTPS origins and absolute data-protection key path. Development remains compatible. Unexpected request failures log exception type/trace ID, not exception objects or arbitrary messages. Production startup does not apply migrations unless explicitly configured or invoked with a migration maintenance argument. Pending migrations fail closed. Default initialization still performs legacy seed/integrity operations; deployment credentials and seed policy require operator review.

Shared browser launchers derive paths from each host csproj; historical gate workflows/verifier entry points remain for audit. Gate 6A installs both SDKs and runs same-run baseline/feature inventories. Inherited failures require the same test identity and normalized error hash; resolved failures are recorded, not silently discarded.

## Dependencies

Current local npm baseline: 2 High package entries (`follow-redirects`, Vite), 3 Moderate entries in the Tailwind 3 selector chain. Compatible lockfile updates remove the two High entries; current complete audit has 3 Moderate entries. These are not three independent vulnerabilities: Tailwind/postcss-nested inherit GHSA-rj75-hqrm-r3gf from postcss-selector-parser <7.1.6. Vite resolves 8.3.5 and follow-redirects resolves 1.16.1. Production-only npm audit has zero findings. No forced transitive major override or audit suppression. Tailwind remains build-only and requires trusted repository CSS, private builds with finite CI timeout, no customer-supplied CSS and no public dev server. A Tailwind 4 visual migration is deferred pending adequate comparison evidence. This is an explicit unresolved build risk, not a fixed finding.

NuGet scans use the official vulnerability feed. Per-project resolved graphs include dependencies and native/runtime-target assets. npm complete and production-only scans are separate. Machine-readable before/after inventories are generated as sanitized CI metadata. New/remaining High or Critical findings fail verification. Zero package findings alone do not prove complete embedded-native advisory coverage.

## EF/database compatibility review

Review sources:
- https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/breaking-changes
- https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/breaking-changes

EF9 introduces pending-model validation and migration locking; EF10 changes parameterized collection translation, SQLite temporal behavior and optional Azure SQL JSON mappings. Keep UseSqlServer, do not select UseAzureSql or change compatibility levels. Existing SQL migrations are not rewritten. A tooling-generated draft snapshot now records the existing tables, BIN2 identity collations, rowversion and named byte constraint. No new migration is retained or applied: the temporary scaffold was removed. The legacy scaffold had empty Up/Down; its only retained snapshot change is the EF ProductVersion annotation. A no-database test confirms both models have no pending EF changes. The historical unnamed Revision > 0 SQL check remains enforced by the original raw SQL migration and is not managed by the snapshot. SQL rowversion, applocks, trailing-space identity, encryption/key rotation, rollback/reapply and process restart remain mandatory inherited tests. No database evidence is claimed from a successful build.

## Verification and blockers

Local API/test compilation on 10.0.402 succeeds; 564 non-SQL engine checks pass with zero skipped. The SQL subset is deliberately not represented as local acceptance. Frontend TypeScript/Vite build and 32 frontend tests pass (19 Office + 13 inherited draft checks). Full engine, real SQL, Windows/Ubuntu, browsers and independent XLSX verification are pending exact final CI. Gate6A summary must report actual results and missing evidence. Mandatory checks are never treated as passing when unevaluated.

Remaining handoff items: final cross-platform evidence, container build/permissions evidence, production TLS/key storage and SQL least privilege. The new real API process test covers SQL Identity persistence, JWT issuance/revocation/malformed/expired/wrong-audience/wrong-signature rejection, cookie flags and footwear CRUD. Synthetic-host browser tests remain separate from this real Program.cs test. Gate 6B must not start until review and separate owner authorization.

## Operational configuration and rollback

Supply ConnectionStrings__DefaultConnection (SQL Server, Encrypt=True, TrustServerCertificate=False); Jwt__Key (>=32 cryptographically random bytes), issuer/audience; explicit Xnk__AllowedOrigins; DataProtection__KeyPath on restricted persistent storage. Encrypt the data-protection key ring at rest using operator-managed storage/certificate controls; a configured directory alone is not encryption. Supply UniversalDocDrafts stable active key ID and 32-byte AES keys, retain old IDs until all dependent seven-day drafts expire, protect keys separately from SQL backups. If draft connection differs, enforce the same SQL TLS policy. Restrict source/temp/customs paths and mount ownership to the app user. Do not enable sensitive EF logging or HTTP body logging. Termination must allow hosted cleanup; process-local sessions do not survive restart, encrypted drafts do.

Run migrations only as an explicit maintenance action against a backed-up, verified target with separate DDL credentials; runtime users should have only required DML/applock permissions. No configuration in this PR provisions SQL or storage. Test unavailable DB and key storage before pilot.

Rollback: stop traffic, preserve encrypted DB and keys, restore previous accepted application image/configuration. This gate does not authorize schema changes or destructive database rollback. Verify the old application against backups/disposable copies before using it with data written under upgraded dependencies. Never discard ciphertext or old keys to make startup pass.

## Residual build advisory acceptance boundary

GHSA-rj75-hqrm-r3gf is Moderate, selector parser <7.1.6, resolved 6.1.4 through Tailwind 3.4.17 -> postcss-nested 6.2.0 -> postcss-selector-parser. Tailwind also references the parser directly. npm audit reports three affected package entries for this single inherited advisory. Supported remediation advertised by npm is Tailwind 4, a separate CSS/configuration migration; forcing parser major 7 beneath Tailwind 3 is not accepted.

Bounded acceptance criteria: only version-controlled trusted CSS/TSX enters the build; workbook/customer inputs never become compiler sources; no public Vite dev/preview service; production frontend serves only compiled static dist via Nginx; build uses a lockfile and finite isolated CI job timeout; full browser regression must pass; no High/Critical or new advisory is accepted. A public CSS compiler, user-theme compiler or hosted development server invalidates this containment and requires remediation before release. This boundary is an engineering risk analysis, not a claim that the affected parser is patched.

## SQLite provenance

EF Core Sqlite 10.0.13 -> SQLitePCLRaw.bundle_e_sqlite3/core/provider 2.1.13 -> SQLitePCLRaw.lib.e_sqlite3 2.1.13. The API production graph uses SQL Server; SQLite occurs in tests/demo hosts only. Local native sqlite_version() is 3.53.3; Linux x64 library SHA-256 b0f5c48026fedcf9f05cf31c6fadf9e1e001ca59cd85aa68b8b6814e52c18a07. The NuGet package is the maintainer-built library, not an arbitrary DLL substitution. CI records package content SHA-512, native file SHA-256, nuspec license/project metadata and executes the actual provider on both hosts. Current official NuGet feed scan of the restored graph is clean; this does not claim every future native CVE is absent.

## Observed CI evidence and authoritative final record

Initial preflight run 40076661880 correctly failed SDK selection before tests: dotnet selected the workspace-parent .NET 8 SDK despite a net10 project path. Correction: run feature builds from the feature checkout; baseline commands remain in baseline and explicitly select SDK 8.0.426. No acceptance was inferred from that run.

Run https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40076877789 verifies implementation HEAD 1eade5a51a7aceaac8aeb25325d3abb410a50f0c. Observed Ubuntu results: 636/636 engine; full suite 823 pass / 10 inherited failures / 833 total. Baseline: 625/625 engine; 812 pass / 10 inherited failures / 822 full. Identity and failure-hash comparison passes; no resolved failures or skipped mandatory tests. Frontend build/model, all 61 inherited authenticated browser checks, five Office journeys, SQL recovery/process restart and 12 independent downloaded/corpus XLSX reopen checks pass. This is historical incremental evidence, not acceptance of later HEADs.

Final verification adds the snapshot-parity test, JWT negative cases, native file provenance and both container builds. The authoritative final Windows/Ubuntu records are each run's `gate6a-summary.json`, `gate6a-dependencies.json`, inherited browser summaries, SQL/workbook QA JSON and Linux `container.json` artifacts. Each summary binds the actual baseline and final feature SHA and reports the dynamically discovered executed inventory. See [PR #19 checks](https://github.com/Nhutduyasda/ShoeDocX/pull/19/checks) for exact final run URLs; do not substitute historical 636-test evidence for the final inventory. Mandatory evidence must all pass before Ready for review.

No explicit High/Critical production risk acceptance is requested. The one inherited Moderate build advisory has the bounded criteria stated above. SQL production certificate/ACL/key-ring controls, backup restoration, actual target hosting limits and deployment remain Gate 6B operator prerequisites.

## Demonstrated legacy ZIP regression and correction

Run 40076877789 Windows had 636 engine passes but 10 full-suite failures versus baseline 2. The verifier correctly failed. Eight previously passing legacy Excel tests exposed `System.NotSupportedException: Memory stream is not expandable` while OpenXML disposed its writable ZIP. Local targeted execution reproduced the same exception at ZipArchive.WriteFile / MemoryStream.EnsureCapacity. The legacy stamp-removal helper constructed a fixed-capacity byte-array MemoryStream. It now copies the template into an expandable stream, rewinds before opening OpenXML, and disposes the stream on failure. Drawing-removal and export semantics are unchanged. This is an independently implemented minimal runtime compatibility correction, not an integration of the excluded legacy deployment PR. Existing actual Excel export assertions must pass after the fix; the same-run baseline comparison records any inherited Linux failures that resolve, without suppressing them.
