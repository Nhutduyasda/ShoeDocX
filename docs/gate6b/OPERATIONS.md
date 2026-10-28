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
| Separate protection PFX / ingress TLS / SQL TLS keys | 0400 service account, no image layers; renew certificate and preserve old decryption certs until old protected keys/backups retire |
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
has an explicit immutable Gate 6A probe; its actual functional/key-protection results
are in gate6b-previous-build-rollback.json. Do not claim compatibility from a successful
forward migration or approve a build that writes new unencrypted protection keys. Preserve
current database/keys and block release until previous build + restored schema is tested.

The separate hardened A/B exercise uses immutable image IDs on the same SQL instance
and mounts: A creates/saves a workflow, B resumes/reapproves/exports, and A does so
after rollback. Startup compares order-independent SHA-256 hashes of all table rows;
all three exports are independently reopened. No key XML is rewritten or SQL lease
deleted to bypass capacity. Require gate6b-hardened-rollback.json. Only that pair and
schema/key contract is exercised; arbitrary past releases and production rollback are
not approved. Archive and approve exact OCI images, a host rehearsal and a recovery
checkpoint before real deployment. See [pending decisions](PLATFORM_DECISION.md).

## Backup and restore

Pause writes for the isolated test checkpoint. SQL BACKUP WITH CHECKSUM covers both
contexts and encrypted drafts. Include actual file/template storage, protected key
ring, full AES key map/IDs, independent protection PFX, ingress/SQL TLS certificates and private keys, and relevant
protected config/operator metadata. The test root CA signing key and the backup
decryption key are independent custodial material, not bundled with the archive. Put recovery set in an
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
support during certificate rollover. Keep `DataProtection:CertificatePath` and
`CertificatePassword` for the active certificate. Supply up to four historical
PFX entries under `DataProtection:DecryptionCertificates`, each with an absolute
`Path` and its protected `Password`. Historical certificates decrypt existing XML;
new key XML is always encrypted using the active certificate. Every configured PFX
must exist, load successfully and include its private key, or startup fails closed.
Passwords belong only in the complete protected operator JSON, never this repository.

Before an authorized rollover, make a verified recovery checkpoint and include all
retained private decryption certificates/passwords in its protected recovery set.
Copy and mount each PFX read-only as 0400 for the application account. First deploy
the old and new decryption certificates together to every instance, then switch the
active certificate and restart. Existing keys are not rewritten; a changed active
certificate alone does not immediately generate a new DataProtection key. Preserve
old certificates while any retained key XML, live payload or backup depends on them.
Do not delete key XML to force rotation, and do not retire certificates by elapsed
certificate validity alone. Verify old payloads, newly generated keys and a complete
restart in isolated rehearsal before any real transition. Early retirement fails
decryption; it is not a supported logout or migration procedure. Real custodian
approval, secure delivery and backup retention remain unprovisioned.

Synthetic verification covers byte-for-byte preservation of old key XML, old/new
payload recovery across fresh providers, encryption of new key XML by the active
certificate, failure after premature old-certificate retirement, and Production
restart with an existing authenticated CSRF cookie. Consult exact-HEAD evidence
before claiming these tests passed. Reference: [Microsoft Data Protection certificate
configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0#unprotect-keys-with-any-certificate-unprotectkeyswithanycertificate).

Revoke compromised users/tokens, restrict
ingress, preserve sanitized incident evidence, identify backup exposure and request
explicit operator approval before real credential/key changes.

## Outage, full disk, migration failure, support

- SQL outage: deny readiness/admission, preserve data, verify DNS/certificate/auth with
  restricted operator tooling; never turn off SQL encryption or certificate validation.
- Full disk/storage denial: stop new uploads/exports, preserve existing files/drafts,
  measure free bytes without listing sensitive names; restore capacity before retry.
  B readiness verifies one temporary byte can be written/deleted and returns 503 if
  unavailable. In isolated CI only, fill verified bounded tmpfs mounts, never the
  host root or SQL volume. Ingress cache is 8 MiB and backend temp 64 MiB. A failed
  spooled ingress upload must not advance its session or leave a partial/completed
  file. After filler removal repeat the upload. Bounded in-memory Excel exports may
  remain successful. Actual ENOSPC, cleanup, draft integrity and recovery are separate
  evidence from permission denial.
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

Open risks: complete Windows native parity (consult actual TCP investigation); table-scoped
runtime privileges; production certificate custody/retirement; production image archive and restricted rollback approval; safe metrics
coverage; generic historical development Compose; no persistent hosted endpoint;
no real participant feedback; dynamic provider pricing; no measured host capacity or SLA.
