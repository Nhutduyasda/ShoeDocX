# Infrastructure and cost assessment — researched 2026-10-26

Assumptions: two or three synthetic office participants in a future approved pilot,
up to 20 rows, small XLSX only, single instance, no paid OCR/AI, no HA promise.
No provider account was accessed. Existing production-named Azure references in PR #4
are historical only; no secrets or resources were read or changed. Native .NET 8/F1
instructions from that PR cannot be carried into this .NET 10 build without review.

| Option | Architecture / recurring cost | Quotas, variable costs, access |
|---|---|---|
| A — public-repo standard GitHub Actions | Isolated runners, transient SQL Developer, test CA, production images; standard public repository runner minutes $0 under current GitHub policy | No enduring pilot endpoint. Hosted job/disk/artifact retention limits apply; artifact storage/bandwidth subject to account terms. No confidential inputs. Credentials are disposable; zero cloud provision. Preferred for this Gate. |
| B — suitable already-owned Linux Docker host | Same Compose, SQL Developer for nonproduction only; no new hosting subscription required if host already exists | Electricity, disk, backup destination, network, operator time and existing host cost are not $0. Budget at least 4 vCPU / 8 GB RAM / 30 GB free disk as an unbenchmarked planning allowance. Loopback only; 2–3 users cannot access remotely until owner approves a private ingress/PKI arrangement. Avoid buying a VPS. |
| C — Azure App Service + Azure SQL | Managed .NET 10 app (supported quickstart), HTTPS provider domain, separate SQL, durable private files, certificate/key storage, backup and monitoring | F1 web compute is $0 with 60 CPU min/day, 1 GB memory/storage and no SLA; it is trials only, may stop on quota/idle, not an always-on durable pilot guarantee. SQL free offer is separate: 100,000 vCore seconds + 32 GB data + 32 GB backup/month; verify subscription eligibility, supported region, and stop-at-limit instead of paid continuation. An all-in $0 secure pilot has NOT been established. Paid B/P hosting, SQL overage, storage, Key Vault operations, backup retention, logs and egress need a regional calculator quote and budget cap before approval. |

Exact future monthly total for C is **not quoted**: owner region, subscription offer,
storage/backup retention and monitoring volumes have not been selected. Public pricing
page is dynamic by region/currency/offer; do not use a scraped price from another region
as an owner quote. The viable paid alternative must be priced in the owner's billing
currency, including taxes and potential overage. No paid option is approved here.

The gateway joins a separate ingress bridge; API/SQL remain on the internal bridge.
Only the gateway publishes a loopback port. Docker documents internal networks as
externally isolated, so the published ingress must not rely on an internal-only network.

Source evidence (official pages checked read-only):

- https://docs.docker.com/compose/how-tos/networking/
- https://docs.docker.com/reference/cli/docker/network/create/
- https://docs.docker.com/engine/network/port-publishing/

- https://docs.github.com/en/billing/concepts/product-billing/github-actions
- https://docs.github.com/en/actions/concepts/billing-and-usage
- https://azure.microsoft.com/en-us/pricing/details/app-service/linux/
- https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
- https://learn.microsoft.com/en-us/azure/app-service/overview-hosting-plans
- https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore
- https://learn.microsoft.com/en-us/azure/app-service/configure-language-dotnetcore
- https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer?view=azuresql
- https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier-faq?view=azuresql-db
- https://learn.microsoft.com/en-us/sql/linux/security/encrypted-connections?view=sql-server-ver17

## Exact owner decision package — pending, not an invitation

Recommended current decision: finish isolated technical acceptance with A; retain
INTERNAL PILOT NOT YET EXECUTED. This incurs no new hosting subscription. Only standard
runners and small sanitized artifacts are used; no large runner or paid API enabled.

For a later accessible pilot, owner must name: (1) existing suitable nonproduction host
or new isolated Azure resource group, (2) region and subscription/billing owner,
(3) maximum monthly all-in budget and stop-at-limit policy, (4) private access method,
trusted HTTPS certificate and allowed participants, (5) data classes, retention and
backup destination, (6) key custodian and recovery contact, (7) exact deployment/migration
approval and time window. Approve only a concrete reviewed bill of materials after
technical blockers close. Do not reuse PR #4 assets implicitly. Two/three actual
participants and invitation permission are separate from infrastructure approval.
