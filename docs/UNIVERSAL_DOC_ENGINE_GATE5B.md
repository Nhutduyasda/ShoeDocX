# Gate 5B — bounded generic multi-sheet workflow

Implementation verification: PASS at `90af778ab9a905fe0b1c97e32230794302f070a0` on both required hosts. PR #16 remains subject to the exact final packaging HEAD checks and separate owner merge authorization. This is not a production-readiness certificate.

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

Both-host implementation evidence and measurements are recorded below. The PR must remain Draft unless both final-HEAD host jobs and every mandatory acceptance category pass. The PR delivery report records the exact final CI-tested packaging HEAD; documentation and browser-evidence refinements must also be verified before readiness.

## Production exclusions and next scope

No actual OCR/source recognition or persistent production sessions. Manual sample entry remains explicit. Excel support is intentionally restricted and not universal workbook understanding. No production deployment or commercial-readiness claim follows from passing this gate.

Gate 5C should first address durable private workflow storage and operational readiness with explicit hosting/cost decisions, then evaluate bounded source recognition separately from the unaccepted decoder work. Broader formulas, independent tables and larger row counts need separate contracts and reproducible safety evidence.


## Verified implementation evidence — 2026-10-19

- Starting/current main at verification: `5dee473d6e791c5ff5f7312520c000860cdca275`.
- Feature branch: `codex/universal-doc-engine-gate-5b-generic-multisheet`.
- [PR #16](https://github.com/Nhutduyasda/ShoeDocX/pull/16), never merged or deployed by this task.
- Verified implementation HEAD: `90af778ab9a905fe0b1c97e32230794302f070a0`.
- [CI run 39198900350](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39198900350).
- [Windows job 117887539426](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39198900350/job/117887539426).
- [Ubuntu job 117887539060](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39198900350/job/117887539060).

| Check | Windows Server 2022 | Ubuntu 24.04 |
|---|---|---|
| Standard pinned .NET builds, baseline and feature | PASS | PASS |
| New Gate 5B cases | 57 PASS, zero failed/skipped | 57 PASS, zero failed/skipped |
| Complete engine | 506 PASS | 506 PASS |
| Baseline full suite | 644 PASS / 2 inherited failures / 646 | 636 PASS / 10 inherited failures / 646 |
| Feature full suite | 701 PASS / same 2 failures / 703 | 693 PASS / same 10 failures / 703 |
| Missing/skipped mandatory identities | Zero | Zero |
| Frontend build and 13 draft tests, both checkouts | PASS | PASS |
| Existing real authenticated browser checks | 13 Studio + 11 intake + 7 example PASS | 13 + 11 + 7 PASS |
| New real authenticated browser journey | 10 PASS; zero runtime errors | 10 PASS; zero runtime errors |
| Downloaded workbook reopening by test host | PASS | PASS |
| Dependency comparison | No new packages/findings | No new packages/findings |

Inherited failures were matched by identity and normalized diagnostic fingerprint; they were neither skipped nor hidden. Existing risks remain: `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 High (GHSA-2m69-gcr7-jv3q), and seven npm build-tool findings (five High, two Moderate). No complete dependency security clearance is claimed.

Sanitized evidence:
[Windows artifact 12163252732](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39198900350/artifacts/12163252732),
[Ubuntu artifact 12162473649](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39198900350/artifacts/12162473649).
Both archives were downloaded and inspected in Work. The actual browser-downloaded two-sheet workbook was also reopened independently using openpyxl, verifying sheet order, identifiers, amounts, formulas, number formats, bottom borders, merged signature cells and unchanged Vietnamese reference content.

| Downloaded XLSX | Bytes | SHA-256 |
|---|---:|---|
| Windows | 8,669 | `83f34b69d0003d2cee0b2c7ed69a0bd717c3557bc77dab0ba547bb43a1d1b333` |
| Ubuntu | 8,648 | `e612d9e88b52c947bf61614d085398ebc1a26c545f15d258ac655c2b7eb40faf` |

### Resource measurements at the verified implementation HEAD

These are standard hosted-runner observations, not latency guarantees or peak resident-memory measurements. Three private sessions exported concurrently at the 20-row limit; all actual workbooks reopened correctly. The current-thread allocation counters include workbook export/reopen work.

| Host | Mapped sheets × rows | Export wall time (ms) | Managed allocation bytes | XLSX bytes | Generated/modified cells |
|---|---|---:|---:|---:|---:|
| Windows | 2 × 2 | 37.46 | 5,680,816 | 8,669 | 20 |
| Windows | 3 × 2 | 55.28 | 6,661,072 | 9,647 | 30 |
| Windows | 2 × 20, three concurrent sessions | 64.20–92.01 | 8,937,312–8,964,800 | 9,904 | 200/session |
| Windows | 3 × 20, three concurrent sessions | 89.67–134.96 | 11,738,392–11,741,344 | 11,471 | 300/session |
| Ubuntu | 2 × 2 | 93.01 | 5,674,176 | 8,648 | 20 |
| Ubuntu | 3 × 2 | 98.32 | 6,652,408 | 9,622 | 30 |
| Ubuntu | 2 × 20, three concurrent sessions | 122.64–221.27 | 8,932,488–9,074,584 | 9,819 | 200/session |
| Ubuntu | 3 × 20, three concurrent sessions | 162.55–234.69 | 11,694,832–11,712,592 | 11,350 | 300/session |

These bounded synthetic results support retaining the proposed three-sheet/20-row subset. They do not justify increasing limits or claiming production capacity.

### Exact changed-file inventory

Only the following 23 feature, test, CI and documentation files changed from the accepted main baseline:

- `.github/scripts/gate5b_browser.py`
- `.github/scripts/verify_gate5b.py`
- `.github/workflows/gate5b-verification.yml`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/StudioRequestFilters.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/StudioRequests.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/UniversalDocStudioController.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/ExampleTemplateWorkflow.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/StudioConfigurationWorkflow.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/UniversalDocWorkflowOrchestrator.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/WorkflowContracts.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/TemplateUnderstanding/ExampleRowCorrespondence.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/TemplateUnderstanding/ExampleSchemaLearner.cs`
- `docs/UNIVERSAL_DOC_ENGINE_GATE5B.md`
- `frontend/src/features/universal-doc-engine/ExampleTemplateEditor.tsx`
- `frontend/src/features/universal-doc-engine/UniversalDocStudio.tsx`
- `frontend/src/features/universal-doc-engine/WorkflowConfigurationEditor.tsx`
- `frontend/src/features/universal-doc-engine/studio.css`
- `frontend/src/features/universal-doc-engine/studioApi.ts`
- `frontend/tests/universalDocGate5B.e2e.mjs`
- `tests/Gate5ADemoHost/Gate5ADemoHost.csproj`
- `tests/Gate5ADemoHost/Program.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5BFixtures.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5BTests.cs`
