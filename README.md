# WorkLens

WorkLens is a self-hosted job-discovery and application-tracking platform built as a full-stack reference application. It combines an ASP.NET Core 10 API, SQL Server, Angular 18, background integrations, optional AI-assisted resume matching, Microsoft Outlook communication tracking, a Manifest V3 browser extension, Docker Compose, and GitHub Actions.

The project is intentionally designed as a **single-user, self-hosted system** rather than a multi-tenant SaaS product. That keeps the domain focused while still demonstrating production-oriented architecture, integration boundaries, persistence, background processing, CI, containerization, and frontend delivery.

## Highlights

- Aggregates jobs from RemoteOK, Remotive, Greenhouse, Dice MCP, Jobicy, We Work Remotely, and an optional ChatGPT-watch feed.
- Persists normalized listings in SQL Server and serves the UI from local storage rather than repeatedly calling upstream providers.
- Tracks applications through a configurable pipeline with notes, contacts, status history, and follow-up reminders.
- Supports named search profiles and resume-derived targeting.
- Optionally scores visible jobs against an active resume using the OpenAI API and caches the result per resume/listing pair.
- Optionally connects to Microsoft Graph to associate job-related Outlook messages with tracked applications.
- Includes a Chrome/Edge Manifest V3 extension for explicitly saving the LinkedIn or Indeed page the user is currently viewing.
- Ships as a three-service Docker Compose stack with a single browser-facing ingress through nginx.
- Runs backend, frontend, database-migration, browser-extension, Docker, dependency-audit, and static-site checks in GitHub Actions.

## Architecture

```text
Browser / Extension
        |
        v
nginx :8080                         Browser-facing ingress
  |  \
  |   +---- Angular SPA / static assets
  |
  +-------- /api/* --------------------------+
                                               |
                                               v
                                      ASP.NET Core 10 API
                                      controllers / DI
                                               |
                         +---------------------+----------------------+
                         |                                            |
                         v                                            v
                 WorkLens.Core                              WorkLens.Infrastructure
           entities / enums / contracts              EF Core / repositories / providers
                         ^                           background services / integrations
                         |                                            |
                         +---------------- dependency direction ------+
                                                                      |
                                                                      v
                                                               SQL Server 2022
```

### Dependency direction

`WorkLens.Core` is the innermost project and has no ASP.NET Core or Entity Framework dependency. It contains domain entities, enums, DTOs, and repository/service abstractions.

`WorkLens.Infrastructure` references Core and implements persistence, EF Core mappings/migrations, job-feed providers, resume matching, Outlook integration, and hosted background services.

`WorkLens.Api` is the composition root. It owns HTTP concerns, controller routing, CORS, Problem Details, OpenAPI exposure, and dependency registration. It references Core and Infrastructure but does not contain persistence implementation.

The Angular client is feature-oriented (`feed`, `tracker`, `communications`, `analytics`, `search-profiles`, `resume`) with shared models/services under `core/`. Routes use standalone lazy-loaded components.

## Runtime topology

The normal full-stack `docker-compose.yml` exposes **only nginx on host port 8080**. The API and SQL Server remain on the private Compose network. This keeps the normal request path same-origin and avoids exposing the database or application API directly to the host network.

```text
host:8080 -> nginx -> api:8080 -> sqlserver:1433
```

`docker-compose.sql.yml` is intentionally different: it is a development helper that publishes SQL Server on `localhost:1433` so the API can be run directly from the .NET SDK while SQL remains containerized.

## Repository layout

```text
WorkLens/
├── src/
│   ├── WorkLens.Api/                 ASP.NET Core HTTP/composition layer
│   ├── WorkLens.Core/                domain types and abstractions
│   └── WorkLens.Infrastructure/      persistence + external integrations
├── tests/
│   └── WorkLens.Infrastructure.Tests/ backend unit tests
├── frontend/                         Angular SPA + nginx gateway
├── browser-extension/                Manifest V3 Chrome/Edge extension
├── portfolio-site/                   optional standalone static portfolio artifact
├── scripts/                          local-development helper scripts
├── docker-compose.yml                full self-hosted stack
├── docker-compose.sql.yml            SQL-only local development profile
├── .env.example                      non-secret environment template
└── .github/workflows/ci.yml          build/test/security validation
```

## Backend design

### Persistence

EF Core uses explicit entity configurations and migrations in `WorkLens.Infrastructure/Persistence`. Repository interfaces live in Core and implementations live in Infrastructure.

Feed ingestion performs batched lookups when upserting provider results instead of issuing one existence query per listing. Read-only listing/application queries use no-tracking where appropriate. A unique index on `(Source, ExternalId)` provides database-level de-duplication.

### Background processing

`FeedRefreshBackgroundService` creates a fresh DI scope per refresh cycle. External provider requests are network-bound and run concurrently; persistence is then applied through the scoped EF Core context without using the context concurrently across threads.

Provider failures are isolated so one unavailable source does not prevent healthy sources from refreshing. Feed health is exposed to the UI from in-memory refresh state.

### API behavior

- Pagination is bounded server-side.
- Cancellation tokens flow through controllers, repositories, EF Core, and HTTP integrations.
- API failures use ASP.NET Core Problem Details for unhandled exceptions.
- Health responses do not return raw exception details.
- Production configuration disables OpenAPI by default.
- Production configuration does not auto-run migrations by default; the single-instance Compose profile explicitly opts in for local/self-hosted convenience.

## Frontend design

The Angular client uses standalone components and lazy route loading. API access is isolated behind typed services. The production app calls relative `/api` routes, allowing nginx to provide a same-origin gateway and avoiding environment-specific API URLs baked into the bundle.

The feed uses RxJS polling against WorkLens's cached API, not against upstream job boards. Network errors leave the polling pipeline alive so a later interval can recover without a full page reload.

PDF resume text extraction runs in the browser with PDF.js; the raw PDF file is never uploaded to the API. Only extracted text is saved when the user explicitly submits it.

## Job-source strategy

WorkLens intentionally distinguishes supported public integrations from user-assisted capture:

- **RemoteOK / Remotive / Jobicy / We Work Remotely**: public feeds/APIs.
- **Greenhouse**: public Job Board API using configured company board tokens.
- **Dice**: official MCP job-search endpoint.
- **LinkedIn / Indeed**: no background crawler. The browser extension only reads the job page the user has actively opened and saves that explicit selection into WorkLens.

This avoids presenting brittle scraping as an integration strategy.

## Resume matching

Resume matching is optional. Set `OPENAI_API_KEY` to enable it. Match scoring is explicitly user-triggered rather than part of the seven-second UI poll, and results are cached in `JobMatches` by resume/listing pair.

The OpenAI provider is implemented behind `IResumeMatchingService`, keeping provider-specific HTTP code in Infrastructure and allowing another implementation to be substituted without changing the API or domain layer.

## Outlook integration

Outlook integration is optional and uses Microsoft identity-platform OAuth plus Microsoft Graph delegated permissions (`User.Read`, `Mail.Read`, `offline_access`, `openid`, `profile`).

The OAuth callback uses the same nginx gateway as the application:

```text
http://localhost:8080/api/outlook/callback
```

Authorization state is time-limited and compared using a fixed-time comparison. Public callback errors use stable error codes; internal exception details are logged server-side rather than reflected into browser query strings.

For this single-user deployment, Outlook state is stored on a dedicated Docker volume. Do not expose that volume or copy it into source control.

## Configuration and secrets

No real credentials belong in committed configuration. `appsettings.json` contains non-secret defaults only, and `.env.example` contains placeholders.

For Docker:

```bash
cp .env.example .env
# set SQL_SA_PASSWORD and any optional integration values
docker compose up -d --build
```

For direct .NET development, use user secrets or environment variables:

```powershell
cd src/WorkLens.Api
dotnet user-secrets set "ConnectionStrings:WorkLensDb" "Server=localhost,1433;Database=WorkLensDb;User Id=sa;Password=...;TrustServerCertificate=True;"
dotnet run
```

Important settings:

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:WorkLensDb` | SQL Server connection string |
| `Database:AutoMigrate` | Apply EF migrations on startup |
| `Swagger:Enabled` | Expose `/openapi/v1.json` outside Development |
| `Cors:AllowedOrigins` | Allowed direct API callers for non-gateway development |
| `JobFeeds:RefreshIntervalSeconds` | Background provider refresh interval |
| `JobFeeds:Greenhouse:BoardTokens` | Greenhouse company tokens |
| `JobFeeds:OpenAi:ApiKey` | Optional AI-match credential |
| `Outlook:*` | Optional Microsoft identity/Graph integration |

## Run the full stack

Prerequisites: Docker Engine / Docker Desktop with Compose v2.

```bash
cp .env.example .env
# edit .env and set SQL_SA_PASSWORD
docker compose up -d --build
```

Open:

```text
http://localhost:8080
```

Useful operations:

```bash
docker compose ps
docker compose logs -f api
docker compose logs -f frontend
docker compose down
docker compose down -v   # destructive: also removes database/state volumes
```

## Local development with SQL Server in Docker

```powershell
Copy-Item .env.example .env
# set SQL_SA_PASSWORD
docker compose -f docker-compose.sql.yml up -d
.\scripts\run-api-with-docker-sql.ps1
```

Then run the Angular development server:

```bash
cd frontend
npm ci
npm start
```

The Angular dev server proxies `/api` to the local API according to `proxy.conf.json`.

## Database migrations

Install the EF CLI at the same major/minor line as the project packages:

```bash
dotnet tool install --global dotnet-ef --version 10.0.11
```

Apply migrations manually:

```bash
dotnet ef database update \
  --project src/WorkLens.Infrastructure/WorkLens.Infrastructure.csproj \
  --startup-project src/WorkLens.Api/WorkLens.Api.csproj
```

The normal Docker Compose profile opts into automatic migration because it is intended as a single-instance self-hosted deployment. For multi-instance production hosting, keep `Database:AutoMigrate=false` and run migrations as an explicit release step.

## Browser extension

See [`browser-extension/README.md`](browser-extension/README.md). Load the unpacked extension in Chrome or Edge, configure the WorkLens API/gateway URL, and use it only on the job page you are actively viewing.

Permissions are deliberately limited to storage, active-tab/scripting behavior, the supported job-site hosts, localhost, and an optional API origin the user explicitly grants from extension settings.

## Continuous integration

`.github/workflows/ci.yml` runs on pull requests and pushes to `main`.

| Job | Validation |
| --- | --- |
| Backend | restore, Release build, `dotnet format`, unit tests, real SQL Server migration test, publish smoke test |
| Frontend | clean npm install, production build, headless unit tests, critical dependency audit |
| Extension | manifest validation, referenced-file checks, JavaScript syntax checks |
| Docker Compose | configuration validation and API/frontend image builds |
| Portfolio | HTML validation and relative-asset checks |

The workflow deliberately contains no deployment step. This repository is self-hosted; promotion to a host is an explicit operational action rather than an implicit side effect of pushing source code.

## Engineering trade-offs

WorkLens is intentionally not pretending to be a multi-tenant enterprise SaaS product. Authentication/authorization between multiple users, distributed cache, horizontal orchestration, queue-backed ingestion, centralized observability, and managed secret stores would be appropriate additions if the deployment model changed.

For its actual scope—a private, single-user job-search platform—the architecture keeps those concerns out while still maintaining clear dependency boundaries, bounded external calls, persistent migrations, error isolation, container networking, CI, and testable business logic.

See [`TECHNOLOGIES.md`](TECHNOLOGIES.md) for the version/tooling matrix.
