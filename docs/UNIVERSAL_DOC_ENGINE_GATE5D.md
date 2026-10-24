# Gate 5D — Office-friendly Excel wizard and bounded compatibility

Status: implementation acceptance **PASS** on Windows Server 2022 and Ubuntu 24.04 at `6c25665eaed1e0ca44c2eff6351bb9627ea27855`. Recommendation: **MERGE READY for the bounded Gate 5D MVP**, subject to successful exact-HEAD checks on the final documentation delivery commit. PR #18 must remain open; no merge, deployment, shared database migration, paid API/hosting or Gate 6 work is authorized.

## Baseline and observed UX issues

Accepted main `0671c99ad5743e2d234190c464fc496989a71012` is the owner-merged Gate 5C PR #17. Preflight found no existing Gate 5D branch/PR; decoder PRs #13/#14 are excluded. Branch: `codex/universal-doc-engine-gate-5d-office-wizard`.

The accepted Studio combined schema correction, multi-sheet mapping and validation policies in one engineering-oriented step, exposed field keys/profile counters and used per-field/per-row controls. API error explanations could append arbitrary backend title/details. Existing draft identity/revision, unsaved-state and export protections must be retained.

## Implementation

The default Studio is a six-step Vietnamese office wizard. Synthetic Demo/technical mode remains separate at `?mode=demo`, preserving the existing screen and all 61 accepted authenticated browser assertions. Those five scripts change only their presentation URL; CI compares their source against main after removing that query string. No security assertion is removed.

Steps: choose blank template and optional filled example; inspect real analyzed sheets/candidates; choose data fields/destinations and explicit shared fields plus arithmetic/cross-foot rules; enter typed header/table data; server-reported review/approval readiness; download after actual bytes arrive. No numeric confidence or inferred business semantics are fabricated.

Visual mapping uses a structured coordinate table, not an unrestricted workbook viewer. The analyzer does not expose trustworthy full cell values, so the UI does not invent a spreadsheet grid. Selected destinations and candidate locations come from authenticated server state. Text is rendered through React escaping; formulas are never executed by the browser and external content is not fetched.

`GET sessions/{id}/office-preview?expectedVersion=…` is informational JSON only: bounded active fields, values and expected destinations plus readiness recomputed from the same accepted confirmation/validation/review/approval policy. Owner/tenant/TTL and version are checked before processing and again before publication. No source/template/example bytes, approval token or workbook bytes are returned. No projector call or alternate export authority is introduced. Export remains exclusively `UniversalDocWorkflowOrchestrator.Export` with its final authorization/version/expiry recheck.

The table editor supports 1–20 rows with add/remove, typed date/boolean presentation, native keyboard navigation, labels and internal horizontal scroll. Printed values remain strings until the accepted server normalizer; identifiers such as 00123 stay Text. Decimal inputs require an unambiguous dot form without grouping or exponent; no floating-point conversion of printed amounts, separator guessing or automatic recomputation. Format guidance is not a client validation PASS claim; all check outcomes/readiness come from the server.

An explicit allowlist supplies Vietnamese corrective error messages. Unknown codes receive a fixed generic message; arbitrary exception titles/details and document contents are not explanations. Check IDs resolve only against known schema/rule labels and bounded row indices. Warnings and required unevaluated checks retain server meaning.

Dirty forms/files block navigation, switching modes, saving drafts and unrelated configuration operations. Inputs are disabled during pending mutations. Before-unload warns for unapplied edits. Applied session state, durable Saved state and approval are separately described. Only opaque session/draft IDs and last observed revision are retained in session storage, reauthorized on refresh; no approval or document payload is stored there. Explicit save/update/new/resume/delete reuse Gate 5C endpoints, AES-GCM, transactions, expected revisions, retention and quotas without snapshot/schema changes.

## Supported limits (unchanged)

Three mapped sheets, four total sheets, 20 output rows, five example rows, eight rules, 32 schema fields, one materialized prototype per mapped sheet; analyzed row <=1,000/column <=32, 2,000 cells, shared raw content <=2 MiB. Existing formula grammar and package restrictions remain. Macros, external links, active content, cross-sheet formulas and independent repeating collections are not enabled. No OCR/AI recognition or decoder integration.

## Synthetic compatibility corpus

| Scenario | Form / boundary | Planned evidence |
|---|---|---|
| A | Vietnamese sales invoice, genuinely ambiguous document-number slots | Full pipeline, real browser, two rows, arithmetic failure/correction, reopen |
| B | Warehouse receipt, leading-zero stock IDs and units | Full pipeline and independent reopen |
| C | Warehouse issue slip, Date/Boolean headers and merged signature | Full pipeline and independent reopen |
| D | Packing list, package counts/weights and number formats | Full pipeline and independent reopen |
| E | Two sheets with explicitly shared ordered fields | Full pipeline, authenticated browser and reopen |
| F | Three data sheets plus unchanged reference, up to 20 output rows | Full pipeline, authenticated browser, mobile and reopen |
| G | Filled example with 1–5 contiguous rows/footer relocation | Parameterized engine tests plus independent output reopen |
| H | Cross-sheet formula / far range / changed reference / merged example | Deterministic rejection, unchanged session, no output |

Generated synthetic workbooks only. Header strings/dates/booleans, line values and leading zeroes, formulas, numeric formats, borders, signature merges, sheet identity/order and reference content must be checked in both ClosedXML and openpyxl. This is a bounded factual matrix, not universal Excel support.

## Verification plan and evidence status

New backend tests exercise corpus output, preview bounds and immutability, owner/tenant denial, low-role denial, stale versions, required review, approval invalidation and row limits. Frontend model tests exercise localization/unknown-message rejection, exact text/decimal handling, dates/booleans and bounded coordinates. Real UI tests use existing login/JWT/CSRF, actual controllers, ephemeral SQL Server, actual backend restart and actual XLSX downloads. Five journeys cover first-time office use, two/three-sheet use, durable recovery, unsupported input and accessibility/responsiveness at desktop/390px including 20 rows.

CI uses the accepted baseline-versus-feature architecture, discovers inventories, compares complete identities/normalized failure fingerprints, runs all old engine/full/browser checks, all 72 accepted Gate 5C SQL cases, frontend builds/tests, real SQL migration/recovery, corpus/browser/openpyxl and dependency advisory comparison. Only JSON metadata is uploaded; workbook bytes and screenshots are private temporary synthetic verification files and removed before publication.

Local TypeScript/build and 18 model tests passed at the initial implementation stage; these were preliminary evidence only. The final Boolean regression increases the discovered model inventory to 19 passing tests. No invented completion time, click savings, usability score or real-user study is claimed.

### First exact-HEAD run and targeted correction

[Run 40061606388](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40061606388) tested `d24b192bc7c42cd080ead5ed43cec321bc64e4a0` on Windows Server 2022 and Ubuntu 24.04. Both built all required projects and passed 625/625 engine tests (604 accepted plus 21 Gate 5D), with zero skipped tests. Full-suite discovery found 822 feature tests versus 801 baseline tests: the same two Windows and ten Ubuntu inherited failures remained. All 61 inherited authenticated browser checks, including the actual Gate 5C SQL process restart and downloaded-workbook reopen, passed. Advisory comparison found no new findings.

The new Office browser suite stopped at an ambiguous `getByRole('alert')` locator because more than one alert was present. This is a Low verification defect, not evidence of a security bypass. The correction selects the exact expected unapplied-change warning while retaining the unchanged-server-version and unchanged-step assertions. No application validation or assertion was weakened. The subsequent exact-HEAD run must complete all five Office journeys and all eleven independent XLSX checks before readiness can be claimed.

Code review also found a Medium draft lifecycle defect: the initial null binding effect removed the opaque draft link before asynchronous bootstrap could restore it. After refresh, a subsequent Save could create an unintended second draft. Persistence now waits until bootstrap has restored the authorized binding. The existing real-browser regression explicitly requires the same draft ID, revision increment by one and exactly one listed draft after refresh; Saved status still requires a new successful server save. No approval metadata is persisted.

The next browser run exposed a Medium Boolean editor defect: the accepted API serializes displayed Boolean values as `True`/`False`, whereas native Office select options use `true`/`false`. Reopening the data step after server validation could therefore disable resubmission. The Office form now adapts only these two Boolean display strings on initialization; Text identifiers and Decimal strings remain unchanged. A focused model test failed before the correction and passes afterward; the real-browser journey retains correction/resubmission after a failed arithmetic check and recovery/reapproval after restart. Full final-HEAD evidence is still required.

### Completed implementation acceptance

[Run 40070948078](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40070948078) is successful on both Windows Server 2022 and Ubuntu 24.04, testing exact implementation HEAD `6c25665eaed1e0ca44c2eff6351bb9627ea27855` against main `0671c99ad5743e2d234190c464fc496989a71012`. The documentation delivery commit changes this report only; its exact SHA and rerun URL are recorded in PR #18 metadata/checks before Draft is removed.

| Evidence per operating system | Windows Server 2022 | Ubuntu 24.04 |
|---|---|---|
| Engine inventory | 625/625 pass, 0 skipped | 625/625 pass, 0 skipped |
| Gate 5C–5D focused inventory | 93/93 pass, 0 skipped | 93/93 pass, 0 skipped |
| Full feature inventory | 820 pass / 2 inherited failures / 822 total | 812 pass / 10 inherited failures / 822 total |
| Baseline inventory | 604 engine; 799 pass / 2 failures / 801 full | 604 engine; 791 pass / 10 failures / 801 full |
| Frontend | TypeScript/build, inherited draft tests, 19 model tests pass | Same |
| Authenticated browser | 61 inherited checks + 5 Office journeys / 16 check groups pass | Same |
| SQL | Gate 5C migration/concurrency/regression and actual process restart pass | Same |
| Office XLSX | 4 real UI downloads + 7 corpus exports independently reopened | Same |
| Dependencies | Official npm/NuGet sources checked; no new advisories | Same |

All 21 added engine cases pass in the engine and full inventories. Failure identities and normalized fingerprints match the accepted baseline; inherited failures are not masked. There are no skipped mandatory checks. No High/Critical defect was demonstrated in this bounded review. The two Medium lifecycle/editor defects and the Low locator defect described above are corrected; no unresolved Gate 5D blocker remains in implementation acceptance.

The authenticated Office journeys prove actual ambiguous header selection, dirty-schema navigation rejection without version mutation, required arithmetic failure and correction, owner/tenant preview denial, stale-approval blocking, refresh/update of the same SQL draft, real backend restart and fresh confirmation/review/approval, two- and three-sheet output, 20-row entry, native keyboard focus and internal scrolling at 390px, and atomic rejection of an unsupported replacement workbook. Runtime exception inventory is empty. Desktop viewport is 1360×1000; mobile is 390×844. These are synthetic acceptance checks, not a real-user usability study.

Corpus A–G exports pass. H rejects cross-sheet formulas, far destinations, changed reference structure and merged example rows with bounded errors and no workbook release. ClosedXML engine assertions and independent openpyxl checks cover leading-zero Text IDs, dates/booleans, printed Decimal values, every mapped sheet, formats, formulas, borders, relocated footer/signature merges, sheet order and unchanged reference cells. Ubuntu metadata includes UI three-sheet/20-row workbook SHA-256 `76e5f10073998a5b7441fbd108ff020e3429d9c91fb408c57850f47f8825535c`; all eleven per-OS workbook hashes are retained in the sanitized CI JSON artifacts. Only JSON metadata was published; synthetic workbook bytes and private logs were removed by the verification runner.

Security conclusion: the new preview remains informational and bounded; authenticated owner/tenant/session checks and expected-version checks gate access. Existing trusted actor resolution, CSRF, pre-binding authorization, TTL/quotas, source/template/example fingerprints, AES-GCM/SQL persistence, approval invalidation and original final export authorization remain in effect. Recovery does not restore approval authority. No browser-provided document or preview flag becomes export authority. The 23-file change inventory is confined to the Studio feature/controller, synthetic tests/host, CI scripts/workflow and this report; no decoder or legacy footwear implementation changes are included.

## Production exclusions

Gate 5D does not resolve inherited SQLite/npm advisories, runtime/dependency hardening (Gate 6A), process-local runtime sessions, SQL/key/migration/backup operations, conservative resume leases, recognition/decoder compatibility or hosting. Protected stable keys, approved retention/erasure, encrypted SQL transport, narrow permissions and a maintained future EF snapshot remain operator prerequisites. No real/shared database migration was run. This is bounded MVP merge readiness, not production readiness. Separate owner approval is required to merge PR #18.
