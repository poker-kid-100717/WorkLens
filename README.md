# WorkLens

A self-hosted job feed and application tracker. Built for on-prem deployment: ASP.NET
Core 10 API + SQL Server backend, Angular 22 frontend, all wired together with Docker
Compose. No dummy/sample data ships with the app — the database starts empty and the
feed is only ever populated from the live sources below.

## What it does

- **Live job feed** — the Angular UI polls the backend every ~7 seconds (5-10s range)
  for the latest cached listings. The backend itself polls upstream sources on a much
  slower, configurable schedule (default 120s) and stores results in SQL Server, so the
  fast UI refresh never hits upstream APIs directly.
- **Real sources, no scraping**: [RemoteOK](https://remoteok.com/api),
  [Remotive](https://remotive.com/api/remote-jobs), the
  [Greenhouse public Job Board API](https://developers.greenhouse.io/job-board.html)
  (per-company board tokens you configure), and
  [Dice's official Job Search MCP server](https://www.dice.com/career-advice/dice-launches-mcp-server-for-ai-powered-job-search)
  (`mcp.dice.com`, no key required).
- **LinkedIn / Indeed**: neither offers a public jobs-search API, and both explicitly
  prohibit automated scraping in their Terms of Service. Rather than build something
  that breaks constantly and risks your account, use **Tracker → "Save from URL"** to
  paste in jobs you find there manually — they track exactly like feed-sourced jobs
  from that point on.
- **Application tracker** — pipeline board (Saved → Applied → Phone Screen →
  Interviewing → Offer / Rejected / Withdrawn / Ghosted), notes, contact info, and
  follow-up reminders with a due-now indicator in the header.
- **Analytics dashboard** — funnel counts, response/interview/offer rates, applications
  per week, and top companies applied to.
- **Search profiles** — named keyword filters (e.g. ".NET Remote") that control what the
  background refresh searches for across all sources.
- **Resume matching** — upload a resume (PDF or pasted text; PDF text extraction runs
  entirely client-side via PDF.js, never touching the backend) and get an AI-generated
  0-100 fit score, matching/missing skills, and a one-line summary for any job in the
  feed. Powered by the OpenAI API — see "Resume matching setup" below.
- **Browser extension** — a Manifest V3 Chrome/Edge extension (in `browser-extension/`)
  that one-click-saves the LinkedIn or Indeed job posting you're currently viewing
  straight into your tracker, auto-filling title/company/location off the page. No
  scraping, no background crawling — it only reads the page you're already on when you
  click it. See `browser-extension/README.md` for installation (it's not published to
  any extension store; you load it unpacked).
- **Public portfolio site** — a standalone, shareable static site (in `portfolio-site/`)
  built from your resume — the link you actually hand to recruiters/companies. Fully
  separate from your private tracker data. See "Portfolio site" below.

See [TECHNOLOGIES.md](TECHNOLOGIES.md) for the full list of tools, SDKs, and accounts
needed to build, run, and maintain this project — including version pins and what's
optional vs. required.

## Architecture

```
WorkLens/
├── src/
│   ├── WorkLens.Api/              ASP.NET Core Web API (controllers, Program.cs, Dockerfile)
│   ├── WorkLens.Core/             Domain entities, enums, DTOs, repository interfaces
│   └── WorkLens.Infrastructure/   EF Core DbContext + migrations, repositories,
│                                     feed providers (RemoteOK/Remotive/Greenhouse/Dice),
│                                     background refresh service
├── frontend/                        Angular 22 app (feed, tracker, analytics, search profiles)
│   ├── nginx.conf                   Serves the built app + proxies /api to the backend
│   └── Dockerfile
├── docker-compose.yml                SQL Server + API + Angular/nginx, wired together
├── .env.example                      Copy to .env and fill in before running
├── browser-extension/                 Manifest V3 extension: one-click save from LinkedIn/Indeed
└── portfolio-site/                    Standalone static portfolio/resume site (shareable link)
```

Clean Architecture layering: `Core` has zero framework dependencies (no EF, no ASP.NET),
`Infrastructure` implements persistence and external integrations against `Core`'s
interfaces, and `Api` wires it all together via DI (`WorkLens.Infrastructure/DependencyInjection.cs`).

## SQL Server in Docker for local development

If you want **SQL Server in Docker while running the .NET API and Angular frontend locally**,
use the dedicated `docker-compose.sql.yml` file:

```powershell
Copy-Item .env.example .env
# Edit SQL_SA_PASSWORD in .env
docker compose -f docker-compose.sql.yml up -d
.\scripts\run-api-with-docker-sql.ps1
```

SQL Server is exposed at `localhost,1433`; EF Core creates/migrates `WorkLensDb` when
the API starts. See [DOCKER_SQL_SETUP.md](DOCKER_SQL_SETUP.md) for the complete Windows
workflow, SSMS settings, stop/reset commands, and full-stack Docker mode.

## Running it on-prem with Docker Compose

Prerequisites: Docker + Docker Compose on the host.

1. Copy the environment template and set a strong SQL Server password:
   ```bash
   cp .env.example .env
   # edit .env — set SQL_SA_PASSWORD to something that meets SQL Server's complexity policy
   ```

2. (Optional) Add Greenhouse company board tokens in `.env` if you want specific
   companies' postings in the feed. Find a token in a company's public career page URL:
   `boards.greenhouse.io/{board_token}`.

3. Build and start everything:
   ```bash
   docker compose up -d --build
   ```

4. Open the app: **http://localhost:8080** (or whatever host/port you map it to).
   The API is reachable directly at **http://localhost:5080** (Swagger/OpenAPI JSON at
   `/openapi/v1.json` when `SWAGGER_ENABLED=true`).

5. On first start, the API automatically applies EF Core migrations and creates the
   `WorkLensDb` schema — no manual DB setup needed. The database starts completely
   empty; nothing is seeded.

6. Go to **Search Profiles** in the app and add at least one keyword profile (e.g.
   "C#, .NET, Angular, Azure, full stack") — the background refresh has nothing to pull
   until you tell it what to search for.

### Stopping / rebuilding

```bash
docker compose down          # stop everything, keep the SQL Server volume
docker compose down -v       # stop everything and wipe the database volume
docker compose up -d --build # rebuild after code changes
```

## Running it without Docker (local development)

**Backend** (requires .NET 10 SDK and a reachable SQL Server instance):
```bash
cd src/WorkLens.Api
dotnet user-secrets set "ConnectionStrings:WorkLensDb" "Server=localhost,1433;Database=WorkLensDb;User Id=sa;Password=...;TrustServerCertificate=True;"
dotnet run
```
Migrations apply automatically on startup (`Database:AutoMigrate` defaults to `true` in
`appsettings.json`). To manage migrations manually instead:
```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/WorkLens.Infrastructure --startup-project src/WorkLens.Api
```

**Frontend** (requires Node 22.22+ or 24+):
```bash
cd frontend
npm install
npm start   # ng serve on http://localhost:4200, proxying to the API's CORS-allowed origin
```
For local dev without Docker, either run `ng serve --proxy-config proxy.conf.json`
(add one pointing `/api` at `http://localhost:5080`) or set
`window.__WORKLENS_API_BASE__` in `public/env.js` to the API's full URL.

## Configuration reference

All backend settings live in `src/WorkLens.Api/appsettings.json`, overridable via
environment variables (Docker Compose already does this — see `docker-compose.yml`):

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:WorkLensDb` | SQL Server connection string |
| `JobFeeds:RefreshIntervalSeconds` | How often the backend polls upstream feeds (min 30s enforced in code; keep it well above 10s to be a good citizen of the free APIs) |
| `JobFeeds:Greenhouse:BoardTokens` | Array of Greenhouse company board tokens to include |
| `Cors:AllowedOrigins` | Origins allowed to call the API (the Angular app's URL) |
| `Database:AutoMigrate` | Auto-apply EF Core migrations on startup (default `true`) |
| `Swagger:Enabled` | Expose the native OpenAPI document |

Frontend runtime config: `frontend/public/env.js` — set `window.__WORKLENS_API_BASE__`
if the API isn't reachable at the default relative `/api` path (e.g. different host).

## Notes on the job sources

- **RemoteOK** (`remoteok.com/api`) and **Remotive** (`remotive.com/api/remote-jobs`)
  are free, public, unauthenticated JSON feeds.
- **Greenhouse** (`boards-api.greenhouse.io`) is a free, public, unauthenticated API,
  but it's per-company — you provide the board token(s) for companies you care about.
- **Dice** has no public REST API, but officially runs a free MCP server at
  `mcp.dice.com/mcp` (no auth) with a `search_jobs` tool. `DiceFeedProvider` speaks its
  JSON-RPC/SSE protocol directly over HTTP.
- **LinkedIn / Indeed** intentionally have no automated integration in this app — see
  "What it does" above. Use "Save from URL" in the Tracker tab instead.

## Resume matching setup

1. Set `OPENAI_API_KEY` in `.env` (get one at https://platform.openai.com/api-keys).
   Leave it blank to disable match scoring — everything else in the app works fine
   without it, the Feed page just won't show match badges.
2. Restart the API container (`docker compose up -d --build api`) so it picks up the
   new environment variable.
3. In the app, go to **Resume**, upload a PDF or paste your resume text, and save.
   PDF text extraction happens entirely in your browser (via PDF.js) — the backend
   never receives or parses the raw PDF file itself.
4. On the **Feed** page, click **Score matches vs. resume** to get a 0-100 fit score,
   matching/missing skills, and a short summary for the jobs currently on screen.
   Scores are cached per (resume, job) pair in the `JobMatches` table, so re-viewing
   the same job doesn't re-call OpenAI.

Match scoring is deliberately **not** part of the 5-10s feed poll — it's an on-demand
button click, both to control OpenAI costs and because scoring is a heavier operation
than a cache read.

## Browser extension

See `browser-extension/README.md` for full setup. Short version: load it unpacked in
Chrome/Edge developer mode, point it at your API's base URL in its Settings page, then
click the extension icon on any LinkedIn or Indeed job posting to save it straight into
your tracker.

## Portfolio site

`portfolio-site/` is a plain static HTML/CSS/JS site — no build step, no backend,
completely independent of the tracker app. It's meant to be the link you send to
recruiters and hiring managers, separate from your private job-search data.

To preview it locally:
```bash
cd portfolio-site
python3 -m http.server 8000
# open http://localhost:8000
```

To host it, upload the folder's contents to any static host (GitHub Pages, Netlify,
an S3 bucket, or a simple nginx container — the same pattern as `frontend/nginx.conf`
works here too). Replace `resume.pdf` with your latest resume file if you want the
"Download résumé" button to serve something current, and edit the content directly in
`index.html` as your experience changes.

## Continuous integration

`.github/workflows/ci.yml` runs on every push/PR to `main` (and can be triggered
manually) with five independent jobs:

| Job | What it checks |
| --- | --- |
| **backend** | `dotnet build` + `dotnet format --verify-no-changes`, then applies every EF Core migration against a real SQL Server service container (not just "does it compile" — catches actual schema/migration bugs), then a publish smoke test |
| **frontend** | `npm ci`, production build, unit tests in headless Chrome, and an informational (non-blocking) `npm audit` |
| **extension** | Validates `manifest.json` is well-formed, every file it references exists, and every `.js` file parses |
| **docker-compose** | `docker compose config` + builds the `api` and `frontend` images |
| **portfolio-site** | HTML validation and a check for broken local asset references |

There's no deploy job — this is a self-hosted, on-prem app, so shipping anywhere is a
deliberate manual step you run yourself (`docker compose up -d --build`, per
"Running it on-prem" above). CI's job is to catch break-the-build issues before you
pull changes onto your server, not to push anything automatically.

## A note on dependency security

The frontend is on Angular 22, and `npm audit` reports no known advisories. CI runs
`npm audit --audit-level=high` on every push and pull request, so a newly disclosed
high or critical advisory fails the build instead of going unnoticed.

`pdfjs-dist` (used for client-side resume PDF text extraction) is pinned to 4.10.38,
which is patched against the known arbitrary-JS-execution advisory in earlier 4.x
versions ([GHSA-wgrm-67xf-hhpq](https://github.com/advisories/GHSA-wgrm-67xf-hhpq)).

## Extending with more sources

Implement `IJobFeedProvider` (see any class under
`src/WorkLens.Infrastructure/FeedProviders/`), register it in
`WorkLens.Infrastructure/DependencyInjection.cs`, and the aggregator will pick it up
automatically on the next refresh cycle — no other code changes needed.
