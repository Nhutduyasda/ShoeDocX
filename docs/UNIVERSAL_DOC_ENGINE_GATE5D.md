# Gate 5D — Office-friendly Excel wizard and bounded compatibility

Status: implementation in progress, **PARTIAL** until exact-HEAD Windows Server 2022 and Ubuntu 24.04 acceptance passes. No merge, deployment, shared database migration, paid API/hosting or Gate 6 work.

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

Local TypeScript/build and 18 model tests pass at the initial implementation stage. Backend/browser/SQL/corpus results remain unverified until GitHub Actions completes. No invented completion time, click savings, usability score or real-user study is claimed. Exact tested HEAD, CI URLs, refreshed inventories, workbook hashes and final changed-file inventory will be recorded after actual acceptance.

## Production exclusions

Gate 5D does not resolve inherited SQLite/npm advisories, runtime/dependency hardening (Gate 6A), process-local runtime sessions, SQL/key/migration/backup operations, conservative resume leases, recognition/decoder compatibility or hosting. Protected stable keys, approved retention/erasure, encrypted SQL transport, narrow permissions and a maintained future EF snapshot remain operator prerequisites. No real/shared database migration was run. Final verdict remains PARTIAL until all mandatory evidence exists.
