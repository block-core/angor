#!/usr/bin/env bash
# android-perf.sh — Build APK, deploy to emulator, capture startup + tab-switch perf timings.
# Usage:
#   ./scripts/android-perf.sh          # full: build + deploy + capture
#   ./scripts/android-perf.sh --skip-build   # deploy existing APK + capture
#   ./scripts/android-perf.sh --capture-only # just capture (app already running)
#
# Requires: adb, Android SDK emulator, .NET 9 android workload
# Perf markers are grep'd from logcat tags: Prewarm, ShellPerf, FindProjectsPerf, ProjectsLoad

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ANDROID_CSPROJ="$REPO_ROOT/src/design/App.Android/App.Android.csproj"
PACKAGE="io.angor.app"
AVD="Pixel_9"
EMULATOR="$HOME/Library/Android/sdk/emulator/emulator"
ADB="${ADB:-adb}"
REPORT_DIR="$REPO_ROOT/perf-reports"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
REPORT="$REPORT_DIR/android-perf-$TIMESTAMP.txt"

# JDK 17 required by Android SDK tooling
export JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home}"

SKIP_BUILD=false
CAPTURE_ONLY=false

for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=true ;;
    --capture-only) CAPTURE_ONLY=true; SKIP_BUILD=true ;;
  esac
done

mkdir -p "$REPORT_DIR"

# ── Helpers ──
log() { printf "\033[1;36m▸ %s\033[0m\n" "$1"; }
err() { printf "\033[1;31m✗ %s\033[0m\n" "$1" >&2; exit 1; }

wait_for_device() {
  log "Waiting for device..."
  "$ADB" wait-for-device
  # Wait for boot_completed
  local tries=0
  while [ "$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" != "1" ]; do
    sleep 2
    tries=$((tries + 1))
    [ $tries -gt 60 ] && err "Emulator didn't boot in 120s"
  done
  log "Device ready"
}

ensure_emulator() {
  if "$ADB" devices | grep -q "emulator\|device$" | grep -v "List"; then
    # Check if any actual device line exists (not just the header)
    local count
    count=$("$ADB" devices | grep -cE "device$|emulator" || true)
    if [ "$count" -gt 0 ]; then
      log "Device/emulator already connected"
      return
    fi
  fi
  log "Starting emulator: $AVD"
  "$EMULATOR" -avd "$AVD" -no-snapshot-load -no-audio -gpu auto &
  EMULATOR_PID=$!
  wait_for_device
}

# ── Build ──
if [ "$SKIP_BUILD" = false ]; then
  log "Building APK (Debug for faster build)..."
  cd "$REPO_ROOT"
  dotnet build "$ANDROID_CSPROJ" -c Debug -t:Install \
    -p:AndroidFastDeploymentType=Assemblies 2>&1 | tail -5
  log "APK built"
fi

# ── Deploy + Launch ──
if [ "$CAPTURE_ONLY" = false ]; then
  ensure_emulator

  # Find the APK
  APK_PATH=$(find "$REPO_ROOT/src/design/App.Android/bin" -name "*.apk" -newer "$ANDROID_CSPROJ" 2>/dev/null | head -1)
  if [ -z "$APK_PATH" ]; then
    # -t:Install already installs, but if we used --skip-build we need to find & install
    APK_PATH=$(find "$REPO_ROOT/src/design/App.Android/bin" -name "*.apk" 2>/dev/null | sort -t/ -k10 -r | head -1)
    [ -z "$APK_PATH" ] && err "No APK found. Run without --skip-build."
    log "Installing APK: $APK_PATH"
    "$ADB" install -r "$APK_PATH"
  fi

  # Force-stop and clear logcat
  "$ADB" shell am force-stop "$PACKAGE" 2>/dev/null || true
  sleep 1
  "$ADB" logcat -c

  log "Launching $PACKAGE..."
  # Use monkey to launch via the launcher intent — avoids hard-coding the CRC activity name
  LAUNCH_OUTPUT=$("$ADB" shell am start -W -a android.intent.action.MAIN -c android.intent.category.LAUNCHER "$PACKAGE" 2>&1)
  echo "$LAUNCH_OUTPUT" | tee -a "$REPORT"
  LAUNCH_TIME=$(echo "$LAUNCH_OUTPUT" | grep -oE "TotalTime: [0-9]+" | awk '{print $2}')
  if [ -n "$LAUNCH_TIME" ]; then
    log "Activity launch time: ${LAUNCH_TIME}ms"
    echo "  ActivityManager TotalTime: ${LAUNCH_TIME}ms" >> "$REPORT"
  fi
fi

# ── Capture perf markers ──
log "Capturing perf logs for 30s (waiting for prewarm + tab-switch)..."
{
  echo "=== Android Perf Report ==="
  echo "Date: $(date)"
  echo "Device: $("$ADB" shell getprop ro.product.model 2>/dev/null | tr -d '\r')"
  echo "Android: $("$ADB" shell getprop ro.build.version.release 2>/dev/null | tr -d '\r')"
  echo ""
  echo "=== Perf Markers (30s capture) ==="
} >> "$REPORT"

# Capture logcat for 30s, filtering for our perf tags
timeout 30 "$ADB" logcat -v time \
  -s "DOTNET:*" "Prewarm:*" "ShellPerf:*" "FindProjectsPerf:*" "ProjectsLoad:*" "FindProjectsCache:*" \
  2>/dev/null | tee -a "$REPORT" || true

# Also grab any lines with our markers from the broader log
echo "" >> "$REPORT"
echo "=== Broad grep for perf markers ===" >> "$REPORT"
"$ADB" logcat -d | grep -iE "Prewarm|ShellPerf|FindProjectsPerf|ProjectsLoad|FindProjectsCache|factoryMs|attachMs|firstRender|disk-load|disk-save|seeded=" >> "$REPORT" 2>/dev/null || true

# ── Parse & summarize ──
echo "" >> "$REPORT"
echo "=== Summary ===" >> "$REPORT"

extract() {
  local label="$1" pattern="$2"
  local val
  val=$(grep -oE "$pattern" "$REPORT" | tail -1)
  if [ -n "$val" ]; then
    echo "  $label: $val" | tee -a "$REPORT"
  fi
}

log "Results:"
extract "Prewarm Latest()" "Latest\(\) completed in [0-9]+ms"
extract "Disk cache load" "disk-load: SUCCESS loaded [0-9]+ cached"
extract "Disk cache save" "disk-save: SUCCESS wrote [0-9]+ projects"
extract "FindProjects factory" "key=FindProjects cached=false factoryMs=[0-9]+"
extract "FindProjects first render" "key=FindProjects.*totalMs=[0-9]+"
extract "VM seeded from cache" "seeded=[0-9]+"
extract "SDK Latest() call" "Latest\(\) returned in [0-9]+ms"
extract "UI update time" "UI update [0-9]+ms"

echo ""
log "Full report: $REPORT"
log "To re-run capture only: ./scripts/android-perf.sh --capture-only"
log "To view live: adb logcat -s DOTNET:* Prewarm:* ShellPerf:* ProjectsLoad:*"
