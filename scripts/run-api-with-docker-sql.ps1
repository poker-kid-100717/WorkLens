$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path '.env')) {
    throw 'Missing .env. Copy .env.example to .env and set SQL_SA_PASSWORD first.'
}

$passwordLine = Get-Content '.env' | Where-Object { $_ -match '^SQL_SA_PASSWORD=' } | Select-Object -First 1
if (-not $passwordLine) {
    throw 'SQL_SA_PASSWORD is missing from .env.'
}

$password = $passwordLine.Substring('SQL_SA_PASSWORD='.Length)
if ([string]::IsNullOrWhiteSpace($password) -or $password -eq 'Change_Me_Strong_Passw0rd!') {
    throw 'Set a real SQL_SA_PASSWORD in .env first.'
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__WorkLensDb = "Server=localhost,1433;Database=WorkLensDb;User Id=sa;Password=$password;TrustServerCertificate=True;"

Set-Location (Join-Path $root 'src/WorkLens.Api')
dotnet run
