# Technology Reference

Everything needed to build, run, and maintain WorkLens — grouped by what you
actually need installed for each activity. This app is self-hosted/on-prem by design,
so nothing here depends on Azure, AWS, or any cloud service except the optional OpenAI
API call for resume matching.

---

## 1. To run it (Docker Compose — the normal path)

This is the only thing required for day-to-day use once it's built.

| Tool | Version | Why |
| --- | --- | --- |
| **Docker Engine** | 24+ | Runs all three containers (SQL Server, API, frontend/nginx) |
| **Docker Compose** | v2 (bundled with modern Docker Desktop / `docker compose` CLI plugin on Linux) | Orchestrates the multi-container stack via `docker-compose.yml` |

No .NET SDK, Node.js, or SQL Server client tools are required on the host to *run* the
app this way — everything needed is baked into the container images at build time.

Hardware: SQL Server's Linux container needs **at least 2GB RAM** allocated to Docker
(Microsoft's documented minimum); 4GB+ recommended if you're also running the API and
frontend containers on the same host.

---

## 2. To build/develop it (source-level work)

Needed if you're editing code, not just running the prebuilt containers.

### Backend (.NET)

| Tool | Version | Why |
| --- | --- | --- |
| **.NET SDK** | 10.0.x | Builds `WorkLens.Api`, `.Core`, `.Infrastructure`; this project targets `net10.0` in every `.csproj` |
| **dotnet-ef** (CLI tool) | 9.0.0 | Generates/applies EF Core migrations (`dotnet tool install --global dotnet-ef --version 9.0.0`) |
| **SQL Server** (any edition, or the Docker container) | 2019+ | Local dev target for the connection string in `appsettings.Development.json` |

Key NuGet packages already referenced (installed automatically via `dotnet restore`,
listed here so you know what's pulled in): `Microsoft.EntityFrameworkCore.SqlServer`,
`Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Http`,
`Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.AspNetCore.OpenApi`.

No Swashbuckle/Swagger UI package — this project uses .NET 10's **built-in** OpenAPI
document generation (`app.MapOpenApi()`), exposed at `/openapi/v1.json`.

### Frontend (Angular)

| Tool | Version | Why |
| --- | --- | --- |
| **Node.js** | 20.x | Runs the Angular CLI and build tooling |
| **npm** | bundled with Node 20 | Installs `frontend/package.json` dependencies |
| **Angular CLI** | 18.2.x (`@angular/cli`, invoked via `npx ng`) | Build/serve/test commands |

Key packages: `@angular/*` 18.2.x, `rxjs` 7.8, `pdfjs-dist` 4.10.38 (patched version —
see the security note below), `wouter`-free routing (uses Angular's own `@angular/router`
with hash-free paths since this isn't sandboxed like the website-builder templates).

### Browser extension

No build step — plain Manifest V3 JavaScript/HTML/CSS, loaded unpacked directly into
Chrome/Edge's `chrome://extensions` developer mode. Node.js is only used in CI to
syntax-check the `.js` files, not to build the extension itself.

### Portfolio site

No build step — plain static HTML/CSS/JS. Any static file server works (see "Hosting
options" in the main README).

---

## 3. To maintain it (ongoing operations)

| Task | Tool/Skill needed |
| --- | --- |
| Adding a new job feed source | C# — implement `IJobFeedProvider` in `src/WorkLens.Infrastructure/FeedProviders/`, register in `DependencyInjection.cs` |
| Schema changes | `dotnet ef migrations add <Name>` + `dotnet ef database update` (or let `Database:AutoMigrate` apply it on next API startup) |
| Rotating the OpenAI key | Update `OPENAI_API_KEY` in `.env`, then `docker compose up -d --build api` |
| Fixing LinkedIn/Indeed extension selectors when their markup changes | Browser DevTools (inspect the page) + edit `content-linkedin.js` / `content-indeed.js` in `browser-extension/` |
| Updating your resume on the portfolio site | Replace `portfolio-site/resume.pdf` and edit the relevant section in `portfolio-site/index.html` directly (plain HTML, no templating) |
| Monitoring container health | `docker compose ps`, `docker compose logs -f api`, or the built-in `/api/health` endpoint |
| CI | GitHub Actions (workflow already included at `.github/workflows/ci.yml`) — needs no local setup, runs entirely on GitHub's runners |

### External accounts/keys needed

| Service | Required? | Purpose |
| --- | --- | --- |
| **OpenAI API key** | Optional | Powers the resume-to-job match scoring feature. Everything else works without it. Get one at platform.openai.com. |
| **GitHub account** | Only if using the provided CI workflow | Hosts the repo and runs `.github/workflows/ci.yml` |
| RemoteOK / Remotive / Greenhouse / Dice | No signup needed | All four feed sources are free and unauthenticated by design (see main README) |

Nothing here needs Azure, AWS, or any other cloud account — this is intentionally a
fully on-prem, self-contained stack per your original request.

---

## 4. Version matrix (what's pinned where)

| Component | Version | Pinned in |
| --- | --- | --- |
| .NET target framework | `net10.0` | Every `.csproj` |
| EF Core | 9.0.0 | `WorkLens.Infrastructure.csproj` |
| SQL Server (container) | `2022-latest` | `docker-compose.yml` |
| Angular | ^18.2.0 | `frontend/package.json` |
| Node (CI + Docker build stage) | 20.x | `.github/workflows/ci.yml`, `frontend/Dockerfile` |
| pdfjs-dist | ^4.10.38 | `frontend/package.json` (patched against [GHSA-wgrm-67xf-hhpq](https://github.com/advisories/GHSA-wgrm-67xf-hhpq)) |
| nginx (frontend runtime image) | 1.27-alpine | `frontend/Dockerfile` |
| OpenAI model (resume matching) | `gpt-4o-mini` (configurable) | `.env` → `OPENAI_MODEL` |

When bumping any of these, rebuild and re-verify: `dotnet build` for backend changes,
`npx ng build --configuration production` for frontend changes, and let CI catch
anything you missed before it reaches your server.
