# WorkLens Technology Reference

This document records the runtime, build, integration, and operational tooling used by WorkLens. The architecture and operating model are described in the main [README](README.md).

## Runtime stack

| Area | Technology | Version / role |
| --- | --- | --- |
| API | ASP.NET Core | .NET 10 |
| Domain / infrastructure | C# | `net10.0` |
| ORM | Entity Framework Core | 10.0.11 |
| Database | SQL Server | 2022 container |
| Frontend | Angular | 18.2.x |
| Reactive client | RxJS | 7.8.x |
| Reverse proxy | nginx | 1.27 Alpine |
| Containers | Docker / Compose | Compose v2 |
| Resume PDF extraction | PDF.js (`pdfjs-dist`) | 4.10.38+ |
| Browser extension | Chrome/Edge Manifest V3 | plain JavaScript |
| CI | GitHub Actions | Ubuntu runners |

## Backend packages

The backend targets .NET 10 consistently across all projects. EF Core packages and the `dotnet-ef` CLI are kept on the same 10.0.11 line to avoid target-framework/tooling drift.

Primary packages:

- `Microsoft.AspNetCore.OpenApi`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.Extensions.Http`
- `Microsoft.Extensions.Hosting.Abstractions`

Project responsibilities:

```text
WorkLens.Core
  domain entities, enums, DTOs, abstractions

WorkLens.Infrastructure
  EF Core, repositories, migrations, external providers,
  hosted services, OpenAI and Microsoft Graph integrations

WorkLens.Api
  HTTP endpoints, composition root, CORS, Problem Details,
  OpenAPI exposure and runtime startup policy
```

## Frontend tooling

Development/build prerequisites:

| Tool | Version |
| --- | --- |
| Node.js | 20.x |
| npm | version bundled with Node 20 |
| Angular CLI | 18.2.x |
| TypeScript | 5.5.x |

The production Angular application is compiled in a Node build stage and copied into an nginx runtime image. The browser calls relative `/api` URLs; nginx proxies those requests to the API container over the private Docker network.

## External integrations

### Job sources

WorkLens contains provider implementations for:

- RemoteOK
- Remotive
- Greenhouse Job Board API
- Dice MCP
- Jobicy
- We Work Remotely
- optional ChatGPT-watch feed ingestion

LinkedIn and Indeed are intentionally not background-scraped. The Manifest V3 extension reads the page the user has explicitly opened and saves that posting into WorkLens.

### OpenAI

Optional resume/job matching uses an API key supplied at runtime through `OPENAI_API_KEY`. Provider-specific code is isolated behind `IResumeMatchingService`.

The model name is configurable with `OPENAI_MODEL` and currently defaults to `gpt-4o-mini`.

### Microsoft Outlook / Graph

Optional Outlook communication tracking uses Microsoft identity-platform OAuth and Microsoft Graph delegated permissions:

- `User.Read`
- `Mail.Read`
- `offline_access`
- `openid`
- `profile`

The default self-hosted callback is routed through nginx:

```text
http://localhost:8080/api/outlook/callback
```

## Container topology

The normal full-stack Compose profile runs:

1. `sqlserver` — SQL Server 2022 Express, private Docker network only.
2. `api` — ASP.NET Core 10, private Docker network only.
3. `frontend` — nginx + Angular, published as host port `8080`.

Only the nginx gateway is published by the normal stack. `docker-compose.sql.yml` is a separate local-development profile that deliberately publishes SQL Server on port 1433.

## Configuration and secret handling

Committed JSON configuration contains non-secret defaults only. Runtime secrets are supplied via `.env`, user secrets, or environment variables.

`.env` is gitignored. `.env.example` documents required/optional variables without containing live credentials.

Production-oriented defaults:

- `Database:AutoMigrate = false`
- `Swagger:Enabled = false`
- OpenAI and Outlook credentials empty/unconfigured

The self-hosted Docker profile explicitly enables startup migrations because it runs as a single API instance. A multi-instance deployment should leave auto-migration disabled and run migrations as a release step.

## Testing and CI

Backend unit tests live under `tests/WorkLens.Infrastructure.Tests`. The solution-level CI flow performs:

- `dotnet restore`
- Release build
- `dotnet format --verify-no-changes`
- `dotnet test`
- EF migration execution against a real SQL Server service container
- API publish smoke test
- Angular production build
- Angular headless unit tests
- critical npm dependency audit
- browser-extension manifest and JavaScript validation
- Docker Compose validation and image builds
- static portfolio HTML/asset validation

## Local tooling

For full-stack execution, only Docker Engine/Desktop and Docker Compose v2 are required.

For source-level backend development:

- .NET SDK 10.0.x
- `dotnet-ef` 10.0.11
- SQL Server (local or containerized)

For source-level frontend development:

- Node.js 20.x
- npm
- Angular CLI through the repository (`npx ng`)

## Version-change checklist

When changing a major framework/runtime version:

1. Keep target frameworks and corresponding framework packages on compatible lines.
2. Update the EF Core runtime, design package, and `dotnet-ef` together.
3. Recreate/verify the lock file for npm changes.
4. Run backend and frontend test suites.
5. Apply all EF migrations to a clean SQL Server instance.
6. Build both Docker images.
7. Run `docker compose config` and a local full-stack smoke test.
8. Update this file and the README if the operating model changes.
