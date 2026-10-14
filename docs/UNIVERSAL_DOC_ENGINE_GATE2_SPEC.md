# ShoeDocX — Gate 2: Deterministic Excel Template Projector

**Status:** APPROVED FOR IMPLEMENTATION (not implemented yet; non-draft PR)
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
