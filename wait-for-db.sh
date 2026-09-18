#!/bin/sh
set -e

echo "Checking database readiness..."

# Wait for PostgreSQL to be available
until pg_isready -h postgres -p 5432 -U postgres; do
  echo "Waiting for PostgreSQL at postgres:5432..."
  sleep 2
done

echo "PostgreSQL is ready. Starting application..."

# Start the actual app
exec dotnet BizfreeApp.dll
