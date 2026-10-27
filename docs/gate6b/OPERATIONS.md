# Operator runbooks — isolated staging only

Status: Implemented procedures; execution is recorded only by exact-HEAD CI artifacts.
Unsupported as production release until Gate 6B blockers close.

## Secure configuration checklist

Use a complete private owner-readable configuration JSON through SHOEDOCX_CONFIGURATION_FILE;
this replaces application defaults, so arrays cannot retain development HTTP origins.
Only ASPNETCORE_ host configuration is layered on top; do not assume ordinary app env
variables override this protected file.
never frontend VITE_* or a committed env file. Never print docker inspect environment,
connection strings, token bodies, private logs or runtime JSON into public Actions logs.
Synthetic credentials still stay out of artifacts. Root `.env.example` is explanatory,
not an approved production configuration.

| Secret / durable material | Required handling / recovery |
|---|---|
| JWT signing key | >=32 random bytes, protected server configuration, retain across routine restart; compromise requires explicit incident rotation and session invalidation |
| SQL admin/migration/runtime credentials | Separate principals; admin for isolated provisioning/restore, migration db_owner only during explicit maintenance, runtime DML only; no sa application login |
| Draft AES-GCM key map + active ID | 32-byte values, stable IDs; retain all keys for nonexpired drafts and retained backups; unknown/wrong key fails closed |
| DataProtection key ring | Restricted directory AND ProtectKeysWithCertificate; back up encrypted XML plus protected PFX and recovery password separately |
| PFX / TLS keys | 0400 service account, no image layers; renew certificate and preserve old decryption certs until old protected keys/backups retire |
| Bootstrap credential | Disabled in ordinary startup, explicit initialize command only; disable immediately after controlled creation |
| Backup key | Random 32 bytes, separate custodian/store from authenticated encrypted archive; not uploaded with backup |
| Optional API keys | Empty in staging; no OCR/OpenAI network access or paid use authorized |

Directory ownership must match actual image UID (currently app/1654). Do not chmod
777. Templates should be readonly in ordinary operations unless a verified writer
requires changes. CI mounts only ephemeral synthetic files. Temp is bounded 0700 tmpfs.

## Startup, migrations and initialization

1. Check accepted commit, official audits, image identities and archived config hashes.
2. Identify the dedicated isolated database by exact name and verify it is not shared.
3. Operator explicitly authorizes maintenance and uses migration credentials in a
   separate temporary configuration. Normal startup has MaintenanceAuthorized false. The background retention service may
perform bounded expired-row deletion after startup; read-only initialization does not
mean the running application never writes.
4. Execute actual backend `--migrate-all`: AppDbContext default migration history then
   UniversalDocDraftDb `ude.__DraftMigrations`. Failure stops; preserve DB and diagnostics.
5. Execute `--initialize-staging` once to create required workspace/template/folder defaults.
   Bootstrap only if explicitly enabled for that command. CI instead adds synthetic
   identities using its disposable helper, never development seeding.
6. Supply exact server-owned actor-ID to tenant bindings (UniversalDocGate4A:Users).
   Claims/header/query tenant IDs are not accepted as membership proof.
7. Switch to runtime DML login, remove migration authorization and bootstrap setting.
8. Start backend/web with validated TLS and explicit trusted proxy, check sanitized
   health and authenticated browser workflow. Never use dev server/Swagger as readiness.

Runtime permissions: db_datareader + db_datawriter is currently database-wide DML,
not table-specific least privilege. It denies DDL/db_owner but can access legacy and
ude data. Writes to both migration history tables are explicitly denied to runtime, and the
migration login is disabled after authorized maintenance. Table-scoped permission
refinement for business tables remains an open risk before production. Backup
and restore require the isolated administrative principal, not runtime. Logins are
explicitly remapped after complete SQL instance loss. No automatic schema recreation
or database deletion after failure.

## Shutdown / deployment / rollback

Stop ingress admissions, allow bounded requests to drain, gracefully stop backend,
verify SQL integrity and stop containers. In-memory sessions/approvals disappear;
saved drafts resume only after reauthentication and renewed confirmation/review/approval.
Deploy an approved immutable build only after a verified recovery checkpoint. Existing
Compose is a single instance and does not promise zero downtime. Previous-build rollback
is NOT TESTED; do not claim compatibility from a successful forward migration. Preserve
current database/keys and block release until previous build + restored schema is tested.

## Backup and restore

Pause writes for the isolated test checkpoint. SQL BACKUP WITH CHECKSUM covers both
contexts and encrypted drafts. Include actual file/template storage, protected key
ring, full AES key map/IDs, PFX and relevant config metadata. Put recovery set in an
AES-256-GCM envelope; hold the 32-byte backup key separately. Store no unencrypted
recovery archive outside restricted temp. Encrypted backup alone is useless without
a tested key custodian and expiration-aware recovery policy.

Restore to a newly authorized isolated instance, VERIFYONLY WITH CHECKSUM, RESTORE WITH
CHECKSUM, restore protected files/keys, remap login SIDs and verify ciphertext/key/
rowversion fingerprints. Start application under runtime login, reauthenticate, resume,
reconfirm, approve, export and reopen with both libraries. Never change draft expiration
to make restore pass. Missing/corrupt backup/key/config/storage fails acceptance; preserve
the failed set instead of deleting it. Do not restore over shared/production DB.

Observed RTO is emitted by CI only if complete restore ran; it excludes downloading
images/building and is not an availability guarantee. Test backup checkpoint is paused;
no recurring backup cadence or production RPO guarantee is established.

## Key rotation / compromise

No real key rotation is authorized. Rehearse with synthetic data only. Add a new draft
key ID and value, switch active ID for new writes, retain old keys until rows and backups
expire; never relabel old ciphertext. DataProtection needs old certificate decryption
support during certificate rollover; current implementation loads a single PFX, so a
multi-certificate rollover is NOT IMPLEMENTED. Block certificate retirement until a
reviewed transition implementation exists. Revoke compromised users/tokens, restrict
ingress, preserve sanitized incident evidence, identify backup exposure and request
explicit operator approval before real credential/key changes.

## Outage, full disk, migration failure, support

- SQL outage: deny readiness/admission, preserve data, verify DNS/certificate/auth with
  restricted operator tooling; never turn off SQL encryption or certificate validation.
- Full disk/storage denial: stop new uploads/exports, preserve existing files/drafts,
  measure free bytes without listing sensitive names; restore capacity before retry.
- Migration failure: leave application stopped, retain exact failed version/schema,
  restore to a separate instance for investigation; no automatic destructive retry.
- Missing keys: fail closed, retrieve approved recovery key material through custodian;
  never mint replacement IDs for existing ciphertext.
- User issue: collect time, sanitized error code, build SHA, step and synthetic reproducer;
  never request workbook contents/tokens/passwords in GitHub.

## Observability and risk register

Only ready/503 health, constant event codes and aggregate counters are appropriate.
Current code removes login identity and attachment-path logging; existing EF/logging
levels are reduced to Warning in staging. A System.Diagnostics.Metrics meter records
HTTP operation/outcome categories (authentication, draft, export, health, other),
authorization rejection, storage failure, bounded expired-row cleanup and application
start/stop. No sensitive route/tenant/actor labels are recorded. Health uses a two-second
SQL deadline and a four-request concurrency cap. These are implemented instruments;
an exporter/dashboard/alert pipeline is NOT IMPLEMENTED. Do not expose public metrics. No operational dashboard or
alerting guarantee is claimed. Raw logs remain private. An end-to-end telemetry consumer and alert thresholds still
need operational verification before production.

Open risks: Windows validated SQL TLS parity; complete fault inventory; table-scoped
runtime privileges; multi-certificate rotation; previous-build rollback; safe metrics
coverage; generic historical development Compose; no persistent hosted endpoint;
no real participant feedback; dynamic provider pricing; no measured host capacity or SLA.
