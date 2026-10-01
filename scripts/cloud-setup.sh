#!/usr/bin/env bash
# Příprava cloudového prostředí Claude Code pro EshopGuard (Ubuntu/Debian).
# Použití: v nastavení cloudového prostředí jako setup script: bash scripts/cloud-setup.sh
# Prostředí musí mít povolený přístup na: builds.dotnet.microsoft.com, dot.net, api.nuget.org,
#   apt.postgresql.org, www.postgresql.org, registry.npmjs.org (a výchozí zrcadla apt).
# Skript neobsahuje žádné klíče. Klíče (Jev, OpenAI) do cloudu nedávat, placené běhy se spouští lokálně se souhlasem.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SUDO=""
if [ "$(id -u)" -ne 0 ] && command -v sudo >/dev/null 2>&1; then SUDO="sudo"; fi

echo "== .NET SDK podle global.json"
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^10\.'; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --jsonfile "$REPO_ROOT/global.json" --install-dir "$HOME/.dotnet"
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
  grep -q 'DOTNET_ROOT' "$HOME/.bashrc" 2>/dev/null || {
    echo 'export DOTNET_ROOT="$HOME/.dotnet"' >> "$HOME/.bashrc"
    echo 'export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"' >> "$HOME/.bashrc"
  }
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet --version

echo "== PostgreSQL 18"
if [ ! -x /usr/lib/postgresql/18/bin/postgres ]; then
  $SUDO apt-get update -y
  $SUDO apt-get install -y curl ca-certificates gnupg lsb-release
  curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | $SUDO gpg --dearmor --yes -o /usr/share/keyrings/postgresql.gpg
  echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" \
    | $SUDO tee /etc/apt/sources.list.d/pgdg.list >/dev/null
  $SUDO apt-get update -y
  $SUDO apt-get install -y postgresql-18
fi
# Když obraz už má jinou verzi PostgreSQL, balíček cluster 18 nezaloží: jinou verzi přesunout na 5433, 18 na 5432.
if ! pg_lsclusters --no-header 2>/dev/null | grep -q '^18 \+main'; then
  for conf in /etc/postgresql/*/main/postgresql.conf; do
    [ -f "$conf" ] || continue
    ver="$(basename "$(dirname "$(dirname "$conf")")")"
    $SUDO pg_ctlcluster "$ver" main stop 2>/dev/null || true
    $SUDO sed -i 's/^port = 5432/port = 5433/' "$conf"
  done
  $SUDO pg_createcluster 18 main --port 5432 --locale C.UTF-8 >/dev/null
fi
# Spustit cluster (v kontejneru bez systemd)
$SUDO pg_ctlcluster 18 main start 2>/dev/null || true
# Stejné přihlášení správce jako lokálně (jen pro zakládací skript rolí, aplikace se připojuje jako eshopguard_app/worker)
if [ "$(id -u)" -eq 0 ]; then AS_POSTGRES="runuser -u postgres --"; else AS_POSTGRES="sudo -u postgres"; fi
$AS_POSTGRES psql -p 5432 -v ON_ERROR_STOP=1 -c "ALTER USER postgres PASSWORD 'postgres';" >/dev/null

echo "== OpenSpec CLI (validace plánu)"
if command -v npm >/dev/null 2>&1; then
  npm install -g @fission-ai/openspec@1.3.1 >/dev/null
  DO_NOT_TRACK=1 OPENSPEC_TELEMETRY=0 openspec --version
else
  echo "   npm není k dispozici, OpenSpec CLI se neinstaluje."
fi

echo "== Obnova balíčků a sestavení"
cd "$REPO_ROOT"
dotnet restore src/EshopGuard.sln
dotnet build src/EshopGuard.sln --no-restore -v minimal
dotnet tool restore

echo "== Role, databáze eshopguard, eshopguard_test a eshopguard_test_jobs, user-secrets (deploy/dev/setup-local.ps1)"
export PATH="$HOME/.dotnet/tools:$PATH"
if ! command -v pwsh >/dev/null 2>&1; then
  dotnet tool install --global PowerShell >/dev/null
fi
PGPASSWORD=postgres pwsh -NoProfile -File "$REPO_ROOT/deploy/dev/setup-local.ps1" -PgBin /usr/lib/postgresql/18/bin
dotnet ef database update --project src/EshopGuard.Data --no-build
echo "Hotovo. Testy bez placených: dotnet test --solution src/EshopGuard.sln --filter-not-trait \"Category=Jev\""
