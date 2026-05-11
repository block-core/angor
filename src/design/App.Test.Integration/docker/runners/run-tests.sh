#!/usr/bin/env bash
# run-tests.sh — Shared entrypoint for cross-distro test runners.
#
# Runs unit tests (Sdk, Shared, AngorApp) and integration tests against
# the public Angor signet (default) or a local signet stack when
# ANGOR_INDEXER_URL / ANGOR_RELAY_URLS / ANGOR_FAUCET_BASE_URL are set.
#
# Environment variables (set by docker-compose.tests.yml):
#   TEST_SCOPE  — "unit" | "integration" | "all" (default: all)
#   DISTRO_NAME — friendly name for logging (e.g. "ubuntu-24.04")

set -euo pipefail

DISTRO_NAME="${DISTRO_NAME:-unknown}"
TEST_SCOPE="${TEST_SCOPE:-all}"
SRC_DIR="/src/src"

echo "============================================"
echo "  Test runner: ${DISTRO_NAME}"
echo "  Scope:       ${TEST_SCOPE}"
echo "  .NET:        $(dotnet --version)"
echo "  OS:          $(cat /etc/os-release 2>/dev/null | grep PRETTY_NAME | cut -d= -f2 | tr -d '"')"
echo "  Indexer:     ${ANGOR_INDEXER_URL:-<public angor signet>}"
echo "  Relays:      ${ANGOR_RELAY_URLS:-<public angor signet>}"
echo "  Faucet:      ${ANGOR_FAUCET_BASE_URL:-<public angor signet>}"
echo "============================================"

# ── Restore once ──────────────────────────────────────────────────────
echo "Restoring NuGet packages..."
# Restore individual test projects (not the full solution, which includes
# Android/iOS/WASM projects requiring workloads not installed in the runner).
dotnet restore "${SRC_DIR}/sdk/Angor.Sdk.Tests/Angor.Sdk.Tests.csproj" --verbosity quiet
dotnet restore "${SRC_DIR}/shared/Angor.Shared.Tests/Angor.Shared.Tests.csproj" --verbosity quiet
dotnet restore "${SRC_DIR}/avalonia/AngorApp.Tests/AngorApp.Tests.csproj" --verbosity quiet
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
