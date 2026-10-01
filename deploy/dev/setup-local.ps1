#Requires -Version 7.4
<#
.SYNOPSIS
  Lokální databáze a úložiště EshopGuard: role, databáze eshopguard a eshopguard_test, user-secrets a deploy/.env pro MinIO.

.DESCRIPTION
  1. Vygeneruje pět hesel databázových rolí (RandomNumberGenerator).
  2. Spustí deploy/sql/00_roles.sql jako postgres pro databáze eshopguard a eshopguard_test.
     Hesla dostane psql jen v proměnných prostředí svého procesu, ne v parametrech.
  3. Zapíše připojení do dotnet user-secrets (eshopguard-data, eshopguard-api, eshopguard-worker, eshopguard-tests)
     přes standardní vstup, ne jako argument.
  4. Vytvoří deploy/.env pro MinIO (root a uživatel aplikace), pokud ještě neexistuje.

  Heslo superuživatele postgres se čte jen z $env:PGPASSWORD nastaveného v této relaci; skript ho nikam neukládá.
  Žádné vygenerované heslo se nevypisuje. Opakované spuštění vygeneruje nová hesla rolí a přepíše user-secrets.

.EXAMPLE
  $env:PGPASSWORD = 'postgres'
  ./deploy/dev/setup-local.ps1
  Remove-Item Env:PGPASSWORD
#>
[CmdletBinding()]
param(
    [string] $PgBin = 'C:\Program Files\PostgreSQL\18\bin',
    [string] $PgHost = 'localhost',
    [int] $PgPort = 5432,
    [string] $PgSuperuser = 'postgres'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$rolesSql = Join-Path $repoRoot 'deploy' 'sql' '00_roles.sql'
$envFile = Join-Path $repoRoot 'deploy' '.env'
$psqlName = if ($IsWindows) { 'psql.exe' } else { 'psql' }
$psql = Join-Path $PgBin $psqlName
if (-not (Test-Path $psql)) {
    $found = Get-Command $psqlName -ErrorAction SilentlyContinue
    if (-not $found) { throw "psql nenalezen v '$PgBin' ani v PATH. Zadejte -PgBin." }
    $psql = $found.Source
}

if ([string]::IsNullOrEmpty($env:PGPASSWORD)) {
    Write-Host 'Nastavte heslo superuživatele jen pro tuto relaci: $env:PGPASSWORD = ''...'' a spusťte skript znovu.'
    exit 1
}

function New-Secret { [System.Security.Cryptography.RandomNumberGenerator]::GetHexString(40).ToLowerInvariant() }

function Read-EnvFile([string] $path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    $values
}

function Set-UserSecrets([string] $id, [hashtable] $values) {
    # JSON jde přes standardní vstup: hesla nejsou v parametrech procesu.
    $json = $values | ConvertTo-Json -Depth 5 -Compress
    $output = $json | & dotnet user-secrets set --id $id 2>&1
    if ($LASTEXITCODE -ne 0) { throw "dotnet user-secrets set --id $id selhalo (kód $LASTEXITCODE)." }
    $null = $output
    Write-Host "  user-secrets ${id}: zapsáno"
}

function New-ConnectionString([string] $database, [string] $user, [string] $password) {
    "Host=$PgHost;Port=$PgPort;Database=$database;Username=$user;Password=$password"
}

# 1. Hesla rolí
$roles = 'OWNER', 'APP', 'WORKER', 'ADMIN', 'CMS'
$passwords = @{}
foreach ($role in $roles) { $passwords[$role] = New-Secret }

# 2. Role a databáze
try {
    foreach ($role in $roles) { Set-Item -Path "Env:ESHOPGUARD_${role}_PASSWORD" -Value $passwords[$role] }
    foreach ($database in 'eshopguard', 'eshopguard_test') {
        Write-Host "== 00_roles.sql pro $database"
        & $psql -h $PgHost -p $PgPort -U $PgSuperuser -d postgres -X -q -v ON_ERROR_STOP=1 -v "db_name=$database" -f $rolesSql
        if ($LASTEXITCODE -ne 0) { throw "00_roles.sql pro $database skončil kódem $LASTEXITCODE." }
    }
}
finally {
    foreach ($role in $roles) { Remove-Item -Path "Env:ESHOPGUARD_${role}_PASSWORD" -ErrorAction SilentlyContinue }
}

# 3. deploy/.env pro MinIO (jen pokud chybí; existující hesla se nemění, MinIO je má uložená ve svazku)
if (-not (Test-Path $envFile)) {
    $lines = @(
        '# Vytvořil deploy/dev/setup-local.ps1. Mimo git. Hesla MinIO pro deploy/docker-compose.dev.yml.'
        'MINIO_ROOT_USER=eshopguard-root'
        "MINIO_ROOT_PASSWORD=$(New-Secret)"
        'ESHOPGUARD_S3_ACCESS_KEY=eshopguard-app'
        "ESHOPGUARD_S3_SECRET_KEY=$(New-Secret)"
    )
    Set-Content -LiteralPath $envFile -Value $lines -Encoding utf8NoBOM
    Write-Host "== deploy/.env vytvořen"
}
else {
    Write-Host "== deploy/.env už existuje, beze změny"
}
$minio = Read-EnvFile $envFile
foreach ($key in 'ESHOPGUARD_S3_ACCESS_KEY', 'ESHOPGUARD_S3_SECRET_KEY') {
    if (-not $minio.ContainsKey($key) -or [string]::IsNullOrEmpty($minio[$key])) { throw "V deploy/.env chybí $key." }
}
$s3Keys = @{ AccessKey = $minio['ESHOPGUARD_S3_ACCESS_KEY']; SecretKey = $minio['ESHOPGUARD_S3_SECRET_KEY'] }

# 4. user-secrets
Write-Host '== user-secrets'
Set-UserSecrets 'eshopguard-data' @{
    ConnectionStrings = @{ Migrations = New-ConnectionString 'eshopguard' 'eshopguard_owner' $passwords['OWNER'] }
}
Set-UserSecrets 'eshopguard-api' @{
    ConnectionStrings = @{ App = New-ConnectionString 'eshopguard' 'eshopguard_app' $passwords['APP'] }
    Storage = @{ S3 = $s3Keys }
}
Set-UserSecrets 'eshopguard-worker' @{
    ConnectionStrings = @{ Worker = New-ConnectionString 'eshopguard' 'eshopguard_worker' $passwords['WORKER'] }
    Storage = @{ S3 = $s3Keys }
}
Set-UserSecrets 'eshopguard-tests' @{
    ConnectionStrings = @{
        Owner = New-ConnectionString 'eshopguard_test' 'eshopguard_owner' $passwords['OWNER']
        App = New-ConnectionString 'eshopguard_test' 'eshopguard_app' $passwords['APP']
        Worker = New-ConnectionString 'eshopguard_test' 'eshopguard_worker' $passwords['WORKER']
        Admin = New-ConnectionString 'eshopguard_test' 'eshopguard_admin' $passwords['ADMIN']
        Cms = New-ConnectionString 'eshopguard_test' 'eshopguard_cms' $passwords['CMS']
    }
    Storage = @{
        S3 = @{
            ServiceUrl = 'http://localhost:9000'
            Region = 'us-east-1'
            Bucket = 'eshopguard-test'
            ForcePathStyle = $true
            AccessKey = $s3Keys.AccessKey
            SecretKey = $s3Keys.SecretKey
        }
    }
}

Write-Host ''
Write-Host 'Hotovo. Další kroky:'
Write-Host '  dotnet tool restore'
Write-Host '  dotnet ef database update --project src/EshopGuard.Data'
Write-Host '  (testovací databázi eshopguard_test migrují testy samy)'
