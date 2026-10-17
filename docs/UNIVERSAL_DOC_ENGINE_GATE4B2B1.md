# Gate 4B.2B-1 — isolated decoder experiment

**Decision: PARTIAL. Do not merge or begin Gate 4B.2B-2.** This is preserved experimental implementation, not an accepted decoder or production-ready security boundary. The actual local Windows AppContainer bootstrap exits before its ready handshake with `0xC0000142` (DLL initialization failure). No local document was passed to an unconfined decoder as a workaround. Hosted evidence must be read independently; an isolated successful decode on one OS would not satisfy both-platform acceptance.

Starting verified main: `5ff844e3761de65b14acf9a69b8a090d43a40152`, containing accepted Gate 4B.2A. Branch: `codex/universal-doc-engine-gate-4b2b1-decoder-poc`. Git fetch, clean working-tree inspection, ancestry check and PR/branch search completed before implementation. The parent older workspace is untouched. This PR has no ASP.NET, Studio, existing test, frontend, database, authentication or footwear-source changes.

## Implemented experiment

Five independent .NET 8 projects under `tools/`:

| Project | Role |
|---|---|
| `UniversalDocDecoder.Protocol` | Strict versioned framing, byte/hash/dimension limits, inert DTOs |
| `UniversalDocDecoder.Worker` | Out-of-web-process PDFium rendering/text and Skia image decoding code; OS guard runs before reading source bytes or initializing decoders |
| `UniversalDocDecoder.Sandbox` | Trusted operator launcher, bounded supervisor/result validation, Windows AppContainer/Job and Linux bubblewrap/cgroup launch paths; no ordinary-process fallback |
| `UniversalDocDecoder.Probe` | Separate verification-only executable attempting real network/filesystem/process/resource operations; not a decoder feature or production endpoint |
| `UniversalDocDecoder.Verifier` | Real subprocess enforcement and synthetic document acceptance executable, sanitized evidence; returns nonzero while acceptance is incomplete |

Neither production API nor previous tests reference these projects. The worker does not export Excel, normalize fields, recognize OCR, own tenants/sessions, or authorize review. Phase 2 APIs, job states and frontend controls were deliberately **not implemented**, as required by the strict security gate.

## Selected stack, alternatives and licenses

| Component | Exact experimental version | Recorded license / purpose |
|---|---|---|
| `bblanchon.PDFium.Win32` | 157.0.8100 | NuGet metadata Apache-2.0; native PDFium packaging for Windows |
| `bblanchon.PDFium.Linux` | 157.0.8100 | NuGet metadata Apache-2.0; native PDFium packaging for Linux |
| `SkiaSharp` | 4.153.1 | MIT; JPEG/PNG/WebP codec API |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | 4.153.1 | MIT wrapper plus native third-party notices; no fontconfig dependency |
| `SkiaSharp.NativeAssets.Win32` (transitive) | 4.153.1 | MIT wrapper plus native third-party notices |
| `SkiaSharp.NativeAssets.macOS` (transitive) | 4.153.1 | MIT wrapper plus notices; part of restored graph, not a supported/tested target |

Versions were queried from the official NuGet flat-container API on 2026-10-15, not inferred from stale examples. The worker project uses exact NuGet ranges and a checked-in lockfile. No application packages were upgraded. No paid or commercially restricted component is added. Wrapper/package licensing does **not** replace PDFium's BSD-style and bundled native codec/font notices; a complete redistribution-notice bundle and nested native component audit are outstanding before any accepted distribution. NuGet PDFium packages did not list embedded license/notice files in their restored file inventory. Do not ship binaries assuming the package-level SPDX expression covers every native component.

Package provenance: PDFium package metadata points to [bblanchon/pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) commit `5b4d6b2ede1c1b0e17f5dc7b996c42d68d534ca8`; Skia metadata points to release/4.153.1 commit `4783f51448f9b070dda4f87b83e941c9599e466e`. NuGet content hashes and 28 native file SHA-256 values are recorded in [decoder-dependencies.json](gate4b2b1/decoder-dependencies.json), with the complete graph in [decoder-sbom.cdx.json](gate4b2b1/decoder-sbom.cdx.json). These hashes establish the inspected artifacts' identities, not a reproducible-from-source attestation or complete native CVE proof.

The pinned upstream PDFium `steps/05-configure.sh` sets `pdf_enable_v8 = $ENABLE_V8` and `pdf_enable_xfa = $ENABLE_V8`. This experiment selects the ordinary PDFium packages, not PDFiumV8. It never creates a form-fill environment or invokes action APIs. A checked build-configuration artifact and fuller native provenance/notice review remain required; an unused JavaScript API is not itself proof that all PDF features are harmless.

PDFium offers real rendering and embedded text in one native library on both OSes, avoiding a second parser solely for text. Managed PdfPig was considered for text but does not replace rasterization and still needs stream/object quotas. MuPDF was not included because AGPL/commercial redistribution requires a separate resolved decision. ImageSharp was not included because its actual license/use conditions require assessment. Skia was chosen for commercially compatible image coverage, not because native code is intrinsically safe. `System.Drawing.Common` is not a Linux decoding solution.

Live official NuGet index/base/update audit found **zero matched advisories across the six decoder packages** at this run. This is narrower than native safety: embedded Skia/PDFium codec and font CVEs require their own versioned component mapping, currently incomplete. Existing test SQLite High and seven npm build-tool affected entries remain inherited and visible. Scoped CI reruns the unchanged baseline advisory comparison and separately audits the new worker graph. Feed failure cannot count as clean.

Reproducibility status: managed/native package versions and NuGet hashes are pinned; native file identities are recorded. Fully reproducible native builds, package signature attestation, complete nested codec SBOM, and an explicitly pinned/self-contained runtime servicing policy are **not established**. CI pins SDK 8.0.426; local SDK is 8.0.424. Updating requires a dedicated exact package/native/runtime review, regenerated lock/SBOM/hashes/notices, official plus native advisories and all OS enforcement/decoding regression tests. No broad auto-upgrade or audit suppression is authorized.

## IPC and data boundary

No document content or filenames appear on the command line. Worker launch executable, working directory and environment come only from trusted local deployment/test configuration. The decoding request contains exactly protocol version, media type, server-computed uppercase SHA-256 and byte length, followed by original bytes over stdin. Unknown JSON properties are rejected. Header length is a little-endian signed 32-bit count constrained to 1–4096 bytes, JSON depth 8. Input is 1 byte–1 MiB; extra bytes and hash mismatch fail. The supervisor closes stdin after the single request.

After OS confinement, worker emits a version/build `ready` frame. Per page, a `page-start` frame precedes a bounded `page` frame and raw BGRA/text bytes; a final `complete` frame is mandatory. The parent verifies implementation ID, actual hash, ordered one-based page numbering/count, dimensions, exact pixel byte length, strict UTF-8, cumulative limits, EOF and zero process exit. Partial output is discarded on any failure. Metadata cannot confer semantic correctness or approval. Output contains pixels/inert embedded text, never HTML/SVG or a raw private source path.

Protocol budgets: 10 pages, 4096 pixels/side, 4 million pixels/page, 20 million/job, 150 DPI PDF rasterization, 1 MiB cumulative text, at most 80 million pixel bytes plus bounded text/metadata. One request and at most 10 page pairs/final frame are accepted. The supervisor has an immediate two-worker capacity cap and rejects excess work; there is no detached queue. Tenant/session scheduling is not present because Phase 2 is forbidden. The worker never receives cookies, JWTs, DB strings, tenant IDs or cloud credentials. The separate probe protocol's harmless canary paths are verification-only and not accepted by the decoder.

## OS controls implemented versus demonstrated

### Windows

The launcher creates a fresh AppContainer profile with zero capabilities, grants only read/execute access to the trusted self-contained runtime directory, denies writes/deletes in its default profile storage, uses an explicit three-pipe inherited-handle allowlist, and sets child-process creation restriction. The worker starts suspended, is attached to a Job Object, and only then resumes. Job configuration: one active process, 256 MiB process/job memory limits, 15 seconds process/job user CPU, kill-on-close. Parent kill/cancellation calls `TerminateJobObject`. Environment is rebuilt from fixed runtime settings and OS-known Windows/private profile locations, not copied from parent secrets. A Job Object is **not** treated as filesystem/network isolation; AppContainer capability/ACL enforcement must pass actual probes.

Actual local attempt: initial `CreateProcessW` error 203 was isolated to launch. Adding the required sanitized Windows/private profile environment fixed that stage. The now-created child exits `C0000142` before any framed ready message. The verifier records `BOOTSTRAP_EXIT_C0000142_`; no network, filesystem, process, CPU or memory claim is inferred from successful attribute configuration. No permission broadening, unsandboxed execution, in-process parsing or elevated worker fallback was used to obtain a decode result. Runtime startup and exact failing Windows initialization dependency require further diagnosis without weakening containment.

### Linux

The launcher requires non-root execution, `/usr/bin/bwrap`, and an explicitly delegated cgroup-v2 subtree `/sys/fs/cgroup/shoedocx-decoder-<uid>`. It refuses missing delegation. Each job gets memory.max=256 MiB, swap.max=0, pids.max=64 and cpu.max=one CPU. Bubblewrap creates user/PID/network/mount and other namespaces, drops capabilities, uses a new session, kills on parent death, and exposes only a read-only runtime, system library directories, `/proc`, a minimal `/dev`, private memory-backed `/tmp`, and read-only resource-control files. The actual worker must verify non-root/cgroup limits and install TSYNC seccomp with no_new_privs before document input. Rules deny network sockets, exec/fork/process clones, ptrace, namespace/mount, keyring, BPF and other dangerous operations while permitting CLR thread clones. CPU RLIMIT is 15 seconds; the parent uses job/page wall deadlines and cgroup.kill.

Linux controls are implemented code, not local Windows evidence. Hosted verification must demonstrate them. Workflow setup may provision a test-only cgroup subtree with runner administration, but the launcher/worker never use sudo. If the runner refuses delegation/user namespaces, it reports BLOCKED; the workflow does not disable AppArmor, switch to a root decoder, or substitute process separation for containment.

Read-only root mounting protects host data; private tmpfs is bounded indirectly by the job's memory cgroup, with a dedicated explicit mount-size policy still needing verification. Library directory visibility and syscall policy require further minimization/audit. cgroup cleanup failures are not yet independently measured. No Linux containment result may be inferred from Windows code or a mocked probe.

## Decoder implementation and fixture status

The decoder code calls actual native codecs, not magic-byte success: Skia `GetPixels` must report Success; BGRA buffers are returned and encoded EXIF orientation is explicitly applied. Animation is rejected. PDFium code discovers pages, renders actual BGRA at bounded DPI, and extracts bounded page-origin embedded text. Layout/reading order and extracted text are untrusted, not recognized business fields.

The synthetic manifest includes baseline/progressive/EXIF JPEG; RGB/RGBA/palette PNG; lossy/lossless/alpha/animated WebP; truncated PNG; selectable/multi-page/rotated/scanned-style/layout PDF; malformed, action, external-reference, encryption-marker and compressed-quota negatives. Synthetic PDF negatives using an encryption/action marker are policy-rejection fixtures, **not proof of correctly parsing every valid encrypted/action-bearing PDF**. Invalid xref recovery, excessive object nesting, escaped names/object streams, true encrypted samples and full adversarial codec corpus remain incomplete.

Local support status: **unverified**, because the confined worker never reached ready. There are no local pixel/text decode PASS results or valid peak CPU/RSS decode measurements to report. Supported-code paths are not advertised as accepted formats. Hosted results, including any actual decoded buffers/dimensions/text, are separate artifacts and must not erase local/blocking limitations.

### Explicit acceptance blockers beyond startup

1. PDFium's public API provides no cumulative expanded-PDF-stream accounting hook. The experiment refuses obvious `/FlateDecode` and `/ObjStm` cases, but raw byte searches are not a complete PDF parser and cannot enforce 32 MiB cumulative expansion, feature rejection, object/nesting limits or escaped-name policies. **The requested PDF security policy is unproven.** A native instrumented allocator/parser budget or a separately reviewed complete bounded policy is needed; do not equate 256 MiB RSS with stream accounting.
2. Per-page 5-second CPU enforcement is not implemented/proven; 15-second job OS CPU and 10/30-second parent wall deadlines are insufficient to claim it.
3. Actual parent-death/descendant cleanup, inherited-handle canaries, worker-capacity races and all 18 specified attack scenarios are not complete. Declared kill-on-close/die-with-parent flags are not substitutes for those tests.
4. Native nested component version/advisory coverage and redistribution notices remain incomplete. A package-level zero-advisory result cannot close this gap.
5. Actual resource limits, peak RSS/CPU and supported raster/text outputs require successful confined runs on **both** OSes. Zero/absent measurements are not passing evidence.

The verifier deliberately returns PARTIAL/nonzero while these blockers remain. They are recorded in machine-readable evidence rather than hidden by skips or a passing mocked sandbox. Fixing startup alone cannot authorize Phase 2.

## Tests and evidence

Fresh baseline and post-change Windows normal SDK 8.0.424 restore/build/test: **606 pass / 0 fail / 0 skipped** each, containing the same 409 engine cases and unchanged outcomes. Frontend clean lock install/build and **13 draft tests pass**. Standard builds retain NU1900 when vulnerability service-index retrieval fails; direct official feed audit succeeded for the new graph and auditing is not disabled. Worker, probe and verifier compile with zero warnings/errors locally after routine compilation fixes. Actual initial Windows confinement run: **BLOCKED** before ready; dependent attacks/decodes were not executed, with no fabricated PASS counts.

[local-enforcement.json](gate4b2b1/local-enforcement.json) contains sanitized actual status and codes. New actual subprocess verifier checks network, host canary read, host-directory write, child spawn, hang cancellation, crash, unmanaged allocation, CPU burn, oversized/malformed/truncated messages and invalid hash/page metadata where startup permits. A parent loopback/file control establishes canaries are accessible before sandboxing. A timeout is not counted as proven network denial. Negative format cases only pass on their specific worker rejection code, not on arbitrary launch failures. Output artifacts contain codes, counts, package/hash metadata, synthetic evidence and measurements, never tokens, source content, native dumps or raw legacy test logs.

Scoped CI: `.github/workflows/gate4b2b1-decoder-verification.yml`, Ubuntu 24.04 and Windows Server 2022, SDK 8.0.426. It checks exact baseline and final PR HEAD, strict tools/docs-only diff scope, fresh standard builds, all previous engine/full test identities and normalized legacy-failure hashes, baseline/feature frontend, both unchanged authenticated browser suites, inherited/live decoder advisories, self-contained worker/probe publish and **actual** enforcement executable. The security acceptance step fails on PARTIAL, BLOCKED or FAIL; missing artifacts or decode/enforcement evidence cannot turn green. A failing security workflow is expected evidence of the unaccepted experiment, not authorization to merge.

PR/run links and exact final SHAs are recorded in the delivery report/PR description after verification, to avoid embedding a self-referential feature SHA. Phase 1 must remain an open normal PR until all mandatory conditions pass. Phase 2 has no branch, PR, endpoints, source admission changes or UI implementation. Phase 2 status: **BLOCKED by Phase 1 acceptance**.

## Preserved application status

| Capability | Status |
|---|---|
| Existing authorized file upload | Accepted Gate 4B.1 restricted subset, unchanged |
| Safe isolated decoding | Experimental/unaccepted; no production route |
| PDF text extraction / rasterization | Implemented worker code, local execution blocked; no recognition claim |
| Common image pixel decoding | Implemented worker code, local execution blocked |
| OCR / semantic understanding | Not implemented; Gate 4B.3 not started |
| Human mapping/review/approval | Existing gates unchanged |
| Excel export | Existing single orchestrator chokepoint unchanged |
| Production readiness | Not established; inherited and new experimental risks remain visible |

## Recovery attempt (2026-10-17, ChatGPT Work)

Reference HEAD `a1aa24bd97cb3d5e31458a79f04b7ad3fc7e7254` and main `5ff844e3761de65b14acf9a69b8a090d43a40152` were fetched again. Run [38712539627](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38712539627) logs independently confirm both hosts built successfully, engine 409/409, full Windows 604/606 (same two failures), Ubuntu 596/606 (same ten), frontend build/draft tests and 24 browser checks. Both enforcement runs stop before ready; all decoding/resource measurements are absent. These are prior-HEAD results, not recovery validation.

Recovery changes:

- Worker and Probe now publish to distinct `worker/` and `probe/` directories. Each has its own dependency/runtime graph; the launcher chooses the corresponding immutable output. No claim that publish collision caused C0000142 has been established.
- Linux writes existing cgroup control files using `FileMode.Open`, never the create/truncate semantics of `File.WriteAllText`. Each operation has a safe stage/type failure code. The earlier generic message concealed which kernel operation failed; old logs cannot establish that root cause retroactively.
- A trusted shell bootstrap waits for an explicit release line. The parent attaches and verifies its PID in the limited cgroup before releasing it to exec bubblewrap. Source bytes still wait for the worker's confined ready message. This closes the former attachment/descendant race without modifying OS restrictions. It does not establish parent-death cleanup.
- Probe supports a dedicated, document-free `--trusted-startup-diagnostic`, executed outside containment solely to distinguish CLR startup from confined bootstrap. It exits before input/probe operations and is explicitly excluded from security evidence. Windows AppContainer permissions, capability list, job limits and inherited handles remain unchanged.
- The subprocess verifier derives its decision from the mandatory inventory and fixture results, emits explicit NOT_RUN entries and rejects missing checks. A nonzero memory/CPU probe exit alone is now UNPROVEN, because it could be an unrelated crash. It cannot count as resource-limit evidence.
- The final CI acceptance job consumes both exact-HEAD reports and requires complete security, decode measurements and native review evidence. Negative evaluator tests cover missing/failed hosts, startup blockers, skipped checks, missing enforcement/decoding, unexpected attack success represented as FAIL, malformed evidence, budget excess, incomplete native review and unresolved native High/Critical issues. Synthetic evaluator fixtures are unit-test data, never sandbox evidence.

### PDF architecture decision still required

The selected uninstrumented PDFium API has no accounting callback in this implementation for cumulative 32 MiB expanded streams, parser object/recursion quotas or full feature rejection. Raw token exclusions cannot meet these predicates (escaped names and alternative filters can bypass them). No wrapper-only adjustment can justify acceptance. Replacing them with a memory cap would change the requirement and is not authorized.

Concrete next architecture: a pinned, source-built PDFium fork with explicit parser/stream expansion counters, object/reference traversal depth and object-count budgets, fail-closed feature policy at parsed object level, and adversarial fixtures for every path, retained inside the same OS sandbox. Its patch/build provenance and complete native third-party notices must be reviewed. This requires further implementation/review and is proposed, not represented as already safe or approved. A second unmanaged general PDF parser would add attack surface and still require its own equivalent budgets. A restricted PDF subset is another option only after an explicit revised compatibility contract and a complete bounded grammar validator; substring scanning is insufficient.

Per-page CPU, parent-death/descendant, handle inheritance, admission/process-count and native nested advisory/notice acceptance remain open until actual evidence exists. Existing SBOM/package hashes are preserved; this recovery does not certify the native inventory. A controlled Linux runner, if needed, must provide non-root bubblewrap user namespaces under its existing AppArmor policy and a delegated cgroup-v2 cpu/memory/pids subtree; disabling protections is not a permitted remedy.


Recovery run `38993717635` on intermediate HEAD `ea453e8` narrowed Linux setup failure to `SANDBOX_LINUX_ATTACH_CGROUP_DENIED` (56 ms); memory/swap/PID/CPU control writes completed first. Trusted CLR diagnostic exited zero, but confinement/decodes remained blocked. The workflow delegated a destination subtree without first putting the supervisor inside it, so migration still crossed the delegation boundary. [Kernel cgroup-v2 delegation containment](https://docs.kernel.org/admin-guide/cgroup-v2.html#delegation-containment) requires write access to the common ancestor's `cgroup.procs`, not merely the destination. Recovery now provisions a supervisor leaf and administratively migrates only the trusted CI shell into it before Python starts; worker launch/attachment remain non-root. Root cgroup permissions are never granted to the worker or runner user. A launcher preflight rejects an outside-delegation supervisor explicitly. Exact final-HEAD CI must confirm the correction; subsequent namespace/runtime failures remain possible.
