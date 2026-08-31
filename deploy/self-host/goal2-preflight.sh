#!/usr/bin/env bash
set -euo pipefail

# Read-only preflight. Values are tested for presence, never printed.
required=(POSTGRES_PASSWORD JWT_KEY BOOTSTRAP_ADMIN_PASSWORD)
for name in "${required[@]}"; do
  if [[ -z "${!name:-}" ]]; then
    echo "missing required secret: ${name}" >&2
    exit 1
  fi
done

if [[ "${EVENTING_PROVIDER:-InMemory}" == "RabbitMQ" && -z "${RABBITMQ_PASSWORD:-}" ]]; then
  echo "RABBITMQ_PASSWORD is required when EVENTING_PROVIDER=RabbitMQ" >&2
  exit 1
fi

echo "GOAL2 self-host secret preflight passed (values were not displayed)."
