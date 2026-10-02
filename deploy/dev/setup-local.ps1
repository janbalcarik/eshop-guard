#Requires -Version 7.4
<#
.SYNOPSIS
  Lokální databáze EshopGuard: role, databáze eshopguard, eshopguard_test a eshopguard_test_jobs a user-secrets s připojením.

.DESCRIPTION
  1. Vygeneruje pět hesel databázových rolí (RandomNumberGenerator).
  2. Spustí deploy/sql/00_roles.sql jako postgres pro databáze eshopguard, eshopguard_test a eshopguard_test_jobs
     (testy fronty úloh mají vlastní databázi, protože mažou ops.jobs).
     Hesla dostane psql jen v proměnných prostředí svého procesu, ne v parametrech.
  3. Zapíše připojení do dotnet user-secrets (eshopguard-data, eshopguard-api, eshopguard-worker, eshopguard-cli,
     eshopguard-tests) přes standardní vstup, ne jako argument. CLI (eshopguard-cli) má cache v databázi eshopguard
     jako role eshopguard_worker.

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
    foreach ($database in 'eshopguard', 'eshopguard_test', 'eshopguard_test_jobs') {
        Write-Host "== 00_roles.sql pro $database"
        & $psql -h $PgHost -p $PgPort -U $PgSuperuser -d postgres -X -q -v ON_ERROR_STOP=1 -v "db_name=$database" -f $rolesSql
        if ($LASTEXITCODE -ne 0) { throw "00_roles.sql pro $database skončil kódem $LASTEXITCODE." }
    }
}
finally {
    foreach ($role in $roles) { Remove-Item -Path "Env:ESHOPGUARD_${role}_PASSWORD" -ErrorAction SilentlyContinue }
}

# 3. user-secrets
Write-Host '== user-secrets'
Set-UserSecrets 'eshopguard-data' @{
    ConnectionStrings = @{ Migrations = New-ConnectionString 'eshopguard' 'eshopguard_owner' $passwords['OWNER'] }
}
Set-UserSecrets 'eshopguard-api' @{
    ConnectionStrings = @{ App = New-ConnectionString 'eshopguard' 'eshopguard_app' $passwords['APP'] }
    # Klíč HMAC e-mailů a IP adres v limitech a auditu (změna 9); nový klíč jen „zapomene“ dosavadní limity.
    Security = @{ IpHashKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
}
Set-UserSecrets 'eshopguard-worker' @{
    ConnectionStrings = @{ Worker = New-ConnectionString 'eshopguard' 'eshopguard_worker' $passwords['WORKER'] }
}
Set-UserSecrets 'eshopguard-cli' @{
    ConnectionStrings = @{ Cli = New-ConnectionString 'eshopguard' 'eshopguard_worker' $passwords['WORKER'] }
}
Set-UserSecrets 'eshopguard-tests' @{
    ConnectionStrings = @{
        Owner = New-ConnectionString 'eshopguard_test' 'eshopguard_owner' $passwords['OWNER']
        App = New-ConnectionString 'eshopguard_test' 'eshopguard_app' $passwords['APP']
        Worker = New-ConnectionString 'eshopguard_test' 'eshopguard_worker' $passwords['WORKER']
        Admin = New-ConnectionString 'eshopguard_test' 'eshopguard_admin' $passwords['ADMIN']
        Cms = New-ConnectionString 'eshopguard_test' 'eshopguard_cms' $passwords['CMS']
    }
}

Write-Host ''
Write-Host 'Hotovo. Další kroky:'
Write-Host '  dotnet tool restore'
Write-Host '  dotnet ef database update --project src/EshopGuard.Data'
Write-Host '  dotnet run --project src/EshopGuard.Cli -- cache init   (jednou: tenant cli pro cache CLI)'
Write-Host '  (testovací databáze eshopguard_test a eshopguard_test_jobs migrují testy samy)'
