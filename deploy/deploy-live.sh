#!/bin/bash
# One-command deploy of ShambaNyumba to this server (it is the live server).
#   bash deploy/deploy-live.sh            # pull main, test, back up the database, deploy both sites
#   bash deploy/deploy-live.sh main       # only shambanyumba.com
#   bash deploy/deploy-live.sh booking    # only booking.shambanyumba.com
# Options (environment): SKIP_PULL=1 (deploy the working tree as it is), SKIP_TESTS=1.
#
# Order matters: the main site applies database migrations at startup, so it always goes first; the booking site
# never migrates. Main runs on 127.0.0.1:5010 (service smartestate), booking on 127.0.0.1:5011 (smartestate-booking).
set -euo pipefail

WHAT=${1:-all}
case "$WHAT" in all|main|booking) ;; *) echo "Usage: $0 [all|main|booking]"; exit 1 ;; esac

REPO=$(cd "$(dirname "$0")/.." && pwd)
APPLY="$REPO/deploy/smartestate-apply.sh"
MAIN_ENV=/home/builder/SmartEstate/smartestate.env
WORK=$(mktemp -d /tmp/shambanyumba-deploy.XXXXXX)
trap 'rm -rf "$WORK"' EXIT

# The system SDK in /usr/lib/dotnet has a broken Razor compiler; build with the official SDK.
export DOTNET_ROOT=/home/builder/.dotnet-official
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

cd "$REPO"

if [ "${SKIP_PULL:-0}" != "1" ]; then
    echo "== Pulling latest main"
    if [ -n "$(git status --porcelain)" ]; then
        echo "ERROR: uncommitted changes in $REPO. Commit or stash them (or run with SKIP_PULL=1)."
        exit 1
    fi
    git checkout -q main
    git pull --ff-only origin main
fi
echo "== Deploying $(git log --oneline -1)"

echo "== Building"
dotnet build SmartBiz.slnx -c Release -nologo -v q

if [ "${SKIP_TESTS:-0}" != "1" ]; then
    echo "== Testing"
    dotnet test SmartBiz.slnx -c Release --no-build -nologo -v q
fi

publish() {   # publish <project> <archive>
    local out="$WORK/$(basename "$2" .tgz)"
    dotnet publish "src/$1" -c Release -o "$out" -nologo -v q
    find "$out" -maxdepth 1 \( -name '*.pdb' -o -name web.config -o -name appsettings.Development.json \) -delete
    tar -czf "$2" -C "$out" .
}

if [ "$WHAT" != "booking" ]; then
    echo "== Backing up the database (before migrations)"
    mapfile -t ENVLINES < "$MAIN_ENV"   # not shell-sourceable (values contain spaces)
    CONN=$(printf '%s\n' "${ENVLINES[@]}" | grep '^ConnectionStrings__DefaultConnection=' | cut -d= -f2-)
    field() { echo "$CONN" | tr ';' '\n' | grep -iE "^($1)=" | head -1 | cut -d= -f2-; }
    DB=$(field 'Database|Initial Catalog'); DB_USER=$(field 'User ID|UID|User'); DB_PASS=$(field 'Password|PWD')
    if [ -z "$DB" ] || [ -z "$DB_USER" ]; then echo "ERROR: couldn't read Database/User ID from $MAIN_ENV"; exit 1; fi
    BAK="/var/opt/mssql/data/${DB}-$(date +%Y%m%d-%H%M%S).bak"
    # SQL Server Express: no WITH COMPRESSION.
    SQLCMDPASSWORD="$DB_PASS" /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$DB_USER" -C -b \
        -Q "BACKUP DATABASE [$DB] TO DISK='$BAK' WITH INIT"
    echo "Backup: $BAK"

    echo "== shambanyumba.com"
    publish RealEstateERP.Web "$WORK/main.tgz"
    sudo bash "$APPLY" "$WORK/main.tgz" main
fi

if [ "$WHAT" != "main" ]; then
    echo "== booking.shambanyumba.com"
    publish RealEstateERP.Booking.Web "$WORK/booking.tgz"
    sudo bash "$APPLY" "$WORK/booking.tgz" booking
fi

echo "== Done. Public check:"
[ "$WHAT" != "booking" ] && curl -s -o /dev/null -w "https://shambanyumba.com/health %{http_code}\n" https://shambanyumba.com/health
[ "$WHAT" != "main" ] && curl -s -o /dev/null -w "https://booking.shambanyumba.com/health %{http_code}\n" https://booking.shambanyumba.com/health
exit 0
