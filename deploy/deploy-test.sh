#!/bin/bash
# Deploy THIS checkout (committed or not, any branch) to the test sites, to try changes before they go live.
#   bash deploy/deploy-test.sh            # test.shambanyumba.com + testbooking.shambanyumba.com
#   bash deploy/deploy-test.sh main       # only test.shambanyumba.com
#   bash deploy/deploy-test.sh booking    # only testbooking.shambanyumba.com
# Options (environment): SKIP_TESTS=1.
#
# The test sites use database SmartEstate (live uses ShambaNyumba), never send email, and have their own sign-in.
# Test main runs on 127.0.0.1:5012 (smartestate-test), test booking on 127.0.0.1:5013 (smartestate-test-booking).
# First time only: sudo bash deploy/test/setup-test-sites.sh
set -euo pipefail

WHAT=${1:-all}
case "$WHAT" in all|main|booking) ;; *) echo "Usage: $0 [all|main|booking]"; exit 1 ;; esac

REPO=$(cd "$(dirname "$0")/.." && pwd)
APPLY="$REPO/deploy/smartestate-apply.sh"
TEST_ENV=/home/builder/SmartEstate/test/test.env
WORK=$(mktemp -d /tmp/shambanyumba-test-deploy.XXXXXX)
trap 'rm -rf "$WORK"' EXIT

[ -f "$TEST_ENV" ] || { echo "Test sites not set up yet: sudo bash deploy/test/setup-test-sites.sh"; exit 1; }
grep -q '^Site__IsTest=true' "$TEST_ENV" || { echo "ERROR: $TEST_ENV is not marked Site__IsTest=true. Refusing."; exit 1; }

export DOTNET_ROOT=/home/builder/.dotnet-official
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$REPO"

echo "== Deploying to TEST: $(git log --oneline -1)$( [ -n "$(git status --porcelain)" ] && echo ' + uncommitted changes')"
dotnet build SmartBiz.slnx -c Release -nologo -v q
if [ "${SKIP_TESTS:-0}" != "1" ]; then
    dotnet test SmartBiz.slnx -c Release --no-build -nologo -v q
fi

publish() {   # publish <project> <archive>
    local out="$WORK/$(basename "$2" .tgz)"
    dotnet publish "src/$1" -c Release -o "$out" -nologo -v q
    find "$out" -maxdepth 1 \( -name '*.pdb' -o -name web.config -o -name appsettings.Development.json \) -delete
    tar -czf "$2" -C "$out" .
}

if [ "$WHAT" != "booking" ]; then
    echo "== Backing up the test database (before migrations)"
    mapfile -t ENVLINES < "$TEST_ENV"
    CONN=$(printf '%s\n' "${ENVLINES[@]}" | grep '^ConnectionStrings__DefaultConnection=' | cut -d= -f2-)
    field() { echo "$CONN" | tr ';' '\n' | grep -iE "^($1)=" | head -1 | cut -d= -f2-; }
    DB=$(field 'Database|Initial Catalog'); DB_USER=$(field 'User ID|UID|User'); DB_PASS=$(field 'Password|PWD')
    LIVE_DB=$(grep '^ConnectionStrings__DefaultConnection=' /home/builder/SmartEstate/smartestate.env | tr ';' '\n' | grep -iE '^(Database|Initial Catalog)=' | cut -d= -f2-)
    [ "$DB" != "$LIVE_DB" ] || { echo "ERROR: test and live use the same database ($DB). Refusing."; exit 1; }
    BAK="/var/opt/mssql/data/${DB}-test-$(date +%Y%m%d-%H%M%S).bak"
    SQLCMDPASSWORD="$DB_PASS" /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$DB_USER" -C -b \
        -Q "BACKUP DATABASE [$DB] TO DISK='$BAK' WITH INIT"

    echo "== test.shambanyumba.com"
    publish RealEstateERP.Web "$WORK/main.tgz"
    sudo bash "$APPLY" "$WORK/main.tgz" test-main
fi

if [ "$WHAT" != "main" ]; then
    echo "== testbooking.shambanyumba.com"
    publish RealEstateERP.Booking.Web "$WORK/booking.tgz"
    sudo bash "$APPLY" "$WORK/booking.tgz" test-booking
fi

echo "== Done. Public check:"
[ "$WHAT" != "booking" ] && curl -s -o /dev/null -w "https://test.shambanyumba.com/health %{http_code}\n" https://test.shambanyumba.com/health
[ "$WHAT" != "main" ] && curl -s -o /dev/null -w "https://testbooking.shambanyumba.com/health %{http_code}\n" https://testbooking.shambanyumba.com/health
