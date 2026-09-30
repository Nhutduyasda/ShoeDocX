# ShoeDocX production deployment

## Status (2026-09-30)

Azure SQL provisioning is complete. The application has not yet been deployed or verified on the Internet. Do not treat a successful build or SQL deployment as production acceptance.

Observed resources in subscription `16216155-36a1-4338-b072-9dfbf2eeb2eb`:

- Resource group: `rg-shoedocx-prod`
- SQL logical server: `shoedocx-prod-nhutduy`, Southeast Asia
- Database: `shoedocx`, Azure SQL Free Offer
- Microsoft Entra-only authentication; the owner's signed-in account is the server administrator
- Free Offer configured with overage billing disabled; validate the deployed database properties before app deployment
- Container Apps, R2, managed identity permissions and GitHub OIDC: not provisioned yet

## Current deployment decision — App Service Free F1

The owner selected App Service Free F1 instead of Container Apps on 2026-09-30 to avoid compute overage billing. The app is not deployed yet. The previous Container Apps instructions below are retained as an alternative, not the active rollout.

Target: Windows App Service, Code publish, .NET 8, Southeast Asia, Free F1 (1 GiB RAM), existing resource group and Azure SQL Free Offer. Optional Application Insights and Defender paid add-ons are not enabled. Keep the generated azurewebsites.net HTTPS hostname. Publish the API with `dotnet publish ... -c Release -o publish /p:UseAppHost=false`, then copy `frontend/dist/` into `publish/wwwroot/` and deploy that folder. Do not publish the repository root or the historical database backup.

The active GitHub workflow verifies tests/frontend/Docker, then publishes the same-origin app with `azure/webapps-deploy@v3`. Configure OIDC at the web app scope, repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_WEBAPP_NAME`. Enable `AZURE_DEPLOY_ENABLED=true` only after secrets, managed identity database permissions, storage and initial admin are ready. The workflow checks that the app's plan SKU is F1 before deploying. No container registry or Container Apps resources are required.

Use App Service Configuration for the same runtime settings listed below, including R2 and the passwordless SQL connection. Enable a managed identity on the web app and grant only required SQL rights. F1 has no deployment slots: rollback by publishing the previous verified commit's artifact. Never include user uploads in the deployment artifact. Files remain in R2 and SQL remains in Azure SQL. Backups and bootstrap admin instructions below still apply. Keep `BootstrapAdmin` disabled after the initial account is created.

Free F1 includes 60 CPU minutes per day and 1 GiB memory. CPU time means consumed CPU, not wall-clock website availability. CPU or bandwidth quota exhaustion stops the app until reset; memory exhaustion can restart it. F1 is intended for development/testing and has no production uptime guarantee. Azure SQL has overage disabled and can pause until next month when its free allowance is exhausted. R2/OCR charges are separate; do not enable billable use without owner approval. Do not upgrade the plan automatically.

App Service creation, deployment, persistent file storage, real login/business flows and restart verification remain pending. The approved goal is still 2–3 internal users, durable data, unchanged business logic, and no paid resource upgrades.

## Previous Container Apps design (not deployed)

The root `Dockerfile` builds React/Vite into ASP.NET `wwwroot` and runs the API on port 8080 as the non-root `app` user. Container Apps ingress terminates HTTPS. `/` and non-file SPA paths return the frontend; `/api/*` goes to controllers, and unknown API paths return 404. No database or user files are included in the image. Secrets are runtime configuration only.

Target container configuration: Consumption workload profile, 0.5 vCPU / 1 GiB, external HTTPS ingress targeting 8080, HTTP concurrency 10, minimum replicas 0, maximum replicas 1, single revision mode. Configure a generous startup probe (e.g. 5-second period, 120 failures) against `/health/live` to accommodate SQL auto-resume and migrations. This endpoint is live only after startup database initialization finishes. Do not continually probe SQL in a health endpoint: doing so prevents SQL auto-pause.

SQL Server remains the sole production database provider. Historical SQLite migrations are excluded from compilation. `MigrationsSqlServer` contains the initial SQL schema and shipment dispatch traceability migration. Startup calls `MigrateAsync`; `--migrate-only` runs initialization and exits. Only one revision/migration runner may initialize at a time; do not deploy overlapping writers. Existing workflows explicitly manage transactions, so automatic EF operation retries are disabled to avoid replaying business/file side effects and the SQL retry-strategy transaction exception. Connection timeout should allow SQL cold resume.

## Durable files

`IFileStorage` has development `LocalFileStorage` and production `R2FileStorage` implementations. Production fails startup unless R2 is configured, or Local is explicitly enabled with a durable mount. Never enable the local override on Container Apps without a persistent volume.

- Customs: upload original Excel to private R2 objects under `customs/`; SQL stores the `r2:` object reference and filename. Commit SQL after upload succeeds. Failed SQL saves clean up the new object; old-file cleanup failures are logged and do not delete the newly committed object.
- Uploaded templates: private R2 objects under `Templates/`; SQL retains configuration and object reference. Exports download the selected template into memory. A missing uploaded template never silently becomes the default template.
- Bundled templates: read-only files published with the backend, especially `Templates/Shipment_Template.xlsx`.
- INV/PKL, settlement exports, ZIP, OCR image input and Excel import previews: in-memory/request data; these need no persistent filesystem. Regeneration uses SQL records and the retained template.
- R2 is accessed only by the API, behind existing authorization. Do not enable public R2 access or serve uploads through `wwwroot`.

Existing local attachments/template uploads are not migrated automatically to R2. Keep backups of `data/customs`, legacy `Uploads/Customs`, uploaded `Templates` and the existing SQL database before moving production. `--migrate-customs-storage` is the existing local legacy-file copy tool, not an R2 importer. To import existing files, upload with their exact keys, verify SHA-256 after download, then update only those metadata paths to `r2:<key>` in a controlled migration. Do not initialize a new empty database and call it a completed data migration. A source SQLite database requires a separate reviewed data conversion; do not point SQL Server at it. The historical `.db.backup_*` in source is excluded from Docker; its contents are not overwritten.

## Runtime configuration

Set secrets through Container Apps secrets or a secure portal takeover, never Git, chat, Docker build arguments or frontend Vite variables.

| Setting | Value / purpose |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | `Server=tcp:shoedocx-prod-nhutduy.database.windows.net,1433;Database=shoedocx;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connect Timeout=120;` |
| `Jwt__Key` | Secret, random, at least 32 bytes; stable across restarts/revisions |
| `FileStorage__Provider` | `R2` |
| `FileStorage__Endpoint` | `https://<account-id>.r2.cloudflarestorage.com` |
| `FileStorage__Bucket` | Private bucket name |
| `FileStorage__AccessKeyId` | Secret, scoped to this bucket |
| `FileStorage__SecretAccessKey` | Secret, scoped to this bucket |
| `OPENAI_API_KEY` | Optional secret; OCR is unavailable without it, not simulated |
| `BootstrapAdmin__Enabled` | `false` normally |
| `BootstrapAdmin__Username`, `BootstrapAdmin__Password`, `BootstrapAdmin__FullName` | Supply securely for initial bootstrap only |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command` | `Warning` recommended |

Enable the app system-assigned managed identity. As the Entra SQL admin, create its contained database user in `shoedocx` and grant the schema/data permissions required by EF migrations. Creating identities, granting DB permissions and federated CI access must be reviewed before applying. Prefer separate migration and runtime principals (migration: DDL; runtime: data access) if deployment automation is later split; the current startup migration requires DDL permissions. Never grant subscription-wide Owner to CI. Confirm SQL network restrictions permit the app; the simple Free Offer wizard creates `AllowAllWindowsAzureIps`, which allows network reachability from Azure services, but still requires database authentication. Review replacing this broad firewall rule with app outbound-IP rules when feasible; don't enable paid private endpoints without approval.

Bootstrap on a controlled first revision: securely set bootstrap credentials and enable it, create the account once, then set Enabled=false and remove the password secret. Verify login after that revision. Development user seeding only runs in Development; never use its public passwords in production. Authentication remains HttpOnly/Secure/SameSite=Strict and frontend `withCredentials: true`. Same-origin needs no production cross-origin CORS exception.

## Build, deploy and rollback

```bash
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj -c Release
cd frontend
npm ci
npm run build
cd ..
docker build -t shoedocx:review .
```

Publish an immutable image to a reviewed registry, then deploy that digest to Container Apps with the settings above. Avoid Azure Container Registry (paid) under the zero-cost requirement. For private GHCR, configure a read-only packages credential as an Azure registry secret via secure input. Do not expose this private app's image publicly without approval. Create an environment with Consumption only and `logs-destination none` to avoid provisioning paid Log Analytics; live console/system log streaming remains available, but there is no retained log archive. No NAT gateway, private endpoint, Dedicated workload profile or Defender paid tier.

The GitHub workflow verifies tests, SPA build and Docker build on PR/main. Deployment is disabled until repository variable `AZURE_DEPLOY_ENABLED=true`, Azure resources and GHCR pull access are configured. It uses GitHub OIDC via `azure/login`, not a stored Azure password/service-principal secret. Use the `production` GitHub environment with required reviewers where supported. Scope federation to `repo:Nhutduyasda/ShoeDocX:environment:production` and scope Azure role to this resource group. Repository variables: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_CONTAINER_APP`.

Rollback app code by redeploying the previous known-good image digest/revision. Keep JWT, SQL, storage and managed identity unchanged. A code rollback does not undo migrations. For an incompatible schema change, restore a backup into a separate database and verify before switching the connection; never drop production data to force an older migration.

## Backup and restore

Azure SQL uses locally redundant backup storage within the configured Free Offer quota. Verify actual retention and point-in-time restore options in the database before relying on them. Restoring/exporting a second database or extra storage can incur costs: review the destination and pricing first. Keep periodic logical database exports in a secured, reviewed destination. R2 objects need a separate backup: SQL backups contain metadata, not files. Download/copy objects plus a manifest of object keys, byte sizes and SHA-256 values, and test restoration against a separate controlled environment. Retain the paired database snapshot and object manifest together. Do not enable lifecycle deletion for business files.

## Free-tier constraints and approval boundary

- SQL: 100,000 vCore-seconds, 32 GB data and 32 GB backup per month, per offer. With overage disabled it pauses until the next month when the free compute quota is consumed. This may interrupt daily work; it is not an always-available free database. Preserve the disabled setting.
- Container Apps: subscription-wide monthly free grant of 180,000 vCPU-seconds, 360,000 GiB-seconds and 2 million HTTP requests. At 0.5 vCPU / 1 GiB, about 100 active replica-hours consumes the CPU/memory grant. Scale-to-zero has cold starts. `maxReplicas=1` limits capacity, not spend. There is no equivalent SQL-style automatic monthly free-quota stop in this configuration. Usage above the grant is billed; a budget alert is not a hard spending cap.
- R2 Standard: 10 GB-month storage, 1 million Class A and 10 million Class B operations monthly, with no egress fee. Higher usage is billed; adding R2 may require billing setup. Do not approve that enrollment/paid usage without the owner. Do not use Infrequent Access (different minimums/free-tier rules).
- OCR OpenAI calls are separately billed and are not covered by Azure/R2 grants. Leave the key unset unless an existing funded account and OCR use are authorized.
- GHCR and GitHub Actions have their own account/package/minute rules. Review private repository runner allowance and registry billing before enabling frequent builds.

References: [Container Apps billing](https://learn.microsoft.com/azure/container-apps/billing), [SQL Free Offer](https://learn.microsoft.com/azure/azure-sql/database/free-offer), [R2 pricing](https://developers.cloudflare.com/r2/pricing/), [logging options](https://learn.microsoft.com/azure/container-apps/log-options), [EF explicit transactions and retries](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency).

## Production acceptance (pending)

Record pass/fail against the real HTTPS URL: root + refreshed SPA path, unknown API 404, login/logout/revocation and cookie flags, SQL connection, Master Data CRUD/import, saved shipment, INV/PKL totals/files, template upload/download/export, customs sync/download, OCR only with funded key, no CORS or mixed content, no secrets in image/assets/logs, no critical application logs. Create clearly named disposable verification records; do not modify existing business records. Save object hashes and SQL identifiers, restart/redeploy the app, then verify the same records and download hashes. Remove only the explicitly disposable records through supported recovery-safe workflows.
