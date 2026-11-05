#!/bin/bash
# ONE-TIME: give live its own database and turn the current one into the test sites' database.
#
#   sudo bash deploy/test/setup-test-sites.sh
#
# 1. Live (shambanyumba.com + booking) is stopped for ~1 minute, the current database SmartEstate is backed up and
#    restored as ShambaNyumba, and both live env files are switched to ShambaNyumba. Live restarts and is health-checked;
#    if it fails, the env files are put back and live runs on SmartEstate as before.
# 2. Test sites get an exact copy of the uploads, and use SmartEstate exactly as it is (accounts, data and photos).
# 3. Live only: the property, unit and listing photos are deleted (database rows and files). Accounts are untouched.
# 4. test.shambanyumba.com (5012) and testbooking.shambanyumba.com (5013): env files, systemd services, nginx, HTTPS.
#    The test sites capture every email (never sent), use their own sign-in cookie, and show a TEST SITE banner.
# Then deploy the code to them with: bash deploy/deploy-test.sh
set -euo pipefail

ROOT=/home/builder/SmartEstate
REPO=$(cd "$(dirname "$0")/../.." && pwd)
LIVE_ENV=$ROOT/smartestate.env
LIVE_BOOKING_ENV=$ROOT/booking/booking.env
TEST=$ROOT/test
OLD_DB=SmartEstate     # becomes the test database, unchanged
NEW_DB=ShambaNyumba    # the new live database
DATA=/var/opt/mssql/data
STAMP=$(date +%Y%m%d-%H%M%S)
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

[ "$(id -u)" = 0 ] || { echo "Run with sudo."; exit 1; }

# ---- SQL login from the live env file (not shell-sourceable: values contain spaces) ----
mapfile -t ENVLINES < "$LIVE_ENV"
CONN=$(printf '%s\n' "${ENVLINES[@]}" | grep '^ConnectionStrings__DefaultConnection=' | cut -d= -f2-)
field() { echo "$CONN" | tr ';' '\n' | grep -iE "^($1)=" | head -1 | cut -d= -f2-; }
DB=$(field 'Database|Initial Catalog'); DB_USER=$(field 'User ID|UID|User'); DB_PASS=$(field 'Password|PWD')
sql() { SQLCMDPASSWORD="$DB_PASS" "$SQLCMD" -S localhost -U "$DB_USER" -C -b -h -1 -W "$@"; }

# ======================= 1. Live gets its own database =======================
if [ "$DB" = "$NEW_DB" ]; then
    echo "== Live already uses $NEW_DB: skipping the database switch."
else
    [ "$DB" = "$OLD_DB" ] || { echo "ERROR: live uses database '$DB', expected $OLD_DB. Nothing changed."; exit 1; }
    if [ "$(sql -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = '$NEW_DB'")" != "0" ]; then
        echo "ERROR: a database called $NEW_DB already exists. Nothing changed."; exit 1
    fi

    # The app login can't create databases: ask once for a SQL Server admin (typed hidden, never stored).
    if [ "$(sql -Q "SET NOCOUNT ON; SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE')")" = "1" ]; then
        admin_sql() { sql "$@"; }
    else
        ADMIN_USER=${SQL_ADMIN_USER:-sa}
        read -r -s -p "SQL Server password for '$ADMIN_USER' (needed once, to create $NEW_DB): " ADMIN_PASS < /dev/tty; echo
        admin_sql() { SQLCMDPASSWORD="$ADMIN_PASS" "$SQLCMD" -S localhost -U "$ADMIN_USER" -C -b -h -1 -W "$@"; }
        admin_sql -Q "SET NOCOUNT ON; SELECT 1" > /dev/null || { echo "ERROR: that SQL login didn't work. Nothing changed."; exit 1; }
        [ "$(admin_sql -Q "SET NOCOUNT ON; SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE')")" = "1" ] \
            || { echo "ERROR: '$ADMIN_USER' can't create databases either. Nothing changed."; exit 1; }
    fi

    echo "== Stopping live (shambanyumba.com, booking.shambanyumba.com) so no change is lost during the copy"
    # Any failure from here until the env files are switched: bring live back on $OLD_DB, as it was.
    restore_live() {
        echo "FAILED: putting live back on $OLD_DB"
        for f in "$LIVE_ENV" "$LIVE_BOOKING_ENV"; do [ -f "$f.bak-$STAMP" ] && cp -p "$f.bak-$STAMP" "$f"; done
        systemctl start smartestate; sleep 5; systemctl start smartestate-booking
    }
    trap restore_live ERR
    systemctl stop smartestate-booking smartestate

    BAK="$DATA/$OLD_DB-before-split-$STAMP.bak"
    echo "== Backing up $OLD_DB to $BAK"
    sql -Q "BACKUP DATABASE [$OLD_DB] TO DISK='$BAK' WITH INIT"

    echo "== Restoring the copy as $NEW_DB (the new live database)"
    FILES=$(sql -Q "SET NOCOUNT ON; SELECT name + '|' + CAST(type AS varchar) FROM [$OLD_DB].sys.database_files")
    MOVES=""
    while IFS='|' read -r logical type; do
        [ -n "$logical" ] || continue
        if [ "$type" = "1" ]; then ext=_log.ldf; else ext=.mdf; fi
        MOVES="$MOVES, MOVE '$logical' TO '$DATA/$NEW_DB${ext}'"
    done <<< "$FILES"
    admin_sql -Q "RESTORE DATABASE [$NEW_DB] FROM DISK='$BAK' WITH RECOVERY$MOVES"
    # The app login must be able to use the copy before live is pointed at it.
    sql -d "$NEW_DB" -Q "SET NOCOUNT ON; SELECT 'copy check: ' + CAST(COUNT(*) AS varchar) + ' users' FROM AspNetUsers"

    echo "== Switching the live env files to $NEW_DB (copies kept as *.bak-$STAMP)"
    for f in "$LIVE_ENV" "$LIVE_BOOKING_ENV"; do
        cp -p "$f" "$f.bak-$STAMP"
        sed -i -E "s/((Database|Initial Catalog)=)$OLD_DB(;|$)/\1$NEW_DB\3/I" "$f"
        grep -qiE "(Database|Initial Catalog)=$NEW_DB" "$f" || { echo "ERROR: couldn't switch $f"; restore_live; exit 1; }
    done

    trap - ERR
    echo "== Starting live"
    systemctl start smartestate
    sleep 15
    if [ "$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5010/health)" != "200" ]; then
        echo "CRITICAL: live main site unhealthy on $NEW_DB. Putting the env files back (live on $OLD_DB as before)."
        cp -p "$LIVE_ENV.bak-$STAMP" "$LIVE_ENV"; cp -p "$LIVE_BOOKING_ENV.bak-$STAMP" "$LIVE_BOOKING_ENV"
        systemctl restart smartestate; systemctl start smartestate-booking
        journalctl -u smartestate -n 30 --no-pager || true
        exit 1
    fi
    systemctl start smartestate-booking
    sleep 8
    echo "Live main: $(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5010/health), booking: $(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5011/health)"
fi

# ======================= 2. Test copy of the uploads =======================
mkdir -p "$TEST/publish" "$TEST/backups" "$TEST/booking/publish" "$TEST/booking/backups"
if [ ! -d "$TEST/uploads" ]; then
    echo "== Copying the uploads (photos included) for the test sites"
    cp -a "$ROOT/uploads" "$TEST/uploads"
fi

# ======================= 3. Live: delete the photos =======================
echo "== Live: deleting property, unit and listing photos (rows in $NEW_DB, files in $ROOT/uploads)"
sql -d "$NEW_DB" -Q "SET NOCOUNT ON; DELETE FROM ListingPhotos; DELETE FROM UnitPhotos; DELETE FROM AssetPhotos;
    SELECT 'photos left in live: ' + CAST((SELECT COUNT(*) FROM ListingPhotos) + (SELECT COUNT(*) FROM UnitPhotos) + (SELECT COUNT(*) FROM AssetPhotos) AS varchar)"
for d in estate-photos unit-photos listing-photos; do
    [ -d "$ROOT/uploads/$d" ] && rm -rf "${ROOT:?}/uploads/$d"
done

# ======================= 4. Test sites =======================
echo "== Test env files (database $OLD_DB, ports 5012/5013)"
make_env() {   # make_env <live env> <test env> <port> <extra lines...>
    local src=$1 dst=$2 port=$3; shift 3
    [ -f "$dst" ] && { echo "$dst exists: kept."; return; }
    grep -vE '^(ASPNETCORE_URLS|Storage__LocalRoot|Auth__|Booking__|Site__|DataProtection__)=' "$src" \
        | sed -E "s/((Database|Initial Catalog)=)$NEW_DB(;|$)/\1$OLD_DB\3/I" > "$dst"
    {
        echo "ASPNETCORE_URLS=http://127.0.0.1:$port"
        echo "Storage__LocalRoot=$TEST/uploads"
        echo "Site__IsTest=true"
        echo "Auth__CookieDomain=.shambanyumba.com"
        echo "Auth__CookieName=.ShambaNyumba.Test.Identity"
        echo "DataProtection__ApplicationName=ShambaNyumbaTest"
        printf '%s\n' "$@"
    } >> "$dst"
    chown builder:builder "$dst"; chmod 600 "$dst"
    grep -qiE "(Database|Initial Catalog)=$OLD_DB" "$dst" || { echo "ERROR: $dst doesn't point at $OLD_DB"; exit 1; }
}
make_env "$LIVE_ENV" "$TEST/test.env" 5012 \
    "Auth__TrustedReturnHosts=testbooking.shambanyumba.com" "Booking__SiteUrl=https://testbooking.shambanyumba.com"
make_env "$LIVE_BOOKING_ENV" "$TEST/booking/booking.env" 5013 \
    "Booking__MainSiteUrl=https://test.shambanyumba.com"
chown -R builder:builder "$TEST"

echo "== systemd services"
cp "$REPO/deploy/test/smartestate-test.service" "$REPO/deploy/test/smartestate-test-booking.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable smartestate-test smartestate-test-booking

echo "== nginx + HTTPS"
cp "$REPO/deploy/test/test-shambanyumba.nginx" /etc/nginx/sites-available/test-shambanyumba
cp "$REPO/deploy/test/testbooking-shambanyumba.nginx" /etc/nginx/sites-available/testbooking-shambanyumba
ln -sf /etc/nginx/sites-available/test-shambanyumba /etc/nginx/sites-enabled/test-shambanyumba
ln -sf /etc/nginx/sites-available/testbooking-shambanyumba /etc/nginx/sites-enabled/testbooking-shambanyumba
nginx -t && systemctl reload nginx
certbot --nginx --non-interactive --redirect -d test.shambanyumba.com
certbot --nginx --non-interactive --redirect -d testbooking.shambanyumba.com

echo "== Done. Live runs on $NEW_DB; test will run on $OLD_DB."
echo "Next: bash deploy/deploy-test.sh    (builds this checkout and starts the two test sites)"
