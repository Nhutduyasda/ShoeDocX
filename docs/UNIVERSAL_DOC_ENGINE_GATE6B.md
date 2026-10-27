# Gate 6B — isolated staging, recovery and internal pilot

Starting main: `2552252d0d916d4184d88482f3649c602468eff8` (merged Gate 6A PR #19).
Branch: `codex/universal-doc-engine-gate-6b-staging-pilot`. One Draft PR; no merge.

**Current verdict: PARTIAL / NO-GO.** Implementation is under exact-HEAD verification.
Do not interpret the presence of a workflow, runbook or test scenario as a passing result.
The authoritative executed result is the same-HEAD `gate6b-regression.json` for each OS
and `gate6b-staging.json` for Linux. CI fails closed while any staging acceptance is
incomplete. No hosted staging or real participant pilot exists.

| Milestone | Status | Evidence / limit |
|---|---|---|
| Main merge and open PR preflight | Tested | Main matches requested baseline; only excluded #4/#13/#14 open at start |
| Production configuration and explicit initialization | Implemented | No ordinary Production DbInitializer writes; both migration chains checked |
| Certificate protected DataProtection key ring | Implemented | PFX injected, private key outside image; execution pending |
| HTTPS reverse proxy and SQL certificate validation | Implemented | Ephemeral test CA, forced SQL TLS; execution pending |
| Runtime least privilege | Implemented | DML roles, no db_owner or CREATE TABLE, history writes denied; execution pending |
| Encrypted backup and complete instance reconstruction | Implemented | SQL CHECKSUM + AES-GCM bundle + ciphertext fingerprint; execution pending |
| Full fault injection / rollback | Not tested | Outstanding inventory below; blocks technical acceptance |
| Windows production SQL TLS parity | Blocked | LocalDB regression is not a validated production SQL TLS endpoint |
| Real office participant pilot | Owner approval required | INTERNAL PILOT NOT YET EXECUTED |
| Hosted staging | Owner approval required | No resource provisioned, existing production assets untouched |
| Production | Blocked | NO-GO |

## Architecture

```mermaid
flowchart TD
    B["Synthetic browser: trusted test CA"] --> N["HTTPS localhost:8443 / Nginx production dist"]
    N --> A["Production ASP.NET Core 10 / app UID / read-only image"]
    A --> S["SQL Server Developer / validated TLS / runtime DML account"]
    A --> K["Restricted storage / certificate-encrypted DataProtection / AES draft keys"]
    M["Explicit migration account"] --> S
    S --> R["Encrypted recovery bundle / key held separately"]
    K --> R
```

Only one host loopback port is published. SQL/API have no host port. The Docker
network is internal. The proxy replaces client X-Forwarded-* values; the backend
accepts one hop from the exact configured proxy IP, never all peers. Host is fixed
by ingress; AllowedHosts is explicit. Health emits only ready/503. Swagger remains
Development-only. Frontend runtime is Nginx with compiled dist, no Node/Vite.

The existing root Compose and HTTP Nginx remain historical development examples,
not a supported staging launch path. Use `deploy/gate6b/compose.yml`; its certificates,
private configuration and mounts are mandatory. SQL Developer is nonproduction only.
The owner Windows computer is never used. Ordinary CI LocalDB is regression evidence,
not production TLS acceptance. Do not weaken ProductionConfiguration for it.

## Runtime changes and security review

Production now requires a certificate path for DataProtection, loads the PFX through
X509CertificateLoader and protects persisted key material. Missing/unreadable/wrong
PFX fails startup. PFX/private password and AES keys require protected operator storage;
file persistence alone does not protect their master secrets. Backups wrap the complete
recovery set in authenticated AES-256-GCM. Backup decryption key is separate and is
not included in the archive or artifacts. Test CA and credentials are random ephemeral
synthetic secrets, never reusable deployment credentials.

Normal Production startup checks legacy and draft pending migrations and draft model
state, then performs integrity reads. It no longer runs default workspace/template/
folder repair. `--migrate-all` requires Database:MaintenanceAuthorized, migrates legacy
and `ude.__DraftMigrations`; `--initialize-staging` separately performs initialization.
BootstrapAdmin in Production requires the explicit initialization command. Development
seeding remains Development-only and explicitly enabled; staging disables it. Old
`--migrate-only` is not the Gate 6B maintenance procedure and cannot be used to bypass
normal production migration policy.

Application sessions are memory-only and are lost on restart. Drafts persist for
exactly seven days from creation and must be resumed, remapped/reconfirmed, reviewed
and approved again. Approval is never restored as export permission. No decoder,
OCR, PDF compatibility, paid inference or roadmap scope expansion is included.

Gate 6A Option A remains in force: Tailwind 3.4.17 with the reviewed deduplicated
postcss-selector-parser 6.1.5. Keep complete audit output and GHSA-rj75-hqrm-r3gf
warning visible; do not claim a clean full audit. Recheck official feeds, package
hashes, trusted-source compiler inputs and absence of runtime compilation. No new
Critical/High is accepted. Lockfile and application UI are unchanged by Gate 6B.

## Acceptance inventory and honest gaps

Dual-OS regression rebuilds all project inventories, compares exact test identities
and normalized failure hashes with this run's accepted-main checkout, requires all
added Gate 6B tests pass and zero skipped tests, rebuilds the frontend, audits official
dependencies, verifies backport bytes and runs inherited authenticated browser suites.
This does not substitute for production staging acceptance.

Linux staging builds real images, validates nonroot/read-only/no-backend-port metadata,
executes explicit migrations, authenticates actual SQL Identity users, runs invoice, warehouse and multi-sheet Office
Wizard flows with local synthetic fixture files through HTTPS, and uses no test routes or
mocked API responses in the Production process. Browser certificate errors are not
ignored. Restored workbook downloads are reopened by ClosedXML and openpyxl. Raw
workbooks, screenshots, tokens, private configuration and logs are private runner temp
only. Uploaded artifact paths contain explicit sanitized metadata JSON only.

The scripted complete-instance recovery saves a two-sheet workflow and uses a controlled pause point while browser
waits: graceful backend stop, ciphertext/key/rowversion fingerprint, SQL backup with
checksum, authenticated encrypted recovery archive, destruction of application/SQL
containers and database volume, reconstruction, VERIFYONLY/RESTORE, login SID remap,
protected keys and file mounts restored, fingerprint equality, fresh authentication,
draft resume, renewed approval and export. If this has not completed in CI it remains
**Not tested**. RTO is an observation only when emitted; no SLA or guaranteed RPO.

Outstanding mandatory faults: SQL outage/auth failure/wrong TLS, missing AES key/wrong
key ID, wrong DataProtection key, corrupt backup, missing attachment storage, incomplete
configuration, expiry after restore, runtime transaction conflict, full disk/permission
denial, interrupted download, expired/revoked JWT and missing/invalid CSRF in the
production proxy path, unauthorized reviewer/approver and owner/tenant draft read,
expired workflow, invalid/oversized/formula/structure rejection and previous-supported-
build rollback. Some are inherited unit/SQL/browser checks; none is relabeled as
production fault-injection evidence until that exact path is executed. This inventory
blocks STAGING TECHNICAL PASS and keeps the PR Draft.

## Handoff

- [Infrastructure, pricing and hosting approval](gate6b/INFRASTRUCTURE.md)
- [Operator runbooks and secrets](gate6b/OPERATIONS.md)
- [Vietnamese quick-start and participant checklist](gate6b/PILOT_VI.md)
- [Feedback and incident templates](gate6b/FEEDBACK.md)

Final acceptance must report separately STAGING TECHNICAL PASS/PARTIAL/FAIL,
INTERNAL PILOT PASS/NOT EXECUTED/FAIL, PR MERGE READY/NOT READY, PRODUCTION GO/NO-GO.
Current claims remain PARTIAL, NOT EXECUTED, NOT READY, NO-GO until executed evidence
and explicit participant authorization support a change. No invitations, real user
accounts, access URL distribution, company files, cloud changes or production migrations.
