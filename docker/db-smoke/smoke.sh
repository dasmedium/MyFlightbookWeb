#!/bin/sh
set -eu

: "${MYSQL_DATABASE:?MYSQL_DATABASE is required}"
: "${MYSQL_USER:?MYSQL_USER is required}"
: "${MYSQL_PASSWORD:?MYSQL_PASSWORD is required}"

mysql_cmd() {
  mysql -h mysql -P 3306 -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" "$@" "$MYSQL_DATABASE"
}

table_count="$(mysql_cmd -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE();")"
echo "Tables in $MYSQL_DATABASE: $table_count"

if [ "$table_count" -lt 50 ]; then
  echo "Expected at least 50 tables, found $table_count" >&2
  exit 1
fi

for table in users flights aircraft flightproperties endorsements flighttelemetry images localconfig; do
  mysql_cmd -e "SELECT 1 FROM \`$table\` LIMIT 1" >/dev/null
  echo "OK: $table"
done

echo "Database smoke test passed."
