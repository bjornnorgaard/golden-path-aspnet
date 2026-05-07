#!/bin/sh
set -eu
if [ -z "${ConnectionStrings__DefaultConnection:-}" ]; then
  echo "ConnectionStrings__DefaultConnection must be set to run migrations and start the app." >&2
  exit 1
fi
./efbundle --connection "$ConnectionStrings__DefaultConnection"
exec ./WebApi
