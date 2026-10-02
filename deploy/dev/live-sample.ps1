#Requires -Version 7.4
<#
.SYNOPSIS
  Živá ukázka zdarma přes API (změna 10, úkol 11.5; zároveň krok 3.6 změny 8): přihlášení odkazem, e-shop, rozpoznání
  platformy, ukázka se skutečným Jevem a OpenAI a výsledky onboardingu do složky jako JSON.

.DESCRIPTION
  Skript volá jen API (https://localhost:5443) a schránku Mailpit (http://localhost:8025); web e-shopu stahuje worker.
  Předpoklady (LOKALNI-OVERENI.md, krok 3.12):
    - migrace až po F6 (dotnet ef database update --project src/EshopGuard.Data);
    - Mailpit: docker compose -f deploy/docker-compose.dev.yml up -d mailpit;
    - API: dotnet run --project src/EshopGuard.Api --launch-profile https;
    - worker s klíči jen v proměnných prostředí (TYPESAFE_API_KEY nebo JEV_API_KEY, OPENAI_API_KEY) a s kontaktem
      v User-Agentu: dotnet run --project src/EshopGuard.Worker.

  Placené: ukázka stojí nejvýš Runs:FreeSample:MaxInternalUsd (výchozí 1,00 USD). Nad odhadem se nic nezaplatí a běh
  skončí failed s kódem sample_budget_exceeded. Spouštějte jen se souhlasem s cenou (CLAUDE.md).

  Každé spuštění založí nový účet a tenanta (e-mail jde jen do Mailpitu). Ukázka je jednou na doménu napříč tenanty:
  když už doménu použil dřívější běh, skript skončí s kódem sample.already_used_for_domain a vypíše, jak nárok smazat.
  Skript nevypisuje ani neukládá token odkazu ani cookie relace.

.PARAMETER ShopUrl
  Adresa e-shopu, který se má zkontrolovat (jen web, který jste sami určili).

.EXAMPLE
  ./deploy/dev/live-sample.ps1 -ShopUrl https://vegis.sk/
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ShopUrl,
    [string] $Email = "live-sample.$(Get-Date -Format 'yyyyMMddHHmmss')@eshopguard.test",
    [string] $Market = 'sk',
    [string] $Api = 'https://localhost:5443',
    [string] $Mailpit = 'http://localhost:8025',
    [string] $OutDir = (Join-Path '.data' "live-sample/$(Get-Date -Format 'yyyyMMdd-HHmmss')"),
    [int] $TimeoutMinutes = 30,
    [switch] $SkipCertificateCheck
)

$ErrorActionPreference = 'Stop'
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$script:csrf = $null
$final = @('finished', 'partial', 'failed', 'canceled')
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Invoke-Api {
    param([string] $Method, [string] $Path, $Body)
    $arguments = @{
        Method = $Method; Uri = "$Api$Path"; WebSession = $session; SkipHttpErrorCheck = $true
        SkipCertificateCheck = $SkipCertificateCheck.IsPresent; Headers = @{}
    }
    if ($Method -ne 'GET') {
        $arguments.Headers['X-CSRF-TOKEN'] = $script:csrf
    }
    if ($null -ne $Body) {
        $arguments.ContentType = 'application/json; charset=utf-8'
        $arguments.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10 -Compress))
    }
    $response = Invoke-WebRequest @arguments
    # application/problem+json comes as bytes in Content; the raw stream is the same for every type.
    $content = [System.Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray())
    $json = if ($content.Length -gt 0) { $content | ConvertFrom-Json -Depth 64 } else { $null }
    [pscustomobject]@{ Status = [int]$response.StatusCode; Json = $json; Raw = $content }
}

function Update-Csrf {
    $answer = Invoke-Api GET '/api/auth/csrf'
    if ($answer.Status -ne 200) { throw "CSRF: HTTP $($answer.Status) $($answer.Raw)" }
    $script:csrf = $answer.Json.token
}

function Save-Answer([string] $Name, $Answer) {
    $path = Join-Path $OutDir "$Name.json"
    Set-Content -Path $path -Value $Answer.Raw -Encoding utf8NoBOM
    Write-Host ("  {0,-12} HTTP {1} → {2}" -f $Name, $Answer.Status, $path)
}

function Stop-WithProblem([string] $What, $Answer) {
    Write-Host "$What selhalo: HTTP $($Answer.Status) $($Answer.Json.code)" -ForegroundColor Red
    if ($Answer.Json.params) { Write-Host ("  params: " + ($Answer.Json.params | ConvertTo-Json -Compress)) }
    exit 1
}

# 0. API a Mailpit běží
$health = Invoke-Api GET '/health'
if ($health.Status -ne 200) { Stop-WithProblem 'Kontrola API (/health)' $health }
try { Invoke-RestMethod "$Mailpit/api/v1/messages?limit=1" | Out-Null }
catch { Write-Host "Mailpit na $Mailpit neodpovídá (docker compose -f deploy/docker-compose.dev.yml up -d mailpit)." -ForegroundColor Red; exit 1 }
Write-Host "API a Mailpit běží. Výsledky: $OutDir"

# 1. Přihlášení odkazem (token jen z Mailpitu, nikam se nevypisuje)
Update-Csrf
$link = Invoke-Api POST '/api/auth/login-link' @{ email = $Email; market = $Market }
if ($link.Status -ne 202) { Stop-WithProblem 'Žádost o odkaz' $link }
$token = $null
for ($i = 0; $i -lt 30 -and -not $token; $i++) {
    Start-Sleep -Seconds 1
    $messages = (Invoke-RestMethod "$Mailpit/api/v1/messages?limit=50").messages |
        Where-Object { @($_.To | Where-Object { $_.Address -eq $Email }).Count -gt 0 }  # not $_.To.Address: Array has a method Address
    foreach ($message in $messages) {
        $text = (Invoke-RestMethod "$Mailpit/api/v1/message/$($message.ID)").Text
        if ($text -match '#t=([A-Za-z0-9_\-]+)') { $token = $Matches[1]; break }
    }
}
if (-not $token) { Write-Host "E-mail s odkazem pro $Email v Mailpitu není." -ForegroundColor Red; exit 1 }
$session_ = Invoke-Api POST '/api/auth/login-link/consume' @{ token = $token; market = $Market }
$token = $null
if ($session_.Status -ne 200) { Stop-WithProblem 'Přihlášení' $session_ }
$tenant = $session_.Json.createdTenantId
Update-Csrf
Write-Host "Přihlášen jako $Email, tenant $tenant."

# 2. E-shop a rozpoznání platformy (API čeká nejvýš Api:InteractiveWaitSeconds)
$base = "/api/t/$tenant/shops"
$shop = Invoke-Api POST $base @{ url = $ShopUrl }
if ($shop.Status -ne 201) { Stop-WithProblem 'Založení e-shopu' $shop }
$shopId = $shop.Json.id
Save-Answer 'shop' $shop
$detection = $shop.Json.detection
for ($i = 0; $i -lt 30 -and $detection.status -eq 'pending'; $i++) {
    Start-Sleep -Seconds 2
    $detection = (Invoke-Api GET "$base/$shopId/detection").Json
}
Save-Answer 'detection' (Invoke-Api GET "$base/$shopId/detection")
Write-Host ("E-shop {0}: platforma {1} ({2}), stav rozpoznání {3}{4}" -f $shop.Json.domain, $detection.platform, $detection.confidence,
    $detection.status, $(if ($detection.failureCode) { ", kód $($detection.failureCode)" } else { '' }))

# 3. Ukázka zdarma (placené: nejvýš Runs:FreeSample:MaxInternalUsd)
$sample = Invoke-Api POST "$base/$shopId/sample"
if ($sample.Status -ne 202) {
    if ($sample.Json.code -eq 'sample.already_used_for_domain') {
        Write-Host "Doména už ukázku měla. Nárok smažte jako postgres a skript spusťte znovu:" -ForegroundColor Yellow
        Write-Host "  DELETE FROM shop.free_sample_claims WHERE domain = '$($shop.Json.domain)';"
        exit 1
    }
    Stop-WithProblem 'Spuštění ukázky' $sample
}
$runId = $sample.Json.runId
Write-Host "Ukázka běží, běh $runId. Stav každých 10 s (worker musí běžet s klíči):"
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
do {
    Start-Sleep -Seconds 10
    $state = Invoke-Api GET "$base/$shopId/sample"
    $s = $state.Json
    Write-Host ("  {0:HH:mm:ss} {1,-12} stažené stránky {2} z {3}, zpracované {4}, pořadí ve frontě {5}" -f (Get-Date), $s.status,
        $s.progress.pagesFetched, $(if ($null -ne $s.progress.pagesPlanned) { $s.progress.pagesPlanned } else { '?' }), $s.progress.pagesProcessed,
        $(if ($null -ne $s.queue.position) { $s.queue.position } else { '-' }))
} while ($final -notcontains $s.status -and (Get-Date) -lt $deadline)
if ($final -notcontains $s.status) {
    Write-Host "Ukázka do $TimeoutMinutes minut neskončila; běží dál ve workeru. Výsledek později: GET $base/$shopId/sample" -ForegroundColor Yellow
}

# 4. Výsledky onboardingu
foreach ($name in 'sample', 'markets', 'languages', 'scope', 'onboarding', 'ownership', 'settings') {
    Save-Answer $name (Invoke-Api GET "$base/$shopId/$name")
}
Save-Answer 'shop' (Invoke-Api GET "$base/$shopId")

$result = $s.result
Write-Host ""
Write-Host ("Ukázka: {0}{1}" -f $s.status, $(if ($s.error) { ", kód $($s.error)" } else { '' })) -ForegroundColor Cyan
if ($result) {
    Write-Host ("  zkontrolováno {0} stránek, nezkontrolováno {1}" -f $result.pagesChecked, ($result.notChecked.byReason | ConvertTo-Json -Compress))
    Write-Host ("  nálezy {0}: {1}; 5 nejzávažnějších: {2}" -f $result.findingCounts.total, ($result.findingCounts.bySeverity | ConvertTo-Json -Compress),
        (($result.topFindings | ForEach-Object { $_.ruleId }) -join ', '))
    Write-Host ("  ukázka opravy: {0}" -f $(if ($result.exampleFix) { 'ano' } else { "ne ($($result.exampleFixMissingReason))" }))
}
$markets = (Invoke-Api GET "$base/$shopId/markets").Json.markets
Write-Host ("  země: " + (($markets | ForEach-Object { "$($_.marketCode) ($($_.evidenceLevel), předvybraná $($_.preselected))" }) -join '; '))
$languages = (Invoke-Api GET "$base/$shopId/languages").Json
Write-Host ("  verze ({0}): " -f $languages.summary.kind) -NoNewline
Write-Host (($languages.versions | ForEach-Object { "$($_.language) $($_.status) kontrola $($_.checked) $($_.jurisdictions -join '+') produkty $($_.productCount) přeloženo $($_.translatedShare)" }) -join '; ')
$scope = Invoke-Api GET "$base/$shopId/scope"
if ($scope.Status -eq 200) {
    Write-Host ("  rozsah: {0} produktů za země {1}, ostatní stránky {2}, problémy {3}" -f $scope.Json.productTotal,
        (($scope.Json.markets | ForEach-Object { "$($_.marketCode)=$($_.productCount)" }) -join ', '), $scope.Json.otherPagesTotal, ($scope.Json.issues -join ', '))
}
else {
    Write-Host "  rozsah: HTTP $($scope.Status) $($scope.Json.code)"
}

Write-Host ""
Write-Host "Skutečná cena (psql jako postgres, databáze eshopguard):" -ForegroundColor Cyan
Write-Host "  SELECT provider, operation, sum(calls), sum(input_tokens), sum(cost_usd) FROM usage.usage_records WHERE run_id = '$runId' GROUP BY 1, 2;"
Write-Host "Pošlete prosím složku $OutDir a výstup dotazu."
