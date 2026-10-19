# Gate 5A — example-based template learning

Status: implemented bounded MVP; acceptance evidence below. Review required; no automatic merge. No decoder integration or OCR. Baseline main `5ff844e3761de65b14acf9a69b8a090d43a40152`. PRs 13 and 14 remain independent and untouched.

## Existing components and additions

Gate 3A already safely reads bounded XLSX packages, analyzes adjacent slots/bold table headings/prototypes, suggests mappings against a supplied schema, validates proposals and issues human confirmation bound to template/profile/proposal hashes. It does not derive a schema. The Studio demo supplies six fields, synonyms, arithmetic checks and observations. Gate 5A adds deterministic field discovery, a session-owned schema draft, optional example comparison, correction UI and manual sample entry. It reuses the existing reader/analyzer, Gate 1 validator, Gate 3B typed normalizer and the same store, actor resolver, confirmation service, review/approval and orchestrator export chokepoint. Existing domain/projector/formula grammar and demo are unchanged.

## Supported MVP contract

No placeholders, paid inference or raw JSON editor. Only existing restricted `.xlsx`: no macros, external relationships, drawings/charts/embedded objects, tables/pivots/conditional formatting/data validation, scripts or unsupported formulas. Studio limits remain 2 MiB shared session bytes, four sheets, 2,000 used cells, row <=1,000, column <=32; 20 output rows. Template and optional example both count against the existing shared budget. No increase to Gate 2 limits.

One data worksheet with bold text column headings and exactly one materialized unmerged prototype row. Headers are horizontal adjacent materialized non-formula/unmerged slots before that table; labels must be bold or in column A. Reference worksheets remain untouched. No broader multi-sheet mapping UI, cross-sheet formulas or multi-row example alignment. Repeated output rows use the existing projector. Additional example rows or moved/changed structure are rejected, not guessed. Templates without supported evidence remain NeedsCorrection and cannot confirm/export.

Text labels normalize Unicode accents for candidate grouping (including Vietnamese đ). Duplicate labels retain competing destinations. All fields default optional; human requiredness is explicit. Actual Boolean/Date cells suggest their stored type. Fractional numeric cells suggest Decimal storage without business semantics. Integral numeric cells remain Text because numeric storage cannot establish whether a value is an identifier. Users can select Decimal; leading-zero string identifiers and numeric zero-padding evidence require Text. Formatting never infers requiredness, arithmetic rules, confidence or source accuracy. Formula destinations are excluded and independently rejected by proposal/layout validation. No arithmetic/cross-foot rules are invented for arbitrary schemas; printed totals need human review. Decimal projection retains existing Excel precision protection.

## Server lifecycle and fingerprints

`example-template` is a fixed server workflow, not executable configuration. Initial upload creates Suggested/NeedsCorrection. Optional example uses the same reader/safety limits and requires matching sheet order/names, materialized cell addresses, bold/number formats, formulas, merges, used range and defining labels. Values may differ. This intentionally strict correspondence is not universal structural equivalence. SHA-256 of actual original and example bytes is in the owner-private draft; no public URL/log/artifact contains user workbook contents. Session owner/tenant binding also binds both byte arrays and draft. The draft records the authoritative profile version and existing profile fingerprint; confirmation and export recheck that binding plus both byte hashes. No formulas are executed for learning.

`PATCH sessions/{id}/schema` accepts bounded field corrections only. Candidate indices refer exclusively to server-held evidence. An explicit manual destination can correct scope or address, but must name a materialized non-formula/unmerged template cell; line destinations must belong to the supported prototype. The final layout validator rejects overlap and header/repeat collision. Keys, labels, enum strings, omission/requiredness, unique scope/key, one sheet/prototype, destination overlap and full schema/layout validity are checked. Every field must resolve or be explicitly omitted if optional. Scope changes require compatible evidence; a header slot cannot become a line destination by changing JSON. Arbitrary expressions/workflow IDs/profile versions/approval properties are rejected.

Each successful correction advances authoritative session, draft, schema/profile and proposal revisions; clears confirmation and review/approval; revalidates retained data. Retained data that no longer fits the schema must be replaced with a manual sample. Uploading another template/example regenerates the draft and clears sample data. ReadyForConfirmation does not grant confirmation. The authenticated user's separate mapping confirmation changes the schema state to UserConfirmed and creates existing hash/revision-bound confirmation evidence. Human business review and Admin approval remain separate.

`POST sessions/{id}/manual-sample` accepts bounded header strings and 1–20 row dictionaries, never CanonicalDocument or authority. Unknown/omitted fields and invalid normalization fail. Values are Manual, unreviewed, without fabricated source evidence. Existing field editor permits subsequent edits. All exports go through `UniversalDocWorkflowOrchestrator.Export` with the original five checks, current identity/owner/tenant/TTL, expected version, mapping binding, validation, review and approval. Atomic store linearization still protects concurrent export/mutation. Source addition/removal keeps existing invalidation behavior.

## API and UI

Existing authenticated CSRF-filtered feature controller adds `/example`, `/schema`, `/manual-sample`. Template upload selects learning only for the fixed example mode. Request/member validation rejects forged authority including nested correction properties. No second store or projector endpoint exists. New UI under the existing route adds Example-Based Template beside Synthetic Demo. Field labels, types, scope, required/optional/omission and candidate selection use ordinary controls; React renders workbook text as escaped content. Six-step demo remains available. Input formats: decimal dot, ISO yyyy-MM-dd, true/false.

## Verification

New in-memory accounting, Vietnamese warehouse and commercial packing fixtures have duplicate metadata labels, leading-zero codes, formatted quantities, prototype formulas, total, merge and reference sheet. Tests cover deterministic evidence/no byte mutation, schema corrections, template/example hashes, mismatches, owner/tenant/TTL, concurrent revisions, unsupported files, leading-zero typing, authority forgery and real workbook export. New authenticated loopback browser acceptance uploads an ordinary synthetic template, resolves ambiguity/types, confirms, enters two rows, reviews/approves/exports, reopens via ClosedXML and verifies values/formulas/formats/merges/reference cells, then changes schema and verifies confirmation/approval invalidation. Fixture endpoints are only in a separate test host.

Exact-HEAD hosted .NET 8 Windows Server 2022/Ubuntu 24.04 workflow compares main and feature test identities on each host, fails on new/changed failures, missing or skipped tests; runs frontend install/build/draft checks, existing 13+11 real browser checks and the new browser acceptance. Package manifests/lockfiles are unchanged; advisory comparison still required. Actual evidence is recorded below. Final-HEAD status must be read from the PR checks, not inferred from this document.

## Gate 5B and limits

Broader multi-sheet editing, multi-row example correspondence, greater row counts, richer geometric inference and explicit business rule authoring require separate work. No universal template understanding, source recognition, decoder security acceptance, durable production sessions or deployment is claimed. No database entities/migrations, legacy footwear flows or OCR modals change.

## Recorded execution evidence

Initial complete dual-host verification at production-code HEAD `4aae9ed341f416c62e12a8f339d7d2f6e188ce71`:
[run 38999684785](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38999684785),
[Ubuntu job 117299774561](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38999684785/job/117299774561),
[Windows job 117299775007](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38999684785/job/117299775007).
Sanitized artifacts: Ubuntu `12089230229`; Windows `12088493626`.

| Evidence | Ubuntu 24.04 | Windows Server 2022 |
|---|---|---|
| Standard pinned .NET 8 builds, baseline and feature | PASS | PASS |
| Baseline engine | 409 passed, zero failed/skipped | 409 passed, zero failed/skipped |
| Feature engine | 444 passed, zero failed/skipped | 444 passed, zero failed/skipped |
| Added Gate 5A tests | 35 passed, zero skipped | 35 passed, zero skipped |
| Baseline full | 596 passed / 10 inherited failures / 606 | 604 passed / 2 inherited failures / 606 |
| Feature full | 631 passed / same 10 failure identities / 641 | 639 passed / same 2 failure identities / 641 |
| Frontend install/build/13 draft tests, both checkouts | PASS | PASS |
| Existing authenticated browser checks | 13 Studio + 11 intake PASS | 13 Studio + 11 intake PASS |
| New authenticated example browser checks | 6 PASS, no runtime errors | 6 PASS, no runtime errors |
| Official NuGet/npm advisory comparison | Unchanged packages; no new findings | Unchanged packages; no new findings |

The full-suite inherited failures are retained, not skipped or reclassified. The engine itself must be entirely green. Raw legacy error messages/workbooks are not archived. Sanitized result identities and failure hashes are in `baseline-*.json`/`feature-*.json`; counts/builds/SHAs are in `summary.json`; `dependencies.json` records the live scan, including inherited findings. Unchanged dependency findings are not a complete security clearance or production readiness claim.

`example/example-summary.json` records real browser actions, including ambiguity correction, explicit schema/mapping confirmation, export and re-review after invalidation. `example/synthetic-gate5a.xlsx` is the actual browser download; the test-only host reopens those bytes with ClosedXML. `example/example-workflow.png` is the synthetic UI screenshot. No browser response interception or mocked backend is used. Final assertion strengthening also checks numeric Amount cells, printed value, Vietnamese unit values, and actual Date/Boolean suggestion metadata. Only synthetic data is used.

Local Work verification: frontend lockfile install, TypeScript/Vite build, 13 existing draft tests, Python/JavaScript syntax and diff checks pass. No local .NET SDK is available; backend and authenticated browser execution evidence comes from the hosted Windows/Ubuntu environments, not the owner's computer.

A second complete dual-host run at `68d43d05cbad989987d96967a036453a7c1b0156`,
[39000290429](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39000290429), also passed with the same counts. Its strengthened numeric Amount, Vietnamese unit and actual Date/Boolean assertions ran successfully. Artifacts: Ubuntu `12088683494`, Windows `12089455115`. The Ubuntu summary independently records that exact feature SHA, 35 added passed tests and six passed browser checks. Its actual browser-downloaded workbook SHA-256 is `cc4ccab599dc216d7f702270251127b44e46260a78ea3a71d85c82272508f0ec`.

The final follow-up strengthens manual-destination evidence attribution (a renamed field label is never presented as workbook evidence), synthetic assertions, human-readable review labels and documentation. The final browser script adds a seventh check that verifies actual labelled values on the review screen before approval. It requires another exact-HEAD workflow run before readiness. [PR #15 checks](https://github.com/Nhutduyasda/ShoeDocX/pull/15/checks) and its final description identify the final feature SHA and run; the earlier run above must not be presented as evidence for untested later code.

## Changed-file inventory

All production edits are inside UniversalDocEngine feature directories. Existing domain, validation, extraction assembly, Excel projector/layout, legacy business logic, database entities/migrations and prior tests/browser scripts remain unchanged. Existing package manifests/lockfiles and package versions are unchanged. The new test-only host reuses reviewed packages; CI requires its resolved package set to be a subset of the baseline inventory.

| Area | Files |
|---|---|
| New backend | `TemplateUnderstanding/ExampleSchemaLearner.cs`, `Application/ExampleTemplateWorkflow.cs` |
| Existing feature API | `Api/StudioRequests.cs`, `Api/UniversalDocStudioController.cs`, `Api/StudioRequestFilters.cs` |
| Existing feature application | `Application/WorkflowContracts.cs`, `Application/SourceIntake.cs`, `Application/StudioDemoCatalog.cs`, `Application/UniversalDocSourceWorkflow.cs`, `Application/UniversalDocWorkflowOrchestrator.cs` |
| Frontend feature | new `ExampleTemplateEditor.tsx`; edited `UniversalDocStudio.tsx`, `studioApi.ts` |
| New synthetic tests | `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5ATests.cs`, `Gate5AFixtures.cs`; `frontend/tests/universalDocExample.e2e.mjs` |
| New test-only host | `tests/Gate5ADemoHost/Gate5ADemoHost.csproj`, `tests/Gate5ADemoHost/Program.cs` |
| New verification | `.github/workflows/gate5a-verification.yml`, `.github/scripts/verify_gate5a.py`, `.github/scripts/gate5a_browser.py` |
| Documentation | `docs/UNIVERSAL_DOC_ENGINE_GATE5A.md` |

There are 22 added/modified files (12 new, 10 modified). Main, PR #13 and PR #14 are not changed or merged.


## Final independent review — 2026-10-19

Reviewed source HEAD: `0ab2de0af309376808fd9031c2ca378ca321db96`, against unchanged accepted main. The final tested SHA and completed CI run are recorded in PR #15 after verification; prior runs do not certify these corrections.

### Findings and corrections

- **Medium, correctness:** ASP.NET routing accepts case variants and a trailing slash, while the upload action used a case-sensitive `/example` suffix. `/EXAMPLE` and `/example/` consequently took the template-replacement path, clearing the example binding and replacing the original template within the same authorized session. Normalize trailing slash/case consistently with routing. Two real HTTP regressions assert independent SHA-256 identities of the original and filled-example bytes. No approval or cross-tenant bypass was established.
- **Low, resource admission:** the pre-model-binding destination check omitted `/example` and did not normalize template/source route variants. The final session authorization still prevented unauthorized storage, but bounded multipart allocation/processing could occur first. Include example and normalized variants before binding. A malformed multipart request to an unavailable private destination must return the session authorization error, not a binding error.
- **Low, test quality:** CS8602 after reassigning the session on line 150. Assert the new schema draft's runtime type/non-nullness with `Assert.IsType<ExampleSchemaDraft>` before its state assertion. Nullable analysis stays enabled; no suppression or assertion removal.
- No additional demonstrated high/critical issue was found in the inspected paths. This is a targeted source/test review, not a penetration test or native decoder clearance.

### Security review matrix

| Control | Reviewed mechanism and regression evidence |
|---|---|
| Template/example authority | Immutable session byte arrays; analyzer hashes actual bytes; RequireTemplate rehashes original and example, checks draft/profile version and fingerprint. Examples require strict structural correspondence; uploads clear document state. Hash tampering tests remain mandatory. |
| Schema and confirmation | Corrections are bounded DTOs, not profiles or documents; server validates scope/key/type/omission/candidates and layout. Confirmation is a separate authenticated transition using proposal/template/profile fingerprints. Unknown authority members, including nested fields, are rejected. |
| Review/approval invalidation | CorrectSchema calls WithProfileVersion, which clears ReviewEvidence/Approval and advances data revision; confirmation also invalidates old review. Uploads clear the document. Manual/field edits create a new unreviewed revision. |
| Concurrency | Store holds the per-session lock throughout expected-version check, transition and commit. New tests force each schema-correction/approval ordering under the actual store lock, reject the stale competing request, and require no approval/review on the corrected state. Export prepares immutable snapshots then commits only if the original version/authorization/TTL still hold. |
| Owner/tenant/identity | Actor comes from authenticated claims and server membership; store enforces owner + tenant and identity expiry on every read/update, with a second expiry check before commit. No request-supplied tenant fallback. |
| CSRF/admission | Existing controller-wide authorization/antiforgery filters cover schema, sample and example; bounded request limits plus four admission slots remain. Destination ownership is checked before multipart binding for template/example/source, including route variants. |
| TTL/quota | Fixed 20-minute lifetime; 16 global/6 tenant/2 owner sessions; shared 2 MiB for template/example/sources. Failed transitions do not commit a version or exceed budget. |
| Canonical authority | Manual DTOs contain only bounded string dictionaries. Server resolves definitions, normalizes values and constructs CanonicalDocument with Manual/unreviewed/no source evidence; unknown fields fail. React sends no document/approval/profile authority and escapes labels as text. |
| Sole export boundary | Only authorized controller Export calls the runtime orchestrator projector path; it validates confirmed layout, actual template, profile/document identity, Gate 1 validation/current review/current approval, then rechecks version before returning. No new export endpoint. |
| Legacy/demo isolation | Diff remains feature/API, synthetic tests, verification and this document. Domain/projector, legacy business logic, existing tests/browser scripts and package manifests/lockfiles are unchanged. Existing Synthetic Demo runs in the mandatory real browser suite. |

Five additional regression cases raise the mandatory Gate 5A inventory from 35 to **40**; CI's exact added-test count is updated accordingly, without changing failure/skip/missing-test comparisons. Required final evidence: 449 engine tests, all 40 Gate 5A cases; baseline-matched full-suite failures; existing 13 Studio + 11 intake and seven example browser checks on both Windows Server 2022 and Ubuntu 24.04; actual XLSX reopen and no introduced dependency findings. Local Work has no .NET SDK; hosted execution is required before a merge-readiness recommendation. Functional limits and decoder separation above remain unchanged.
