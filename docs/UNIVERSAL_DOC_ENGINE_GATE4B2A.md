# Gate 4B.2A — dependency security and document compatibility

Starting verified `origin/main`: `21e48a5e2ddb426ccf57290c2e8407d8d00f774e` (accepted PR #11). Fetched before work on 2026-10-15. Branch: `codex/universal-doc-engine-gate-4b2a-security-readiness`. No earlier Gate 4B.2A PR or remote branch was found. The original parent workspace is preserved; work is confined to the clean `ShoeDocX-Gate3A` clone.

**Production readiness is not established.** This gate patches two test graph packages and one frontend build package, measures existing admission, and specifies a future decoding boundary. SQLite and seven npm affected package entries remain explicitly unresolved. Admission is not decoding or recognition. There is no production decoder, OCR integration, migration, deployment, customer data or business-logic change.

## Evidence and full inventories

Sanitized machine-readable records are in [gate4b2a](gate4b2a/). These contain the full graph rather than only the three reported packages:

* [Baseline inventory](gate4b2a/baseline-inventory.json) and [feature inventory](gate4b2a/feature-inventory.json): every restored package per project/target, direct requirements, dependency edges, selected compile/runtime/native assets; every npm lockfile package, development flag, requirements and integrity.
* [Reviewed exact delta](gate4b2a/dependency-delta.json): before/after versions, including required transitive patch updates. CI rejects any unreviewed graph change.
* [Baseline](gate4b2a/nuget-baseline.json) / [feature NuGet](gate4b2a/nuget-feature.json): working official vulnerability index, base/update timestamps, complete resolved package/version inventory and matched advisory intervals. These checks supplement, never suppress, NU1900.
* [Baseline](gate4b2a/npm-baseline.json) / [feature npm](gate4b2a/npm-feature.json), and [production-only npm](gate4b2a/npm-production.json): registry advisory results, propagated affected packages, exact nodes and counts.
* [Baseline](gate4b2a/baseline-tests.json) / [feature tests](gate4b2a/feature-tests.json): actual full-suite identities, outcomes and normalized failure hashes; no raw legacy workbook output.

Local evidence records this audit run. Hosted artifacts independently bind final feature SHA, exact baseline SHA and OS; a local record cannot authorize merging an untested HEAD.

## .NET findings and exposure

All four projects target .NET 8. The production API uses SQL Server. **None of the three flagged package IDs resolves in the API graph.** A test project reference to the API does not ship test dependencies into an API deployment. The two browser hosts are disposable test executables, not production hosts.

| Finding | Projects / dependency path | Runtime exposure | Decision and risk |
|---|---|---|---|
| `SQLitePCLRaw.lib.e_sqlite3` 2.1.6, High | Test suite, Gate4ADemoHost, Gate4B1DemoHost: direct `Microsoft.EntityFrameworkCore.Sqlite` 8.0.11 → `SQLitePCLRaw.bundle_e_sqlite3` 2.1.6 → native library 2.1.6 | Native SQLite is actually used by synthetic test databases on Windows/Linux. Not in API graph; source documents are not executed as SQL. Test-only does not mean harmless if untrusted SQL/database input reaches a test host. | **Outstanding.** [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q), CVE-2025-6965, affects ≤2.1.11. Advisory lists no patched package; upstream SQLite fix is ≥3.50.2. Merely moving to 2.1.11 does not remediate it. Replacing the native bundle/provider requires separate compatibility/security verification; no major replacement is forced here. |
| `System.Net.Http` 4.3.0, High | Test suite only: direct `xunit` 2.5.3 → `xunit.assert` or `xunit.core` → `xunit.extensibility.core` / `xunit.extensibility.execution` → `NETStandard.Library` 1.6.1 → package | .NET 8 loads `HttpClient` from `shared/Microsoft.NETCore.App/8.*`, verified by assembly version/location tests. Old resolved package presence is not evidence that its redirect implementation executes. No API package exposure. | **Fixed resolved graph:** test-only private reference 4.3.4, minimum patched version in [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57), CVE-2018-8292 (redirect credential disclosure). No xUnit/framework upgrade. |
| `System.Text.RegularExpressions` 4.3.0, High | Same xUnit paths → `NETStandard.Library` → package; also via `System.Xml.ReaderWriter`, directly or through `System.Xml.XDocument` | .NET 8 loads `Regex` from shared framework, verified by assembly version/location tests. Existing PDF admission regex remains bounded with its existing timeout. | **Fixed resolved graph:** test-only private reference 4.3.1, minimum patch in [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj), CVE-2019-0820 (regex DoS). No production package added. |

The HTTP patch necessarily moves `Microsoft.NETCore.Platforms` 1.1.0→1.1.1, `Microsoft.NETCore.Targets` 1.1.0→1.1.3, `System.Runtime` 4.3.0→4.3.1, `runtime.native.System.Security.Cryptography.OpenSsl` and its ten platform-specific packages 4.3.0→4.3.2. Every changed version is enumerated in the delta. These legacy platform assets require regression checks even though modern .NET framework resolution wins for HTTP/regex. Existing test assertions and framework/test-runner versions are unchanged. Full suite and runtime assembly checks pass locally; hosted verification is mandatory on both OSes.

The audit classifies this application's graph and observed local framework loading, not arbitrary published artifacts or every deployed runtime installation. Runtime patch servicing remains an operational requirement. No customer deployment was inspected or changed.

## Frontend findings and exposure

All eight baseline affected entries are under development/build roots; `tailwindcss` is a devDependency. None is a production runtime root. Actual browser bundle/build and authenticated UI tests remain required; lockfile `dev` labels alone cannot prove reachability. Current source intake does not pass documents, glob patterns, CSS selectors or source maps into this tooling. Build systems still process repository content and must treat untrusted contributions as untrusted.

| Package (baseline) | Root path | Advisory / patch / disposition |
|---|---|---|
| `braces` 3.0.3 | Tailwind 3.4.17 → chokidar 3.6.0 → braces; Tailwind → micromatch 4.0.8 → braces; Tailwind → fast-glob 3.3.3 → micromatch → braces | High [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm), recursive stack exhaustion, ≤3.0.3. Latest registry release remains 3.0.3; no official patched release. **Outstanding.** |
| `chokidar` 3.6.0 | Tailwind → chokidar | High propagated from braces, not an additional independent advisory. **Outstanding.** A watcher major replacement under Tailwind 3 is not a compatible patch. |
| `fast-glob` 3.3.3 | Tailwind → fast-glob → micromatch | High propagated from braces. **Outstanding.** |
| `micromatch` 4.0.8 | Tailwind → micromatch; Tailwind → fast-glob → micromatch | High propagated from braces. **Outstanding.** |
| `postcss-nested` 6.2.0 | Tailwind → postcss-nested → postcss-selector-parser 6.1.4 | Moderate propagated selector-parser advisory. **Outstanding.** |
| `postcss-selector-parser` 6.1.4 | Tailwind → selector-parser; Tailwind → postcss-nested → selector-parser | Moderate [GHSA-rj75-hqrm-r3gf](https://github.com/advisories/GHSA-rj75-hqrm-r3gf), flat-selector quadratic CPU exhaustion; patched ≥7.1.6. Existing consumers require ^6.1.1/^6.1.2. A forced transitive major override is not proven compatible by a build alone; **outstanding**, needs a maintained compatible consumer/backport or dedicated selector differential coverage. |
| `source-map-js` 1.2.1 | direct dev `postcss` 8.5.28 → source-map-js ^1.2.1; also other build consumers of PostCSS | High [GHSA-68fv-2mgg-jv7q](https://github.com/advisories/GHSA-68fv-2mgg-jv7q), indexed-source-map offset DoS. **Fixed** to 1.2.2 within existing range, only lockfile node/version/URL/integrity changed. Clean install/build/draft and browser regression evidence required. |
| `tailwindcss` 3.4.17 | direct devDependency | High propagated from affected glob/watcher tree; moderate selector paths also present. Latest queried 3.x is 3.4.19, still in audit range; no claimed complete compatible fix. Registry suggests Tailwind 4, which is deliberately not forced. **Outstanding.** |

Counts: baseline **8 (6 High, 2 Moderate)** → feature **7 (5 High, 2 Moderate)**. Production-only npm audit: **0**. No React, Vite, Ant Design or Tailwind major upgrades, forced fixes, advisory exclusions or warning suppressions. These are affected package entries, not eight independent CVEs. The application must not accept customer CSS/glob/source-map input into its build process.

## Measured real-document compatibility

Fixtures are synthetic, tiny, embedded test resources with a generation script; no customer filenames or documents. PDF fixtures have valid object offsets/xref, actual content/font resources, two pages, or an embedded JPEG as appropriate. Image fixtures are generated by Pillow solely for tests; Pillow is not an app dependency. All cases call the actual unchanged `SourceIntake.Admit`; existing authenticated HTTP tests continue to cover the outer admission route.

| Case | Current measured outcome and exact reason | Needed future capability / risk | Expected cost / isolation |
|---|---|---|---|
| Selectable-text PDF | Rejected: `SOURCE_PDF_SUBSET_UNSUPPORTED`; resources/content/font objects exceed strict three-object blank-page grammar | PDF object graph, streams, fonts and text transforms; malformed objects/fonts, decompression and external actions | Cost scales with objects/fonts/pages and expanded streams; isolated worker required |
| Two-page PDF | Rejected: same code; page tree is not the single fixed blank-page grammar | Bounded page tree traversal and render scheduling; cyclic/huge trees | Per-page work, cumulative page/pixel cap; isolated worker |
| Scanned PDF | Rejected: same code; XObject image and content stream unsupported | PDF rendering plus image decode, later OCR; nested image/stream bombs and native corruption | Compressed bytes do not bound decoded pixels; isolated worker |
| Baseline JPEG, no metadata | Admitted, hash/bytes preserved; **entropy not decoded** | Full entropy decode before OCR; malformed coefficients can still fail later | Decode proportional to pixels, cap first and after decode; worker |
| JPEG with EXIF | Rejected: `SOURCE_JPEG_SUBSET_UNSUPPORTED`; APP1 metadata outside JFIF allowance | Bounded metadata parser, EXIF orientation; offset cycles/large profiles/privacy | Bounded metadata plus pixel buffer; worker |
| Progressive JPEG | Rejected: `SOURCE_JPEG_SUBSET_UNSUPPORTED`; SOF2/multiple scan grammar unsupported | Progressive coefficient/scan decoding; CPU/memory abuse | Multiple passes/coefficient buffers; worker |
| RGB PNG without ancillary chunks | Admitted; bounded scanline inflate/filter checks, not general recognition | Pixel reconstruction and rendering | ≤262,144 pixels currently, expanded scanlines ≤1 MiB; future decoder still isolated |
| Palette PNG with transparency | Rejected: `SOURCE_MALFORMED_OR_UNSUPPORTED`; IHDR color type 3 fails RGB/RGBA requirement before PLTE/tRNS | Palette/index and alpha decode; chunk bounds/CRC/profile abuse | Palette plus bounded pixel buffer; worker |
| Simple lossy VP8 WebP | Admitted; **entropy not decoded** | Full VP8 keyframe decode | Bounded pixels; native codec worker |
| Lossless VP8L WebP | Rejected: `SOURCE_WEBP_SUBSET_UNSUPPORTED`; VP8L chunk unsupported | VP8L transform/Huffman decode; corruption/bombs | Bounded pixels and transform buffers; worker |
| Extended alpha VP8X WebP | Rejected: same code; only one VP8 chunk permitted | VP8X/ALPH, bounded metadata and alpha | Additional alpha buffer; worker |
| Animated WebP | Rejected: same code; VP8X/ANIM/ANMF unsupported | Prefer explicit rejection in 4B.2B; do not silently pick a frame | Frame-count/time/memory amplification; worker if ever enabled |
| >1 MiB/source | Rejected: `SOURCE_SIZE_LIMIT` before container parsing | Separate approved admission budget change, streaming quota design; do not increase implicitly | Current four-source/2 MiB shared session cap remains; future worker cannot replace admission quotas |

Admission success never means a file is safe or recognized. The tests assert server hash and exact original bytes for accepted examples, with `AdmittedNotRecognized` and null image page count. Representative rejection does not prove every file in a format category will yield the same error: ordering, malformed structure, dimensions, bytes and metadata can trigger earlier bounds. The over-cap test uses synthetic padding to exercise pre-decode size enforcement, not a claim of valid large-JPEG rendering. No cap or format rule is relaxed.

## Proposed Gate 4B.2B boundary (design only)

1. **Untrusted admission:** existing authenticated owner/tenant, cookie CSRF, TTL, version and source quotas remain authoritative. A separate reviewed change may allow additional containers into a quarantined state. Extension/MIME/signature alone never establishes decode success. Preserve original immutable bytes and server SHA-256; discard original filename. Do not expose raw files through a static path.
2. **Safe decode/render:** backend schedules an opaque job from the authorized immutable snapshot, expected version and source hash. A dedicated process receives bounded source bytes through a pipe; it receives no session cookies, tenant credentials, DB connection, environment secrets, URL, filesystem path or provider keys. Decode output is bounded pixel/text observations with page dimensions, not trusted executable content. Rehash input and associate every output with source hash and decoder build/version.
3. **Recognition:** a separate replaceable `IExtractionProvider` adapter consumes validated raster/text outputs. No paid calls in this gate. OCR remains a later, explicitly authorized feature, with cancellation and quotas independent of decoding. Embedded text is untrusted observation, not automatically correct OCR evidence.
4. **Provider-neutral observations:** existing `ExtractionBatch` / `ExtractionPipeline` validate source IDs/hash/page/region/confidence. `StudioRecognitionInput` and backend-owned `VerifiedExtractionSource` remain the authority; worker/provider output cannot overwrite provenance, owner, tenant or approval.
5. **Gate 3B normalization:** use the existing pipeline and normalizers, with bounded text length/observation count and explicit failures. No second normalization engine.
6. **Gate 1 review:** decoded/recognized data remains unapproved; display evidence safely as inert text/raster, require existing human-confirmed mapping and review/approval.
7. **Gate 2 export:** existing orchestrator remains sole projector caller. Before committing a recognition result or exporting, reauthorize owner/tenant, expiry, expected version and unchanged source set/hash under the store's atomic update. Source mutation/deletion invalidates extraction/review/approval; cancellation or stale worker output cannot restore it.

### Decoder choices

* **PDFium** is the preferred rendering candidate, subject to a pinned reproducible build and wrapper evaluation on Windows/Linux x64. [Upstream build documentation](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/README.md) supports those systems and allows disabling JavaScript and XFA (both enabled by default). Build both off. PDFium is a native parser, not an OS sandbox; a .NET wrapper provides no containment. Require an SBOM, native CVE tracking, supported artifact provenance and update procedure before adoption.
* **PdfPig** ([upstream](https://github.com/UglyToad/PdfPig)) is a managed alternative for selectable-text/layout extraction, not a substitute for raster rendering of scans. It still needs object/stream/time budgets and isolation for hostile PDFs. Do not equate managed memory safety with resource safety or accurate extraction.
* **MuPDF** ([upstream](https://github.com/ArtifexSoftware/mupdf)) offers rendering and native codecs but requires an AGPL/commercial licensing decision and native patch management. Do not add it without resolving distribution obligations. Poppler/Ghostscript similarly add native attack surface, packaging and licensing review; they are not drop-in in-process security boundaries.
* **Images:** evaluate a pinned managed image library covering JPEG/PNG/WebP, or small pinned native libjpeg-turbo/libpng/libwebp components in the same worker. [libwebp's decode API](https://developers.google.com/speed/webp/docs/api) exposes decode configuration, but limits and process isolation remain ours. [ImageSharp licensing](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE) must be checked for this application's use; it is not assumed to be unrestricted for every deployment. Avoid `System.Drawing.Common` as a Windows/Linux decoding strategy. No candidate library is selected as production-safe based solely on feature coverage.

### Proposed measurable resource and OS controls

Initial 4B.2B trial budget: retain current 1 MiB/source and shared 2 MiB session limits; max 10 PDF pages, 4096 pixels/side, 4 million decoded pixels/page, 20 million/job, 150 DPI render ceiling, 32 MiB cumulative expanded PDF streams, 1 MiB text output, and 10,000 observations. Detect/check limits before allocation where possible and after actual decode. A 4-million-pixel RGBA page is about 16 MB; render one page at a time and release buffers. Reject animation/encryption/attachments, malformed streams and excessive recursion/object counts; trial object cap 10,000 and nesting 64. These numbers are design budgets needing adversarial measurement, not implemented guarantees.

One decode worker per active job, initially two globally and one per tenant, bounded queue of eight jobs. Hard memory cap 256 MiB/job, 10 s wall/5 s CPU per page, 30 s wall/15 s CPU per job, bounded IPC and stdout, no crash dump/customer byte logging. Propagate request cancellation and source/session expiry to kill the **whole process tree**, with a short cleanup deadline; a cancellation token alone cannot stop a stuck native parser. Reject partial/truncated output. Measure peak RSS and CPU on both platforms before accepting budgets.

Linux: non-root worker, read-only minimal filesystem, isolated user/mount/network namespaces, no egress, seccomp policy, cgroup CPU/memory/PID limits, no inherited handles and a bounded private tmpfs if unavoidable. Windows: restricted token/AppContainer with no network capability, Job Object hard memory/CPU/process limits and kill-on-close, restrictive working-directory ACLs and inherited-handle allowlist. **A Job Object alone does not restrict filesystem/network access.** If these controls cannot be established, fail closed; don't fall back to the web process. Prefer pipe-only input/output and no writable disk. Reject external file/network references, embedded JavaScript, launch/actions, XFA, remote fonts/resources and attachments independently of OS egress denial. Decoder defaults are insufficient.

No actual worker, decoder, image preview or widened admission is implemented here. Gate 4B.2B must demonstrate escape/egress denial, bounded malformed document handling, cancellation/crash recovery, runtime patching and stale-result races. Gate 4B.3 must evaluate real recognition adapters, layout/rotation/language accuracy, neutral observation provenance and human evidence review using synthetic data before any customer pilot.

## Verification and delivery status

Fresh Windows local SDK **8.0.424**, standard restore/build/VSTest:

| Check | Baseline | Feature |
|---|---|---|
| Full backend inventory | 591 passed / 0 failed / 0 skipped | 606 passed / 0 failed / 0 skipped |
| Engine cases present in full suite | 394 passed | 409 passed |
| Frontend clean lock install / TypeScript + Vite build | PASS | PASS |
| Frontend draft tests | 13 passed | 13 passed |
| Authenticated Studio / intake browser checks | Prior accepted reference only | 13 + 11 passed, zero runtime errors |
| Official NuGet matched findings | 3 High | 1 High, test SQLite outstanding |
| npm matched entries | 6 High / 2 Moderate | 5 High / 2 Moderate |

All 591 baseline identities/outcomes match feature; all 15 additions pass. They cover 12 synthetic compatibility fixtures, >1 MiB rejection, and two shared-framework loading checks. Existing Gate 4A/4B.1 tests and assertions are byte-identical. Production source, export chokepoint, sessions, mapping, authorization, invalidation, CSRF, TTL and concurrency code are unchanged. No frontend source or legacy footwear business logic changes.

NU1900 occurred during local standard restore/build against the service index; it is preserved, not filtered or disabled. Independent live official NuGet base/update feed audit succeeded for both inventories. The first sandboxed VSTest attempt aborted before testhost connection; a fresh permitted run completed all cases. The existing legacy Vite chunk-size warning persists. Local Chromium startup initially failed under the system Node/browser combination; the bundled Node runtime and installed Chromium headless shell completed both unchanged authenticated suites. Hosted exact-head outcomes are recorded in the delivery report and sanitized artifacts; only completed runs count.

The new scoped workflow uses **Ubuntu 24.04 / Windows Server 2022**, SDK 8.0.426, exact baseline and PR-head checkouts, standard restore/build of all four projects, targeted and full VSTest inventories, clean frontend builds/tests, live NuGet/npm audits, exact reviewed dependency deltas and unchanged authenticated Studio/intake browser suites (13 + 11 checks). The prior TRX privacy/sanitization code is reused. It rejects skipped/aborted/incomplete tests, missing baseline identities, new or changed failures, failed security cases, unknown dependency deltas, failed required patches, new advisories and worsening severity. It never interprets advisory-source failure as clean. Inherited findings remain visible and do not authorize a production-readiness claim.

The PR must stay open until both hosted jobs pass on its exact final HEAD, both browser suites pass, and all implemented changes are verified. No merge on pending/failed checks; no force merge. A blocked remediation or missing mandatory verification means **PARTIAL**, not PASS. Feature SHA/PR/run links and any merge SHA are reported after delivery, avoiding a self-referential commit SHA inside this document.
