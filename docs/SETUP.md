# Local setup

[Back to README](../README.md)

## Prerequisites and database

- Git; .NET 8 SDK for native development.
- Node 22.12+ and npm for native frontend development. Node 24 is also suitable for the draft tests.
- A reachable SQL Server instance and a dedicated development database. The API uses `UseSqlServer`; historical SQLite migrations are excluded from compilation.
- Docker with Compose for the container route. SQL Server is external: Compose does not create a database service.

The API applies its SQL Server migrations on startup. Use an account able to create/update the development schema. Do not point a first-run demo at a production database.

## Docker Compose

```bash
git clone https://github.com/Nhutduyasda/ShoeDocX.git
cd ShoeDocX
cp .env.example .env
```

PowerShell copy: `Copy-Item .env.example .env`.

Edit `.env` locally; it is ignored by Git:

| Variable | Purpose |
| --- | --- |
| `SQLSERVER_CONNECTION_STRING` | Required SQL Server connection reachable from the API container |
| `JWT_KEY` | Required random signing secret, at least 32 bytes |
| `WEB_PORT` | Web port; set `5173` for this guide, otherwise Compose defaults to `80` |
| `BACKEND_PORT` | Direct API port; default `5270` |
| `OPENAI_API_KEY` | Optional for manual workflows; required for OCR. Replace the example placeholder or leave empty |
| `BOOTSTRAP_ADMIN_ENABLED` | Enable only for first administrator creation |
| `BOOTSTRAP_ADMIN_USERNAME` / `BOOTSTRAP_ADMIN_PASSWORD` | Your chosen username and unique strong password |
| `BOOTSTRAP_ADMIN_FULL_NAME` | Administrator display name |

On Docker Desktop, `host.docker.internal` can address a host SQL Server. Enable SQL Server TCP connectivity and configure an appropriate database login; Windows integrated authentication in the native default configuration does not automatically work inside a Linux container. Use encryption appropriate to your server rather than treating example connection settings as production guidance.

Generate a JWT secret locally, for example in PowerShell:

```powershell
$jwtBytes = [byte[]]::new(48)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
[Convert]::ToBase64String($jwtBytes)
```

Keep that value private. Set bootstrap fields before the first start. Identity requires a password of at least 10 characters, including uppercase, lowercase, digit and non-alphanumeric characters.

```bash
docker compose up -d --build
docker compose ps
docker compose logs -f backend
```

Open [http://localhost:5173](http://localhost:5173) with `WEB_PORT=5173`. Authenticate with the administrator you configured. After creation, set bootstrap to `false`, remove the bootstrap password from `.env`, and apply the changed environment:

```bash
docker compose up -d --force-recreate backend
```

Department roles include `Admin`, `Kho` (warehouse), `Xnk` (export-import) and `KeToan` (accounting); certain unlock actions also authorize `XnkManager`. No general user-management or registration endpoint was found in the audited controllers. Bootstrap creates an administrator; provisioning additional role accounts requires a separately reviewed administration procedure. Do not assume there is a self-service registration flow.

### Current container limitations

The backend Dockerfile still has a legacy SQLite-style default connection string; Compose overrides it with the required SQL Server connection. A standalone backend container needs an explicit SQL Server override.

The frontend Dockerfile uses `node:20-alpine`, while the current Vite toolchain expects a recent Node release. The Docker build was not executed during this audit, so compatibility of the image's resolved Node version remains unverified. Native verification used the installed runtime. These documentation changes do not alter Dockerfiles.

Swagger is disabled in the Compose Production environment. `/api` is a route prefix rather than a browsable index.

## Native development on Windows

From the repository root:

```powershell
Set-Location backend/ShoeExportInvoice.Api
dotnet user-secrets set "Jwt:Key" "<your-random-secret>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-development-SQL-Server-connection>"
dotnet user-secrets set "DevelopmentUsers:Enabled" "false"
dotnet user-secrets set "BootstrapAdmin:Enabled" "true"
dotnet user-secrets set "BootstrapAdmin:Username" "<your-chosen-username>"
dotnet user-secrets set "BootstrapAdmin:Password" "<your-unique-strong-password>"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --urls "http://localhost:5270"
```

Replace bracketed values locally. The checked-in Development settings enable development-user seeding, so explicitly disable it as shown for a clean demo. After the administrator is created, stop the API, set `BootstrapAdmin:Enabled` to `false`, remove `BootstrapAdmin:Password` with `dotnet user-secrets remove`, and restart.

In a second terminal from the repository root:

```powershell
Set-Location frontend
npm ci
npm run dev
```

The UI is at [http://localhost:5173](http://localhost:5173); Development Swagger is at [http://localhost:5270/swagger](http://localhost:5270/swagger).
Vite proxies `/api` to port 5270. `XNK_API_TARGET` changes that proxy target; `VITE_API_URL` changes the client base URL. Same-origin proxying is the default path for cookie authentication.

## OCR configuration

Current OCR uses OpenAI, with `OpenAI:Model` and `OpenAI:Endpoint` read from configuration. The checked-in model is `gpt-4o-mini`; this is a configuration value, not a recommendation or availability guarantee.

For native Development, set `OpenAI:ApiKey` using user secrets or set `OPENAI_API_KEY` in the API process environment. Compose maps `OPENAI_API_KEY`. Never add keys to tracked `appsettings.json`. Missing credentials produce an error; there is no automatic simulated OCR fallback. Manual entry and quick paste remain available.

AI-assisted template analysis is a separate service with demo credit behavior; configure and verify it separately before relying on it.

## Templates, files and maintenance

Compose mounts `./Templates` at `/app/Templates` and `./data` at `/app/data`. SQL Server holds records; `data` is for file storage, including customs attachments. Back up the database **and** attachment/template storage together.

The default template is `Templates/Shipment_Template.xlsx`, configurable through `Xnk:ShipmentTemplatePath` outside the fixed Compose variable list. Partner templates may use their own cell mappings. See [ARCHITECTURE.md](ARCHITECTURE.md). Do not publish existing business workbooks as demo previews.

For an upgrade from legacy customs storage, back up first, then run the API with `--migrate-customs-storage` in the correct configured environment. The maintenance path retains original `Uploads/Customs` files for comparison and exits after migration/integrity checks. `--migrate-only` initializes/migrates the database and exits. Verify backups and configuration before invoking either against important data.

```bash
docker compose logs -f
docker compose restart
docker compose down
```

`restart` does not apply modified environment variables; use `up -d --force-recreate` for those changes. `down` stops/removes the app containers; SQL Server and bind-mounted files are separate.
