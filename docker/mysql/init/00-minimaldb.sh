#!/bin/sh
set -eu

# Load the newest MinimalDB snapshot into MYSQL_DATABASE during first boot.
# The legacy schema is the compatibility contract: do not transform it here.
latest="$(ls -1 /mfb/support/MinimalDB-*.sql | sort | tail -n 1)"
if [ -z "$latest" ]; then
  echo "No MinimalDB-*.sql found in /mfb/support" >&2
  exit 1
fi

echo "Loading MyFlightbook schema from $latest into $MYSQL_DATABASE"
mysql --protocol=socket -uroot -p"$MYSQL_ROOT_PASSWORD" "$MYSQL_DATABASE" < "$latest"
echo "MyFlightbook schema loaded."
