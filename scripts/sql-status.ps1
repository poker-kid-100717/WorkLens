$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

docker compose -f docker-compose.sql.yml ps
Write-Host ''
docker logs --tail 40 worklens-sqlserver
