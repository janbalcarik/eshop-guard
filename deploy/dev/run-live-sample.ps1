#Requires -Version 7.4
<#
.SYNOPSIS
  Živá ukázka jedním příkazem (krok 3.12 v LOKALNI-OVERENI.md): připraví prostředí, spustí Mailpit, API a worker, projde
  ukázku přes API (live-sample.ps1), uloží výsledky, logy a cenu a všechno, co spustil, zase zastaví.

.DESCRIPTION
  Z cmd v kořeni repozitáře: deploy\dev\run-live-sample.cmd (zavolá tento skript v PowerShellu 7).
  Postup:
    1. klíče Jevu a OpenAI ze src/.env, z proměnných prostředí nebo z uživatelských proměnných Windows; když chybí, zeptá
       se (zadání se nezobrazuje). Klíče dostane jen proces workeru, nikam se nezapisují ani nevypisují;
    2. souhlas s cenou (nejvýš Runs:FreeSample:MaxInternalUsd, výchozí 1,00 USD) a kontakt do User-Agentu;
    3. zastaví dřívější API a worker, ověří PostgreSQL a user-secrets (chybějící doplní setup-local.ps1, který zároveň
       vygeneruje nová hesla rolí), migrace, sestavení a vývojový certifikát HTTPS;
    4. Mailpit: použije běžící, jinak ho stáhne do .data/tools/mailpit a spustí jen na 127.0.0.1;
    5. API a worker na pozadí (logy do složky výsledků), ukázka přes live-sample.ps1, cena běhu z usage.usage_records;
    6. výsledky a logy v .data/live-sample/<čas>/ a vedle jako .zip; API, worker a Mailpit, které spustil, zastaví.

  Ukázka je jednou na doménu: když už ji doména měla, zeptá se na smazání nároku (jako postgres).

.PARAMETER Mock
  Zkouška zdarma s falešným Jevem a OpenAI (bez klíčů a bez souhlasu s cenou). Spotřebuje nárok domény na ukázku,
  skutečný běh se pak zeptá na jeho smazání.

.PARAMETER Yes
  Bez otázek: souhlas s cenou, smazání nároku domény a kontakt z git config user.email.

.EXAMPLE
  deploy\dev\run-live-sample.cmd
.EXAMPLE
  deploy\dev\run-live-sample.cmd -Mock
#>
[CmdletBinding()]
param(
    [string] $ShopUrl = 'https://www.naturfyt.sk/',
    [string] $Contact,
    [switch] $Mock,
    [switch] $Yes,
    [string] $PgBin = 'C:\Program Files\PostgreSQL\18\bin',
    [string] $PgSuperuserPassword = 'postgres',
    [int] $TimeoutMinutes = 30,
    [switch] $SkipCertificateCheck
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
Set-Location $repo
$outDir = Join-Path $repo ".data/live-sample/$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$apiUrl = 'https://localhost:5443'
$mailpitUrl = 'http://127.0.0.1:8025'
$pwshPath = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
if (-not (Test-Path $pwshPath)) { $pwshPath = (Get-Command pwsh).Source }
$started = [System.Collections.Generic.List[object]]::new()

function Write-Step([string] $Text) { Write-Host ''; Write-Host "== $Text" -ForegroundColor Cyan }

function Confirm-Choice([string] $Question) {
    if ($Yes) { return $true }
    $answer = Read-Host "$Question [A/n]"
    return [string]::IsNullOrWhiteSpace($answer) -or $answer.Trim() -match '^(a|ano|y|yes)$'
}

function Show-Log([string] $Name) {
    foreach ($file in "$Name.log", "$Name.err.log") {
        $path = Join-Path $outDir $file
        if ((Test-Path $path) -and (Get-Item $path).Length -gt 0) {
            Write-Host "--- konec $file" -ForegroundColor DarkGray
            Get-Content $path -Tail 40 | Write-Host
        }
    }
}

function Invoke-Dotnet([string] $What, [string] $LogName, [string[]] $Arguments) {
    # Výstup jen do logu; při chybě se ukáže jeho konec.
    $log = Join-Path $outDir $LogName
    & dotnet @Arguments *>> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content $log -Tail 30 | Write-Host
        throw "$What skončilo kódem $LASTEXITCODE (celý výstup: $log)."
    }
}

function Invoke-Psql([string] $Sql, [string] $Database = 'eshopguard', [switch] $Table) {
    $previous = $env:PGPASSWORD
    $env:PGPASSWORD = $PgSuperuserPassword
    try {
        $format = if ($Table) { @('-P', 'footer=off') } else { @('-t', '-A') }
        $output = & $script:psql -h localhost -U postgres -d $Database -X -q -v ON_ERROR_STOP=1 @format -c $Sql 2>&1
    }
    finally {
        if ($null -eq $previous) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue } else { $env:PGPASSWORD = $previous }
    }
    if ($LASTEXITCODE -ne 0) { throw "psql: $($output -join ' ')" }
    return $output
}

function Read-DotEnv([string] $Path) {
    $values = @{}
    if (-not (Test-Path $Path)) { return $values }
    foreach ($line in Get-Content $Path) {
        $text = $line.Trim()
        if ($text.Length -eq 0 -or $text.StartsWith('#')) { continue }
        if ($text.StartsWith('export ')) { $text = $text.Substring(7).TrimStart() }
        $at = $text.IndexOf('=')
        if ($at -lt 1) { continue }
        $value = $text.Substring($at + 1).Trim()
        if ($value.Length -ge 2 -and $value[0] -eq $value[-1] -and '"', "'" -contains $value[0]) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        else {
            $value = ($value -split '\s+#', 2)[0].Trim()
        }
        $values[$text.Substring(0, $at).Trim()] = $value
    }
    return $values
}

function Find-Key([hashtable] $DotEnv, [string[]] $Names) {
    # Stejné pořadí jako CLI: src/.env, proměnná prostředí, uživatelská proměnná Windows.
    foreach ($name in $Names) {
        if ($DotEnv[$name]) { return [pscustomobject]@{ Value = $DotEnv[$name]; Source = "src/.env ($name)" } }
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) { return [pscustomobject]@{ Value = $value.Trim(); Source = "proměnná prostředí $name" } }
        if ($IsWindows) {
            $value = [Environment]::GetEnvironmentVariable($name, 'User')
            if ($value) { return [pscustomobject]@{ Value = $value.Trim(); Source = "uživatelská proměnná Windows $name" } }
        }
    }
    return $null
}

function Read-Key([string] $Name) {
    $value = Read-Host "Vložte $Name (zadání se nezobrazí)" -MaskInput
    if ([string]::IsNullOrWhiteSpace($value)) { throw "$Name chybí." }
    return [pscustomobject]@{ Value = $value.Trim(); Source = 'zadáno teď' }
}

function Get-SecretNames([string] $Id) {
    $lines = & dotnet user-secrets list --id $Id 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($lines | ForEach-Object { ($_ -split ' = ', 2)[0].Trim() })
}

function Start-Background([string] $Name, [string] $File, [string[]] $Arguments, [hashtable] $Environment) {
    $parameters = @{
        FilePath = $File; ArgumentList = $Arguments; WorkingDirectory = $repo; PassThru = $true; NoNewWindow = $true
        RedirectStandardOutput = (Join-Path $outDir "$Name.log"); RedirectStandardError = (Join-Path $outDir "$Name.err.log")
    }
    if ($Environment) { $parameters.Environment = $Environment }
    $process = Start-Process @parameters
    $started.Add([pscustomobject]@{ Name = $Name; Process = $process })
    return $process
}

function Test-Mailpit {
    try { Invoke-RestMethod "$mailpitUrl/api/v1/messages?limit=1" -TimeoutSec 3 | Out-Null; return $true } catch { return $false }
}

function Test-Api {
    try {
        $response = Invoke-WebRequest "$apiUrl/health" -TimeoutSec 3 -SkipHttpErrorCheck -SkipCertificateCheck:$SkipCertificateCheck
        return $response.StatusCode -eq 200
    }
    catch { return $false }
}

function Stop-Started {
    for ($i = $started.Count - 1; $i -ge 0; $i--) {
        $process = $started[$i].Process
        if (-not $process.HasExited) {
            try { $process.Kill($true); $null = $process.WaitForExit(15000) } catch { }
        }
    }
    if ($started.Count -gt 0) { Write-Host "Zastaveno: $(($started | ForEach-Object Name) -join ', ')." }
    $started.Clear()
}

$exitCode = 1
try {
    Write-Host "Živá ukázka $ShopUrl$(if ($Mock) { ' (zkouška zdarma s falešným Jevem a OpenAI)' })" -ForegroundColor Cyan
    Write-Host "Výsledky a logy: $outDir"

    # 1. Nástroje a klíče
    Write-Step 'Nástroje a klíče'
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet nenalezen (nainstalujte .NET SDK podle global.json).' }
    $script:psql = Join-Path $PgBin $(if ($IsWindows) { 'psql.exe' } else { 'psql' })
    if (-not (Test-Path $script:psql)) {
        $found = Get-Command psql -ErrorAction SilentlyContinue
        if (-not $found) { throw "psql nenalezen v '$PgBin' ani v PATH (parametr -PgBin)." }
        $script:psql = $found.Source
    }
    $workerEnvironment = @{}
    if ($Mock) {
        $workerEnvironment['EshopGuard__Jev__UseMock'] = 'true'
        $workerEnvironment['EshopGuard__Rewrite__UseMock'] = 'true'
        Write-Host 'Klíče se nepoužijí (falešný Jev a OpenAI).'
    }
    else {
        $dotEnv = Read-DotEnv (Join-Path $repo 'src/.env')
        $jev = Find-Key $dotEnv 'JEV_API_KEY', 'TYPESAFE_API_KEY'
        if (-not $jev) { $jev = Read-Key 'TYPESAFE_API_KEY (klíč Jevu)' }
        $openAi = Find-Key $dotEnv 'OPENAI_API_KEY'
        if (-not $openAi) { $openAi = Read-Key 'OPENAI_API_KEY' }
        Write-Host "Klíč Jevu: $($jev.Source). Klíč OpenAI: $($openAi.Source)."
        $workerEnvironment['EshopGuard__Jev__UseMock'] = 'false'
        $workerEnvironment['EshopGuard__Rewrite__UseMock'] = 'false'
        $workerEnvironment['JEV_API_KEY'] = $jev.Value
        $workerEnvironment['OPENAI_API_KEY'] = $openAi.Value
        $jev = $null; $openAi = $null; $dotEnv = $null

        Write-Host 'Ukázka je placená (Jev a OpenAI): nejvýš 1,00 USD. Nad strop se nic nezaplatí, běh skončí kódem sample_budget_exceeded.'
        if (-not (Confirm-Choice 'Spustit placenou ukázku?')) { throw 'Bez souhlasu s cenou se ukázka nespouští.' }
    }

    if (-not $Contact) {
        $gitEmail = "$(& git config user.email 2>$null)".Trim()
        if ($Yes) { $Contact = $gitEmail }
        else {
            $entered = Read-Host "Kontakt do User-Agentu (e-mail nebo web; uvidí ho stahovaný e-shop) [$gitEmail]"
            $Contact = if ([string]::IsNullOrWhiteSpace($entered)) { $gitEmail } else { $entered.Trim() }
        }
    }
    if ([string]::IsNullOrWhiteSpace($Contact)) { throw 'Chybí kontakt do User-Agentu (parametr -Contact).' }
    $contactPart = if ($Contact -match '^https?://') { $Contact } else { "mailto:$Contact" }
    $workerEnvironment['EshopGuard__Crawl__UserAgent'] = "EshopGuard/0.1 (+$contactPart)"

    # 2. Dřívější procesy, databáze, user-secrets, migrace, sestavení, certifikát
    Write-Step 'Příprava'
    $old = @(Get-Process | Where-Object { $_.ProcessName -match '^EshopGuard\.(Api|Work)' })
    if ($old.Count -gt 0) {
        Write-Host "Zastavuji dřívější API nebo worker (PID $($old.Id -join ', '))."
        foreach ($process in $old) { try { $process.Kill($true); $null = $process.WaitForExit(15000) } catch { } }
    }
    if (Test-Api) { throw "Na $apiUrl už běží jiné API (Visual Studio?). Zastavte ho a spusťte skript znovu." }

    try { $null = Invoke-Psql 'SELECT 1' -Database 'postgres' }
    catch { throw "PostgreSQL na localhost:5432 neodpovídá nebo nepřijal heslo uživatele postgres (služba postgresql-x64-18 běží?). $($_.Exception.Message)" }

    $needed = [ordered]@{
        'eshopguard-data'   = @('ConnectionStrings:Migrations')
        'eshopguard-api'    = @('ConnectionStrings:App', 'Security:IpHashKey')
        'eshopguard-worker' = @('ConnectionStrings:Worker')
    }
    $missing = @(foreach ($id in $needed.Keys) {
            $names = Get-SecretNames $id
            foreach ($key in $needed[$id]) { if ($names -notcontains $key) { "$id $key" } }
        })
    if ($missing.Count -gt 0) {
        Write-Host "V user-secrets chybí: $($missing -join ', '). Spouštím setup-local.ps1 (nová hesla rolí, user-secrets)."
        $previous = $env:PGPASSWORD
        $env:PGPASSWORD = $PgSuperuserPassword
        try { & $pwshPath -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'setup-local.ps1') -PgBin (Split-Path $script:psql) }
        finally { if ($null -eq $previous) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue } else { $env:PGPASSWORD = $previous } }
        if ($LASTEXITCODE -ne 0) { throw "setup-local.ps1 skončil kódem $LASTEXITCODE." }
    }
    else {
        Write-Host 'user-secrets: v pořádku.'
    }

    Write-Host 'Migrace databáze…'
    Invoke-Dotnet 'dotnet tool restore' 'setup.log' @('tool', 'restore')
    Invoke-Dotnet 'Migrace' 'setup.log' @('ef', 'database', 'update', '--project', 'src/EshopGuard.Data')
    Write-Host 'Sestavení…'
    Invoke-Dotnet 'Sestavení' 'build.log' @('build', 'src/EshopGuard.sln', '-nologo', '-v', 'q')
    if (-not $SkipCertificateCheck) {
        & dotnet dev-certs https --check --trust *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Host 'Vývojový certifikát HTTPS není důvěryhodný. Windows se zeptá, potvrďte Ano.' -ForegroundColor Yellow
            & dotnet dev-certs https --trust
            if ($LASTEXITCODE -ne 0) { throw 'dotnet dev-certs https --trust selhalo.' }
        }
    }

    # 3. Mailpit
    Write-Step 'Mailpit'
    if (Test-Mailpit) {
        Write-Host "Mailpit už běží ($mailpitUrl)."
    }
    else {
        $toolDir = Join-Path $repo '.data/tools/mailpit'
        $exe = Join-Path $toolDir $(if ($IsWindows) { 'mailpit.exe' } else { 'mailpit' })
        if (-not (Test-Path $exe)) {
            $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'amd64' }
            $asset = if ($IsWindows) { "mailpit-windows-$arch.zip" } elseif ($IsMacOS) { "mailpit-darwin-$arch.tar.gz" } else { "mailpit-linux-$arch.tar.gz" }
            Write-Host "Stahuji $asset z GitHubu (axllent/mailpit)…"
            New-Item -ItemType Directory -Force -Path $toolDir | Out-Null
            $archive = Join-Path $toolDir $asset
            Invoke-WebRequest "https://github.com/axllent/mailpit/releases/latest/download/$asset" -OutFile $archive
            if ($asset.EndsWith('.zip')) { Expand-Archive -Path $archive -DestinationPath $toolDir -Force }
            else { & tar -xzf $archive -C $toolDir; if ($LASTEXITCODE -ne 0) { throw "Rozbalení $asset selhalo." } }
            Remove-Item $archive
        }
        $null = Start-Background 'mailpit' $exe @('--listen', '127.0.0.1:8025', '--smtp', '127.0.0.1:1025')
        $deadline = (Get-Date).AddSeconds(30)
        while (-not (Test-Mailpit)) {
            if ((Get-Date) -gt $deadline) { Show-Log 'mailpit'; throw 'Mailpit se do 30 s nespustil.' }
            Start-Sleep -Milliseconds 500
        }
        Write-Host "Mailpit běží ($mailpitUrl)."
    }

    # 4. API a worker
    Write-Step 'API a worker'
    $api = Start-Background 'api' 'dotnet' @('run', '--no-build', '--project', 'src/EshopGuard.Api', '--launch-profile', 'https')
    $deadline = (Get-Date).AddMinutes(2)
    while (-not (Test-Api)) {
        if ($api.HasExited) { Show-Log 'api'; throw 'API se nespustilo.' }
        if ((Get-Date) -gt $deadline) { Show-Log 'api'; throw 'API do 2 minut neodpovědělo na /health.' }
        Start-Sleep -Seconds 1
    }
    Write-Host "API běží ($apiUrl)."

    $worker = Start-Background 'worker' 'dotnet' @('run', '--no-build', '--project', 'src/EshopGuard.Worker') $workerEnvironment
    $workerEnvironment = $null
    $workerLog = Join-Path $outDir 'worker.log'
    $deadline = (Get-Date).AddMinutes(2)
    while (-not ((Test-Path $workerLog) -and (Select-String -Path $workerLog -Pattern 'Application started' -SimpleMatch -Quiet))) {
        if ($worker.HasExited) { Show-Log 'worker'; throw 'Worker se nespustil.' }
        if ((Get-Date) -gt $deadline) { Show-Log 'worker'; throw 'Worker se do 2 minut nespustil.' }
        Start-Sleep -Seconds 1
    }
    Write-Host 'Worker běží.'

    # 5. Nárok domény na ukázku
    $uri = [Uri]$ShopUrl
    $domain = ($(if ($uri.IsDefaultPort) { $uri.IdnHost } else { "$($uri.IdnHost):$($uri.Port)" })).ToLowerInvariant() -replace '^www\.', ''
    if ([int](Invoke-Psql "SELECT count(*) FROM shop.free_sample_claims WHERE domain = '$domain'") -gt 0) {
        if (-not (Confirm-Choice "Doména $domain už ukázku měla (dřívější běh). Smazat nárok a pokračovat?")) {
            throw 'Nárok domény zůstal, ukázka se nespustí.'
        }
        $null = Invoke-Psql "DELETE FROM shop.free_sample_claims WHERE domain = '$domain'"
        Write-Host "Nárok domény $domain smazán."
    }

    # 6. Ukázka přes API
    Write-Step 'Ukázka'
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'live-sample.ps1'),
        '-ShopUrl', $ShopUrl, '-OutDir', $outDir, '-TimeoutMinutes', $TimeoutMinutes, '-NoCostHint')
    if ($SkipCertificateCheck) { $arguments += '-SkipCertificateCheck' }
    & $pwshPath @arguments
    $exitCode = $LASTEXITCODE
}
catch {
    Write-Host ''
    Write-Host "Chyba: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
}
finally {
    Stop-Started
}

# 7. Cena, balík výsledků
$samplePath = Join-Path $outDir 'sample.json'
if (Test-Path $samplePath) {
    $runId = "$((Get-Content $samplePath -Raw | ConvertFrom-Json).runId)"
    if ($runId -match '^[0-9a-fA-F-]{36}$') {
        try {
            $cost = Invoke-Psql "SELECT provider, operation, sum(calls) AS calls, sum(input_tokens) AS input_tokens, sum(output_tokens) AS output_tokens, sum(cost_usd) AS cost_usd FROM usage.usage_records WHERE run_id = '$runId' GROUP BY 1, 2 UNION ALL SELECT 'celkem', '', sum(calls), sum(input_tokens), sum(output_tokens), sum(cost_usd) FROM usage.usage_records WHERE run_id = '$runId' ORDER BY 1, 2" -Table
            Set-Content -Path (Join-Path $outDir 'cost.txt') -Value $cost -Encoding utf8NoBOM
            Write-Host ''
            Write-Host "Skutečná cena běhu $runId (usage.usage_records):" -ForegroundColor Cyan
            $cost | Write-Host
        }
        catch { Write-Host "Cenu se nepodařilo načíst: $($_.Exception.Message)" -ForegroundColor Yellow }
    }
}

if (-not (Get-ChildItem -Path $outDir)) {
    Remove-Item -Path $outDir
    exit $exitCode
}
$zip = "$outDir.zip"
try {
    Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip -Force
    Write-Host ''
    if ($exitCode -eq 0) { Write-Host 'Hotovo.' -ForegroundColor Green } else { Write-Host 'Skončilo chybou (výše).' -ForegroundColor Red }
    Write-Host "Pošlete prosím soubor $zip (výsledky, logy a cena; klíče ani hesla v něm nejsou)."
}
catch {
    Write-Host "Balík .zip se nepodařilo vytvořit ($($_.Exception.Message)); pošlete prosím složku $outDir." -ForegroundColor Yellow
}
exit $exitCode
