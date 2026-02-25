#!/bin/bash
set -e

# Create separate databases for each integration test project so they
# don't interfere when dotnet test runs projects concurrently.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE chain_sharp_data_tests OWNER $POSTGRES_USER;
    CREATE DATABASE chain_sharp_scheduler_tests OWNER $POSTGRES_USER;
EOSQL
