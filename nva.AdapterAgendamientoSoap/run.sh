#!/usr/bin/env bash
set -e

if [ -f .env ]; then
  export $(grep -v '^#' .env | xargs)
else
  echo "ERROR: archivo .env no encontrado"
  exit 1
fi

JAR=$(ls target/*.jar | grep "adapter-agendamiento-soap" | head -n 1)
echo "Ejecutando: $JAR"
java -jar "$JAR"
