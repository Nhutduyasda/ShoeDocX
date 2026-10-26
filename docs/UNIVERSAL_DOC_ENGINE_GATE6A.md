# Gate 6A — runtime and dependency security

## Visual Acceptance & Advisory Discrepancy Decision

This follow-up starts at `5602aaad360199b09c40c9e58668ffc4899fb648`; main remains `3d07a4f1db7e95cccb1f36e7de23569e5f57c2c5`. The immutable visual baseline is pre-backport `1df7badbf3635fda03687cb8604dde8858cd9bb5`. The feature is the exact PR HEAD checked out by CI and recorded in every pair's featureCommit. No old green run accepts modified verification code. Actual results are in the exact-HEAD **gate6a-visual.json**, **gate6a-backport.json**, **gate6a-dependencies.json** and **gate6a-summary.json** artifacts linked from PR #19. Historical NOT_RUN statements below describe earlier submissions and are superseded only by an actual passing new visual report.

The verifier serves the two immutable production builds on loopback origins against the **same** production Program.cs legacy API and the same real controller/SQL draft host. Legacy uses a fresh, prefix-guarded disposable SQL database, development-only synthetic users and 16 synthetic products created through actual authenticated API requests. Office uses actual UI upload/schema/mapping/manual-validation/review/approval/draft-resume/delete/export interactions and real SQL drafts. No HTTP response mocks or replacement screen components. Production application source/CSS remains unchanged. The only test-host addition is prefix-guarded disposal of the ephemeral legacy database. Both generated authorized XLSX files are independently reopened with openpyxl.

Determinism: same OS per comparison, Node 22/npm supplied by the same setup action, Playwright 1.62.1/its exact installed Chromium, device scale 1, vi-VN locale, Asia/Ho_Chi_Minh timezone, browser Date fixed at 2026-10-24T10:00:00Z, same role and data. Backend authorization/SQL clocks remain real. Font readiness, real network idle, disabled screenshot animations, reduced motion and hidden caret stabilize rendering. Browser version and OS are recorded from execution, never assumed. Raw screenshots and XLSX remain in a private temporary runner directory, removed after evaluation; only sanitized JSON is uploaded. The repository itself is **public**, so CI must not be described as confidential/private hosting.

Comparison policy is written before the first capture: RGBA PNG comparison with pngjs 7.0.0; channel deltas <=16 count as rendering noise; at most 0.001 of pixels may exceed that delta. Visible-element bounds must match within 0.5 CSS px; selected computed styles, visible control/heading text, element order/count and page dimensions match exactly. Structural differences fail independently of pixel score. No threshold widening after results. No document content or controls are masked. The **one dynamic region** is the SQL-generated draft timestamp paragraph (`.office-drafts article.uds-state > p`); SQL transaction clocks and encryption AAD are not modified to manufacture matching dates. Containers' repeated aggregate text is excluded from DOM text comparison; their children, styles and bounds are compared. Print-media main-container computed styles are compared separately on the real Shipment screen; this is not a claim to test every physical printer.

The complete matrix below defines 28 required pairs; each actual status, viewport, commit, browser version, image hashes, difference score and structural differences is emitted per pair in gate6a-visual.json. Missing states fail coverage; no static PASS is inferred from this checklist.

| Real view / identifier | Desktop 1360x1000 | Mobile 390x844 |
|---|---|---|
| 01 Login | Required pair | Required pair |
| 02 Legacy Overview | Required pair | Required pair |
| 03 Warehouse | Required pair | Required pair |
| 04 Shipment | Required pair | Required pair |
| 05 Product management | Required pair | Required pair |
| 06 Product modal / real select popup | Required pair | Required pair |
| 07 Product table / second pagination page | Required pair | Required pair |
| 08 Office template selection | Required pair | Required pair |
| 09 Office mapping configuration | Required pair | Required pair |
| 10 Office manual entry / keyboard focus | Required pair | Required pair |
| 11 Office validation error / approval blocked | Required pair | Required pair |
| 12 Human review and approval | Required pair | Required pair |
| 13 Durable draft list and actual resume | Required pair | Required pair |
| 14 Completed authorized export | Required pair | Required pair |

The earlier six-file byte-identity check remains mandatory on each host. Pixel/DOM results independently validate the real screens. Tablet evidence is required only if inspection reveals a material intermediate breakpoint problem; desktop/mobile parity alone does not establish arbitrary browser/OS equivalence.

### Advisory reachability and evidence

The reviewed 6.1.5 tarball and installed parser have all three linear-time corrections; real class/id/Sass adversarial tests execute the installed code. Tailwind 3.4.17 (`^6.1.2`) and postcss-nested 6.2.0 (`^6.1.1`) resolve the single 6.1.5 lock entry, with no other selector-parser copies. Registry signatures and integrity, upstream source/published hashes and the signed release's fix-parent relationship are checked again on both hosts. This proves the reviewed remediation for this specific advisory, not all possible parser weaknesses.

Runtime reachability review: parser is dev/build-only, absent from production npm dependencies. No source import of Tailwind/PostCSS/parser exists in frontend/src or API runtime code; workbook/source inputs go to bounded application handlers, never to a CSS compiler. The frontend Docker final stage is Nginx copying only dist; Node/compiler/node_modules are build-stage-only. The API remains .NET/SQL Server, with no npm/compiler invocation. Source import review plus production graph and container stage review support absence from browser/backend execution, rather than relying solely on a package-name search of minified output.

Containment actually enforced in this workflow: same-repository PR and exact Gate 6A branch condition (fork PRs do not execute this job), contents:read permissions, exact-SHA checkout, persist-credentials:false, fresh hosted Windows/Ubuntu runners, npm ci lockfile installs, a 60-minute job limit, finite subprocess limits and loopback-only UI/API verification servers. Customer uploads are not CI source inputs. This is **trusted repository source build isolation**, not confidential builds and not proof that every repository writer is benign. Changes to dependency code/CSS/compiler inputs still require human review. Untrusted uploaded CSS, public dev-server/compiler hosting, compromised registries/maintainers or malicious repository writers fall outside the bounded acceptance proposed below.

Official GitHub advisory retrieved 2026-10-26 still reports `<7.1.6` affected / `7.1.6` patched, despite the 2026-10-19 upstream 6.1.5 backport. Fresh full and production npm results remain authoritative in gate6a-dependencies.json; the known prior result is 3 Moderate entries for one GHSA, 0 High/Critical, production 0. NuGet findings must also be freshly reported. A flagged audit is never called clean or suppressed. Remaining uncertainty is the discrepancy in official affected ranges, security-only maintenance of 6.x, and future advisories/supply-chain changes; no risk waiver is automatic.

### OWNER DECISION PACKAGE — neither option preapproved

**Option A (technical recommendation if the full matrix/regressions pass):** accept the independently demonstrated 6.1.5 fix for the bounded build-only dependency, retain Tailwind 3, preserve the official npm warning in every complete audit, and continue advisory monitoring at each dependency review/CI run. Reopen the decision for new advisories, changed resolution/hashes, untrusted compiler inputs, public compiler/dev-server exposure or lost regression evidence. This acceptance would not authorize merge, deployment or production readiness by itself.

**Option B:** require Tailwind 4 and a separately complete configuration/CSS/Ant Design/print/responsive/security regression review before Gate 6A can become MERGE READY. No migration is performed merely to cosmetically remove the warning.

If visual and full exact-HEAD technical checks pass while the advisory still disagrees, verdict is **TECHNICAL ACCEPTANCE PASS — OWNER SECURITY DECISION REQUIRED**, overall PARTIAL / NOT READY; keep Draft until the owner explicitly selects an option. If visual or technical checks fail or a new risk appears, keep PARTIAL / NOT READY with the actual blocker. Source-level proof and screenshot parity are not a production-readiness claim.

Suggested upstream correction (draft only; **not posted**): "Please review GHSA-rj75-hqrm-r3gf's affected range to account for the official security-only 6.1.5 backport. Release d90b6e38a0c79d07b02bf0d602bc242fe94be6b9 follows fix 70b0779c843462197380d25daa02d1528bf7eae5, backported from 62b191792df0a0bc56062e5a875bc74aae2a51cd. The npm 6.1.5 artifact includes all three Set-based paths. Current <7.1.6 classification still flags it. Please validate the backport and amend the reviewed range if appropriate." Supporting release/source/registry URLs are retained in the sections below. No external project communication is authorized.

## 2026-10-26 dependency follow-up (supersedes historical parser findings below)

Starting feature HEAD: `1df7badbf3635fda03687cb8604dde8858cd9bb5`; accepted main remains `3d07a4f1db7e95cccb1f36e7de23569e5f57c2c5`. PR #19 is open/Draft, with no submitted reviews at preflight. Historical .NET/security work and evidence below are preserved.

The earlier statement that Tailwind 4 was the only supported fix is superseded. Upstream released **postcss-selector-parser 6.1.5** on 2026-10-19, backporting all three linear-time Set membership passes from 7.1.6. Release `d90b6e38a0c79d07b02bf0d602bc242fe94be6b9` is the direct child of fix `70b0779c843462197380d25daa02d1528bf7eae5`, itself referencing original `62b191792df0a0bc56062e5a875bc74aae2a51cd`. Published dist/parser.js contains the deduplication, class/id membership and Sass-interpolation fixes. Registry gitHead matches the release; tarball SHA-512 matches the lockfile; npm registry signatures verify against the registry keys. No npm SLSA attestation was provided in retrieved metadata; none is claimed. The 6.x line now receives security fixes only.

Sources:

- https://github.com/postcss/postcss-selector-parser/releases/tag/6.1.5
- https://github.com/postcss/postcss-selector-parser/commit/70b0779c843462197380d25daa02d1528bf7eae5
- https://registry.npmjs.org/postcss-selector-parser/6.1.5
- https://github.com/advisories/GHSA-rj75-hqrm-r3gf
- https://nvd.nist.gov/vuln/detail/CVE-2026-104844 (retrieved page had no usable independent version-range data; no independent clearance inferred).

Exactly one lock entry changes: parser 6.1.4 -> 6.1.5. Tailwind stays 3.4.17 (`^6.1.2`), postcss-nested stays 6.2.0 (`^6.1.1`), and both resolve one deduplicated 6.1.5. No direct dependency, override, application code, CSS, Preflight setting or Ant Design configuration changes. Published parser SHA-256: `b00cfcba4229ca9db927fa401d1d2b40ecaec14401316bfce8283aa8cc2b46fc`. Release source SHA-256: `bf2ebf51b3ca0085f5e7e242df84bc08ee8c17e24b45ec1974d98b44c18b0430`.

Local clean npm ci, build, 32 existing frontend tests and five new installed-artifact regression tests PASS. New checks cover both consumer resolutions, three upstream-style hostile/control complexity ratios (60,000 atoms, fastest of three runs, ratio <2 fixed before execution), 20,000 class nodes and leading-node deduplication. Each cross-platform CI independently verifies the archive, upstream fix relationship/signature, installed bytes, dependency constraints and these execution checks.

**Audit disagreement persists:** complete npm audit before/after: 0 Critical, 0 High, 3 Moderate entries for GHSA-rj75-hqrm-r3gf. Production-only before/after: zero findings. GitHub still lists `<7.1.6` affected and `7.1.6` patched, with its displayed update preceding the backport. Verified patched implementation and current database classification are recorded separately. Audit output is neither suppressed nor called clean; no owner risk acceptance is inferred.

Tailwind 3 is retained for this minimal backport submission under the task's explicit final rule to keep Draft when advisory data disagrees with the verified backport. Tailwind 4 migration is NOT performed or called complete. It remains the alternative if the backport cannot be accepted, and must meet all visual requirements before readiness.

Each CI host also checks out the starting Gate 6A HEAD in dependency-baseline, installs/builds it with the same Node/npm environment, checks that only the intended lock entry changed, and compares every compiled HTML/CSS/JS file by SHA-256 against the feature. Locally all six files are byte-identical. Before/after lock graphs, installed resolution, signature/patch evidence and compiled file hashes are saved in gate6a-backport.json. This demonstrates unchanged artifacts, **not** completion of the requested 14-view desktop/mobile screenshot matrix. That matrix is NOT RUN and remains a readiness blocker; no visual-equivalence claim or widened image threshold. Existing real authenticated desktop/mobile Office journeys, keyboard/focus, 20-row scrolling, SQL restart and XLSX checks remain mandatory in the new exact-HEAD run.

Technical verification PASS is separate from delivery acceptance: **PARTIAL / SECURITY REVIEW REQUIRED — NOT READY** while advisory disagreement or requested visual evidence remains. Keep PR #19 Draft. Exact tested SHA comes from the new summaries' feature_sha and final PR body; an older green run is historical only. No merge, main change, deployment, paid API, shared migration, excluded PR modification or Gate 6B.

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

Swagger regression: the real Program.cs HTTP probe exposed HTTP 500 from Swashbuckle 10 when the existing Studio action parameters decorated IFormFile with FromForm. A standalone real Swagger provider test reproduced the generator exception without SQL. Following the upstream file-upload contract, only the redundant FromForm annotation on the two direct IFormFile parameters is removed; ASP.NET infers BindingSource.FormFile. Form scalar binding, route/case handling, CSRF/admission/size filters, version and ownership checks remain intact. Both real Swagger generation and authenticated upload/browser regressions are mandatory after the correction. Upstream: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/configure-and-customize-swaggergen.md#handle-forms-and-file-uploads.

Final local diagnostics after ZIP/schema corrections: 37 targeted legacy tests pass; 99 Swagger/upload/intake checks pass. A complete local run excluding only the 72 mandatory SQL draft checks and the one real SQL application-process check executes 762 tests: 760 pass and the two unchanged owner-absolute-path fixture failures remain. These local diagnostics do not replace required SQL/Windows/Ubuntu CI. The native provider reports sqlite_version() 3.53.3; exact final CI summaries now extract the executed native version from authenticated test-run TRX output and bind it to the final feature SHA.
