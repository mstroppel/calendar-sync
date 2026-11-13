#!/bin/sh
set -eu

# Use dummy settings and disable networking so this check cannot touch calendars.
log_file=$(mktemp)
trap 'rm -f "$log_file"' EXIT

status=0
docker run --rm --network none \
  --env SOURCE_SERVER_URL=http://127.0.0.1:1/ \
  --env SOURCE_CALENDAR_URL=http://127.0.0.1:1/source/ \
  --env SOURCE_USERNAME=smoke-test \
  --env SOURCE_PASSWORD=smoke-test \
  --env TARGET_SERVER_URL=http://127.0.0.1:1/ \
  --env TARGET_CALENDAR_URL=http://127.0.0.1:1/target/ \
  --env TARGET_USERNAME=smoke-test \
  --env TARGET_PASSWORD=smoke-test \
  --env SOURCE_EVENT_PATTERN='.*' \
  --env TARGET_EVENT_NAME=smoke-test \
  --entrypoint timeout \
  "${1:?Usage: docker-smoke-test.sh IMAGE}" \
  --signal=TERM --kill-after=5s 10s dotnet Rafatz.CalendarSync.Worker.dll \
  >"$log_file" 2>&1 || status=$?

cat "$log_file"

# A healthy worker stays running until timeout sends SIGTERM (exit code 124).
if [ "$status" -ne 124 ]; then
  echo "Expected a running worker; container exited with status $status" >&2
  exit 1
fi
grep -Fq 'Application started.' "$log_file"
