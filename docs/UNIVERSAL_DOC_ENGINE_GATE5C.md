# Gate 5C — secure durable Studio drafts

Status: independent final review PASS at corrected application HEAD `51370b42dcc6eb1d24c8a72394cfd2718e088c66`. Three Medium and two Low findings corrected. MERGE READY for the bounded MVP subject to the exact final documentation-delivery CI recorded in PR #17. No merge or deployment authorized.

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


## Final supported-scope verification — 24 October 2026

The complete implementation and final UI at `6d01c04f467db83d5e3b97640f0eb682fea03522` passed [run 40049598233](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40049598233) on both hosts:
[Windows job 120739059114](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40049598233/job/120739059114) and [Ubuntu job 120739059412](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40049598233/job/120739059412).
This supersedes the earlier pending implementation status. The final delivery changes only this evidence documentation and the artifact upload path, which now accepts JSON directly in the workflow as well as removing binaries in the verifier. Its exact successful delivery HEAD/run is recorded in PR #17 and `summary.json`; application/test source remains identical to this verified revision.

| Mandatory evidence | Windows Server 2022 | Ubuntu 24.04 |
|---|---|---|
| Standard backend/frontend builds, 13 draft tests | PASS | PASS |
| Gate 5C, actual discovered/executed SQL tests | 41 PASS, zero skipped | 41 PASS, zero skipped |
| Complete engine | 573 PASS | 573 PASS |
| Feature full suite | 768 PASS + same 2 inherited failures / 770 | 760 PASS + same 10 inherited failures / 770 |
| Baseline full suite | 727 PASS + 2 inherited failures / 729 | 719 PASS + 10 inherited failures / 729 |
| Existing authenticated browser checks | 43 PASS | 43 PASS |
| Final Gate 5C authenticated browser checks | 16 PASS, zero runtime errors | 16 PASS, zero runtime errors |
| Real SQL Server, migration apply/rollback/reapply | PASS (LocalDB Express 15.0.4382.1) | PASS (Developer 16.0.4305.5) |
| Separate-process save/recovery, killed transaction rollback | PASS | PASS |
| Real backend restart, re-login, recovery and renewed approval | PASS | PASS |
| Actual XLSX download and independent openpyxl reopen | PASS | PASS |
| New packages / new dependency findings | 0 / 0 | 0 / 0 |

Same-host complete identities and normalized diagnostic hashes were checked; there are no missing or skipped mandatory tests and no newly failing application test. Existing advisory data was refreshed: SQLitePCLRaw.lib.e_sqlite3 2.1.6 High remains; the current npm audit reports five inherited build-tool findings (two High, three Moderate), identical for baseline/feature. Historical seven-finding reports above are not substituted for current audit results.

The browser proves repeated saves update one draft, refresh keeps only reauthorized convenience metadata, and a concurrent save is rejected until the user reloads and explicitly chooses replacement. It also proves actual cross-owner/tenant denial, loss of old approval, renewed confirmation/review/approval, 390px controls, deletion and denial of later resume. Original/example hashes, two/three-sheet recovery, rule recovery, typed manual values, leading-zero identifiers and source re-admission are checked against real persisted state. Corrupt ciphertext, swapped identities, unsupported format, wrong/missing keys, bad workbook/schema/rules/fingerprints and expiry fail closed. Database-backed quotas and expected revisions are exercised with competing connections; process probes verify persistence across independent application processes and interrupted transaction rollback.

Both artifacts were downloaded and independently inspected in Work:
[Windows 12543038116](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40049598233/artifacts/12543038116) and [Ubuntu 12542658740](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40049598233/artifacts/12542658740). They contain only JSON metadata, with no plaintext workbook/source binary. The downloaded workbook is reopened in CI before removal; only its hash and QA result are published.

| Actual downloaded XLSX | Bytes | SHA-256 |
|---|---:|---|
| Windows | 8,669 | `f383df3f389cf45be2893c3601bece5640deb26d0771cedc93acb20dd616407a` |
| Ubuntu | 8,648 | `bd30c7a241b902d8c4975554e54e4cd869b1a6471f67d56ed9cc633cce5dcf47` |

Reopen assertions cover both data sheets, leading-zero identifiers, quantities/prices/printed amounts, row/footer formulas, numeric formats, borders, signature merges, worksheet order and the unchanged Vietnamese reference sheet. No actual workbook bytes are retained in GitHub artifacts.

Recommendation: **PASS for the bounded Gate 5C MVP**, with the final delivery SHA subject to its successful exact-HEAD rerun recorded in PR #17. Retention, conservative resume leases, process-local ordinary sessions, explicit operator migration/key setup, backup/secure-erasure limitations and inherited advisories remain as documented. This is not production readiness. No real/shared database migration, paid cloud provisioning, deployment, Gate 5D, decoder changes or Gate 5C merge was performed.

## Independent final security review — 2026-10-24

Review baseline: main `6469be3480115c05b9efd8e0a650bdbc27f38df9`; starting PR #17 HEAD `75ded555d00975116c794845632ddf8e2b2dd3c4`. Main and PR were fetched again; no newer feature commits or review comments were present. The original exact-HEAD run 40050607155 remains historical evidence, not evidence for these corrections. No merge, deployment, shared-database migration, paid hosting, Gate 5D or decoder change is part of this review.

### Findings and minimal corrections

- **Medium — incomplete configured drafts could save successfully but fail recovery.** Resume invoked the example correspondence confirmation check while rebuilding an unconfirmed mapping. A valid pending schema with distinct fields on both worksheets legitimately needs further common-identifier mapping corrections. Recovery now reconstructs that pending state; unchanged `ConfirmMapping` still rejects it with `EXAMPLE_SHARED_ORDERED_IDENTIFIER_REQUIRED`, and export cannot proceed. Workbook structure/hash admission, schema/layout/rule validation and configuration fingerprint checks remain mandatory. This changes no accepted export structure.
- **Medium — missing and observed-empty manual values could save successfully but fail recovery.** Snapshot capture serialized display fields, including missing values, as null; normalization correctly rejected null on recovery. Capture now excludes absent document fields and encodes an existing typed empty value as an empty string. Recovery preserves the distinction without inventing values, reviews or approvals. Old malformed snapshots still fail closed rather than guessing their intended missing/empty meaning.
- **Medium — SQL padded equality could alias distinct trusted owner/tenant strings.** BIN2 collation does not disable SQL trailing-space comparison padding. Durable actor validation now rejects identifiers ending in ASCII space before any ownership lookup, decryption or mutation. Names are never trimmed or normalized into another identity. Runtime identity semantics remain unchanged; operators must use unambiguous trusted membership identifiers. Focused real-SQL regressions cover both owner and tenant aliases.
- **Low — unapplied Data review edits were absent from the durable-save guard.** Per-field dirty reporting now disables durable save and clears the displayed Saved claim until the edit is applied or restored. Browser regression checks the actual control and status text.
- **Low — malformed SQL connection configuration could escape bounded configuration errors.** Validate connection-string syntax in isolated provider registration and return `DRAFT_CONFIGURATION_UNAVAILABLE` (503). No fallback, credential logging or connection retry is introduced.

The first two regression tests were committed before application corrections at `a161d1a3668ccf60d8bdad3a9905435ada19a7e2`. [Run 40052950453](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40052950453) reproduced both failures on real Windows LocalDB and Ubuntu SQL Server: 47 pass, exactly two fail, zero skipped. Errors were respectively `EXAMPLE_SHARED_ORDERED_IDENTIFIER_REQUIRED` and `DRAFT_PAYLOAD_INVALID`. This intentionally red run is retained as root-cause evidence, not acceptance evidence.

The additional owner/tenant padding regressions were committed without their correction at `c173f2215acead2cecc4b43275ac7fd6ba1f4bb9`. [Run 40053815653](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40053815653) reproduced both aliases on real Windows LocalDB and Ubuntu SQL Server (68 pass, two fail, zero skipped on each): the ownership boundary accepted the differently spelled trusted identities. The minimal correction rejects trailing ASCII spaces rather than trimming an identifier into another identity. The strengthened regression also asserts that the raw BIN2 SQL predicate matches the padded identity, while the application rejects it before any ownership lookup/decryption/mutation. Corrected inventories are recorded below and in PR #17.

The added browser selector initially matched both active and omitted worksheet fields with the same Quantity label. It now selects the active Manual row; no assertion or acceptance requirement was removed.

### Review boundaries and security conclusions

AES-256-GCM retains a fresh cryptographically random 12-byte nonce and 16-byte tag. AAD binds draft ID, owner, tenant, workflow, revision, creation, expiry, format and key ID. Additional tests mutate the remaining AAD fields and exercise active-key rotation: historical-key reads, explicit re-encryption, unchanged retention, and stale-key rejection. UpdatedAt is display/sort metadata and is not authenticated; it grants no authority. Format 1 is checked both outside and inside the authenticated payload; unknown properties and unsupported versions fail closed. Keys remain protected operator configuration, with no generated production default. Historical keys must be retained only through the approved live-data/backup obligations, then removed by the operator; automatic key deletion is outside this MVP.

SQL operations retain short-lived contexts, a transaction-owned global application lock, expected revisions and SQL rowversion. A lock timeout/storage exception is bounded as 503; global serialization is deliberately conservative at the existing 2/6/16 limits and is not a high-throughput design. Focused SQL tests additionally cover a real application-lock timeout without a write, a stale rowversion writer outside the application lock, tenant/global resume capacity, revision replacement and an expired lease. Quotas, save/delete races, interrupted uncommitted writer rollback, database outage and additive migration apply/rollback/reapply use real isolated SQL Server. A lost commit acknowledgement remains ambiguous and never returns Saved: refresh runtime version and private draft metadata before deliberate retry. No migration is run against a real/shared database. The handwritten initial migration has no maintained EF model snapshot; future migration scaffolding must first establish/review that snapshot and reconcile the explicit binary collations and constraints.

Save-point regression matrix covers A template-only unresolved suggestions, B example without corrections/confirmation, C corrected but still unconfirmable schema, D multi-sheet configuration, E rules, F unapproved manual sample, and G prior approval. Each uses a fresh runtime store and actor after saving, compares original/example bytes and server mappings/rules/values, and proves no confirmation/review/approval survives and export is blocked. Every A–G point also has a separate-process save/exit/recovery probe, in addition to fresh-store comparisons and the real authenticated browser restart flow. Schema corrections are atomic: partially applied browser corrections are never saved as server state; nullable unresolved candidates remain suggestions until a complete correction is accepted. Missing/empty values have an additional focused regression.

Resume leases remain additional conservative DB admission, not replacement runtime authority. Failed reconstruction releases leases best-effort; if SQL is down, reserved capacity expires after its bounded lease. Successful leases remain until session expiry even after local removal/process death. Ordinary sessions remain process-local, with the existing store enforcing its own quotas and TTL. Deleting a draft denies future recovery but does not revoke an independently recovered active session. Browser refresh reauthorizes opaque associations and never restores a Saved or approval claim.

Trusted membership, owner/tenant checks, CSRF authorization before sensitive binding, strict client DTOs, source/template/example hashes, expected runtime versions and the original export chokepoint remain in force. Recovered values are Manual and Unreviewed; no recognition/source authority or prior approval is restored. Legacy footwear entities/migrations/export remain unchanged. Existing baseline and authenticated browser regressions must pass on the final exact HEAD.

### First correction verification record (historical)

The corrected application at **`4fb63cd3297a5bf8e2e726dc779340817c58c16c`** passed [run 40054028411](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40054028411): [Windows Server 2022 job 120752875866](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40054028411/job/120752875866) and [Ubuntu 24.04 job 120752876041](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40054028411/job/120752876041). This supersedes the independent-review pending status. Both metadata archives were downloaded and independently checked in Work, including complete identities and inherited failure fingerprints.

| Actual corrected-application evidence | Windows Server 2022 | Ubuntu 24.04 |
|---|---|---|
| Gate 5C discovered/executed | 70 PASS, zero skipped | 70 PASS, zero skipped |
| Baseline engine | 532 PASS | 532 PASS |
| Feature engine | 602 PASS | 602 PASS |
| Baseline full | 727 PASS + 2 inherited failures / 729 | 719 PASS + 10 inherited failures / 729 |
| Feature full | 797 PASS + same 2 failures / 799 | 789 PASS + same 10 failures / 799 |
| Existing authenticated browser | 43 PASS | 43 PASS |
| Gate 5C authenticated browser | 17 PASS, zero runtime errors | 17 PASS, zero runtime errors |
| SQL Server | LocalDB Express 15.0.4382.1 | Developer 16.0.4305.5 |
| A–G independent-process exits/recovery; migration apply/rollback/reapply | PASS | PASS |
| Lock timeout, rowversion, revisions, quotas, killed transaction rollback | PASS | PASS |
| Actual backend restart, fresh login, review/approval and downloaded XLSX reopen | PASS | PASS |
| New packages / dependency findings | 0 / 0 | 0 / 0 |

70 Gate 5C cases are 29 more than the starting revision. Complete discovered identities equal the executed new identities in both engine and full suites. Baseline failures are preserved by identity and normalized diagnostic fingerprint; no mandatory test is skipped or assertion weakened. Standard backend/frontend builds and the 13 inherited draft tests pass. The current audit still reports the inherited SQLite native High advisory and five npm findings (two High, three Moderate), equal on baseline/feature.

- [Windows metadata artifact 12544243881](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40054028411/artifacts/12544243881), SHA-256 `4b4861f13f6c13511fcb4828f6c752d01452ffe0afecd655cd6bc96598366426`.
- [Ubuntu metadata artifact 12544628292](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40054028411/artifacts/12544628292), SHA-256 `9d6ef329ed4351aaf020c659717f4cbcfbdf52a824b82e1aee25d08576590af6`.

Actual browser XLSX downloads were independently reopened with openpyxl before removal: Windows 8669 bytes, SHA-256 `c2ca7730b42a56414ce51aa4a7cb612529a0bba95cd108b2dacf109fcec7814c`; Ubuntu 8648 bytes, SHA-256 `3146f13bab51d65c8b329fc96eb2778672e952abb13f3e364b7cb8629208adf7`. Both data sheets, typed/leading-zero values, formats, formulas, borders, merges, sheet order and the reference sheet pass. Real-SQL engine tests additionally reopen three-sheet output. Archives contain JSON metadata only; no plaintext documents, source bytes, workbook binaries, screenshots or secrets are published.

The final delivery adds only this acceptance record and two regression strengthenings: the raw SQL padding predicate assertion and actual browser Save as a new draft/distinct identity/deletion checks. The browser verifier now requires 18 Gate 5C checks (61 total with the unchanged 43); that addition is subject to the full exact-final-HEAD run. The exact delivery SHA, its successful CI URL, final inventories and inspected artifact hashes are recorded in PR #17 and immutable sanitized `summary.json` to avoid a self-referential commit hash in its own document. The delivery recommendation is **MERGE READY only with that successful exact-HEAD record; otherwise NOT READY**.

Cryptography/key lifecycle, SQL transaction/concurrency, incomplete A–G recovery, trusted auth/tenant isolation and fresh-approval export boundaries pass the bounded review. No Critical/High finding remains from this review; all three Medium and two Low findings above are corrected. Operational prerequisites remain stable protected keys, supported SQL Server and narrowly scoped permissions, explicit authorized migration, a future maintained EF model snapshot, clock/session routing, conservative lease capacity and approved backup/retention/erasure procedures. Old malformed missing/empty snapshots fail closed; users must re-save from a valid active session. This is not production readiness. PR #17 remains open for separate owner merge approval; main, decoder PRs #13/#14 and shared infrastructure are unchanged.


### Historical padded-record ownership boundary

The SQL padding finding also applies in reverse to correctly encrypted records written by an older configuration whose stored owner/tenant ends in ASCII space. Rejecting padded incoming actors alone is insufficient. New regressions seed an authenticated historical row and require an unpadded actor to receive no list metadata and 404 for read, update, resume admission and delete. At `f4585159a4e97237be7022e2fdab37ebf8d0bf5a`, [run 40055425339](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40055425339) demonstrated both nonempty unauthorized listings on real Windows LocalDB and Ubuntu SQL Server: 70 pass, two fail, zero skipped on each. This is a refinement of the same Medium ownership finding, not a new feature.

`Find` now compares stored owner/tenant spelling with the trusted actor using ordinal equality before expiry/revision diagnostics, decryption or mutation. `List` projects bounded metadata only and applies the same exact comparison before returning it; no ciphertext is loaded for listing. Incoming padded actors remain rejected. Historical ambiguous rows are not reassigned, trimmed or automatically decrypted; they can conservatively consume capacity until retention cleanup or separately authorized operator remediation. No actual/shared database was accessed. Operators must configure unambiguous membership identifiers.

The prior 70-test pass did not certify this additional boundary. The expanded corrected revision passed the 72-case inventory and complete acceptance below, superseding the pending status and earlier ownership conclusion.


## Final independent review acceptance — 24 October 2026

Exact corrected application/test HEAD: **`51370b42dcc6eb1d24c8a72394cfd2718e088c66`**. [Run 40055694739](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40055694739) completed SUCCESS on [Windows Server 2022](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40055694739/job/120758068418) and [Ubuntu 24.04](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40055694739/job/120758068209). Both archives were downloaded, unpacked and independently checked in Work, including full test identities and failure fingerprints, browser outcomes, dependency comparison, SQL metadata and workbook QA.

| Actual discovered/executed evidence | Windows Server 2022 | Ubuntu 24.04 |
|---|---|---|
| Gate 5C | 72 PASS, zero skipped | 72 PASS, zero skipped |
| Baseline engine | 532 PASS | 532 PASS |
| Feature engine | 604 PASS | 604 PASS |
| Baseline full | 727 PASS + 2 inherited failures / 729 | 719 PASS + 10 inherited failures / 729 |
| Feature full | 799 PASS + same 2 failures / 801 | 791 PASS + same 10 failures / 801 |
| Existing authenticated browser | 43 PASS | 43 PASS |
| Gate 5C authenticated browser | 18 PASS | 18 PASS |
| Total authenticated browser / runtime errors | 61 PASS / zero | 61 PASS / zero |
| Real SQL Server | LocalDB Express 15.0.4382.1 | Developer 16.0.4305.5 |
| Migration apply/rollback/reapply; interrupted writer rollback | PASS | PASS |
| A–G process-exit recovery, lock/revision/rowversion/quota boundaries | PASS | PASS |
| Actual backend restart/re-login, renewed approval, downloaded XLSX/openpyxl | PASS | PASS |
| New packages / dependency findings | 0 / 0 | 0 / 0 |

The 72 Gate 5C cases add 31 cases to the starting revision. Discovery equals executed new identities in both engine and full suites; no mandatory test is skipped or acceptance assertion weakened. Existing failures match same-host baseline identity and diagnostic fingerprint. All standard builds and 13 inherited frontend draft tests pass. Browser checks include Save as a new draft producing a distinct authorized record and deletion of both records, unapplied field edits clearing Saved state, refresh association, explicit conflicts, cross-owner/tenant denial, renewed confirmation/review/approval and 390px controls.

- [Windows artifact 12545058461](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40055694739/artifacts/12545058461): ZIP SHA-256 `933ec7935d038171c24eb99d648a9f513a5f47d849628ac7beb607c9b2897efc`.
- [Ubuntu artifact 12545169413](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40055694739/artifacts/12545169413): ZIP SHA-256 `75943c439ad3b74f870b72bda8e4f7eb80b14c92dc1a669fa2a5b0bba0e24c87`.

Actual independently reopened XLSX: Windows 8669 bytes, SHA-256 `a13c5f6c28004423ae9a3021ee5884e4fcdf8d6e5143e7be73b163b1302e9f56`; Ubuntu 8648 bytes, SHA-256 `e95875874058b683438f81c1dded861bcf5252a35181a223d3e8346fd5ba1bfc`. Both sheets' values, leading-zero identifiers, formats, formulas, borders, merges, worksheet order and reference content pass. Three-sheet output is additionally reopened in real-SQL engine tests. Workbooks are removed before artifact publication; archives contain JSON metadata only, with no plaintext document/source bytes or secrets.

**Final security conclusions:** AES-GCM nonce/tag/AAD, key rotation and fail-closed formats pass. SQL transactions, expected revisions, rowversion, global locks, fixed expiry and quotas pass, including both directions of padded ownership and authenticated historical rows. A–G incomplete recovery passes without old confirmation/review/approval. Auth, CSRF, trusted tenant ownership, expected versions and the unchanged export chokepoint pass. All three Medium and two Low findings are corrected; no unresolved Critical/High finding was identified by this bounded review.

Operational exclusions remain: example-based durable drafts only; normal sessions are process-local; successful leases conservatively occupy capacity until expiry; deletion does not revoke an independently recovered session; historical ambiguous identities and malformed missing/empty snapshots fail closed. Operators need stable protected keys, approved key/backup retention and erasure procedures, supported SQL Server, validated encrypted production transport, narrow runtime DML/application-lock permissions, separate authorized DDL migration, clock/session routing and a reviewed EF model snapshot before future scaffolding. Commit acknowledgement loss remains ambiguous and requires refreshed metadata before deliberate retry. The inherited SQLite native High advisory and five npm findings (two High, three Moderate) remain unchanged.

**Recommendation: MERGE READY for the bounded Gate 5C MVP**, conditioned on the successful exact-final-HEAD delivery run recorded in PR #17 and its immutable `summary.json`. This final commit changes documentation only; application/tests remain exactly the verified revision above. The PR record supplies the final delivery SHA/run to avoid embedding a self-referential SHA in its own document. No merge, deployment, paid hosting, real/shared database migration, Gate 5D or decoder changes were performed. This is not production readiness.
