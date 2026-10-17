# Gate 4B.2B — Architecture decision and targeted sandbox feasibility

Date: 2026-10-17. Status: **DECISION REPORT COMPLETE; DECODER PARTIAL / RELEASE BLOCKED**.
Decision owner: repository owner. This is a recommendation, not approval to change the security contract.

## A. Verified state and evidence boundary

- Accepted main: `5ff844e3761de65b14acf9a69b8a090d43a40152`.
- Examined executable feature HEAD: `35ad64185f430d24fae26225212634e3019cc90a`.
- Existing [PR #13](https://github.com/Nhutduyasda/ShoeDocX/pull/13): open, unmerged; Gate 4B.2B-1 PARTIAL; Gate 4B.2B-2 NOT STARTED.
- [Exact executable-HEAD run 38995148865](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38995148865): both host jobs and final acceptance fail security acceptance; evaluator negative tests pass. No new application regression was detected.

| Existing evidence | Windows Server 2022 | Ubuntu 24.04 |
|---|---|---|
| Baseline and feature engine | 409 pass / 0 fail / 0 skipped each | 409 pass / 0 fail / 0 skipped each |
| Baseline and feature full suite | 604 pass / same 2 failures / 606 each | 596 pass / same 10 failures / 606 each |
| Frontend install/build/draft tests | PASS | PASS |
| Browser checks | 13 Studio + 11 intake PASS | 13 Studio + 11 intake PASS |
| Trusted CLR diagnostic | exit 0, outside confinement; diagnostic only | exit 0, outside confinement; diagnostic only |
| Confined Probe startup | C0000142 before ready, 111 ms | permission-denied exit 1 before ready, 29 ms |
| Enforcement / decoding | 2 blocked entries, 46 NOT_RUN | 2 blocked entries, 46 NOT_RUN |
| CPU/RSS/decoded-output measurements | absent | absent |

[Windows job](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38995148865/job/117285965604), [Ubuntu job](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38995148865/job/117285965371), [acceptance job](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38995148865/job/117287739975). Sanitized artifacts: Windows `12087200799`, Ubuntu `12087130795`. Startup latency is not decoding throughput or proof of resource enforcement.

This assignment adds only this document. Runtime, native packages, workflows, evaluator and production code are unchanged. Its documentation commit uses `[skip ci]` to avoid another full cross-platform/browser run, as requested; pending/skipped checks are not green acceptance. Evidence above belongs to the named executable HEAD, not a fictional successful run on the documentation HEAD. An exact one-file diff establishes applicability to unchanged code. No new sandbox execution is claimed.

## B. Windows diagnosis: boundary established, root cause UNKNOWN

Static inspection of `WindowsSandbox.cs` verifies the intended launch configuration: fresh AppContainer SID, zero capabilities, SECURITY_CAPABILITIES attribute `0x20009`, three-pipe HANDLE_LIST `0x20002`, child restriction `0x2000e`, suspended Unicode environment launch with extended startup information, Job attachment before resume, one active process, 256 MiB process/job memory, 15-second user-CPU limits and kill-on-close. These settings match the broad documented AppContainer launch pattern [S1]; successful configuration calls do not prove their security effects.

Runtime directory grants are specific to that AppContainer SID and read/execute only. Default profile storage has explicit write/delete denial; TEMP points to a deliberately unwritable location. SystemRoot/windir/SystemDrive and profile paths are rebuilt. No arbitrary parent environment is copied. `StartupInfo.Desktop` is unset. Exact effective access through parent directories, native dependencies, system/profile ACLs, desktop/window-station objects and runtime initialization has not been measured. Writable-profile or desktop requirements remain hypotheses, not diagnoses; no permissions were broadened.

The first failing executable is **Probe**, not a successful Worker launch. Its project references Protocol and links SandboxGuard, with no PDFium/Skia package reference. The document-free diagnostic returns before SandboxGuard. Therefore:

1. A published trusted CLR bootstrap can run outside AppContainer on the hosted OS.
2. The confined Probe dies before its ready handshake. The evidence does not establish whether managed entry was reached.
3. No PDFium/Skia initialization was required for this failure. A PDFium/Skia defect is not supported as the startup root cause.
4. Separate publish directories did not resolve it. They eliminate one packaging ambiguity, not every dependency/ACL possibility.
5. Windows application-event access succeeded, but the matching event list was empty. No failing module, loader call or effective-access denial was identified.

**Confidence:** high in the observations and isolation from decoder loading; low in any proposed DLL/ACL cause. AppContainer feasibility on this hosted runner remains unproven, not disproven.

The package versions/native hashes recorded in section G identify selected artifacts. They are not a manifest of the actual loaded CLR/system DLLs. SDK 8.0.426 is known; exact published runtime patch, apphost/coreclr/hostfxr identities, loaded system/VC runtime identities and loader search outcomes are missing.

A controlled, disposable Windows Server 2022 environment must capture a bounded trusted startup trace: minimal native no-op under the exact attributes, minimal CLR under them, then Worker bootstrap with no source input. Record entry markers, process/token/job/architecture facts, file and module SHA-256/version/signature manifests, attempted loader basenames and return statuses, and targeted effective-access checks. Use ProcMon/ETW or debugger loader diagnostics only where available; keep raw traces private and export sanitized operations/statuses. Do not grant broad filesystem access, make the profile writable speculatively, or process documents outside confinement. Two bounded trace attempts without a discriminating result should end UNKNOWN with the missing event identified, not trigger another broad fix loop.

## C. Linux diagnosis: cgroup placement fixed; subsequent denial UNKNOWN

The trusted shell barrier is attached to and checked in its job cgroup before release to exec bubblewrap. The workflow now places the supervisor inside the delegated tree. Earlier `SANDBOX_LINUX_ATTACH_CGROUP_DENIED` matches the kernel rule requiring migration permissions at the common ancestor [S2]; final evidence proceeds past that attachment stage.

Final code: `BOOTSTRAP_LINUX_PERMISSION_DENIED_EXIT_00000001_`. Bounded stderr is fully read, but only a coarse classification is retained. In particular, the namespace classifier tests “not permitted” or “No permissions”, while the generic classifier also accepts “Permission denied”. A namespace denial using that latter wording could therefore be classified generically. This is a diagnostic ambiguity, not proof of a namespace failure. No code is changed here to guess the cause.

| Candidate cause | What is established | Classification now |
|---|---|---|
| Cgroup creation/limits/attachment | launcher passed these stages at final executable HEAD | earlier configuration defect corrected; not full resource enforcement proof |
| User namespace / AppArmor | no syscall/audit event identifies a denial | UNKNOWN |
| UID/GID mapping | no explicit mapping-stage evidence | UNKNOWN |
| bubblewrap mounts / proc / tmpfs | no operation-specific failure retained | UNKNOWN |
| Executable permissions / runtime files | ordinary CLR diagnostic works; confined paths differ | UNKNOWN |
| Seccomp initialization | guard installs it before ready; no stage entry evidence identifies execution | NOT DEMONSTRATED |
| Runner policy versus sandbox arguments | no successful minimal namespace control or failing syscall trace | INSUFFICIENT EVIDENCE |

Do not label GitHub-hosted Linux “unsupported” solely from this exit code. Neither host-policy failure nor a bad mount/exec configuration has been distinguished.

A useful next diagnostic is a finite ladder on the same disposable non-root reference runner: host `/usr/bin/true`; user namespace only; UID/GID mapping; PID/network/mount namespace setup with trusted no-op; exact read-only runtime mounts and execution; minimal CLR; SandboxGuard/seccomp; Worker ready without source. A control omitting a primitive is diagnostic only and cannot count as acceptance. Preserve fixed operation/errno markers and a private syscall trace of the trusted bootstrap if permitted. Inspect existing AppArmor/audit denials without disabling policy. Check cgroup membership/readback at each transition and perform cleanup after each test. Stop at the first discriminating failure; classify unavailable primitive, host configuration, launcher configuration or runtime failure only with that evidence.

Available Work is root, lacks the .NET SDK and a writable delegated non-root cgroup test setup, and has no Windows surface. A local namespace experiment here would test another trust context, not distinguish these hosted causes. No further local document or bootstrap run was useful enough to justify it. Existing hosted evidence is reused; the controlled-runner ladder above is **NOT RUN**, not a claimed fix.

## D. Proven limitations versus unknowns

**Proven in this code:** confined ready was not obtained; 21 decode fixtures never ran; per-page CPU and cumulative PDF expansion instrumentation are absent; feature rejection is substring-based; parent-death/inherited-handle/admission coverage is incomplete; nested-native review is incomplete. `cpu.max` limits CPU rate, not total consumed CPU time. Windows Job limits currently meter user CPU while Linux RLIMIT_CPU uses process CPU; a cross-platform accounting definition is still needed.

**Unknown:** specific Windows initialization dependency; Linux denied operation; host primitive compatibility; practical decoding RSS/CPU/quality; exact native transitive component/advisory closure; a deployable $0 hosting configuration. A successful bootstrap would resolve none of these independently missing parser/acceptance predicates.

## E. Architecture comparison and ranked recommendation

**Primary recommendation: Option B, a Linux-only isolated decoding backend, conditional on explicit owner approval.** Keep the web UI cross-platform. Freeze speculative Windows startup fixes after the bounded evidence exercise. Validate one Linux security boundary and the PDF-budget contract before implementing product integration. Do not adopt a custom PDFium fork as the default low-cost product plan before measuring its maintenance burden.

This changes a backend support commitment, not browser access. It does **not** automatically change the accepted dual-OS gate: until approved, that gate still requires both hosts and remains PARTIAL.

| Dimension | A — retain dual OS | B — Linux backend only (recommended) | C — different decoder / managed recognition |
|---|---|---|---|
| Feasibility today | neither OS accepted; possible in principle, unproven as specified | technically justified simplification; Linux boundary and parser still unproven | candidates exist; no candidate has met this contract |
| Remaining work | two bootstrap investigations, both escape/lifecycle matrices, PDF policy | one boundary/host matrix plus same PDF policy | comparative corpus, new protocol adapter, isolation, licensing and budgets; or vendor review |
| CI | controlled Windows and Linux runners if hosted primitives/tracing insufficient | one representative Linux enforcement runner; browser tests remain cross-client | exact provider/library versions; reproducible local fixtures and output validation |
| Maintenance | AppContainer/Job/ACL plus namespace/seccomp/cgroup policy and two native graphs | one OS/kernel policy and native deployment graph | adds Java/Node/etc. or vendor API lifecycle and contracts |
| Packaging | win-x64 and linux-x64 apphosts, native DLL/SO and runtime servicing | one pinned Linux image/RID, small immutable runtime | depends on stack; wrapper license is not embedded-library clearance |
| Operations/cost | highest engineering/support burden; no $0 proof | lower burden than A; no verified free host yet | managed service adds recurring page/API cost; self-hosted keeps compute cost |
| Decision | retain only for a real backend-Windows requirement | approve direction, then prove host compatibility | fallback if strict parser/hosting economics fail |

### Option C candidates

- **qpdf plus renderer:** documented nesting/container/filter-chain limits are useful [S7], but do not establish a cumulative 32 MiB budget across qpdf and a later renderer. qpdf is another untrusted parser, not a safe preflight outside isolation. Warnings/unfilterable streams must fail closed if used for admission. No replacement is selected.
- **PDF.js or PDFBox:** evaluate permissive-license candidates with an independent corpus and release-specific dependency review. Browser execution can reduce server parsing but shifts resource/attack exposure to clients; browser output cannot authorize a server document or trusted fields. Worker termination is not proof of the existing OS/parser budgets. A server-side alternative still needs confinement. PDF.js core licensing is documented upstream [S8]; no package was added.
- **MuPDF/Ghostscript:** require resolving actual AGPL/commercial conditions with the owner before selection [S9]. They are not assumed to be free permissive drop-ins for a commercial hosted product, nor assumed automatically unsuitable without a licensing decision. Parser quotas still need evidence.
- **Managed recognition:** transfers parser operations to a provider, with data-region, retention, access, deletion, contract and per-page cost obligations. Document Intelligence describes regional processing and temporary retention/deletion [S10]. It does not expose proof of ShoeDocX's internal parser counters. This requires explicit approval of a different trust/acceptance model; validation, tenant binding, human review and export authorization remain necessary. No paid/provider call occurred.

## F. PDF policy and minimum acceptance contract

The inspected Worker uses ordinary PDFium load/page/render/text exports. It does not wire an allocator/stream expansion hook, a complete PDF object traversal policy or recursion counters. This is a statement about the selected integration, not proof that every alternative PDF library is incapable. Raw names can be escaped, objects indirect or compressed, and filter chains vary; substring scans cannot certify the required subset. No V8/form action invocation is not the same as parser-level feature rejection.

| Requirement retained | Appropriate enforcement | Current state |
|---|---|---|
| Source <= 1,048,576 bytes; SHA-256; one framed request | trusted broker + strict protocol | coded; complete confined path unproven |
| <= 10 PDF pages | parser page count before rendering | coded, NOT RUN |
| 32 MiB cumulative expanded PDF streams | instrumented decoder/parser across every filter path, repeat and intermediate expansion; or complete verified subset with a proven bound | absent / BLOCKED |
| Object and recursion caps | parsed graph, cycles and direct/indirect traversal controls | absent; numeric thresholds not yet accepted |
| Reject encryption/actions/external refs/embedded content/XFA/unsupported animation | parsed feature policy; no execution APIs; explicit safe rejection | only partial exclusions; unaccepted |
| <= 4096 per dimension, 4M pixels/page, 20M/job | codec metadata and checked allocations/output; malicious embedded images also need parser/codec budgets | coded, NOT RUN |
| <= 1 MiB total UTF-8 text; bounded output | worker and hostile-output parent validation | coded, NOT RUN |
| Output <= 81,114,112 bytes | 80,000,000 pixel bytes + 1,048,576 text + 65,536 metadata allowance | coded, NOT RUN |
| 256 MiB process/job memory; zero swap on Linux | OS boundary, including descendants and tmpfs accounting | configured, not demonstrated |
| 5 CPU seconds/page; 15 CPU seconds/job | enforceable CPU accounting/termination; separate per-page workers are one design candidate, with aggregate parse/startup/retry CPU included | per-page absent; total configured/unproven |
| 10-second page / 30-second job wall limits | trusted supervisor, cancellation and entire-tree kill | coded, unproven |
| Windows one process; Linux pids.max 64 for CLR threads, no unauthorized child processes; max two admitted workers | Job/cgroup + exec/clone restrictions + atomic admission | configured, unproven |
| Network/host files/parent canaries/handles denied; cleanup and crash recovery | actual OS attempts, not mock configuration or timeout | NOT RUN / BLOCKED |

This is the **minimum strict contract for acceptance under the existing requirements**, not a statement of implementation feasibility. Refusal of unsupported inputs is permitted; parser safety cannot depend on detecting a dangerous raw substring first. Repeated parsing must consume the same job budget; caching cannot silently omit resource use. A nonzero exit alone is not an identified OOM/CPU-limit event. CPU accounting must specify user versus user+system and measurement/termination precision; scheduler granularity is not zero. No unspecified thresholds are invented as accepted values here.

Three explicit policy paths:

1. **Keep strict PDF counters (current default):** obtain reviewed instrumentation or a complete verified subset parser, including embedded codecs and adversarial coverage. A PDFium fork carries substantial build, fuzzing, licensing and security-backport obligations. Do not commit to it merely to preserve the current library choice.
2. **Approve a narrower input subset:** define its compatibility losses, complete grammar/canonical reconstruction, bounded graph/stream semantics and reject recovery ambiguities. Rejecting all PDFs or scanning tokens is not a solution for a product promising PDF support.
3. **Approve a revised resource/trust contract:** e.g. replace the exact stream counter with a reviewed whole-job containment strategy or provider boundary. That is a deliberate relaxation/change of the 32 MiB requirement, not an equivalent mechanism. It requires owner risk approval, precise replacement tests and independent review. It is NOT applied by this document.

Until one path is approved and proven, general PDF acceptance stays blocked. Per-page child processes might simplify CPU attribution but repeated opens add parsing cost and do not solve stream/object policy by themselves.

## G. Dependency, license and servicing findings

Continue, do not replace, `docs/gate4b2b1/decoder-sbom.cdx.json`, `decoder-dependencies.json`, `provenance-review.txt` and `recovery-native-review.json`.

| Item | Known identity/evidence | Missing closure |
|---|---|---|
| PDFium Linux/Win32 | NuGet 157.0.8100; package content hashes and native SHA-256 entries; packaging revision `5b4d6b2ede1c1b0e17f5dc7b996c42d68d534ca8` | exact underlying PDFium/Chromium source/DEPS-to-binary chain, enabled native codecs, complete notices and attestations |
| SkiaSharp and native assets | 4.153.1; source metadata revision `4783f51448f9b070dda4f87b83e941c9599e466e`; wrapper MIT and native-assets notice filenames recorded | exact embedded Skia/codecs/font/shaping revisions, compiled options and advisory mapping |
| Native inventory | 28 file hashes; six package components, including macOS restored assets | which files are actually shipped/loaded on each supported target; transitive system libraries and architectures |
| .NET / OS runtime | net8.0; hosted SDK 8.0.426; self-contained publish | apphost/coreclr/hostfxr patch manifests, OS libc/libseccomp or Windows/VC dependency versions, rebuild/servicing proof |
| Advisory review | last exact-HEAD official NuGet scan: zero matched package findings | nested-native CVE status UNKNOWN; no native High/Critical clearance |

The prior file inventory found no bundled PDFium notices in its two restored packages; Skia native-assets packages list LICENSE and THIRD-PARTY-NOTICES. Presence of filenames is not a complete vetted redistribution bundle. Candidate nested components to map, **only where actually enabled**, include image codecs, compression, font/shaping and color-management libraries; do not assign guessed version numbers from wrapper versions. Review vendor advisories and component source revisions, not just NuGet IDs. Unresolved High/Critical in the active decoder path blocks release; unknown coverage also blocks clearance.

Microsoft currently lists .NET 8 support ending in November 2026 [S11]. With this review dated October 2026, a supported-runtime migration/servicing plan is a near-term release dependency. No runtime upgrade is made here. Rebuild self-contained images on supported runtime/OS/native updates; regenerate package/native/runtime manifests and notices; rerun corpus, escape, resource and lifecycle tests before release. Define an owner, monitoring cadence, urgent advisory response and rollback procedure rather than pinning old binaries indefinitely.

## H. Recommended minimum viable secure decoder architecture

**Proposed B design, not authorized or integrated:** existing authenticated orchestrator -> trusted job broker -> fresh Linux sandbox per source/job -> validated inert pixel/text response -> existing canonical validation and human workflow.

Use a controlled Linux host as the reference feasibility environment, with namespaces, read-only minimal mounts, non-root worker, dropped capabilities, no_new_privs, TSYNC seccomp, delegated cgroup-v2 cpu/memory/pids and tree cleanup. Keep the decoder in a separate PID/network/mount boundary from the broker, without DB/cloud/session credentials, parent filesystem, management socket, or metadata access. The broker alone stages one owned source and collects bounded output. No arbitrary source URL or filename can select mounts/executables. Prefer short-lived jobs and an immutable image/digest over a long-lived multi-tenant parsing daemon.

Server authorization resolves tenant/user/session/source revision, source hash and job nonce. The worker receives bytes/hash/limits and opaque correlation IDs, not authority to select an owner. Results are accepted only against the current server-side source/session revision; expired, cancelled, replaced or cross-tenant results are discarded. Replay/idempotency, bounded retries, global/per-tenant admission and leases are trusted-broker responsibilities. Native output remains untrusted until protocol/value checks; it cannot confer approval. Review/export revision/fingerprint gates remain intact.

Keep communication framed pipes on a reference host; a remote execution adapter needs authenticated control transport and one-job input/output capabilities, not general storage credentials in the decoder. Any helper with fetch credentials must remain outside the parser's reachable process/filesystem boundary. A sidecar or same-container child alone does not establish that separation. Destroy writable storage after cancellation, failure and success; define retention independently in the source store.

Logs contain job/result codes, binary/policy revisions, byte/page counts and resource summaries, not original documents, tokens or extracted text. Measure queue delay, cold start, decode CPU/RSS/wall/output, quota kills, crashes, retry rate, stale results and cleanup leaks. Alert on resource/admission abuse and native/runtime advisories. Durable source/job state is a later authorized integration concern: the inspected Studio session store is in-memory with a 20-minute lifetime, not a ready-made durable commercial job queue. No schema/migration is introduced now.

## I. Next gate and required testing environment

Recommended next implementation gate **after owner decisions**: `Gate 4B.2B-F — Linux host capability and parser-budget feasibility`, a standalone experiment continuing this PR or its explicitly approved successor, not Gate 4B.2B-2 integration.

Deliverables must be bounded:

1. One pinned disposable Linux reference environment plus **one** selected target-host capability test. Produce the finite ladder in C and a capability matrix: supported, denied, configuration defect or unknown. Never disable production policy as a test remedy.
2. Choose the parser-contract path from F; demonstrate the hardest stream/object/feature predicate on synthetic adversarial fixtures before broad format expansion. If strict counters are infeasible within the accepted maintenance budget, stop with an architecture decision, not partial security integration.
3. Once confinement and parser policy exist, run all 21 format fixtures and every required network/file/child/handle/resource/lifecycle/admission predicate. Include escaped names, filter chains, object streams, cyclic references, malformed recovery, embedded-image/font expansion, cancellation at startup/page/output and parent termination. Test above/below every threshold, collect reason-specific OS evidence, and discard partial output.
4. Preserve exact executable-HEAD, package/native/runtime/image/kernel/policy identities. Native review/notices and CPU accounting definitions must close. Any skipped mandatory case blocks acceptance.

The reference runner needs non-root user namespaces authorized under its AppArmor policy, controlled read-only mounts, libseccomp/TSYNC, cgroup-v2 delegation and observable kill/OOM/CPU events. Administrative preparation may provision the reference environment; untrusted workers never get host privileges. Capture sanitized traces privately. A managed target must prove equivalent **per-job** file/network/credential/process/resource/cleanup properties; platform marketing claims and a configured memory value are insufficient.

If dual-OS is retained, the Windows controlled-runner ladder in B and the complete Windows enforcement/decoding matrix remain mandatory as well. No Linux result waives Windows. No source change in this decision task requires a new executable CI run.

## J. Hosting and cost implications

| Environment | Compatibility assessment | Cost/operations implication |
|---|---|---|
| Controlled Linux VM/host | can provision required primitives in principle; actual reference test still required | most control, but patching/monitoring/availability burden; conflicts with owner's no-VPS preference unless separately approved |
| Azure Container Apps Consumption / Jobs | Linux images and no privileged host-level containers [S3]; existing writable host-cgroup provisioning cannot simply be copied into an app | candidate only; prove nested requirements or approve and verify a platform-native per-job boundary |
| Cloud Run Jobs | host privileges/sysfs writes/in-container mounts are restricted; managed namespaces exist, but do not imply delegated inner cgroups [S5] | another candidate boundary, not a drop-in for this launcher; jobs have minimum billable duration [S6] |
| Bare Docker/process separation | not acceptance by itself; shares kernel unless strengthened; no automatic tenant/no-egress/credential isolation | simple packaging must not be mistaken for a security proof |

Azure Consumption publishes monthly grants of 180,000 vCPU-seconds and 360,000 GiB-seconds; jobs consume active allocated resources, and other services/network features can add charges [S4]. As arithmetic only, 1,000 executions at 60 seconds, 1 allocated vCPU and 0.5 GiB would use 60,000 vCPU-seconds and 30,000 GiB-seconds. This is not a validated SKU pairing, workload estimate or $0 bill: cold starts, billed allocation, registry, storage, queue, logs, egress, networking and other subscription use must be included. Quotas/budget alerts are not hard spend caps.

For Cloud Run, distinguish its managed isolation from an approved replacement for the current sandbox. Its contract exposes service-identity metadata and platform namespaces; the decoder must still be denied credentials and unintended egress [S5]. Do not assume all Cloud Run jobs use gVisor or that “full Linux compatibility” grants host control. No cloud resources are created.

**No reviewed deployment currently proves the combination of strict parser budgets, dual OS, no VPS and guaranteed zero recurring cost.** The practical choice is to validate B and a hosting boundary, then explicitly decide the remaining cost/contract trade-off. Keep deployment separate from this architecture decision.

## K. Independent product work, separate from decoder acceptance

No implementation begins here. Create future approved work from accepted main, never use PR #13 as the product feature base.

| Priority | Useful independent work | Evidence and guardrail |
|---|---|---|
| 1 | Review UX and safe synthetic end-to-end demo | explicit simulated extraction, provenance/uncertainty, correction/re-review; preserve server revision-bound approval and export checks; do not advertise OCR |
| 2 | Flexible versioned extraction schemas | extend existing typed/provider-neutral contracts with example datasets, validation, schema/profile migration rules; no business-field hardcoding or decoder dependency |
| 3 | Learn mapping from example templates | existing suggester uses normalized labels/approved synonyms and unconfirmed proposals; add examples/evidence and ambiguity resolution, never automatic human confirmation |
| 4 | Multi-sheet correctness and UX | projector already iterates layout.Sheets; test per-sheet choices, formulas/footer preservation and conflicts rather than rebuilding “multi-sheet support” as absent; current formula grammar intentionally excludes cross-sheet references |
| 5 | Larger bounded row expansion | current validator caps items/region at 1,000 and total generated cells at 200,000; benchmark memory/output/formula costs before proposing new caps; no blind limit increase |

Synthetic demonstrations can feed known observations into existing normalization/review/export. Template workbook parsing remains an untrusted-input surface with its own ZIP/XML/input limits; “independent of decoder” does not mean automatically safe. In-memory demo sessions are not evidence of commercial durability. Preserve legacy footwear flows and existing Gate 4A security gates.

## L. Explicit owner decisions and final disposition

| Decision | Recommended choice | Effective default until approved |
|---|---|---|
| Backend support scope | approve Linux-only decoder backend; Windows/mobile remain web clients | dual-OS acceptance remains mandatory |
| PDF resource contract | fund a bounded feasibility comparison first; do not default to maintaining a custom native fork | strict 32 MiB/object/recursion/feature/per-page CPU requirements remain |
| If strict policy is impractical | separately choose narrower supported PDF subset, revised risk contract, or managed provider | general PDF decoding/integration blocked |
| Hosting | select one target and authorize only its capability experiment; approve any cost separately | no deployment, no VPS purchase, no $0 guarantee |
| Runtime/native lifecycle | approve a supported-runtime and dependency servicing plan before release | no package/runtime upgrades in this task |
| Product development | separate approved review/schema/mapping work from accepted main | no Gate 5 implementation or decoder integration here |

**Recommendation B is a proposed product architecture, not an accepted Gate.** This report is complete; root causes remain UNKNOWN at the specificity described above, current decoder is PARTIAL/BLOCKED, PR #13 stays open, main is unchanged, Gate 4B.2B-2 and OCR integration remain NOT STARTED. Do not edit the evaluator to implement these proposed scope/contract choices before approval.

## Primary sources checked on 2026-10-17

- S1: [Microsoft AppContainer launch/ACL model](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer).
- S2: [Linux kernel cgroup-v2 delegation containment](https://docs.kernel.org/admin-guide/cgroup-v2.html#delegation-containment).
- S3: [Azure Container Apps containers and limitations](https://learn.microsoft.com/en-us/azure/container-apps/containers).
- S4: [Azure Container Apps billing](https://learn.microsoft.com/en-us/azure/container-apps/billing).
- S5: [Cloud Run container runtime contract, security restrictions and metadata](https://docs.cloud.google.com/run/docs/container-contract).
- S6: [Cloud Run pricing](https://cloud.google.com/run/pricing).
- S7: [qpdf global/parser/filter limits](https://qpdf.readthedocs.io/en/stable/cli.html#global-limits). These are documented capabilities, not an audited replacement implementation.
- S8: [Mozilla PDF.js license](https://github.com/mozilla/pdf.js/blob/master/LICENSE).
- S9: [Artifex licensing description for PyMuPDF/MuPDF](https://artifex.com/blog/pymupdf-acquired-by-artifex). Release-specific legal review is still required; this report is not license clearance.
- S10: [Document Intelligence data processing, retention and deletion](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/document-intelligence/data-privacy-security).
- S11: [Official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- S12: [GitHub documentation-only workflow skip behavior](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs). Skipped required checks can remain pending and block merge.

Source-derived product facts are distinguished above from repository observations and proposed designs. Hosting documentation is not a substitute for target-host security tests. No new library, host or provider has passed the existing decoder contract in this task.
