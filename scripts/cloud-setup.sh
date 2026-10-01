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

echo "== .NET SDK podle src/global.json"
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^10\.'; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --jsonfile "$REPO_ROOT/src/global.json" --install-dir "$HOME/.dotnet"
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
if ! command -v psql >/dev/null 2>&1 || ! psql --version | grep -q ' 18\.'; then
  $SUDO apt-get update -y
  $SUDO apt-get install -y curl ca-certificates gnupg lsb-release
  curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | $SUDO gpg --dearmor -o /usr/share/keyrings/postgresql.gpg
  echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" \
    | $SUDO tee /etc/apt/sources.list.d/pgdg.list >/dev/null
  $SUDO apt-get update -y
  $SUDO apt-get install -y postgresql-18
fi
# Spustit cluster (v kontejneru bez systemd)
$SUDO pg_ctlcluster 18 main start 2>/dev/null || $SUDO service postgresql start || true
# Stejné přihlášení správce jako lokálně (jen pro zakládací skript rolí, aplikace se připojuje jako eshopguard_app/worker)
$SUDO -u postgres psql -v ON_ERROR_STOP=1 -c "ALTER USER postgres PASSWORD 'postgres';" >/dev/null
# Role a databáze, až je změna 2 založí (deploy/sql/00_roles.sql)
if [ -f "$REPO_ROOT/deploy/sql/00_roles.sql" ]; then
  PGPASSWORD=postgres psql -h localhost -U postgres -v ON_ERROR_STOP=1 -f "$REPO_ROOT/deploy/sql/00_roles.sql"
else
  echo "   deploy/sql/00_roles.sql zatím neexistuje (vznikne ve změně 2), role se nezakládají."
fi

echo "== OpenSpec CLI (validace plánu)"
if command -v npm >/dev/null 2>&1; then
  npm install -g @fission-ai/openspec@1.3.1 >/dev/null
  DO_NOT_TRACK=1 OPENSPEC_TELEMETRY=0 openspec --version
else
  echo "   npm není k dispozici, OpenSpec CLI se neinstaluje."
fi

echo "== Obnova balíčků a sestavení"
cd "$REPO_ROOT/src"
dotnet restore EshopGuard.sln
dotnet build EshopGuard.sln --no-restore -v minimal
echo "Hotovo. Testy bez placených: dotnet run --project src/tests/EshopGuard.Core.Tests -- -notrait \"Category=Jev\""
