# Gate 5C — secure durable Studio drafts

Status: IMPLEMENTATION / PARTIAL until real SQL Server, process restart, full regression and browser evidence completes. No deployment or Gate 5C merge authorized.

Gate 5B PR #16 was explicitly approved by the owner and merged into accepted main `6469be3480115c05b9efd8e0a650bdbc27f38df9`. Feature parent `424fa56ffe56003a56fc5889459bc0e46d04dcc8` is retained in its ancestry. Decoder PRs #13/#14 remain untouched.

Drafts use the existing EF Core SQL Server dependency in an isolated `UniversalDocDraftDb`, `ude.Drafts` and `ude.ResumeLeases`, with independent migration history. No legacy footwear entity/filter/tenant resolver is used. Runtime sessions remain the existing in-memory 20-minute store and export chokepoint.

Payload format 1 explicitly stores original template/example bytes and hashes, applied schema corrections, sheet mappings, validation rules, typed manual values as invariant strings, re-admissible source bytes/hash/media type saved runtime version and a canonical configuration fingerprint. Credentials, CSRF state, runtime handles, confirmation, review and approval are excluded. Current MVP durable saves support example-based workflows only; legacy Synthetic Demo/intake functionality remains available without durable save. Sources are re-admitted with fresh IDs/timestamps/derived metadata, never recognition authority.

Encryption: AES-256-GCM, fresh random 96-bit nonce, 128-bit tag. AAD binds draft ID, owner, tenant, workflow, revision, creation/expiry, format and key ID. Payloads are at most 5 MiB serialized (base64 overhead and bounded metadata); unchanged raw session content budget is 2 MiB. At most 16 records means at most 80 MiB plaintext-equivalent envelope capacity plus metadata/DB overhead, logs/backups excluded. Database columns contain ciphertext only for document content. No plaintext workbook/source payload is logged or archived in CI. Synthetic keys are CI-only.

Configuration: `UniversalDocDrafts:Enabled=true`, `ActiveKeyId`, `Keys:<id>` (base64 32-byte secret supplied by protected server configuration), and existing `ConnectionStrings:DefaultConnection`. Optional `UniversalDocDrafts:ConnectionString` permits isolated tests or explicitly configured SQL connections. Missing/invalid configuration or unavailable database fails closed with bounded errors; no volatile fallback. Key rotation: retain old named keys to read unexpired rows, write with the new active key, update explicitly to re-encrypt without extending original retention, remove old keys only after retention/backup obligations are addressed. Never generate an ephemeral runtime key for durable data.

Database: rowversion plus expected revision; all admission/save/delete/resume-lease operations take a transaction-owned SQL application lock. Independent processes share counts and serialization. Quotas: two drafts/owner, six/tenant, sixteen/global. Retention is fixed at seven days from creation and is never extended by updates. Expiry is enforced on every read before decryption, regardless of cleanup. Cleanup physically removes at most sixteen expired drafts/leases per 30-second attempt; storage failures retry next tick. Deletes commit atomically and immediately deny subsequent reads/resumes. Physical deletion does not erase backups, transaction logs, replicas or forensic media. Operators must define backup retention and secure deletion separately.

Resume uses DB-backed 20-minute admission leases (2/6/16 across resumed drafts/processes) in addition to existing per-process active-session quotas. A final DB transaction binds the lease end to the new session expiry and rechecks draft revision/expiry; successful leases conservatively remain until expiry even if a runtime session is removed or the process exits; failed reconstruction releases its lease. Existing ordinary runtime creation is still per-process and not redesigned. Recovered sessions never inherit approval, and all source/workbook/configuration safety checks rerun before publication. A final durable revision/expiry check rejects concurrent draft changes/deletion. Draft deletion does not revoke an already independently reconstructed active session; its normal runtime authorization applies.

Save captures and advances the authorized runtime session version atomically before committing a database snapshot. Failure is never shown as Saved; the runtime version may have advanced on a failed DB save, so refresh runtime state before retry. A successful saved snapshot is a point-in-time capture; later runtime edits are unsaved. Save/update/delete use DB transactions and explicit expected draft revision; conflicts require refresh and deliberate retry. No infinite automatic retry or overwrite. Database commit acknowledgement loss can leave an ambiguous outcome: reload the metadata before retrying.

Migration is additive and feature-owned: `Gate5CDurableDrafts` (`202610190001_Gate5CDurableDrafts`). Apply only with explicit operator authorization using this context; the application does not auto-migrate a real/shared DB. Rollback: first disable draft endpoints and stop workers, back up according to approved private-data policy, then migrate this context to 0; this drops only `ude.ResumeLeases` and `ude.Drafts` and destroys all stored drafts. Existing AppDbContext entities/migrations remain untouched. Tests create random `gate5c_*` databases and apply/rollback only there.

Hosting prerequisites: supported SQL Server, migration permission during a separately approved setup, runtime table access and transaction application-lock permission, stable protected key-ring configuration, UTC clock consistency and the existing trusted authentication/membership configuration. Multi-instance normal session routing remains an operational consideration. No cloud infrastructure is provisioned. Hosted database/compute/backup/monitoring are not promised to cost $0; production cost depends on verified service quotas, region, traffic, retention and key/backup arrangements. CI uses hosted ephemeral Developer/LocalDB infrastructure only, not a production license or hosting plan.

Verification inventory and exact-head results will be added after actual CI evidence. Mandatory remaining acceptance: both-host baseline comparison, real SQL Server rollback/concurrency/restart, authenticated browser restart/login/recovery, actual XLSX reopen, dependencies and 390px layout. Any blocker keeps the PR Draft/PARTIAL. No production-readiness claim.

Implementation review corrections: real-SQL tests originally mutated an EF byte array in place (not persisted) and the uncommitted probe attempted a constraint-invalid two-byte envelope. The harness now assigns a modified ciphertext copy and stages a valid revision update before killing its process. The security/rollback assertions remain unchanged. Configuration identity normalizes runtime profile/schema version counters and sorts destinations before comparing the reconstructed profile/layout; original template/example hashes remain separately verified. An explicit stale-configuration-fingerprint case is included.

Architecture regression detected and corrected: the preserved Gate 1 architecture test reflects every public feature contract and rejects EF Core types. SQL/AES implementation adapters and their DI factory were moved to `Infrastructure/UniversalDocDrafts`; only provider-independent snapshot/store contracts remain in the UniversalDocEngine namespace. No assertion was altered. The feature owns its isolated SQL schema/context while domain/application public contracts remain free of EF types and legacy services/entities.

## Verified implementation evidence

The implementation at `ff7864c15e4f5d7089e79dcc3859cbbcd7f40646` passed [run 39214832108](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39214832108) on both hosts. This historical result precedes the final UI refresh/conflict hardening below. Both sanitized artifacts were downloaded and inspected in Work; no plaintext workbook/source binary was present.

| Evidence | Windows Server 2022 | Ubuntu 24.04 |
|---|---|---|
| Real SQL Server | LocalDB Express 15.0.4382.1 | Developer 16.0.4305.5 |
| Gate 5C discovered/executed tests | 41 PASS | 41 PASS |
| Baseline engine | 532 PASS | 532 PASS |
| Feature engine | 573 PASS | 573 PASS |
| Baseline full suite | 727 PASS + 2 inherited failures / 729 | 719 PASS + 10 inherited failures / 729 |
| Feature full suite | 768 PASS + same 2 failures / 770 | 760 PASS + same 10 failures / 770 |
| Existing browser acceptance | 43 PASS | 43 PASS |
| Initial Gate 5C browser acceptance | 13 PASS | 13 PASS |
| Real process restart, killed transaction rollback, migration rollback/reapply | PASS | PASS |
| Actual downloaded XLSX, independent openpyxl QA | PASS | PASS |

[Windows job](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39214832108/job/117936144572) · [Ubuntu job](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39214832108/job/117936144322).
[Windows metadata artifact](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39214832108/artifacts/12169478139) · [Ubuntu metadata artifact](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/39214832108/artifacts/12169471556).

The final UI hardening retains only an opaque session/draft association and the last observed draft revision in browser session storage. Every refresh reauthorizes the active session and private draft list; that association never restores payloads, approval or a Saved claim. It prevents repeated saves after refresh from silently creating a duplicate. A newer draft revision requires an explicit replacement choice, or a separate Save as a new draft action. Failed operations invalidate the local Saved indicator. The final browser suite adds repeated-save, refresh-association and conflict/no-fake-Saved checks, for 16 Gate 5C checks. No server snapshot or credential is stored in the browser.

An operator-only design-time factory supports reviewed migrations without unrelated application startup configuration. After separate authorization, supply `SHOEDOCX_DRAFT_MIGRATION_CONNECTION` through a protected process environment and run `dotnet ef database update --context UniversalDocDraftDb --project backend/ShoeExportInvoice.Api`. Rollback uses `database update 0` with the same context/project after approved shutdown/backup. No real/shared database migration was executed in this gate. The initial migration is handwritten additive SQL; future schema work requires a maintained model snapshot and manual review before automated scaffolding. Runtime keys may use `UniversalDocDrafts__ActiveKeyId` and `UniversalDocDrafts__Keys__<id>` environment entries; no production key is supplied here.

Final acceptance remains conditional on a successful run at the delivery HEAD containing this UI hardening. PR #17 records that exact final SHA, run URL, both-host results and metadata artifact hashes after verification. The workflow also records the exact SHA in `summary.json`; this avoids putting a self-referential commit SHA inside its own documentation. No mandatory test may be skipped. Inherited failures are compared by complete identity and normalized diagnostic hash; no allow-list hides new regressions. Only JSON evidence is uploaded; downloaded workbooks, source bytes and document screenshots are removed before artifact publication. Both synthetic hosts' package graphs must be subsets of the audited baseline graph.

Inherited risks remain: test-only SQLite native advisory and npm build-tool advisories are reported by the actual dependency audit, not cleared by this gate. Normal active sessions remain volatile and process-local; successful resume leases conservatively consume capacity until expiry. Retention does not imply backup/secure-erasure guarantees. Durable saves support the bounded example-based workflow, not a general document archive or OCR. No cloud resources, deployment, Gate 5D or Gate 5C merge is authorized. Gate 5C PASS is not production readiness.

## Complete changed-file inventory

- `.github/scripts/gate5c_browser.py`
- `.github/scripts/verify_gate5c.py`
- `.github/workflows/gate5c-verification.yml`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/UniversalDocDraftController.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/UniversalDocStudioController.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/DurableDraftWorkflow.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/UniversalDocRegistration.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Persistence/DraftContracts.cs`
- `backend/ShoeExportInvoice.Api/Infrastructure/UniversalDocDrafts/DraftCipher.cs`
- `backend/ShoeExportInvoice.Api/Infrastructure/UniversalDocDrafts/DraftDesignTimeFactory.cs`
- `backend/ShoeExportInvoice.Api/Infrastructure/UniversalDocDrafts/DraftPersistenceRegistration.cs`
- `backend/ShoeExportInvoice.Api/Infrastructure/UniversalDocDrafts/SqlUniversalDocDraftStore.cs`
- `backend/ShoeExportInvoice.Api/Infrastructure/UniversalDocDrafts/UniversalDocDraftDb.cs`
- `docs/UNIVERSAL_DOC_ENGINE_GATE5C.md`
- `frontend/src/features/universal-doc-engine/ExampleTemplateEditor.tsx`
- `frontend/src/features/universal-doc-engine/UniversalDocStudio.tsx`
- `frontend/src/features/universal-doc-engine/studioApi.ts`
- `frontend/tests/universalDocGate5C.e2e.mjs`
- `tests/Gate5CDemoHost/Gate5CDemoHost.csproj`
- `tests/Gate5CDemoHost/Program.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4AHost.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5CSqlFixture.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5CTests.cs`
