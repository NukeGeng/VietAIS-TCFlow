#!/usr/bin/env bash
set -euo pipefail

# Read-only preflight. Values are tested for presence, never printed.
required=(POSTGRES_PASSWORD JWT_KEY BOOTSTRAP_ADMIN_PASSWORD)
for name in "${required[@]}"; do
  value="${!name:-}"
  if [[ -z "$value" ]]; then
    echo "missing required secret: ${name}" >&2
    exit 1
  fi
  case "$value" in
    replace-with-*|change-me*|example*|'<*>')
      echo "sample placeholder is not allowed for secret: ${name}" >&2
      exit 1
      ;;
  esac
done

if [[ "${EVENTING_PROVIDER:-InMemory}" == "RabbitMQ" && -z "${RABBITMQ_PASSWORD:-}" ]]; then
  echo "RABBITMQ_PASSWORD is required when EVENTING_PROVIDER=RabbitMQ" >&2
  exit 1
fi

if [[ "${EVENTING_PROVIDER:-InMemory}" == "RabbitMQ" ]]; then
  case "${RABBITMQ_PASSWORD}" in
    replace-with-*|change-me*|example*|'<*')
      echo "sample placeholder is not allowed for secret: RABBITMQ_PASSWORD" >&2
      exit 1
      ;;
  esac
fi

echo "GOAL2 self-host secret preflight passed (values were not displayed)."
