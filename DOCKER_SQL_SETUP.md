# WorkLens: SQL Server in Docker

This package supports two development modes:

1. **SQL Server only in Docker** — recommended while actively debugging the .NET API and Angular app locally.
2. **Entire stack in Docker** — SQL Server + API + frontend with one Compose command.

The SQL Server container uses Microsoft SQL Server 2022 Express and stores its data in a named Docker volume named `sqlserver-data`.

## Prerequisites

- Docker Desktop with Linux containers enabled
- For local API development: .NET 10 SDK
- For local frontend development: Node.js 20+ / npm

## First-time setup

From PowerShell in the project root:

```powershell
Copy-Item .env.example .env
notepad .env
```

Set a strong value for:

```text
SQL_SA_PASSWORD=your-strong-password
```

SQL Server requires a password with at least 8 characters and a mix of uppercase, lowercase, digits, and symbols.

## Option A: Run only SQL Server in Docker

Start SQL Server:

```powershell
docker compose -f docker-compose.sql.yml up -d
```

Or use the helper:

```powershell
.\scripts\start-sql.ps1
```

Check status:

```powershell
docker compose -f docker-compose.sql.yml ps
```

View logs:

```powershell
docker logs -f worklens-sqlserver
```

SQL Server will be reachable from Windows at:

```text
Server: localhost,1433
User: sa
Password: value from SQL_SA_PASSWORD in .env
Database: WorkLensDb
```

`WorkLensDb` is created automatically by EF Core migrations when the API starts.

### Run the .NET API locally against Docker SQL Server

Recommended helper command:

```powershell
.\scripts\run-api-with-docker-sql.ps1
```

Or set the connection string manually in the current PowerShell session:

```powershell
$env:ConnectionStrings__WorkLensDb = "Server=localhost,1433;Database=WorkLensDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
cd .\src\WorkLens.Api
dotnet run
```

The API applies EF Core migrations automatically on startup.

### Run Angular locally

In another PowerShell window:

```powershell
cd .\frontend
npm install
npm start
```

## Option B: Run the entire application in Docker

From the project root:

```powershell
docker compose up -d --build
```

Then open:

- App: http://localhost:8080
- API: http://localhost:5080
- Health: http://localhost:5080/api/health

In full-stack Docker mode, the API connects to SQL Server using the internal Docker hostname `sqlserver`, not `localhost`.

## Stop SQL Server without deleting data

```powershell
docker compose -f docker-compose.sql.yml down
```

The database remains in the Docker volume.

## Completely reset the database

This permanently deletes the SQL Server Docker volume and all WorkLens data:

```powershell
docker compose -f docker-compose.sql.yml down -v
```

Then start SQL Server again and start the API; migrations will recreate the schema.

## SSMS connection

If you use SQL Server Management Studio:

```text
Server name: localhost,1433
Authentication: SQL Server Authentication
Login: sa
Password: value from .env
Trust server certificate: enabled
```

## Useful Docker commands

```powershell
# List WorkLens containers
docker ps --filter "name=jobs"

# SQL logs
docker logs --tail 100 worklens-sqlserver

# Restart SQL only
docker restart worklens-sqlserver

# Inspect the persistent volume
docker volume ls | Select-String sqlserver-data
```
