# Gate 5B — bounded generic multi-sheet workflow

Status: implementation under verification in Draft PR #16. This document is not a PASS or production-readiness certificate. The acceptance decision requires exact final HEAD Windows and Ubuntu evidence, including the authenticated browser journey.

## Baseline and boundaries

Accepted main: `5dee473d6e791c5ff5f7312520c000860cdca275` (Gate 5A merge). PR #13 and PR #14 are not dependencies and must remain unchanged. No decoder, paid inference, OCR, deployment, database migration, framework upgrade or legacy business change is included.

The existing `CanonicalDocument`, `DocumentWorkflowProfile`, Gate 1 checks, Gate 2 layout validator/projector and server session store remain authoritative. Runtime export remains solely in `UniversalDocWorkflowOrchestrator.Export`.

## Mapping and field identity

The example-based schema learner groups label evidence by scope, normalized label **and worksheet**. Same labels on different sheets yield separate generated keys, never an automatic shared business field. Duplicate labels within a sheet remain ambiguous and require explicit candidate selection. Human schema correction retains exact source references; no fuzzy remapping of rules occurs.

Schema correction can establish up to three mapped data sheets with one supported prototype each. The complete multi-sheet form then replaces the entire ordered mapping collection atomically. Each source field can be deliberately selected on more than one sheet. To share fields originally suggested separately, explicitly omit the unwanted optional duplicate field and map the retained source key to the desired destinations. Omitted fields cannot also be mapped or used by checks. Every active field must have a destination; every mapped sheet needs a line region. Independent row groups and header-only sheets are unsupported.

`PATCH .../multisheet-mapping` accepts bounded sheets containing a name, prototype row, header destinations and line columns. It does not accept a layout fingerprint, tenant, actor, confirmation or approval. The server builds an `ExcelTemplateLayout`, then validates it against original bytes, the profile and `MappingProposalValidator`. Duplicate sheets, bad prototypes, overlapping destinations, header/row collisions, formulas, merges, missing sheets and unknown fields fail closed.

## Multi-row examples

One to five contiguous repeated rows can correspond to the single prototype in the original template. `ExampleRowCorrespondence` reports both SHA-256 identities, worksheet, original prototype, example row range/count, support status, footer evidence and bounded mismatch reasons. Unsupported uploads leave the existing session unchanged; the API returns escaped, bounded structural problem details.

The comparator analyzes both workbooks independently. It derives editable header/data slots only from the original template, checks contiguous rows/table headings, constructs a structural expectation with ClosedXML insertion and relative R1C1 prototype formulas, parses supported footer SUM references using library addresses, and compares actual cell sets, formulas, values outside editable slots, complete cell styles, merged ranges, widths, row heights, visibility and hidden rows/columns. This is not regex-only correspondence. Additional-row footer formulas are limited to verified one-column SUM ranges rooted at the prototype; other footer expressions are explicitly unsupported. Unsupported structures/formulas are rejected by the existing restricted XLSX reader before learning.

Confirmation repeats correspondence using the **selected** mapped sheets. Unselected sheets are compared as reference content without writable slots. All selected sheets must have the same example count. Multi-sheet examples additionally need an explicitly shared Text field whose values are nonempty, unique, identical and identically ordered across all selected sheets. All explicitly shared source fields must also contain equal typed values across their corresponding example destinations. Equal row counts alone are insufficient. This is a bounded correspondence check, not proof of real-world business identity.

Original template and example bytes remain separate immutable snapshots. Neither correspondence nor sample values authorize mapping or prove source recognition. Changing either upload invalidates the draft/profile, mapping confirmation, document review and approval.

## No-code rule contract

`PATCH .../validation-rules` replaces at most eight ordered checks. `GET .../validation-rules` returns the authorized session view. Fields come from the server schema. The form supports row Add/Subtract/Multiply/Divide with LineItem Decimal operands and a separate LineItem Decimal printed result; cross-foot sums a LineItem Decimal field against a Header Decimal printed total. No expressions, JSON editor, functions, scripts or computed source replacement are offered.

Tolerance is an absolute decimal value between 0 and 1. Requiredness and BlockingError/Warning are explicit. The server rejects unknown/omitted fields, wrong scope/type, unsupported operators, malformed/duplicate identifiers, duplicate definitions, multiple conflicting targets, self-verification, out-of-range tolerance, more than eight rules and unknown nested properties. `DocumentValidator` provides missing-input, division-by-zero, checked overflow, arithmetic and cross-foot behavior. Required unevaluated checks remain NotEvaluated and block export. Optional omitted schema fields may be NotEvaluated; they are never represented as passed.

Rules never calculate and overwrite printed amounts. Validation establishes consistency only. Human-readable row/total issue descriptions accompany check outcomes; warnings remain visible and retain the existing conservative review/export policy.

## Revision lifecycle and authorization

Upload → schema correction → complete multi-sheet mapping → rule configuration → explicit mapping confirmation → manual/synthetic sample → validation → human review → authorized approval → export.

Every content-changing configuration update increments the server profile and proposal revision, recalculates profile fingerprints, clears mapping confirmation, clears extraction evidence, changes document profile version and invalidates review/approval. Rule reordering/removal also invalidates evidence. An identical rules request does not fabricate a new content revision. Schema changes cannot silently break or rename rule references.

All mutations use the existing trusted actor, tenant/owner access, fixed session TTL, expected version and atomic store transition. Feature-scoped authenticated endpoints preserve CSRF and request-size limits. Unknown nested authority is rejected. Multipart destination authorization still occurs before binding. Export validates its authoritative snapshot, current Gate 1 eligibility and mapping conversion, uses the original Gate 2 projector, then performs the existing version/identity/TTL recheck before releasing bytes. Tests force configuration/export races to exercise that final check.

Browser refresh stores only a private session identifier in sessionStorage. Restoring it requires a fresh authenticated server read; no authority or workbook content is restored from browser storage. Discard removes the identifier. Workbook-derived text uses ordinary React text rendering.

## Limits and workbook integrity

Unchanged limits: 20 output rows, four total Studio worksheets, 2,000 used cells, row <= 1,000, column <= 32, shared 2 MiB template/example/source budget, 20-minute session lifetime and existing tenant/owner/global admission quotas. New limit: three mapped data sheets; one shared ordered line-item collection.

The projector is unchanged. Reopened output tests inspect actual values and numeric/text types, leading-zero identifiers, Vietnamese reference text, sheet order, repeated formulas, footer SUM relocation, number formats, borders and merged signature cells. Unmapped reference sheets retain supported content. Unsupported features include macros, cross-sheet formulas, names, dynamic arrays, embedded/active content, independently repeating groups and arbitrary footer transformations.

Resource tests exercise two and three mapped sheets at two and twenty rows, including three concurrent private sessions. Per-export wall time, current-thread managed allocation, output bytes and generated cell counts are recorded in standard test output/TRX. Allocation is not a peak resident-memory measurement. No row/byte/admission limits are increased based on these tests.

## Verification inventory and evidence

`Gate5BTests` contains 57 independently enumerated cases. Categories include two/three-sheet exports, shared keys, independent duplicate labels, reference preservation, missing/duplicate/header-only/invalid mappings, formula/merge rejection, one/two/five example rows, shifted footer correspondence, incompatible headers/formulas/reference content/extra rows/counts, unchanged input bytes, exact numeric rules/tolerances, missing values, overflow, division by zero, invalid configurations, nested authority, CSRF, private ownership, expiry, tampered hashes/profile, stale concurrent updates, approval invalidation and forced export races.

All existing Gate 1–5A engine cases remain in the inventory. The same-host verifier compares baseline and feature engine/full-suite identities and normalized diagnostics, requires every new Gate 5B case to pass and rejects missing/skipped/newly failing cases. Existing 13 Studio + 11 intake + 7 example authenticated browser checks run unchanged. The new ten-check real-browser journey uploads a two-data-sheet/reference workbook and a multi-row example, resolves ambiguous labels, shares fields, configures both rule types, demonstrates a failing amount blocking export, corrects/reviews/approves, downloads/reopens Excel, restores a session on refresh, invalidates approval by policy edit and checks a 390px layout and runtime errors.

Workflow: `.github/workflows/gate5b-verification.yml`, Windows Server 2022 and Ubuntu 24.04, pinned .NET SDK 8.0.426, exact PR HEAD checkout. It builds baseline/feature, compares standard VSTest inventories and NuGet/npm dependency evidence, runs frontend build/draft tests, existing browsers and new real-browser/reopen checks. Sanitized artifacts contain exact HEAD and host identity. A running workflow or an earlier successful HEAD is not final evidence.

At documentation creation, final-host evidence and resource measurements are pending. The PR must remain Draft unless both final-HEAD host jobs and every mandatory acceptance category pass. CI run URLs and the final decision belong in the PR delivery report after inspection; do not infer PASS from this implementation description.

## Production exclusions and next scope

No actual OCR/source recognition or persistent production sessions. Manual sample entry remains explicit. Excel support is intentionally restricted and not universal workbook understanding. No production deployment or commercial-readiness claim follows from passing this gate.

Gate 5C should first address durable private workflow storage and operational readiness with explicit hosting/cost decisions, then evaluate bounded source recognition separately from the unaccepted decoder work. Broader formulas, independent tables and larger row counts need separate contracts and reproducible safety evidence.
