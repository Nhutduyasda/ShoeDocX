# Gate 4B.1 — secure source intake and recognition readiness

Starting main: `89c133429d0c4abecc648cfb0c2ccd0a9a980136` (accepted Gate 4A merge).
Branch: `codex/universal-doc-engine-gate-4b1-source-intake`.

**Admission is not recognition.** No OCR implementation, recognized document content, paid service, external document processing, production database change, migration or deployment is included. The existing six-step Demo Extraction uses its separate server-owned synthetic JSON fixture even when sources are present. It never extracts from an uploaded document.

## Admission and API contract

Authenticated, explicitly mapped Studio membership and exact session owner/tenant are required. Browser headers, form fields, JSON and filenames cannot provide tenant authority or provenance. Source IDs are cryptographically random 32-byte hexadecimal identifiers; SHA-256 is computed over the actual admitted bytes. Original filenames are discarded, never used as paths, retained, returned or logged. Path separators, drives, controls and overly long names are rejected.

All routes are under `/api/universal-doc/sessions/{id}/sources`:

| Method | Contract |
|---|---|
| POST | One multipart `file`, one `expectedVersion`; extra form fields or files rejected. Returns updated safe session view. |
| GET | Safe source list, session ID/version/expiry; never bytes, filename, owner, tenant, internal profile or approval evidence. |
| DELETE `/{sourceId}` | Strict JSON `{ expectedVersion }`; unknown properties rejected; returns updated safe session view. |

All mutations require the existing cookie-bound `X-Studio-CSRF` token. Ownership/expiry is checked before source multipart model binding and again at atomic commit. Source count, aggregate bytes, current version, identity expiry and TTL are rechecked within the per-session lock. An unknown/cross-owner/cross-tenant session returns 404; expired known sessions return 410 and release the stored snapshot, or 404 after cleanup. Stale versions return 409. No endpoint retrieves source bytes or launches recognition.

Multipart admission has a 1 MiB file cap and 1 MiB + 16 KiB HTTP request cap. Framework multipart buffering has an in-memory threshold above the entire accepted request, preventing disk spill within this subset. The bounded stream reader checks declared size before reading, actual bytes during reading, and exact actual/declared equality afterwards. MIME must match the supported filename extension and structural signature; neither field alone is trusted. Parser failures have fixed safe codes without raw contents.

## Supported structural subset

All images have positive dimensions at most 4096 per side and 262,144 total pixels. Header dimensions are verified container metadata, not a claim that compressed pixels have been fully decoded or recognized. No image/PDF renderer or general-purpose parser is introduced. Signature/structural admission is **not proof of harmlessness**, complete entropy validity or readability by an eventual OCR engine.

| Format | Admitted subset | Explicit limits / unsupported |
|---|---|---|
| PNG | Signature; bounded, CRC-checked IHDR/IDAT/IEND; 8-bit RGB/RGBA; no interlace; zlib scanlines have exact bounded length and valid filter bytes. | At most 128 chunks; at most 1 MiB expanded scanlines. No ancillary chunks, profiles, animation, palette, grayscale, 16-bit or trailing payload. |
| JPEG (`.jpg`/`.jpeg`) | SOI, baseline SOF0, bounded quantization/Huffman tables, consistent component/scan headers, a single scan, byte stuffing/restart markers, terminal EOI; optional bounded JFIF. | At most 128 markers. No EXIF/comments/other metadata, progressive/multiscan/arithmetic JPEG or trailing payload. Entropy is not decoded. |
| WebP | Exact RIFF/WEBP length, one VP8 lossy key-frame chunk, bounded frame partition/header and dimensions, exact padding. | No VP8L, VP8X, animation, metadata/extra chunks, scaling or trailing payload. VP8 entropy is not decoded. |
| PDF | **Only a strict PDF 1.4 single blank-page grammar:** catalog, page tree, one page with fixed 612×792 media box, three objects, classic xref/trailer and verified original-byte offsets. | At most 16 KiB; ASCII/LF or CRLF. Ordinary content-bearing/scanned PDFs, text/content streams, resources, fonts, attachments, actions/scripts/links, encryption, object streams, incremental updates and all other grammar return `SOURCE_PDF_SUBSET_UNSUPPORTED`. This deliberately does not claim useful PDF recognition. |

Images have **null page count**, never an invented count of one. The PDF subset returns page count one only after verifying the complete restricted page tree. MIME, actual length, actual hash, server timestamp and safe opaque ID are returned; owner and tenant remain server-only. No preview, HTML, external resource, script or embedded URL is interpreted or fetched. The future decoder must validate compressed content independently in isolation before recognition.

## Memory and lifetime

The existing Gate 4A resource budget is retained: 16 sessions globally, six per tenant, two per owner, four simultaneous Studio requests and 20-minute absolute TTL with periodic cleanup. Each session's **template plus all source bytes share the existing 2 MiB cap**, rather than allocating a second source budget. Each source is at most 1 MiB; at most four sources per session. Maximum retained template/source payload remains 32 MiB globally. PNG inflation is streamed row by row with an exact 1 MiB expansion cap, never allocated from attacker-declared dimensions without bounds. Small metadata and framework/request buffers remain transient, as in Gate 4A; these are payload bounds rather than a process-RSS guarantee.

Immutable server snapshots reside only in the existing single-process store. A source deletion removes its entry; session deletion, expiry, cleanup and disposal drop the store's references to bytes/private metadata. Managed GC reclaims unreachable memory; this is not secure memory erasure, durable storage, multi-process consistency or restart recovery. A trusted in-flight server operation may temporarily hold an old immutable snapshot, but cannot commit/export it after a source mutation, deletion or expiry. No customer bytes belong in logs, test reports, screenshots, static paths or CI artifacts.

## Source mutation and five preserved controls

Upload and removal both atomically clear `Extraction`, `Document` and `Validation`; review/approval evidence disappears with the old document. Mapping confirmation remains bound to the unchanged template, but a new extraction/review/approval cycle is required. Reconfirmation or possessing the earlier session snapshot cannot restore approval. Export still prepares from a snapshot then checks owner/tenant/expiry/version at its final store commit. Tests pause review/approval/export at deterministic boundaries, upload a source, and require 409 from the stale operation. Concurrent upload/delete has exactly one version winner.

1. The existing `UniversalDocWorkflowOrchestrator` remains the sole new runtime Gate 2 projector caller.
2. Authoritative source, mapping, document and approval state stay on the backend; browser state cannot approve or bind provenance.
3. Existing Gate 3A confirmation remains mandatory before review/export.
4. Every session/source access uses the existing authenticated server actor, explicit tenant membership and owner checks. Forged headers and another user's CSRF token do not authorize a session.
5. Production changes stay in the existing Application/Api and frontend feature folders. Program.cs, App.tsx, legacy navigation/auth/controllers/services/migrations and Gate 1–3B implementations remain unchanged. All existing test assertions remain unchanged. The test-only host receives only an optional pre-start callback to register deterministic clock control; CI verifies that exact two-line fixture extension.

## Future trusted provider handoff

`StudioRecognitionInput` is an application-only capability, never a request/response DTO. The orchestrator first authorizes the current actor/session/version; the input checks source ownership and rehashes server-held immutable bytes. `OpenRead` supplies a private read-only memory stream only to trusted server code. Its `ExtractionContext` contains existing `VerifiedExtractionSource` values built from the stored IDs, actual SHA-256, length and verified optional page count. Browser or provider batch data cannot overwrite these values.

A future adapter must implement existing `IExtractionProvider`, emit existing `ExtractionBatch`, and submit it to existing `ExtractionPipeline`. No second extraction engine or provider invocation is added here. Tests feed forged source/hash/page/region/tenant observations through the existing pipeline and require rejection. Provider page, region, grouping, confidence and text remain untrusted observations; syntactic validation cannot establish their truth. A future production adapter still needs sandboxed decoding, resource/security review, cancellation/quotas, human evidence review and an expected-version commit that rechecks authorization/TTL/source identity. This gate deliberately has no real-recognition commit route.

## Dependency security findings

No package versions or existing manifests/lockfiles change; the test-only browser host uses the same SQLite 8.0.11 reference already present in the accepted host. No PDF/OCR parser or image library is added to production. Image fixture bytes were generated from a one-pixel white synthetic image; no customer documents are used.

Fresh local `npm audit --json` found **8 baseline affected package entries: 6 high, 2 moderate**:

| Installed package | Version | Severity | Advisory / dependency path |
|---|---|---|---|
| braces | 3.0.3 | high | [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm), affected ≤3.0.3 |
| chokidar | 3.6.0 | high | via braces; audit range 2.0.0–3.6.0 |
| fast-glob | 3.3.3 | high | via micromatch/braces |
| micromatch | 4.0.8 | high | via braces |
| postcss-nested | 6.2.0 | moderate | via postcss-selector-parser; audit range 2.0.3–6.2.0 |
| postcss-selector-parser | 6.1.4 | moderate | [GHSA-rj75-hqrm-r3gf](https://github.com/advisories/GHSA-rj75-hqrm-r3gf), affected <7.1.6 |
| source-map-js | 1.2.1 | high | [GHSA-68fv-2mgg-jv7q](https://github.com/advisories/GHSA-68fv-2mgg-jv7q), affected ≥1.0.0,<1.2.2 |
| tailwindcss | 3.4.17 | high | propagated build-tool paths above; audit range 0.5.0–3.4.19 |

The .NET CLI package check alone is insufficient while standard local builds report NU1900. `gate4b1_dependencies.py` independently retrieves the **working official NuGet vulnerability index plus base/update feeds**, handles their compressed responses, and matches every restored direct/transitive package version against advisory intervals. Local official-feed update: `2026-10-15T05:26:01.9165571Z`; 187 unique resolved package/version pairs checked across API/tests/fixture hosts. Findings are existing baseline packages:

| Package | Installed | Severity | Affected interval / advisory |
|---|---|---|---|
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.6 | high | ≤2.1.11; [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q); existing SQLite test fixture |
| System.Net.Http | 4.3.0 | high | <4.3.4; [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) |
| System.Text.RegularExpressions | 4.3.0 | high | ≥4.3.0,<4.3.1; [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) |

These are resolved-package findings, not a claim about exploitability of .NET 8's framework implementations. Source admission does not pass documents to SQLite, HTTP clients, CSS/glob tools or legacy libraries. The tiny PDF grammar uses bounded input and a fixed timeout on the framework regex implementation. CI checks baseline/feature package inventories and findings exactly, and uses live npm audit with unchanged lockfiles. No new dependency finding is accepted; advisory-source failure prevents a passing gate. Auditing is not disabled, `audit fix --force` is not run, and no major upgrade or unrelated remediation is attempted. Baseline vulnerability remediation remains separate work.

## Verification evidence

Fresh Windows / SDK 8.0.424 standard VSTest, no skips or aborts:

| Scope | Starting main passing/failing/skipped | Feature passing/failing/skipped |
|---|---|---|
| UniversalDocEngine | 336/0/0 | 394/0/0 |
| Complete suite | 533/0/0 | 591/0/0 |
| New Gate 4B.1 cases | — | 58/0/0 |

Standard local API build: zero errors; two occurrences of the same NU1900 vulnerability-metadata warning (restore/build). Incremental no-restore fixture build: one NU1900, zero errors. An early overlapping build retried a testhost-locked output file (MSB3026); final builds/tests are sequential and do not retain that warning. TypeScript/Vite build and all 13 existing frontend draft cases pass; the pre-existing legacy bundle-size warning remains.

Tests include real authenticated HTTP for four valid formats; empty, extension/content/MIME/signature/CRC/truncation/unsupported-PDF and size attacks; stream declared/actual bounds; decompression/container/trailing payload rejection; aggregate/count budgets; filename traversal; forged identity/hash/tenant; owner/tenant/header/CSRF/anonymous denial; deterministic TTL/cleanup; source/session removal; static-path privacy; upload/delete concurrency; source invalidation and fresh demo cycle; paused upload races against review/approve/export; provider observation forgery; bounded seeded malformed-input mutations; static no-network/filesystem/recognition checks. The complete previous Gate 1–4A inventory remains present and passing locally.

Real browser uses the actual existing AuthController/Identity/JWT cookies and production Studio endpoints with disposable synthetic SQLite users. The unmodified Gate 4A suite has 13 checks and three real workbook downloads. The new suite has 11 checks covering actual file selection/upload, backend metadata rendered in the DOM, no OCR claims, phone layout, explicit unsupported PDF, cross-tenant GET/POST/DELETE denial, upload/removal invalidation and subsequent export denial, and deterministic expiry. Clock control exists **only in the separate loopback test executable** and requires authenticated Admin plus CSRF. No production expiry-control endpoint exists. Browser summaries include no tokens, raw request bodies or source contents; screenshots contain only synthetic UI/metadata, with the opaque source ID hidden.

The narrowly scoped workflow runs Ubuntu 24.04 and Windows Server 2022 / SDK 8.0.426, fresh exact baseline and feature checkouts, standard builds/VSTest, frontend lock install/build/draft tests, working advisory review and both real Chromium suites. Complete inventories, prior test identities, outcome/failure hashes and all 58 new passing cases are checked. Full-suite host-dependent legacy failures may only match that run's exact baseline identities and normalized diagnostic hashes; no fixed failure allowlist or weakened test assertions. Raw TRX/legacy logs stay in temporary runner storage; `contents: read` artifacts contain sanitized counts/identities/hashes, package advisories and synthetic screenshots. The PR must remain open unless both jobs and every mandatory acceptance check pass on the final HEAD.

Initial hosted [run 38701675577](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38701675577) passed both jobs on implementation SHA `b87bd6c0f8deb3659f108148060d8f09dd5794b1` (before the final two JPEG table-reference attack cases):

| Host | Baseline engine P/F/S | Feature engine P/F/S | Baseline full P/F/S | Feature full P/F/S |
|---|---|---|---|---|
| [Ubuntu](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38701675577/job/116311104412) | 336/0/0 | 392/0/0 | 523/10/0 | 579/10/0 |
| [Windows](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38701675577/job/116311104225) | 336/0/0 | 392/0/0 | 531/2/0 | 587/2/0 |

Both hosted jobs recorded zero build warnings/errors for API/tests/fixture, passed baseline/feature frontend builds and 13 draft tests, live dependency comparisons and both real-browser suites (13 + 11 checks). All engine cases passed. Two old developer-workbook fixture-path failures occur on both hosted systems; Ubuntu also retains eight old stream/export failures. Their exact identities and normalized failure-message hashes matched the fresh baseline; there are no new regressions. They do not reproduce locally. Actions v4 runner deprecation messages are distinct from compiler/build warnings.

Final JPEG validation additionally requires every scan/component table reference to resolve to a declared bounded table; two real HTTP malformed-file cases raise the final local inventories to 394 engine / 591 full / 58 new. The final acceptance source of truth is the **exact final PR HEAD's** `summary.json`, `dependencies.json`, per-run TRX-derived inventories and both browser summaries attached to [PR #11 checks](https://github.com/Nhutduyasda/ShoeDocX/pull/11/checks). They bind starting/final SHAs and actual counts, so this initial run cannot authorize merging a later untested commit. The PR's final report records the final run links and SHA after that acceptance completes.

## Exact changed files

Modified production: `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/{StudioRequestFilters,UniversalDocStudioController}.cs`; `Application/{WorkflowContracts,InMemoryUniversalDocSessionStore,UniversalDocWorkflowOrchestrator}.cs`; `frontend/src/features/universal-doc-engine/{studioApi.ts,UniversalDocStudio.tsx,studio.css}`.

Added production: `Application/SourceIntake.cs`, `Application/UniversalDocSourceWorkflow.cs` under the same backend feature.

Verification/documentation: `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4B1Tests.cs`; optional callback in existing `Gate4AHost.cs`; `tests/Gate4B1DemoHost/{Gate4B1DemoHost.csproj,Program.cs}`; `frontend/tests/universalDocIntake.e2e.mjs`; `.github/scripts/{verify_gate4b1,gate4b1_checks,gate4b1_dependencies}.py`; `.github/workflows/gate4b1-verification.yml`; this document. No original old workspace files are modified.

## Remaining limits

This is private, ephemeral, owner-only source readiness. Ordinary PDF documents are unsupported; the blank PDF fixture proves structural admission, not useful PDF parsing. Image entropy is not fully decoded; metadata/signature checks cannot establish harmlessness or recognition quality. No real OCR, PDF text extraction, rotation/layout understanding, customer-document acceptance, durable storage, replica synchronization or production rollout has passed. Real recognition requires a separately evaluated adapter and decoder, bounded isolated execution and a fresh human evidence-review workflow. No paid calls or deployment occur here.
