# Pending owner platform and pilot decisions

Prepared 2026-10-28. No platform waiver, deployment or participant approval exists.
Current exact source SHA, execution results, CI URLs and changed-file list are recorded
in [PR #20](https://github.com/Nhutduyasda/ShoeDocX/pull/20). Pending scripts are not evidence of PASS.

## D1 — supported platform scope

Recommended decision, **not applied**: approve the bounded initial pilot platform as
Linux x86-64 Docker Compose only, after its current technical acceptance passes.
Retain Windows Server 2022 regression as mandatory. Record Windows native Production
as NOT REQUIRED only after the owner explicitly approves that scope. Windows clients
may use the HTTPS web UI; this does not mean the server runs on Windows.

The Windows hosted-image inventory includes SQL LocalDB, not a preconfigured full
certificate-managed TCP SQL instance. LocalDB is rejected by ProductionConfiguration.
An independent disposable job now attempts Microsoft's current signed Express media,
bounded silent installation, loopback TCP, a KeyExchange SQL certificate, forced
encryption, an explicitly trusted root, separate migration/runtime SQL accounts and
actual native HTTPS Production/authentication/CSRF. Installer signatures, versions,
hashes, phase and fixed failure identifiers are sanitized evidence. No unsigned media,
named-pipe substitution, TrustServerCertificate=True or hostname override is allowed
in Production. Actual outcome is gate6b-windows-tcp.json, not this plan.

Even successful direct native startup is only a subset: a reviewed native reverse
proxy, unprivileged Windows application identity, complete native browser journeys,
owner/tenant checks and real draft recovery are not established by that job. Do not
describe Windows parity as impossible on all free runners; the precise failed phase
or missing parity requirements must be reported. Without D1, Windows remains BLOCKED
and the full staging gate remains PARTIAL.

Official evidence: [runner image inventory](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md),
[current SQL Express source](https://www.microsoft.com/en-us/sql-server/sql-server-downloads),
[silent SQL installation](https://learn.microsoft.com/en-us/sql/database-engine/install-windows/install-sql-server-from-the-command-prompt),
[SQL certificate requirements](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/certificate-requirements).

## D2 — initial release rollback policy

Proposed restriction: allow only a successfully tested, recorded hardened A/B pair
with the same schema, protected key ring, retained certificates, AES key map and
runtime permission contract. Candidate A source is frozen at
`b0604b9472a79905d1a9b5c7afe13e681f23a232`, previously verified by
[run 40476120847](https://github.com/Nhutduyasda/ShoeDocX/actions/runs/40476120847).
Candidate B adds temporary-storage readiness and bounded ingress cache behavior.
Current CI builds separate A/B images, records their immutable local image IDs and
exercises A-create/save/export, B-upgrade/resume/reapprove/export, then A-rollback/
resume/reapprove/export on the same database and storage. Database row hashes at
startup, saved ciphertext/rowversion, key XML and independent XLSX reopening are checked.
The real twenty-minute expiry test runs while B is active; resume leases expire
naturally. No lease or clock override is used.

A is rebuilt from its pinned, reviewed source using current trusted base images and
the current test CA. It is not claimed to be byte-identical to the prior CI image.
The exact A image used for this run is frozen by image ID before the two-version
exercise. There is no persistent image registry or signed release-image archive yet.
For an actual pilot, archive the reviewed OCI images/manifest and verify that same
pair on the selected host before deployment approval.

Gate 6A remains an explicitly unsupported rollback target. A/B staging rollback,
complete backup restore and forward recovery have separate reports. No schema-changing
upgrade, older arbitrary version, unavailable certificate, lost key or production
rollback is approved by this exercise. Stop admission and restore an approved isolated
checkpoint if a version pair is outside the verified contract; no destructive retry
or automatic migration downgrade. An initial release has no older production release
until a real release history and recovery checkpoint exist. Owner must explicitly
accept this restriction; otherwise the release path remains NO-GO.

## D3 — smallest accessible pilot approval package

| Item | Concrete proposal / pending owner input |
|---|---|
| Host | Name one already-owned, dedicated nonproduction Linux x86-64 host; planning allowance 4 vCPU, 8 GB RAM and 30 GB free disk, plus encrypted backup space. Capacity is not benchmarked or guaranteed. Owner Windows computer excluded. |
| Services | Actual .NET 10 API, compiled React/Nginx HTTPS, SQL Server Developer for this nonproduction evaluation only. One instance; no uptime/SLA promise. Same bounded Compose with internal SQL/API network. |
| Access | Existing approved private SSH tunnel/VPN. Compose binds ingress to loopback only. HTTPS hostname and operator-managed certificate must validate in every participant browser; no certificate-warning bypass and no public SQL/API port. |
| Costs | Maximum new hosting/API subscription proposed: USD 0. Existing host, electricity, disk, backup, network and operator costs remain owner's responsibility; availability must be confirmed. No paid overage or new VPS. If no suitable existing host/access exists, do not provision; remain CI-only. |
| Custody | Named operator and recovery contact hold separate SQL admin/migration/runtime credentials, JWT key, AES key IDs/map, active/historical protection PFX/passwords, TLS material and backup key. Keep server configuration private; backup key outside encrypted archive. No production key rotation. |
| Backup | Existing owner-approved separate encrypted destination. Proposed pilot retention: 14 days, daily paused checkpoint and before every release; verify restore before first participant. Retention/destination/custodian require approval; no recurring backup job is enabled. |
| Accounts | Two ordinary synthetic test identities and one approver/admin, or one ordinary identity plus a distinct admin for two people. Exact people, actor-to-tenant bindings, secure account delivery and invitation permission must be approved separately. No identities have been created for real people. |
| Samples | Generate synthetic invoice A, warehouse B and three-data-sheet/reference F fixtures from the existing helper. Max 20 output rows, three mapped sheets/four Studio sheets/five filled examples/eight rules. No company documents, OCR, PDF or paid AI. |
| Acceptance | Each authorized person uses the Vietnamese guide, deliberately corrects a validation error, saves/resumes a draft, reviews/reapproves as permitted, downloads/reopens XLSX and submits genuine feedback. Include unsupported-template and unauthorized-access checks. |
| Release | D1 and D2 decisions, current technical evidence and immutable image archive must precede a separately approved deployment/migration window. No automatic merge/deploy. |
| Deletion | Drafts expire seven days after creation. Pilot operator closes sessions, disables accounts and deletes the isolated instance/files at pilot end; retained backups expire after the approved 14-day period. Deletion is not instantaneous across backups. |
| Incident | Stop ingress/admissions, preserve sanitized build/error/timestamp evidence, notify named operator via an approved channel, protect backups/keys and reproduce with synthetic data. Never attach tokens, passwords or company workbooks to GitHub. |

Guides: [Vietnamese pilot](PILOT_VI.md), [feedback/incident forms](FEEDBACK.md),
[operator and recovery procedures](OPERATIONS.md), [cost assumptions](INFRASTRUCTURE.md).

**INTERNAL PILOT NOT EXECUTED.** D1/D2 do not authorize D3, participant invitations,
public hosting, cloud resources, real data, real key rotation or merge. PR remains
Draft pending required owner decisions and any remaining technical blocker.
