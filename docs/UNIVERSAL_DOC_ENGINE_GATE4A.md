# Gate 4A — Secure Workflow Orchestration and Isolated Studio

Status: implementation and local Windows security/browser verification passed. Merge remains conditional on successful Windows and Ubuntu CI at the exact final PR HEAD. The PR/delivery report records final hosted runs, feature SHA and merge SHA.

Starting baseline: `1d8c535c6a239091942a103d297ee0473c07d1fc`, verified latest GitHub main containing Gates 1–3B. Working branch: `codex/universal-doc-engine-gate-4a` in the existing separate `ShoeDocX-Gate3A` clone. Initial worktree was clean and no existing Gate 4A branch/PR was found. The original old workspace was preserved.

## Architecture and five security gates

The authenticated `/universal-doc-studio` route calls real MVC endpoints under `/api/universal-doc`. Controllers accept narrow request DTOs and call `UniversalDocWorkflowOrchestrator`. Immutable application snapshots live in `IUniversalDocSessionStore`. Gates 1–3B retain their existing contracts and implementations.

| Gate | Implementation | Evidence |
|---|---|---|
| 1. Single export chokepoint | Application/UniversalDocWorkflowOrchestrator.cs, Export | Direct export denials at five stages; stale profile/template/confirmation; post-edit denial; paused export/mutation races; source scan confirms sole new projector caller |
| 2. Backend-owned sessions | Application/InMemoryUniversalDocSessionStore.cs | Real HTTP version conflicts, concurrent PATCH, TTL/cleanup with controllable time, capacity/discard/disposal and bounded uploads |
| 3. Mapping confirmation | Orchestrator Upload/ChangeMapping/ConfirmMapping/ConfirmedLayout | Uses existing Gate 3A services; invalid references, prerequisite denials, mapping/approval race, re-confirmation does not reapprove |
| 4. Authenticated user/tenant binding | Application/UniversalDocCurrentActor.cs and Api controller/filters | Anonymous denial on all 14 routes; actual cookie/JWT login; cross-user/tenant 404; header/body/approval spoofing; unmapped 403; role 403; identity expiry; CSRF tests |
| 5. Strict isolation | Application/Api and frontend feature folders; gate4a_checks.py | Existing Program.cs has only import/registration; App.tsx only lazy isolated pathname entry; guard compares exact original contents after removing those additions |

## Actor and tenant authorization

The existing JWT middleware validates signature, issuer, audience, lifetime, security stamp, active user, role and revocation as before. No AuthController, CurrentTenantService, middleware, JWT settings, CORS or AppDbContext changes are made.

The isolated actor resolver requires an authenticated principal with exactly one authenticated NameIdentifier, role and valid unexpired `exp`. Tenant membership uses only an exact server-owned configuration lookup:

```text
UniversalDocGate4A:Users:<authenticated-user-id> = <authorized-tenant-id>
```

Example deployment-environment variable naming (substitute a real authenticated user ID under server control): `UniversalDocGate4A__Users__REAL_USER_ID=internal-demo-tenant`. No mapping is configured by default. Unmapped users fail closed with 403, including administrators. There is no default tenant, automatic enrollment or client-controlled membership endpoint. The resolver does not read X-Tenant-Id, request JSON/query/routes, localStorage or tenant claims. This is explicit single-process demo membership configuration, not a production multi-tenant membership service.

Every session operation verifies exact owner and tenant. Cross-user access is unsupported even for an Admin and returns the same 404 as an unknown session. Review requires validated Admin or KeToan role; approval and document export require Admin. Controllers and the orchestrator both enforce these permissions. A KeToan user can review their own session but cannot approve/export it; this gate does not implement reviewer delegation.

Existing HttpOnly authentication cookies are unchanged. A scoped antiforgery service issues a separate HttpOnly/SameSite=Strict cookie and an authenticated request token through GET bootstrap. Every unsafe Studio request requires X-Studio-CSRF and token validation before model binding. CSRF cookies are Secure outside Development; HTTP is supported only for the Development loopback acceptance environment. Existing global CORS/SameSite/JWT behavior is not weakened. Client JSON DTOs disallow unmapped properties, so forged approval/review/tenant/profile/revision authority objects receive 400 rather than being reconstructed as domain state.

## Session lifetime, concurrency and memory

`ConcurrentDictionary` entries hold immutable snapshots behind per-entry locks. Creation uses an admission lock; transitions check expected version, authorization and expiry, run under the entry lock, recheck expiry and atomically publish Version+1. A stale expectedVersion returns 409. Two PATCH requests using the same version cannot both succeed. No client version is assigned as authoritative state.

Session IDs are 32 cryptographically random bytes encoded as 64 hex characters. Absolute TTL is 20 minutes and is never extended by activity. A hosted cleanup task removes expired entries every 30 seconds. An owner accessing an expired retained entry gets 410; after cleanup/removal it is indistinguishable from missing and returns 404. DELETE checks ownership/version, drops references and removes the entry. Shutdown disposes the store and clears references. Managed memory is reclaimed by GC; secure physical memory erasure is not claimed.

Limits: 16 sessions/process, 6/tenant, 2/user; 2 MiB/template; four concurrent Studio requests; 16 KiB JSON limits on complex edit/create requests; supported templates additionally limited to four sheets, 2000 materialized cells, row 1000/column 32 and 32 candidates/field. Existing Gate 3A package/feature limits still apply. Multipart buffers stay in memory within the bounded request limit (per-action MemoryBufferThreshold), and template destination authorization precedes multipart model binding. There is no application workbook persistence. Gate 4A accepts one selected mapped sheet, one prototype row and at most 20 exported item rows; unaffected reference sheets are preserved.

Snapshots hold private owner/tenant/timestamps/version, unbound mapping profile and current effective profile, immutable template bytes/hash, original structure/suggestions, proposal/revision/confirmation, mapping state, extraction provenance, current CanonicalDocument and server validation. Safe view DTOs expose only bounded UI values, original synthetic evidence references, revisions and diagnostic codes; they never return template bytes, internal profiles, confirmations, review/approval objects or tenant/owner IDs. Session IDs alone do not grant access.

Sessions disappear after restart, are not shared across replicas, and provide no durable/distributed guarantees. No production durability or multi-instance deployment is supported. A distributed store is a separately approved phase.

## Mapping, data and approval transitions

Upload runs existing TemplateStructureAnalyzer and TemplateMappingSuggester and remains DraftSuggested, even for unique suggestions. Ambiguous/unresolved mappings require explicit correction. The user can correct sheet, prototype row, header destinations and item columns; MappingProposalValidator checks actual workbook bytes and Gate 2 compatibility. HumanMappingConfirmationService confirms with the authenticated actor, retaining evidence only server-side.

Confirmation is bound to the exact template SHA, unbound mapping profile fingerprint/schema, proposal revision/content and layout. A separately held effective profile binds the confirmed layout fingerprint. Before extraction, review, approval and export, conversion is revalidated against the original profile/bytes and the expected effective-profile fingerprint is checked. This avoids passing a newly bound profile as if it were the original Gate 3A confirmation profile.

Changing mapping creates a new immutable profile Version+1, advances proposal revision, clears confirmation, returns to DraftSuggested and calls CanonicalDocument.WithProfileVersion. Earlier review/approval is invalidated and validation is recomputed. Reconfirming binds the new layout/profile and still does not approve the document. Replacing an existing template follows the same invalidation rules. Original mapping suggestions remain labeled as suggestions; the corrected mapping is held independently.

Demo extraction uses bounded server-owned synthetic fixture observations through Gate 3B. It does not accept provider observations from the browser. Output is Draft/Unreviewed, with synthetic JSON source evidence and no approval. PATCH fields validates declared scope/key/row and typed normalization rules, sets Manual/Unreviewed provenance, uses CanonicalDocument.Edit and recomputes Gate 1 validation. Raw original observations remain historical evidence alongside the edited value; they are not overwritten as if the source had changed.

Review and approval are separate authorized transitions using server identity. Review refuses blocking/unevaluated required checks and unresolved warnings; the demo has no warning-acknowledgement shortcut. Approval requires current completed Gate 1 review. Field edits, template replacement, mapping changes or re-extraction invalidate prior approval. No endpoint accepts a replacement CanonicalDocument or human evidence object.

## Export linearization

Only UniversalDocWorkflowOrchestrator.Export calls the Gate 2 projector in new runtime code. It resolves the actor, reads an authorized immutable session snapshot, checks expected version/expiry/template identity and revalidates mapping confirmation/effective profile. It freshly runs DocumentValidator and ExportEligibilityPolicy, requiring CanExport. It then projects from the original stored template and current approved document.

After preparing bytes, a final atomic store update rechecks actor expiry, session owner/tenant/TTL and expected session version. If an edit/mapping change was committed during preparation, export returns 409 without returning the prepared bytes. A successful final check consumes a new session version and is the export's linearization point. Mutations after that point are subsequent operations; this does not claim that edits after a completed export can retract a downloaded workbook. The browser reloads current server state after download. Response filename is fixed `synthetic-document.xlsx` and uses the Excel MIME type, never a client-provided path.

## API contracts

All routes require authentication and server membership. All unsafe routes also require the antiforgery token. Version requests contain only `{ expectedVersion }`.

| Method / path under /api/universal-doc | Request / result |
|---|---|
| GET /bootstrap | Scoped CSRF token, workflow names and server-computed permission hints |
| GET /templates/{workflow} | Generated synthetic sample template; no document projector call |
| POST /sessions | `{ workflow }`; private session view |
| GET /sessions/{id} or /mapping | Current authorized safe view |
| DELETE /sessions/{id} | Expected version; remove ephemeral session |
| POST /sessions/{id}/template | Multipart file + expectedVersion, bounded .xlsx |
| PATCH /sessions/{id}/mapping | Expected version, sheet/startRow, header-key/cell and item-key/column dictionaries |
| POST /sessions/{id}/mapping/confirm | Expected version; explicit authenticated confirmation |
| POST /sessions/{id}/extract-demo | Expected version; server-owned Gate 3B fixture |
| PATCH /sessions/{id}/fields | Expected version, scope/key/row and raw edited value |
| POST /sessions/{id}/review | Expected version; named authorized Gate 1 review |
| POST /sessions/{id}/approve | Expected version; current review required |
| POST /sessions/{id}/export | Expected version; orchestrated generated Excel bytes |

401 means missing/invalid/expired identity; 403 unmapped identity or insufficient role; 404 unavailable private session; 410 observed owner session expiry; 409 stale version, unmet workflow prerequisite or stale evidence; 400 invalid/forged input/CSRF; 429 capacity/busy. Responses use fixed safe codes, with readable client explanations. Internal diagnostic/error values and credentials are not logged by the feature.

## Studio and reproducible synthetic demo

Frontend additions live only under `src/features/universal-doc-engine/`; styles are scoped under .uds. App.tsx adds a lazy pathname entry after the existing AuthProvider/login checks. No router replacement, Navbar/AppLayout/page changes, global styling or invoice draft state changes. Vite's existing SPA fallback supports direct refresh and its existing XNK_API_TARGET proxy supports the isolated local backend.

Three selectable synthetic workflow labels (invoice, receipt, packing) use the same deliberately small generic six-field numeric profile and two fixed rows. These are demonstration choices, not claims of industry-specific extraction accuracy. Template mapping is one supported sheet plus unaffected reference sheets. Session recovery after browser refresh is not implemented: authenticated direct refresh works, but the browser starts a new Studio view; old sessions remain private until discarded/expired. The browser never stores authoritative documents, tenants, roles or approvals.

Production main retains its existing SQL/auth startup requirements. For acceptance without touching a production database, the **test-only** `tests/Gate4ADemoHost` uses the existing AuthController, real Identity password verification, signed/validated JWT cookies, production Studio controller/DI/orchestrator/store and an ephemeral SQLite Identity fixture. It binds only loopback. EnsureCreated is confined to this disposable test fixture; no production migration/schema change is added. No Studio API responses are mocked. No alternative test authentication scheme is registered in production.

```powershell
# Use installed .NET 8; this host is synthetic acceptance tooling only.
dotnet run --project tests/Gate4ADemoHost/Gate4ADemoHost.csproj
# In a second terminal:
$env:XNK_API_TARGET='http://127.0.0.1:5288'
cd frontend
npm ci
npm run dev -- --host 127.0.0.1 --port 5176 --strictPort
```

Open `/universal-doc-studio` at that Vite origin. Fixture-only account: `admin-a` / `Studio@Test2026!`. These public synthetic test credentials do not authenticate to any production database. Other fixtures: admin-b (different tenant), other-a (different private owner), reader-a (no review/approval), reviewer-a (review only), unmapped (valid auth, no Studio membership). Signing keys are random per test host and never published.

Acceptance: create session → download/upload synthetic template → inspect suggestions → try export (409) → explicitly confirm → Demo Extraction → inspect/edit data → complete review → approve → download real Excel → edit after approval → export denied → repeat review/approval → export succeeds → correct mapping → approval invalidated → re-confirmation alone still denied. A forged tenant header from the other authenticated tenant returns 404; a forged tenant body returns 400.

## Security regression matrix

56 xUnit cases in UniversalDocEngine/Gate4ATests exercise real HTTP/auth/controller/application behavior, plus targeted internal-corruption/race/store checks. Race tests use captured snapshot/transition barriers; expiry tests inject a controllable TimeProvider, never sleep to provoke races.

| Required regression | Evidence in Gate4ATests |
|---|---|
| 1. Export without review | DirectExportCannotSkipHumanApproval, five stages |
| 2. Forged approval | ForgedAuthorityJsonIsRejected, eight authority properties |
| 3. Expired session | ExpiryAndCleanupPreventSessionReuse |
| 4–5. Cross-tenant / cross-user | CrossTenantAndCrossUserReadEditApproveExportAreDenied |
| 6. Header tenant spoof | HeaderTenantSpoofCannotSwitchAuthorizedTenantOrRevealSession |
| 7. Mapping invalidates approval | MappingEditInvalidatesApprovalAndReconfirmationDoesNotReapprove |
| 8. Field invalidates approval | FieldEditInvalidatesApprovalAndRecomputesValidation |
| 9. Stale profile | StaleServerHeldIdentitiesPreventExport(profile), exact error code |
| 10. Concurrent PATCH | ConcurrentPatchRequestsCannotSilentlyOverwrite |
| 11. Approval/edit race | ApprovalRacingWithMutationCannotPreserveOldReview(field) |
| 12. Export/mapping race | ExportRacingWithMutationRejectsPreparedApprovedSnapshot(mapping), final version-conflict code |
| 13. Template fingerprint | StaleServerHeldIdentitiesPreventExport(template) |
| 14. No frontend-approved reconstruction | ForgedCanonicalObjectsAndTenantBodiesAreRejectedAtEveryWriteBoundary |
| 15. No auto-approval | AuthenticatedSixStepWorkflow..., DirectExportCannotSkipHumanApproval(extracted) |
| 16. Unsupported workbook | UnsupportedWorkbookStructureIsRejected |
| 17. Unauthorized reviewer | RoleWithoutApprovalAuthorityCannotApproveOrExport |
| 18. TTL cleanup | ExpiryAndCleanup..., SessionAdmissionBoundsTenantGlobalCapacityAndDisposal |
| 19. Step prerequisites | BrowserCannotSkipMappingConfirmation and real-browser checks |
| 20. Legacy unchanged | NewControllerIsAuthenticatedAndOnlyOrchestratorCallsProjector and exact-file isolation CI guard |

Additional checks cover all anonymous routes, missing membership/identity, guessed IDs, stale confirmation, incorrect scope/type/row/mapping references, cross-user/missing/forged CSRF tokens, oversized uploads, bounded owner/tenant/process capacity, safe DTOs and discard/disposal.

## Verification evidence

Local Windows SDK 8.0.424: fresh baseline engine 280/0/0, full 477/0/0; feature engine 336/0/0 and full 533/0/0 (passed/failed/skipped). All 56 new security/workflow cases pass. Actual baseline/feature TRX comparison confirms complete test identities, no new failures, no skips/aborts. Standard restore/backend build succeed: zero errors and one NU1900 warning for unavailable NuGet vulnerability metadata; restore/test compilation also report NU1900. Auditing was not disabled.

Frontend lockfile installation and TypeScript/Vite builds pass before/after. All 13 existing draft tests pass. Unchanged baseline dependency audit reports eight advisories (two moderate/six high); no dependency/lockfile changes or forced fixes were introduced. Existing oversized legacy bundle warning remains. The Studio is independently lazy loaded.

Real browser automation with installed Edge and Playwright 1.62.1 passes 13 acceptance checks with zero JavaScript runtime errors: authenticated direct refresh, actual template analysis, data/evidence rendering, three real Excel downloads, unconfirmed/stale-approval denials, mapping invalidation, cross-tenant header 404 and forged tenant body 400. The interactive in-app browser also demonstrated the actual six-step flow and edit/reapproval/mapping invalidation. Screenshots show real synthetic runtime state, not mockups. Local raw TRX/logs stay outside source control in temporary gate4a-baseline/feature directories; screenshot/status evidence is under the temporary shoedocx-gate4a-browser directory.

Scoped `gate4a-verification.yml` checks exact PR HEAD and starting baseline on Ubuntu 24.04 and Windows Server 2022, SDK 8.0.426 and Node 22. It performs standard builds/tests, complete TRX inventory/regression checks (280/477 baseline, 336/533 feature), strict source-isolation checks, baseline/feature lockfile install/build/draft tests and the real browser flow against the loopback synthetic Identity/API host. All new security tests must pass. Known hosted legacy failures must match independently executed same-host baseline identities and normalized diagnostic hashes. Zero execution/skips/aborts or changed/missing/unrelated tests fail acceptance.

Browser runtime is installed in runner temporary storage, without changing the frontend lockfile. Only sanitized counts/identities/outcomes/hashes, synthetic UI screenshots and response status paths are uploaded. No cookies, JWTs, CSRF tokens, internal session IDs, real workbook values, raw logs/TRX, provider payloads or deployment secrets are artifacts. The final PR records hosted job URLs/results and conditional merge evidence.

## Limitations and production exclusions

All five gates are implemented for this narrow authenticated, single-process synthetic demo. Sessions are ephemeral and single-instance; configuration membership is explicit and server-owned, not full tenant administration. No reviewer delegation, durable storage, arbitrary workflow creation, multi-table document selection or browser-session recovery. Original templates and data are not processed by a live OCR service. Real customer data has not been validated. Parsing remains subject to existing Gate 3A supported-format/resource limits and is not a hostile-file isolation sandbox. No production durability/security certification, multi-instance deployment, production OCR, paid AI call, migration, deployment or Gate 4B is authorized or performed.
