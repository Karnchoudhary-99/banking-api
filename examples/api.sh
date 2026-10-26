#!/usr/bin/env bash
# Banking API tour against a running server (default http://localhost:5000).
# Start one first: dotnet run --project src/BankingApi.Server
# Requires: curl, python3 (only to pull ids out of JSON responses).
set -euo pipefail

BASE="${BASE:-http://localhost:5000}"

id_of() { python3 -c 'import sys,json; print(json.load(sys.stdin)["id"])'; }

echo "== health =="
curl -s "$BASE/health"; echo

echo "== create accounts =="
ASHA_JSON=$(curl -s -X POST "$BASE/accounts" -H 'Content-Type: application/json' -d '{"owner":"Asha","initialBalance":100}')
RAHUL_JSON=$(curl -s -X POST "$BASE/accounts" -H 'Content-Type: application/json' -d '{"owner":"Rahul","initialBalance":50}')
ASHA_ID=$(echo "$ASHA_JSON" | id_of)
RAHUL_ID=$(echo "$RAHUL_JSON" | id_of)
echo "Asha: $ASHA_ID"
echo "Rahul: $RAHUL_ID"

echo "== deposit 25.50 to Asha =="
curl -s -X POST "$BASE/accounts/$ASHA_ID/deposit" -H 'Content-Type: application/json' -d '{"amount":25.50}'; echo

echo "== transfer 30 Asha -> Rahul (idempotency key demo-transfer-1) =="
curl -s -X POST "$BASE/transfers" -H 'Content-Type: application/json' -H 'Idempotency-Key: demo-transfer-1' \
  -d "{\"fromAccountId\":\"$ASHA_ID\",\"toAccountId\":\"$RAHUL_ID\",\"amount\":30}"; echo

echo "== replay the same transfer (same key: no second movement) =="
curl -s -X POST "$BASE/transfers" -H 'Content-Type: application/json' -H 'Idempotency-Key: demo-transfer-1' \
  -d "{\"fromAccountId\":\"$ASHA_ID\",\"toAccountId\":\"$RAHUL_ID\",\"amount\":30}"; echo

echo "== Asha's statement =="
curl -s "$BASE/accounts/$ASHA_ID/statement"; echo

echo "== all accounts (Asha should be 95.50, not 65.50) =="
curl -s "$BASE/accounts"; echo
