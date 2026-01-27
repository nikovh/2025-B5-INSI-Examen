#!/usr/bin/env bash
set -e

if [ -f .env ]; then
  export $(grep -v '^#' .env | xargs)
else
  echo "ERROR: archivo .env no encontrado"
  exit 1
fi

export ARTEMIS_URL="tcp://192.168.56.101:61616"
export ARTEMIS_USER="nicolas"
export ARTEMIS_PASS="hola"

export ARTEMIS_TOPIC="nva_amq_estado_cuenta"
export ARTEMIS_DEST_TYPE="topic"

export CONTROL_ACCESO_BASE_URL="http://localhost:5003"

# Reintentos
export REST_MAX_RETRIES="5"
export REST_BACKOFF_MS="800"

java -jar target/adapter-control-acceso-1.0.0.jar
