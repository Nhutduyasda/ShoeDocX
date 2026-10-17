# Gate 4B.2B-F — bounded Linux-only decoder feasibility

Status: **BLOCKED; RECOMMENDATION REDESIGN**. Stop the current GitHub-hosted bubblewrap route. This is an evidence-based host/policy limitation plus a separate unresolved parser contract, not a claim that Linux decoding is universally impossible. The finite investigation completed; no sandbox/decoder PASS is claimed.

Draft PR: [#14](https://github.com/Nhutduyasda/ShoeDocX/pull/14). Executable tested HEAD: `b77040603b49597b2e9efa77c03257b379133b54`. Exact-HEAD [run 38997906969](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38997906969), [job 117294412595](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38997906969/job/117294412595). Evaluator/classifier tests PASS; capability gate exits 1 (BLOCKED), sanitized audit/upload succeed. Artifact `12088065995`, archive SHA-256 `e0395c6f1eadf0f85a0e6161d8d09daea649f5d26adb5287b16a98b8983bb711`. Checked-in copies: [summary](gate4b2bf/linux-summary.json), [audit](gate4b2bf/apparmor-audit.json).

The final follow-up changes only documentation/evidence and uses `[skip ci]`; executable files remain byte-identical to this tested HEAD. This does not turn failed/pending checks green or pass any historical Gate.

## Scope and authorization

Starting accepted main: `5ff844e3761de65b14acf9a69b8a090d43a40152`.
Branch: `codex/universal-doc-engine-gate-4b2b-f-linux-feasibility`.
Reference PR #13: `98dae66b96efdf8fefb33d15f69e73e40489cac8`, open/unmerged; historical dual-OS acceptance remains PARTIAL. It is not modified or cherry-picked.

Owner approval targets only the new feasibility backend to Linux; browser clients remain cross-platform. It does not waive PDF counters/resources, approve Phase 2, authorize cloud provisioning or paid spend, or pass historical Gate 4B.2B-1. Only isolated tools, tests, workflow and documentation are added from main. No production dependency or application contract changes; no broad application regression rerun is needed for this standalone experiment.

## Reproducible experiment and stopping rules

Ubuntu 24.04 GitHub-hosted runner; distribution gcc/libc development files, bubblewrap and strace installed. Exact package versions, kernel and compiled static probe SHA-256 are recorded. One job, ten-minute maximum, no paid API or cloud subscription changes. Existing GitHub execution allowance availability is not a cost guarantee; this does not enable billed capacity or authorize quota purchase.

A small **trusted C probe**, with no CLR, PDFium, Skia or document input, differentiates kernel/runtime causes. The finite sequence is:

1. Verify non-root supervisor; trusted host native identity control.
2. Direct user namespace creation plus explicit single UID/GID mapping; then PID/network/mount namespace calls where the first operation succeeds.
3. One separate bubblewrap mapping/native-exec control, because AppArmor can mediate different executables differently. The mapping control omits some final isolation and is **diagnostic only**, never document-processing evidence.
4. If bubblewrap fails, one bounded strace of the same trusted control, exporting syscall/errno and namespace flags only; raw stderr/trace remain private temporary data. Read-only AppArmor audit correlation records fixed operation/errno. **Stop**; later isolation, resources and all document decoding are NOT RUN.
5. Only if the mapping control succeeds: delegated cgroup placement before exec; full bubblewrap user/PID/network/mount/other namespaces, read-only static runtime, 16 MiB private tmpfs and no capabilities. Probe ready checks actual namespace identities, UID/GID, no_new_privs, seccomp, absent synthetic environment canary and closed synthetic FD 200.
6. Only after confined ready: actual socket/file/write/clone attempts, tmpfs saturation, cgroup memory OOM event, CPU-limit signal calibration, PID controller saturation and cancellation/tree cleanup. Failures stop expansion. A bootstrap success alone is not isolation acceptance.

The native seccomp policy is a tiny architecture-checked default-deny allowlist for this trusted single-threaded probe, not a reviewed .NET/native-decoder policy. The special PID-controller stress probe intentionally forks harmless sleeping children inside the same restricted namespace/cgroup; it is a kernel controller test, not evidence that a decoder may spawn children. CPU calibration is a 1-second soft/2-second hard RLIMIT test; it does **not** prove 5-second per-page or 15-second aggregate document accounting. Parent-death via independently killing the supervisor is not implemented and remains NOT RUN. No missing check can produce overall PASS. The conditional later probes were compiled/reviewed but were NOT RUN on this host; they are future test scaffolding, not accepted enforcement.

Memory success requires an actual `memory.events` oom_kill increase, not arbitrary nonzero exit. PID success requires EAGAIN plus `pids.events:max`. Filesystem success requires exact expected denial, network/child attempts require EPERM. A timeout is not network-denial evidence. Cancellation kills the whole cgroup and verifies it empty. Source/environment/credential access is tested only with synthetic controls, never real secrets.

## Linux capability matrix and root-cause record

Reference: Ubuntu 24.04, kernel `6.17.0-1022-azure`, non-root UID 1001; bubblewrap `0.9.0-1ubuntu0.3`, gcc package `4:13.2.0-7ubuntu1`, libc6-dev `2.39-0ubuntu8.9`, strace `6.8-0ubuntu2`. Static probe SHA-256 `6b5b2bf87dfa838938456d166ac5d869f8d9ec4f56e84bbc74d1f103c80c49dc`.

| Capability | Actual new-track result | Meaning |
|---|---|---|
| Non-root host control | SUPPORTED: UID/GID 1001, NNP 0, seccomp 0; exit 0 | trusted host executable works |
| User namespace creation | creation call returned success | namespace existence is not sufficient capability permission |
| Explicit UID/GID mapping | SECURITY POLICY / HOST DENIAL at `/proc/self/setgroups` setup, errno 13 (direct probe does not separately label open versus write) | prerequisites for GID map could not be prepared; later direct uid_map/gid_map writes NOT RUN |
| Bubblewrap mapping/native-exec control | BLOCKED, exit 1; classified ID_MAPPING_FAILURE | no confined native ready, much less CLR/decoder startup |
| Bounded traced control | same exit and same stderr hash as untraced; `openat` EACCES | diagnostic agrees with original control; no denied unshare syscall in this trace |
| AppArmor | correlated `capable` denials and `open` errno -13 for probe/bwrap | policy denials observed; exact denied capability numbers/path are not exported |
| PID/network/mount availability independently | NOT RUN after mapping blocker | do not infer these are unsupported |
| Read-only runtime / bounded tmpfs | NOT RUN | configured candidate only |
| no_new_privs / seccomp in sandbox | NOT RUN | no successful native confined bootstrap |
| Cgroup memory/CPU/PID enforcement | NOT RUN | administrative delegation step succeeded; enforcement not tested |
| Cancellation/process tree / independent parent death | NOT RUN | no lifecycle security claim |
| No inherited synthetic env/FD / network / filesystem denial | NOT RUN | no actual escape attempt was made |
| CLR/native decoder startup / PDF/image decoding | NOT RUN | no source document processed |

| Trusted diagnostic | Wall ms | Exit / timeout |
|---|---:|---|
| Host native identity | 0.55 | 0 / false |
| Direct namespace + mapping ladder | 0.54 | 1 / false |
| Untraced bwrap control | 1.52 | 1 / false |
| Traced bwrap control | 185.35 | 1 / false |
| Decode CPU / RSS / wall / output | absent | NOT RUN |

`unprivileged_userns_clone=1`, `max_user_namespaces=63838`, and `apparmor_restrict_unprivileged_userns=1` were read without changes. The supervisor was unconfined before namespace entry. Ubuntu documents that the default unprivileged-userns policy can allow creation yet deny subsequent namespace capabilities [H8]. This is consistent with the direct probe and correlated denials. UID/GID 65534 after creation is not proof that our maps were installed: before map installation it can be the overflow/unmapped identity.

**Root-cause confidence:** high that the tested non-root namespace/ID-mapping route is blocked by host security policy before CLR/PDFium/Skia initialization; medium that Ubuntu's restricted-userns AppArmor mechanism is the complete causal explanation. The sanitized trace/audit omit exact capability identity and bwrap's failed procfs pathname; do not invent those details or claim every possible policy layer is identified. The old PR #13 run is not retroactively reclassified as proven: the new native-only control reproduces a matching pre-decoder mapping failure on this reference host.

**Disposition:** stop this host route. No AppArmor/sysctl change, custom allow-profile, privileged worker or weaker namespace option was tried. A controlled host would need a reviewed policy authorizing the required namespace capabilities for the trusted launcher while retaining confinement; that is a new environment/security decision, not automatic permission to disable protection. The finite ladder already distinguishes this from missing PDFium/Skia or a demonstrated CLR failure. There is no need for another speculative settings loop.

The reviewed cgroup fix is reused as an idea, not the previous worker: test administration prepares a delegated subtree and supervisor leaf; only the trusted test shell is administratively moved into it. Workers remain non-root. In the trusted single-threaded Python child, attachment happens before exec, closing the before-attachment descendant race. No sysctl, AppArmor profile, seccomp protection or mandatory namespace is disabled. A controlled runner would require authorized non-root user namespaces under its actual policy, cgroup-v2 memory/cpu/pids delegation, bounded mounts and observable OOM/kill events. A managed app cannot assume this host administration is available.

## PDF parser-budget feasibility

Requirements remain: 1 MiB source, ten pages, 32 MiB cumulative expanded PDF streams, 256 MiB process/job memory, five CPU seconds/page, 15 CPU seconds/job, 4M pixels/page and 20M/job, 4096 per dimension, 1 MiB text, 81,114,112-byte output bound, ten/30-second page/job wall limits, parsed object/recursion/feature restrictions. Numeric object/depth thresholds still need an explicit reviewed specification; no invented defaults are accepted.

The selected PR #13 Worker has no verified cumulative expansion hook, complete object traversal/feature validator or per-page CPU mechanism. Its raw token exclusions are not structural validation. The new track does not copy that parser into a seemingly successful new implementation. `pdf_budget` records **BLOCKED: NO_VERIFIED_32MIB_CUMULATIVE_PDF_ACCOUNTING**, based on static review, with dynamic PDF tests NOT RUN. This is an evidence gap, not a benchmark claiming all instrumentation is impossible.

| Approach | 32 MiB / graph/filter enforcement | Other obligations | Decision |
|---|---|---|---|
| Instrumented PDFium | needs counters before allocation/output across every filter/intermediate/repeated expansion, object stream and relevant embedded codec path; reference cycles and traversal depth need explicit control | source-to-binary provenance, tests/fuzzing, native security backports and licenses; malformed recovery must fail closed | possible engineering direction; no maintainable small patch proven, high fork burden |
| Verified narrow PDF subset | complete bounded grammar and canonical reconstruction can bound allowed stream/graph work; escaped names/indirect objects/cycles must be parsed or rejected structurally | embedded fonts/images and compressed/filtered/object streams may be excluded only in an approved compatibility contract; no raw token shortcuts | viable research candidate, not implemented or generally PDF-compatible |
| Maintained alternate parser/renderer | qpdf documents nesting/container/filter-chain limits, but not this end-to-end cumulative budget across a renderer; adding another parser adds attack surface | PdfPig/PDFBox/PDF.js or other renderer needs release-specific proof; image/font decode still consumes resources; MuPDF licensing requires separate decision | no reviewed drop-in satisfying every retained predicate identified |

For all approaches, encryption/actions/external references/embedded content/XFA must be handled by parsed policy; permissive recovery or ignored errors cannot turn malformed input into an accepted subset. Per-page processes can simplify CPU attribution but repeated parse/startup/retry CPU must count against the whole-job budget; kernel quota rate is not total consumed CPU. Exact CPU accounting and termination precision must be specified. OS RSS/memory caps are not expanded-stream counters. Even an exact decompression counter alone does not certify all native allocation paths.

No adversarial PDF is executed outside a proven sandbox. No toy decompression counter is represented as a complete PDF parser. Native image/PDF decoding and the 21-fixture corpus are deliberately not expanded while either hard prerequisite is blocked. Strict general-PDF acceptance is presently infeasible **with the selected uninstrumented integration**, not proven theoretically impossible for every stack.

## Dependencies and license review

This experiment adds no NuGet/npm/native decoding package. The static probe contains distribution libc runtime code; its GCC/libc versions and binary hash are evidence artifacts, not a commercial redistribution bundle. Do not ship the probe binary as a product without reviewing libc/static-link redistribution obligations.

Reference PR #13 pinned PDFium 157.0.8100 and SkiaSharp 4.153.1, six package components, 28 native hashes and zero matched NuGet advisories at the last tested executable HEAD. Its SBOM/native reports are reference evidence only, not accepted dependencies of this branch. Exact source bytes reviewed from that ref:

| Reference file | SHA-256 |
|---|---|
| Worker/Decoder.cs | `8271a06c121c4120f8d212492febd5499d9da5948bcb7b370000b5c0f8c81323` |
| decoder-sbom.cdx.json | `01e8770efb37ef093384506e94e7d9f228b8ee61dbb79cdc94dfbd70791b2130` |
| decoder-dependencies.json | `341242fa569843b462bafd8965d7df00371ea2393ebc3308219958742d234e23` |

Missing closure: exact embedded codec/font/shaping/compression versions and advisory mapping, full PDFium notices, verified Skia notices, native build options and binary provenance, active shipped/loaded assets, and self-contained .NET/OS/runtime servicing. Wrapper license expressions and package-feed zeros do not provide native clearance. Significant custom-decoder maintenance conflicts with the low-cost/low-maintenance objective; no AGPL/commercial alternative is selected or paid for. The existing .NET 8 support/upgrade planning issue remains; no application runtime is upgraded here.

## Managed hosting compatibility — documentation, not deployments

Sources checked 2026-10-17; capability findings below are platform documentation/inferences, not successful target executions.

| Objective | ACA Consumption / Jobs (primary) | ACA Sandboxes (second, platform-native alternative) |
|---|---|---|
| Boundary | Linux containers; privileged host mode unsupported [H1]; no documented writable delegated inner cgroup guarantee | documented separate sandbox boundary and programmable lifecycle [H6]; equivalence to our contract untested |
| Existing bwrap launcher | cannot copy CI sudo/cgroup provisioning; nested namespace/seccomp/mount availability requires actual host test | do not assume nested privileges; evaluate outer boundary instead |
| Identity/metadata | keep decoder free of job secrets and management identities; identity lifecycle settings can restrict availability [H4]; broker credentials separate | use Entra control plane outside parser; verify no inherited secrets, metadata or management access |
| Egress | inbound auth/private ingress does not deny egress; UDR/firewall path exists with environment restrictions [H5] | documented default-deny egress engine [H7]; verify raw IP/DNS/UDP/metadata and all protocols, not just HTTP rules |
| Filesystem | per-execution staged source only; readonly/runtime and tmp quotas need evidence | snapshots/volumes are stateful; do not resume a customer-contaminated sandbox; explicit deletion/retention tests required |
| Resource limits | allocated CPU/RAM and replica timeout/retry settings [H2] do not prove exact 256 MiB child memory, cumulative CPU or parser counters | minimum documented tier allocates 0.5 GiB [H6]; exact 256 MiB parser ceiling still needs enforceable inner control, not a waiver |
| Cancel/cleanup | execution stop APIs exist [H2]; verify all descendants, stale-result rejection, volume cleanup and cancellation latency | lifecycle stop/suspend is not deletion; verify process death and disk/memory snapshot removal |
| Prerequisites untested | region/quota, account grants, network policy, inner syscalls, data path and billing | Entra account/role availability, region/quota, lifecycle, syscall/resource policies and billing |

ACA Sandboxes is distinct from Jobs and dynamic sessions. Its current docs describe untrusted-code isolation and deny-by-default egress configuration, making it a materially relevant managed-boundary research candidate. Documentation alone does not prove tenant/file/credential/CPU/parser guarantees. Personal Microsoft accounts are documented as unsupported; no account change or role assignment is performed. Use fresh single-job sandboxes and explicit delete, not cross-tenant snapshot reuse. This candidate does not bypass the PDF-budget blocker.

## Cost model and $0 boundary

No resources are provisioned, paid provider called or billing setting changed. The test uses one bounded existing GitHub-hosted workflow; available included Actions quota is not independently readable through this connection. No extra paid quota is authorized. A future run must stay within existing included execution resources; no self-hosted/VPS purchase is implied.

Azure's published Consumption grants are 180,000 vCPU-seconds and 360,000 GiB-seconds/month; Jobs use active allocated-resource billing [H3]. Illustrative pilot assumption: 1,000 jobs/month, 60 allocated seconds/job including startup, 0.25 vCPU + 0.5 GiB => 15,000 vCPU-seconds and 30,000 GiB-seconds. This arithmetic is below those grants **only if available/unconsumed**; actual cold starts, region/SKU and usage require measurement. It is not a full free deployment estimate.

Complete estimate must include the following conditional terms, using the subscription's current region/currency rates (no authenticated quote available):

| Cost term | Pilot quantity assumption | Estimate / missing decision |
|---|---|---|
| Compute | above workload; retries capped, concurrency one initially | potentially $0 within unused grants, otherwise metered; not guaranteed |
| Source/output storage | average 0.5 MiB input, 2 MiB output, seven-day retention at 1,000 jobs/month => about 0.57 GiB steady state | storage + read/write operations; not assumed free |
| Logs | 10 KiB/job => about 10 MiB/month before platform diagnostics | ingestion/retention may add cost; cap content and retention |
| Registry/build | one ~0.5 GiB immutable image plus limited retained revisions | registry storage/pulls/build execution; public/private choice and license exposure need review |
| Queue/control calls | ~1,000 enqueue/start/result/delete cycles plus polls | request/queue/API rates and orchestration overhead unquoted |
| Egress | up to ~2 GiB exported output/month plus dependencies | regional internet/service transfer charges unquoted; avoid raw-document logging |
| Networking/security | deny-egress design, any private endpoint/firewall/NAT | can dominate pilot cost; not covered merely by compute grant |
| Sandboxes | allocation + snapshot/disk/volume lifetime, even after stop | quote/retention model unverified; stop is not automatic free deletion |

Formula: total = metered compute after grants + storage/operations + logs + registry/build + queue/control + egress + required network/security + retained sandbox state. A budget alert is not a hard spend cap. There is no supported numeric all-in dollar estimate or guaranteed $0 architecture without selecting a valid boundary and obtaining account/region rates. Both managed candidates remain unprovisioned; any required paid security feature needs separate owner approval. No recurring infrastructure cost is authorized by this report.

## Risks, recommendation and owner decisions

Final recommendation: **REDESIGN / do not expand to decoding**. The tested GitHub-hosted namespace/ID-mapping route is STOP/BLOCKED under its observed policy; retain the independent parser/CPU/native blockers. Do not disable AppArmor/userns restrictions or replace counters with a memory cap. The finite experiment must report actual syscall/audit facts, not infer production readiness from configured namespaces or native bootstrap.

Preferred direction remains Linux-only broker -> fresh credential-free isolation boundary -> bounded inert output, with server-owned source/session/hash/revision binding and existing human review/export gates. A managed native boundary can reduce OS operations only after equivalent no-egress, no-parent-data/credential, resource and lifecycle properties are tested. In-memory Studio sessions are not durable job storage; no integration/migrations are implemented.

Owner decisions still needed: (1) keep strict parser contract and fund/maintain instrumentation, or explicitly approve a narrower subset/other contract; (2) authorize a controlled non-root Linux reference environment if included hosts cannot provide required primitives; (3) select and separately authorize any target provisioning/cost; (4) approve a supported runtime/native servicing plan. Linux-only feasibility approval does not answer these decisions.

Gate 4B.2B-F can acknowledge only measured sub-properties. Historical Gate 4B.2B-1 stays PARTIAL; Gate 4B.2B-2/OCR/production deployment remain NOT STARTED. PR #13 is untouched. All new work stays on a dedicated Draft PR; no merge is authorized.

## Primary sources

- H1: https://learn.microsoft.com/en-us/azure/container-apps/containers
- H2: https://learn.microsoft.com/en-us/azure/container-apps/jobs
- H3: https://learn.microsoft.com/en-us/azure/container-apps/billing
- H4: https://learn.microsoft.com/en-us/azure/container-apps/managed-identity
- H5: https://learn.microsoft.com/en-us/azure/container-apps/user-defined-routes
- H6: https://learn.microsoft.com/en-us/azure/container-apps/sandboxes-overview
- H7: https://learn.microsoft.com/en-us/azure/container-apps/sandboxes-egress-policies
- H8: https://documentation.ubuntu.com/release-notes/24.04/ (unprivileged user namespace restrictions)
- Parser limits reference: https://qpdf.readthedocs.io/en/stable/cli.html#global-limits
- Kernel delegation: https://docs.kernel.org/admin-guide/cgroup-v2.html#delegation-containment

Reference architecture and native evidence remain on PR #13 at the immutable ref stated above; they are not copied into accepted main or relabeled as new-track PASS.


## Verification and unchanged repository boundaries

Local: static C compilation with `-std=c11 -O2 -Wall -Wextra -Werror -static` PASS; trusted identity-only control PASS (local root context, not hosted isolation evidence); five unittest methods PASS, including 22 check names x five negative status mutations and redaction/classification tests. Workflow YAML/bash syntax checked. Hosted: the same five tests PASS on exact executable HEAD; non-root controls, one bwrap control and one traced repetition executed, then stopped at the mapping denial. No full application suites were deliberately rerun because production/shared contracts are unchanged; unrelated existing workflows are branch-scoped.

PR #13 and accepted main remain unchanged. The experiment adds no decoder package and makes no native advisory-clearance claim. The generated reports distinguish static PDF review from dynamic tests. There are no resource-use measurement rows or decoded buffers to advertise. Executable HEAD and final documentation HEAD must both be reported; a documentation-only follow-up does not rewrite the historical test result.
