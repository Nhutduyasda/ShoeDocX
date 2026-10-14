# ShoeDocX — Gate 2: Deterministic Excel Template Projector

**Status:** PASS — standard .NET 8 CI verification passed on Ubuntu and Windows; historical blockers below are superseded by final CI evidence
**Date:** 2026-10-14
**Gate 1 accepted baseline:** `942981364648027205f3f3684aabf7614ee8d671`
**Working branch:** `codex/universal-doc-engine-gate-2`
**PR base:** `main` (Gate 1 PR #6 merged into main as `823e7d276468c8ff64711f755c21fdd9cf3250cd`)

## Purpose
Prove that a validated, reviewed `CanonicalDocument` from Gate 1 can populate a customer's explicit `.xlsx` template without silently altering formulas, layout, or data. This is the deterministic foundation for future learn-by-example / visual mapping; it does NOT implement OCR, AI template inference, user-configurable UI, production APIs, or public SaaS.

## Architectural boundary
- Implement in `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Excel/**` and minimal needed *new engine-only* configuration contracts. Use existing ClosedXML 0.104.2; do not refactor `ExcelImportExportService`, `TemplateAiParserService`, current footwear `DocumentTemplateConfig`, controllers, or frontend.
- No database migrations, persistence, auth, DI/Program.cs, file-system storage, cloud, payment, AI calls or deployment.
- Gate 1's `DocumentValidator` and `ExportEligibilityPolicy` are the authoritative policy. A projector must refuse a document for which `CanExport` is false. It must not accept caller-provided precomputed eligibility alone.
- Use finite in-memory input/output streams and ensure the source workbook is never modified. No external reference fetches or workbook code execution.
- Gate 1 is merged into main; do not change its approved legacy-safe behavior. Gate 2 implementation is on PR #7, not Draft.

## Explicit mapping format
Do NOT interpret arbitrary `ExcelMapping.Destination` strings as raw Excel addresses without a validated grammar. Gate 1 currently allows free-form strings such as `Items.Amount`; design a backward-compatible, versionable, typed layout/mapping definition for Gate 2, tied to `FieldReference` and `FieldScope`.

A minimal MVP supports:
1. A selected worksheet and literal header cell addresses (e.g. `J4`) mapped to header fields.
2. A contiguous repeating item region with start row, a prototype/template row, mapped destination columns, and configurable maximum number of generated rows.
3. Relative formula copying from the prototype row, with explicit handling of inserted rows and shifted footer/total sections.
4. Multiple independent worksheets, with each layout validated explicitly; do not assume `INV` and `PKL` sheet names.
5. A versioned template identity; require matching expected SHA-256 of workbook bytes (or an equally strong approved template identity), so an already reviewed document cannot silently be rendered into a substituted workbook.

Validate and reject nonexistent worksheets, invalid coordinates, overlapping target cells, duplicate destinations, formula overwrite attempts, unsupported row/merge scenarios, mismatched field scope or type, and out-of-bounds ranges. No arbitrary formulas, C# expressions, macros, or auto-inferred mappings.

## Deterministic projection behavior
- Preserve existing sheets, untouched cells, merged regions outside the repeat area, formatting, number formats, print settings and workbook metadata to the extent supported by ClosedXML. Document all unsupported constructs; NEVER claim 100% Excel fidelity.
- Preserve existing formula cells. When inserting repeated rows, clone row styles and supported formulas while adjusting relative references correctly. Absolute/mixed references must remain correct.
- Do not replace a formula with OCR text or a numeric value.
- Write typed `Text`, `Decimal`, `Date`, and `Boolean` values as their respective Excel cell types. Text beginning `=`, `+`, `-`, `@` remains literal text (formula injection protection).
- Missing required mapped values must fail safely; optional missing fields must follow explicit configuration (blank or leave untouched), never silently become zero.
- Reject empty line-item sets where the selected layout requires rows; do not fabricate items.
- Prevent template sample values from leaking into output rows that should have been blanked; do not erase other user-owned template content without an explicit mapping policy.
- Do not cache or persist customer workbooks. Bound input size, generated row count, and malformed file handling; reject `.xls`, `.xlsm`, external connections/macros or other unsupported formats with explicit errors rather than converting silently.
- Formula cached results may not be recalculated server-side. Tell callers Excel may need to recalculate on opening. Do not promise arithmetic correctness merely from Excel formulas.

## Acceptance tests (synthetic data only)
All tests must create their own .xlsx workbooks in memory or use synthetic test fixtures. At minimum:
- Three distinct document layouts: accounting purchase list, warehouse receipt, and generic commercial packing list; no real company files.
- Header fields, 1/2/N repeated rows, footer shifts, totals, multiple worksheets.
- Relative, absolute and mixed formula references; unmodified formula cells, preserved style/number format, merged areas outside repeating region.
- Typed values incl. Unicode Vietnamese, dates, money decimals, boolean, literal text that looks like formulas.
- Zero lines, missing fields, invalid cell references, overlapping mappings, duplicate destinations, absent sheet, unsafe formula overwrite, oversized row expansion.
- Template SHA mismatch, stale review, missing review/approval, blocking errors, stale profile; must refuse export.
- Re-export with the same inputs is stable at the *semantic workbook* level (not necessarily byte-for-byte ZIP equivalence).
- Open the generated bytes again with ClosedXML and assert workbook cell values/formulas/styles.
- All Gate 1 tests must continue to pass and no new legacy regressions may be introduced.

## Required final evidence
1. Verify and report base SHA/branch; do not accidentally branch from `main` while Gate 1 is unmerged.
2. Baseline and post-change `dotnet build` and `dotnet test` with .NET 8 SDK; targeted Gate 1+2 and full suite; report any pre-existing failures separately.
3. Detailed file list, API/contracts, supported and unsupported Excel features, security limits, test results (pass/fail/skip), examples from synthetic templates.
4. Existing non-draft PR #7 targeting `main`; owner authorizes merge ONLY after successful Gate 2 verification and no newly failing regressions. Otherwise leave PR open. No deploy.
5. Gate 2 verdict: PASS/PARTIAL/FAIL; do not start Gate 3.

## Non-goals
No AI Learn-by-Example, Excel auto-detection, Visual Point-and-Click UI, OCR, dynamic grid, actual customer template uploads, tenant persistence, subscription billing, API integration, or production deployment. These require separate gates and authorization.


## Implemented Gate 2 contract (2026-10-14)

- `ExcelTemplateLayout` v1 contains the exact SHA-256 of approved template bytes and explicitly named sheet layouts. Every sheet layout has header A1 destinations and one owned repeating row. The prototype must equal the start row. Multiple independently mapped worksheets are supported.
- `ExcelProjectionMap.ApprovedLayoutFingerprint` is an optional, backward-compatible constructor parameter. Set it to `layout.Fingerprint()` **before** completing review/approval. Gate 1 hashes this configuration with the profile. Gate 2 compares that approved fingerprint to the actual supplied layout and separately checks the template bytes. Changing either configuration invalidates the old review. Existing legacy free-form destinations are never interpreted as addresses by Gate 2.
- `ExcelLayoutValidator` checks field references/scope, coordinates, Excel boundaries, duplicate sheets/destinations, collisions, merged edited cells, prototype references, formula overwrite, row limits and unsupported workbook features.
- `ExcelTemplateProjector.Project(Stream, filename, document, profile, layout)` returns `ExcelProjectionResult` with fresh workbook bytes or structured errors. It recomputes Gate 1 eligibility internally; there is no caller-provided authorization flag. It reads a private bounded copy, restores seekable input position, and never changes input bytes. A non-seekable stream is consumed but never written or closed.
- Header coordinates use uppercase unqualified A1 grammar, e.g. `J4`, and must precede the repeating region. Columns are typed integers 1–16384. Field type comes from the workflow schema and is checked by Gate 1. Layout version is exactly 1; unsupported versions fail.
- Header missing-value policy is explicit Blank or Preserve. The repeating row is an explicitly owned region: clear all non-formula sample values, preserve styles, and write mapped values; optional item values become blank. Unmapped static labels must reside outside that row.
- Row expansion inserts rows before the footer, clones row height/styles and prototype formulas through R1C1 references, and extends footer A1 ranges ending at the prototype through the final item. Formula references in prototype cells may point only at or above the prototype row. References to rows below it fail explicitly.
- Formula grammar is deliberately limited to A1 references, numeric literals, parentheses, unary signs, arithmetic `+ - * /`, and `SUM(A1:A2)` ranges. Relative, absolute and mixed references are supported. Names, sheet-qualified references, structured references, IF/other functions, array/data-table formulas and workbook extensions are rejected. This restriction applies to every worksheet, including untouched sheets.
- No server-side formula evaluation is performed. Excel uses Auto calculation on opening; cached formula results are not guaranteed. Semantic determinism concerns values, formulas and styles, not ZIP byte identity.
- Supported tested fidelity: sheet names, untouched content, additional worksheets, number formats, row height, borders, merges outside edited rows, footer movement and page orientation. Full Excel fidelity is **not** claimed.
- Only `.xlsx` is accepted. ZIP preflight rejects macro/external-link/connection/embedding/drawing/chart/control/query parts, external relationships, duplicate parts and unsupported worksheet extensions. Tables, pivots, conditional formats, data validation and merges intersecting edited cells are rejected. Images/charts are intentionally outside v1.
- Limits: 10 MiB input, 50 MiB uncompressed package, 2000 ZIP entries, 1000 generated items globally, per-region maximum up to 1000, 200000 projected repeating cells across sheets, 32767 text characters per cell. Decimal values that cannot round-trip through Excel's 15 significant decimal digits and dates before 1900 fail explicitly. Input XML disables DTD/external resolution.
- No DI registration, API endpoint, migration, storage integration, OCR, AI, legacy footwear change or deployment was added.

## Verification and gate decision

See `UNIVERSAL_DOC_ENGINE_GATE2_VERIFICATION.md` for commands, reproducible fallback runner and limitations.

.NET 8 SDK 8.0.426 builds the API and test project with zero warnings/errors. Standard targeted and full `dotnet test` attempts fail inside VSTest **before executing tests**. The same failure occurs on the accepted Gate 1 baseline and with SDK 8.0.408. This is a verification blocker, not a passing test run.

A direct xUnit 2.5.3 runner provides supplementary evidence: **85 Gate 1 + 54 Gate 2 = 139 passed, 0 failed, 0 skipped**. Final full-suite comparison: baseline **272 passed / 10 failed / 0 skipped**; feature **326 passed / 10 failed / 0 skipped**; the failed test names match exactly. The ten baseline failures are legacy fixed-capacity stream errors (eight tests) and unavailable hard-coded Windows fixture paths (two tests).

The three synthetic scenarios cover accounting purchase data, a warehouse receipt with material code/description/unit/quantity/notes/footer, and a separate commercial packing layout with formulas and an untouched reference worksheet. Tests assert concrete typed values, Vietnamese text, unsafe literal strings, formula shifts, totals, styles, independent mapped sheets, footer merges, print orientation, source immutability, semantic repeatability, and explicit negative cases.

**Decision: PARTIAL. PR #7 stays open, non-draft, targeting main. Do not merge until standard .NET 8 testing is successfully executed and its baseline comparison confirms no new failures. No Gate 3 or deployment.**


## Final standard CI acceptance — PASS

Standard GitHub-hosted verification completed successfully on both Ubuntu 24.04 and Windows Server 2022 using SDK 8.0.426. Run: https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38532488449 . Tested implementation/CI HEAD: `9588439290c13431057374cedf3cd1281a2b2ef9`; accepted baseline: `823e7d276468c8ff64711f755c21fdd9cf3250cd`.

| Host | Baseline engine P/F/S | Feature engine P/F/S | Baseline full P/F/S | Feature full P/F/S |
|---|---|---|---|---|
| Ubuntu 24.04 | 85/0/0 | 139/0/0 | 272/10/0 | 326/10/0 |
| Windows Server 2022 | 85/0/0 | 139/0/0 | 280/2/0 | 334/2/0 |

Every feature failure matches a freshly executed baseline failure by identity and complete normalized message hash on the same host. No new legacy regression, missing/skipped test or runner abort was accepted. All 54 Gate 2 tests and 85 Gate 1 tests pass through standard VSTest in targeted and full runs on both platforms. The synthetic Excel integrity assertions therefore have standard runner evidence, not only fallback evidence.

Linux retains eight pre-existing fixed-capacity stream failures plus two unavailable Windows fixture-path failures; Windows retains only the two fixture-path failures. No legacy source/test changes were made. The earlier PARTIAL decisions are historical and superseded by this PASS. A documentation-only acceptance update is revalidated on its exact PR HEAD before the owner-authorized merge. No deployment or Gate 3.
