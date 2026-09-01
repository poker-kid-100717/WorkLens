$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path '.env')) {
    Copy-Item '.env.example' '.env'
    Write-Host 'Created .env from .env.example.' -ForegroundColor Yellow
    Write-Host 'Edit SQL_SA_PASSWORD in .env, then run this script again.' -ForegroundColor Yellow
    exit 1
}

$envText = Get-Content '.env' -Raw
if ($envText -match 'SQL_SA_PASSWORD=Change_Me_Strong_Passw0rd!') {
    Write-Host 'Change SQL_SA_PASSWORD in .env before starting SQL Server.' -ForegroundColor Yellow
    exit 1
}

docker compose -f docker-compose.sql.yml up -d

docker compose -f docker-compose.sql.yml ps
Write-Host ''
Write-Host 'SQL Server is available at localhost,1433.' -ForegroundColor Green
Write-Host 'Database name: WorkLensDb (created automatically when the API starts).' -ForegroundColor Green
