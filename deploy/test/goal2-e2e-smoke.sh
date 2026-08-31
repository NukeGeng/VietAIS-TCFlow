#!/usr/bin/env bash
set -euo pipefail

# Requires a running isolated GOAL2 host and a caller-provided test identity.
# The script intentionally never prints response bodies that may contain data.
base_url="${TCFLOW_BASE_URL:-http://localhost:5080}"
health_url="${base_url%/}/health"

curl --fail --silent --show-error "$health_url" >/dev/null

if [[ -z "${TCFLOW_TEST_TOKEN:-}" ]]; then
  echo "TCFLOW_TEST_TOKEN is required for authenticated checks" >&2
  exit 1
fi

auth=(-H "Authorization: Bearer ${TCFLOW_TEST_TOKEN}")
status="$(curl --silent --output /dev/null --write-out '%{http_code}' "${auth[@]}" "${base_url%/}/api/v1/projects")"
if [[ "$status" != "200" && "$status" != "403" ]]; then
  echo "unexpected authenticated project endpoint status: $status" >&2
  exit 1
fi

echo "GOAL2 authenticated smoke preflight passed at ${base_url}."
