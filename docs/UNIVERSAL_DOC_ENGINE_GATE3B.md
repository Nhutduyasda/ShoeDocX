# Universal Document Engine — Gate 3B

Status: PASS for the initial standard hosted comparison and local Windows verification. The final PR HEAD is revalidated before the owner-authorized merge; its run and merge SHA are recorded in PR #9 and the delivery report.

Gate 3B is a **provider-neutral extraction normalization engine**, not a completed general-purpose OCR service. All recognition observations in tests are synthetic. No PDF pixels or scanned images were recognized, no AI service was called, and no recognition accuracy is claimed.

## Source and scope

Starting baseline: `73dc382bb9b61b9d8a87d4a80bab07d478a8cb1c`, verified latest GitHub main containing accepted Gates 1, 2 and 3A. Implemented in the existing separate Windows clone `ShoeDocX-Gate3A` on `codex/universal-doc-engine-gate-3b`. Initial worktree was clean; remote branch/PR searches found no Gate 3B work to recover. The original old workspace was preserved.

Six added production files under `Features/UniversalDocEngine/Extraction/`: Contracts, IExtractionProvider, ExtractionObservationValidator, TypedValueNormalizer, DocumentExtractionAssembler and ExtractionPipeline. Existing Gate 1/2/3A contracts, legacy OCR/AI services, controllers, auth, frontend, migrations and business services are unchanged. No endpoint, DI registration, database, deployment or Gate 4.

## Contract and trust boundary

The trusted integrating caller supplies `ExtractionContext`: immutable workflow profile, authorized source metadata, and approved per-field normalization rules. `VerifiedExtractionSource` identifies an opaque source ID, SHA-256 of its actual bytes, byte length and optional page count. The caller must compute/verify these against the source and authorize tenant access. A provider-supplied hash alone is not verification; the module compares claims to this trusted context and cannot authenticate a caller or inspect source bytes it has not received. It never interprets source IDs as file paths or URLs.

`IExtractionProvider.ObserveAsync` receives the context and cancellation token, returning an immutable `ExtractionBatch`. Its envelope repeats tenant, profile ID/version, schema version, full workflow fingerprint and provider ID. Each `ExtractionObservation` contains a unique bounded observation ID, authorized source ID and matching fingerprint, explicit document/table/row grouping, row position, existing `FieldReference`, raw text, optional page/region/confidence and optional alternative ordering. Unexpected field references, enrichment targets and computation targets are rejected, not silently mapped. Adapters must reject unknown wire properties themselves; there is no JSON/HTTP deserializer or provider-specific wire format in this gate.

Only source IDs in the trusted request are accepted. Sources remain independent even if their document/table/row IDs coincide. Any malformed observation, contradictory identity, invalid region/group or admission-limit violation rejects the batch with structured fixed-message diagnostics and no drafts. The async pipeline validates context before calling the provider, checks cancellation before/after the provider, propagates provider exceptions without logging payloads, then uses the same validated assembler as direct synchronous callers. There is no unchecked public assembly route.

## Deterministic normalization

- Text is preserved byte-for-character as a .NET string, including Vietnamese Unicode, leading zeros, whitespace and formula-like strings. It is never evaluated; Gate 2 writes literal text.
- Decimals use `decimal` and invariant parsing. Supported conventions explicitly distinguish decimal dot/grouping comma from decimal comma/grouping dot. Grouping must be in groups of three. No currency, exponents or guessed separators. Conservative defaults try both conventions and accept only a unique numeric result: `1,234` is ambiguous, while `1,234.50` is 1234.50. A trusted field rule can select a convention. Exact representability is checked; overflow or precision loss cannot become rounded financial data.
- Dates use only `yyyy-MM-dd`, `dd/MM/yyyy`, `MM/dd/yyyy`, `dd.MM.yyyy`, and `yyyyMMdd`. Defaults try all these explicit formats; `01/02/2026` is unresolved when two dates result. Trusted rules may narrow the formats. Missing years, invalid dates and inferred dates are rejected.
- Booleans default to ordinal case-insensitive `true`/`false`. Trusted per-field tokens may configure alternatives such as `có`/`không`; overlapping tokens are invalid. Unrecognized tokens never become false.
- Not observed, observed empty, missing raw text, invalid type, conflicting observations, valid zero and valid false remain distinguishable. Empty observations retain a typed missing value and an issue; absent/invalid/conflicting values are not inserted into canonical data. Diagnostics and the immutable observation sidecar preserve the distinction.

Rules refer only to extraction-schema fields. Invalid rules or profile configurations stop admission. Normalization does not depend on machine culture, current date or confidence scores.

## Grouping and canonical assembly

Provider document IDs are required. Header observations are document scoped and cannot claim a table/row. Line observations require an explicit table ID, row ID and zero-based position. Row positions may have gaps, but must be unique between distinct rows and consistent for every field/alternative of a row. This subset requires one page per row; multi-page row joins require explicit correction outside this gate.

Grouping key is `(source ID, document group ID, table group ID)`. Each independent table emits its own canonical draft. Document-level header observations are shared only within their explicitly identified document; no totals are allocated between tables and rows are never concatenated across tables. If separate tables have separate totals/headers, the provider must identify separate document groups. A continuing table across pages must reuse its table identity and provide global row positions. Page-local positions that collide are rejected rather than guessed. The engine cannot establish that grouping claims match pixels; recognition/grouping accuracy remains a provider and reviewer responsibility.

Output document/table order is ordinal by IDs; line order is explicit row position. Canonical IDs use an unambiguous serialized tuple, avoiding delimiter collisions. All candidates for the same scoped field/row remain unresolved, including identical duplicate observations: neither confidence nor candidate rank selects a winner. Line-field diagnostics include explicit row ID and position, including when a field has no candidate. Candidate IDs and the full bounded observation sidecar retain raw text, fingerprints, alternatives, pages and regions for trusted review. Do not log/serialize the sidecar into public artifacts.

Every emitted value has its schema type, `FieldSource.Extracted`, `FieldReviewStatus.Unreviewed` and existing `SourceEvidence`. Source fingerprints remain in the sidecar rather than changing the Gate 1 provenance contract. Missing page/region or unknown page bounds generate explicit limitations; positions are never invented. Confidence is optional raw metadata in the documented [0,1] range and never grants correctness or review status.

Each output is the existing `CanonicalDocument` in Draft, with no review/approval evidence or acknowledged warnings. Gate 1 validation runs on it and reports missing required values, ranges, arithmetic and printed-total inconsistencies. Diagnostics expose unresolved optional fields too. `RequiresManualCorrection` is an attention signal, never approval/export eligibility. A trusted reviewer must examine the sidecar and diagnostics; Gate 1 may permit intentionally optional missing values after explicit independent review.

## Review and export boundaries

The pipeline never calls CompleteReview, Approve, Acknowledge or the Excel exporter. Gate 1 alone governs business review and export eligibility. High provider confidence, successful parsing and Gate 3A mapping confirmation cannot bypass it. Supported edits advance the existing document revision and clear old review/approval evidence.

The end-to-end test uses existing Gate 3A analysis, explicit synthetic human mapping confirmation and revalidated conversion to a Gate 2 layout. A trusted fixture binds the mapping fingerprint into a new profile before extraction. Extraction initially lacks a printed total and is denied export. A separate synthetic reviewer corrects that field through CanonicalDocument.Edit, checks Gate 1, completes review and approval, then exports through the original Gate 2 projector. Reopened output verifies typed decimal cells, leading-zero ID, literal `=literal`, expanded formulas, footer SUM, number format, borders, signature merge and an unaffected reference sheet. Subsequent edits invalidate approval.

## Resource and evidence limits

Hard ceilings: 20 sources; 50 MiB/source and 100 MiB/request; 1000 pages/source and 2000 known pages/request; 5000 observations; 100 output document/table groups; 1000 row positions/table; 10000 characters/raw field and 1,000,000 raw characters/request; 128 characters/identifier; 256 extraction fields/rules and bounded Gate 1 policy collections. Pages must be positive and within supplied bounds when known; unknown bounds are reported. Regions must have positive area wholly inside [0,1] and require a page. Decimal regexes use the nonbacktracking engine and bounded input.

Limits apply to already-materialized .NET contracts. Future transport/adapters must enforce request/JSON/token/decompression limits before allocation. No parser of hostile document bytes is added. The caller must enforce resource isolation, source-byte/page verification, provider identity, tenant and reviewer authorization. Claims of page positions, row grouping or OCR confidence are provenance metadata, not proof of source accuracy.

No external URL/file fetch, shell evaluation, macros, network calls, keys or customer-data persistence. Production services emit no logs. CI reuses the existing TRX privacy guard: raw console/TRX stays in temporary runner storage, while artifacts contain synthetic test identities, counts, outcomes and normalized message hashes, never workbook values, OCR text or provider payloads.

## Synthetic scenarios and validation

95 Gate 3B xUnit cases in `UniversalDocEngine/ExtractionTests.cs`:

- Accounting: invoice `00017`, date 2026-10-15, Vietnamese supplier, two explicitly ordered rows (2 × 6 = 12 and 1 × 7.50 = 7.50), printed total 19.50, full field metadata/provenance. Gate 1 arithmetic/cross-foot pass; deliberately changed printed values remain 999 and fail consistency checks rather than being repaired automatically.
- Warehouse: leading-zero material IDs, Vietnamese descriptions/units/notes, two rows, observed-empty note, valid zero quantity and ambiguous `1,234`. The quantity stays unresolved until a trusted comma-decimal rule selects 1.234. No synthetic signature is inferred from absent observations.
- Commercial packing: side-by-side left/right tables, continuing table across pages 3/4, a second independent document, missing totals, conflicting quantity candidates and preserved regions/order. No cross-table or cross-document row mixing.
- Negative cases: missing required/unknown/wrong-scope fields, enriched/computed targets, invalid/ambiguous decimal/date/boolean, malformed envelopes/observations/context, source fingerprints, tenant/profile/schema identity, invalid pages/regions/confidence, absent evidence, duplicate row positions, missing grouping, payload/count/document limits, null raw text, exact decimal precision, cancellation before/after provider, deterministic culture/order independence and immutable input.
- Safety: zero auto-acceptance, approval or warning acknowledgement, initial Gate 2 export denial, separate explicit review/confirmation and invalidation after edit.

## Local Windows evidence

SDK 8.0.424 and runtime 8.0.30 from the installed user-local .NET 8 toolchain; default system .NET 10 was not used. Fresh starting baseline: engine 185/0/0 and full suite 382/0/0 (passed/failed/skipped). Post-change: new Gate 3B 95/0/0, engine 280/0/0, full suite 477/0/0. Standard restore/build/test exit codes all zero. Backend build: zero errors, one NU1900 warning; restore/test compilation also report NU1900 for unavailable api.nuget.org vulnerability metadata. This is a metadata-access warning, not a reported package vulnerability, and auditing was not disabled.

Raw local TRX/logs remain outside the source tree in the user's temporary `shoedocx-gate3b-baseline` and `shoedocx-gate3b-feature` directories. They are not committed/uploaded. Existing historical hosted legacy failures are compared independently on each CI OS, not assumed to reproduce locally.

## Hosted CI and acceptance

`gate3b-verification.yml` runs standard builds and VSTest on Ubuntu 24.04 and Windows Server 2022, pinned SDK 8.0.426, `contents: read`, nonpersistent checkout credentials and no deployment. It checks out the exact PR HEAD plus exact starting SHA `73dc382bb9b61b9d8a87d4a80bab07d478a8cb1c` on each runner.

`verify_gate3b.py` adapts the existing Gate 3A design, reusing unchanged Gate 2 parsing/privacy guards. It requires baseline inventories 185 engine/382 full and feature inventories 280 engine/477 full, all 95 additions in ExtractionTests passing, every baseline identity preserved, exact full/targeted engine equivalence, no skips/aborts/zero execution, exit/TRX agreement and no new/changed legacy failures. Existing failures must match same-host identities and normalized complete diagnostic hashes. Guard unit tests exercise accepted inventories and rejection of changed/new failures, missing tests, incomplete engine, unrelated additions, zero execution and aborted runners.

The delivery report/PR records actual hosted run URLs, tested feature SHA and conditional merge SHA. Do not mark final acceptance or merge until both hosted comparisons pass on the exact final PR HEAD. No direct-xUnit substitute, force push or weakened legacy assertions.

Initial implementation run: https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38682425151, tested SHA `b199ffb40b4ff0041fe4301a5ff77d5de8d8335b`. Both standard jobs succeeded, including nine synthetic CI guard unit tests:

| Host / SDK 8.0.426 | Baseline engine P/F/S | Feature engine P/F/S | Baseline full P/F/S | Feature full P/F/S |
|---|---|---|---|---|
| Ubuntu 24.04 | 185/0/0 | 280/0/0 | 372/10/0 | 467/10/0 |
| Windows Server 2022 | 185/0/0 | 280/0/0 | 380/2/0 | 475/2/0 |

Ubuntu job: https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38682425151/job/116249717347 . Windows job: https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38682425151/job/116249717508 . Every new test and every existing engine test passed; legacy failures matched the independently executed same-host baseline by identity and normalized message hash. Hosted Windows retains two unavailable developer fixture-path tests; Ubuntu additionally retains eight historical stream failures. Neither was hidden or reproduced on local Windows (477/477 passed). Final row-specific diagnostic improvements also pass local engine 280/280 and full 477/477 checks and require a new exact-HEAD hosted run before merge.

PR and final acceptance record: https://github.com/Nhutduyasda/ShoeDocX/pull/9 . Review boundaries, safe grouping, exact normalization, provenance limitations, no unrelated modifications and standard baseline comparisons satisfy the supported Gate 3B subset. Final merge remains conditional on successful revalidation of the exact final feature SHA.

## Future provider integration

Implement a separately reviewed adapter to IExtractionProvider with authenticated source access and bounded wire parsing. Supply actual recognition evidence, trusted metadata derived from real bytes, stable cross-page grouping and documented confidence calibration. Add recognition accuracy evaluation with consented fixtures, safe execution/resource isolation and authenticated tenant/reviewer workflows. Those tasks, real OCR/PDF/image recognition, paid services, APIs, UI, persistence, deployment and later gates remain outside Gate 3B.
