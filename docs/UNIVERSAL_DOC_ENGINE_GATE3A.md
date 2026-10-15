# Universal Document Engine — Gate 3A

Status: IMPLEMENTED / CI VERIFICATION PENDING. No merge until Ubuntu and Windows standard CI passes.

## Baseline and source recovery

Accepted and freshly cloned remote main: `2b0c35281161d07831f5f52b6903dd7edfb3afd8`. The prior Work filesystem/checkout is no longer accessible in this session; unpushed commits from that unavailable checkout cannot be ruled out. The current accessible workspace contained only the uploaded instructions, no repository or local branches. GitHub branch/PR searches found no Gate 3A implementation; a fresh clone was clean with no local-only commits. Implemented on `codex/universal-doc-engine-gate-3a`, preserving existing GitHub history. No access to or changes in either Windows workspace.

## Components and states

All production additions live in `Features/UniversalDocEngine/TemplateUnderstanding/`; Gate 1/2 and footwear source remain unchanged.

- `TemplateWorkbookReader`: finite private byte copy, extension/package/XML checks, explicit limits and known malformed-input failures. Restores seekable source cursor; nonseekable input is consumed, never changed/closed. No filesystem persistence, external relationships, formula evaluation or code execution.
- `TemplateStructureAnalyzer`: immutable SHA-bound structure snapshots with sheet/used ranges, label cells, materialized adjacent value cells, local formula strings, bold/number-format evidence, merged ranges, candidate header slots, candidate table column positions, possible prototype rows, total/signature rows.
- `TemplateMappingSuggester`: exact normalized schema-key/approved synonym matches. Normalization folds case, Vietnamese accents/đ, spaces and punctuation. No fuzzy/semantic matching, paid inference, hidden provider or automatic confidence threshold. Every candidate carries actual label coordinates/text, rationale and evidence. Multiple candidates are retained as Ambiguous; no match is Unresolved. Every suggestion always requires review. ApprovalReference identifies caller-approved vocabulary; it is not an authentication credential. Unique candidates may form an **unconfirmed draft** only.
- `MappingProposalValidator`: re-reads the actual workbook; ignores analysis as authority. Checks proposal version/revision, template SHA, complete workflow fingerprint, profile validity, complete field resolution or explicit optional omissions, prototype evidence and unchanged Gate 2 validator constraints. A temporary in-memory validation profile satisfies Gate 2's fingerprint check, but it is never returned, persisted or treated as approved. No profile/document approval occurs here.
- `HumanMappingConfirmationService`: requires a named reviewer from a trusted caller, revalidates proposal, returns immutable confirmation with an internal-only constructor. Binds exact template bytes, full profile/schema/tenant identity, proposal revision, proposal content and final mapping fingerprints. Conversion revalidates all identities and current workbook. A revised/tampered proposal or changed profile/template cannot reuse a confirmation.

Distinct states are: **candidate suggestions → draft proposal → structurally valid proposal → explicitly human-confirmed mapping → caller-bound approved Gate 2 layout**. Structural validity or a UniqueCandidate never grants confirmation. Human correction may supply a different explicit layout; the actual template and Gate 2 constraints are still validated. Required fields cannot be omitted; optional unresolved fields require explicit omission declarations. Revisions are immutable, and `Revise` advances revision. Even changing content without advancing revision invalidates the full proposal fingerprint.

## Supported deterministic subset

This is a narrow heuristic for supported customer-style synthetic templates, not universal Excel inference. Candidate tables require at least two bold textual column headings. Data prototypes follow immediately; formatting-only materialized cells count as evidence, but arbitrary nonexistent blank cells are not invented. Multiple consecutive candidate sample rows remain ambiguous and are rejected for conversion because Gate 2 owns one prototype row. Human correction cannot silently overwrite a prepopulated multirow table. Missing/inconclusive structure must be corrected outside this isolated feature or remain unresolved.

Header candidates are materialized right/below adjacent cells, not formulas or merged destinations. Competing slots are preserved, never ranked into approval. A source field may be repeated on explicitly selected independent sheets, but competing suggestions require human resolution. Output layout remains the unchanged Gate 2 v1 contract (headers before one owned item row per mapped sheet).

Limits: `.xlsx` only, 10 MiB compressed input, 50 MiB expanded package, 2000 ZIP entries, 20 sheets, 20000 analyzed materialized cells, last used row ≤10000 and column ≤256. Narrower analysis limits are explicit; Gate 2 additionally enforces its original 1000-item/per-region/coordinate/edited-cell limits when validating and exporting. Tables/pivots, conditional formatting, data validation, macros, external links/relationships/connections, embedded objects, drawings/charts, array/data-table formulas, unsupported worksheet extensions and unsupported formulas fail safely. Merged headings and unaffected footers are recorded/preserved; edited merged cells fail.

Formula support is exactly Gate 2's local A1 arithmetic/SUM subset, with relative/absolute/mixed references. This feature reads formula expressions only; no formula evaluation or cached-result promises. Final exports may need Excel recalculation on opening. It does not claim full Excel fidelity.

## Gate 2 integration and review lifecycle

1. Analyze exact workbook bytes and suggest schema mappings with approved aliases.
2. Resolve every ambiguous/unresolved field or explicitly omit optional fields. Build a versioned proposal for the selected single-row layout.
3. Validate and confirm as a named, authorized mapping reviewer.
4. Convert using the same pre-binding workflow profile; changed profile/schema/template/proposal invalidates confirmation.
5. Trusted caller explicitly binds the returned layout fingerprint into a new workflow profile snapshot. This is an independent integration decision, not a side effect of confirmation. The profile change intentionally invalidates old business-document review evidence.
6. Independently review/approve the business CanonicalDocument against that bound profile.
7. Call the existing ExcelTemplateProjector. It re-evaluates Gate 1 eligibility, handles expansion, formulas and safe typed writes, then output is reopened/asserted.

Mapping confirmation never calls CanonicalDocument.CompleteReview or Approve, and never authorizes export. No second exporter, DI registration, production API, database or UI was introduced. Authentication, tenant authorization and reviewer identity must be enforced by the integrating trusted caller; this domain feature cannot authenticate a name. Confirmation objects are in-process domain evidence, not signed portable credentials. Do not accept client-forged serialized confirmations in a future API.

## Synthetic examples and tests

46 new cases in `UniversalDocEngine/TemplateUnderstandingTests.cs` cover independent accounting, Vietnamese material receipt and commercial multi-sheet packing layouts. End-to-end outputs are reopened and values/types, literal formula-like text, Vietnamese Unicode, row expansion, relative/absolute/mixed formulas, totals, number formats, borders, signature merges and reference sheets asserted. All fixtures are synthetic in memory.

Negative cases include missing/ambiguous labels; human correction; unknown fields/wrong scope; duplicate header/item destinations; header-region collision; invalid cells/sheet/prototype/row limits; unsupported merges/tables/formulas/package parts; malformed/oversized/sparse workbooks; missing confirmation; blank reviewer; tampered revision/layout/template/profile/schema; explicit optional omissions; deterministic repeat analysis and input immutability. A draft business document remains denied even after mapping confirmation.

## CI verification

`gate3a-verification.yml` is scoped to the in-repository Gate 3A branch/PR targeting main and uses `contents: read`, nonpersistent checkout credentials and no deployment secrets. Hosted Ubuntu 24.04 and Windows Server 2022 use pinned SDK 8.0.426, exact PR HEAD and accepted starting main. Standard builds and targeted/full dotnet test run on both checkouts within each runner. No direct-xUnit-only acceptance.

`verify_gate3a.py` reuses the unchanged Gate 2 TRX parsing/privacy guards and independently checks baseline engine 139, feature engine 185, full inventories 336/382, every new Gate 3A case passing, no disappeared baseline tests and no new/changed legacy failures. Complete execution/counters/exit codes are mandatory; aborts, zero execution, skips and unrelated runner errors fail. Expected legacy failures are compared against that same host's fresh baseline, never hidden by continue-on-error. Artifacts contain identities/outcomes/counts/normalized message hashes; legacy workbook values, raw console and full TRX are not published.

The user's Windows local 336/336 baseline report is separate evidence from hosted runners and is not assumed reproducible there. Gate 2 hosted baseline was Ubuntu 326/10 and Windows 334/2; current CI must determine both baseline and feature afresh. Known hosted legacy failures concern fixed-capacity memory streams on Linux and unavailable hard-coded Windows developer fixture paths. No legacy test/source edits to hide these.

Work restore/build attempts encountered unavailable NuGet packages/network; hosted CI is the authoritative standard verification. Results and CI URLs will be recorded once available. Pending CI is not PASS.

## Windows Agent handoff

Use GitHub as the source of truth. Do not restart Gate 3A or copy files/ZIPs from Work. In the separate clean clone `C:\Users\nhutd\Documents\antigravity\clever-faraday\ShoeDocX-Gate3A`, preserve any unexpected local edits and fetch before checkout. Do not change the original workspace.

```powershell
Set-Location 'C:\Users\nhutd\Documents\antigravity\clever-faraday\ShoeDocX-Gate3A'
git status --short
git fetch origin --prune
# If merged: validate latest origin/main in a local audit branch.
git switch -c audit/gate3a-accepted origin/main
# If unmerged instead: git switch --track origin/codex/universal-doc-engine-gate-3a
git rev-parse HEAD
dotnet --version
dotnet restore tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj
dotnet build backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --filter 'FullyQualifiedName~UniversalDocEngine' --logger 'trx;LogFileName=gate3a-engine.trx'
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --logger 'trx;LogFileName=gate3a-full.trx'
```

Use the final pushed/merge SHAs and PR URL from the delivery report. Expected engine count is 185 (139 Gate 1/2 + 46 Gate 3A). Full inventory is 382; locally available old fixtures may yield all passes, while hosted failures are allowed only if reproduced on the same-host baseline. Do not assume unavailable legacy fixture paths were fixed. If a local audit branch already exists, inspect it and use another new audit name rather than forcing/resetting it.

## Remaining scope and risks

No real AI inference, OCR/PDF extraction, fuzzy label semantics, Learn-by-Example, web UI, upload endpoint, tenant persistence, production integration, deployment, Gate 3B or Gate 4. Deterministic heuristics can miss unformatted/nonadjacent layouts, and multilingual label equivalence depends on the caller-approved vocabulary. Workbook parsing is bounded but ClosedXML is not a hostile-file sandbox; a production integration needs authenticated admission, execution/resource isolation and explicit supported-format policy. No workbook is sent to an external AI provider and no paid AI call is made.
