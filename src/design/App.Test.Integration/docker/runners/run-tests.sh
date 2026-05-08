#!/usr/bin/env bash
# run-tests.sh — Shared entrypoint for cross-distro test runners.
#
# Runs unit tests (Sdk, Shared, AngorApp) and integration tests against
# the local signet stack. The infra stack must already be running on the
# shared Docker network (angor-test-net).
#
# Environment variables (set by docker-compose.tests.yml):
#   TEST_SCOPE  — "unit" | "integration" | "all" (default: all)
#   DISTRO_NAME — friendly name for logging (e.g. "ubuntu-24.04")

set -euo pipefail

DISTRO_NAME="${DISTRO_NAME:-unknown}"
TEST_SCOPE="${TEST_SCOPE:-all}"
SRC_DIR="/src"

echo "============================================"
echo "  Test runner: ${DISTRO_NAME}"
echo "  Scope:       ${TEST_SCOPE}"
echo "  .NET:        $(dotnet --version)"
echo "  OS:          $(cat /etc/os-release 2>/dev/null | grep PRETTY_NAME | cut -d= -f2 | tr -d '"')"
echo "============================================"

# ── Wait for infra stack ──────────────────────────────────────────────
wait_for_service() {
    local host="$1" port="$2" label="$3" timeout="${4:-120}"
    echo -n "Waiting for ${label} (${host}:${port})..."
    local elapsed=0
    while ! (echo > /dev/tcp/"$host"/"$port") 2>/dev/null; do
        sleep 2
        elapsed=$((elapsed + 2))
        if [ "$elapsed" -ge "$timeout" ]; then
            echo " TIMEOUT after ${timeout}s"
            return 1
        fi
        echo -n "."
    done
    echo " ready (${elapsed}s)"
}

if [ "$TEST_SCOPE" != "unit" ]; then
    wait_for_service angor-test-btc-node    38332 "Bitcoin RPC"
    wait_for_service angor-test-fulcrum     50001 "Fulcrum"
    wait_for_service angor-test-faucet-api  5500  "Faucet API"
    wait_for_service angor-test-relay-1     7777  "Relay 1"
    wait_for_service angor-test-relay-2     7777  "Relay 2"
    wait_for_service angor-test-mempool-api 8999  "Mempool API"
    echo ""
fi

# ── Restore once ──────────────────────────────────────────────────────
echo "Restoring NuGet packages..."
dotnet restore "${SRC_DIR}/Avalonia.sln" --verbosity quiet
dotnet restore "${SRC_DIR}/design/App.Test.Integration/App.Test.Integration.csproj" --verbosity quiet

FAILED=0

run_tests() {
    local project="$1" label="$2"
    echo ""
    echo "── ${label} ──"
    if dotnet test "$project" --no-restore --verbosity normal --logger "console;verbosity=normal"; then
        echo "✓ ${label}: PASSED"
    else
        echo "✗ ${label}: FAILED"
        FAILED=1
    fi
}

# ── Unit tests ────────────────────────────────────────────────────────
if [ "$TEST_SCOPE" = "unit" ] || [ "$TEST_SCOPE" = "all" ]; then
    run_tests "${SRC_DIR}/sdk/Angor.Sdk.Tests/Angor.Sdk.Tests.csproj"           "SDK Unit Tests"
    run_tests "${SRC_DIR}/shared/Angor.Shared.Tests/Angor.Shared.Tests.csproj"  "Shared Unit Tests"
    run_tests "${SRC_DIR}/avalonia/AngorApp.Tests/AngorApp.Tests.csproj"         "Avalonia Model Tests"
fi

# ── Integration tests ────────────────────────────────────────────────
if [ "$TEST_SCOPE" = "integration" ] || [ "$TEST_SCOPE" = "all" ]; then
    run_tests "${SRC_DIR}/design/App.Test.Integration/App.Test.Integration.csproj" "Integration Tests"
fi

echo ""
echo "============================================"
if [ "$FAILED" -eq 0 ]; then
    echo "  ${DISTRO_NAME}: ALL TESTS PASSED"
else
    echo "  ${DISTRO_NAME}: SOME TESTS FAILED"
fi
echo "============================================"

exit $FAILED
