#!/usr/bin/env bash
# Dev test runner: builds the Android APK (installs + launches it if a device
# is connected) and runs the desktop app. Use this instead of plain
# `dotnet run` so both surfaces are validated each cycle (see AGENTS.md).
#
# Usage: ./scripts/dev-run.sh [--skip-android]

set -euo pipefail
cd "$(dirname "$0")/.."

JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home}"
export JAVA_HOME

ANDROID_PROJ=src/design/App.Android/App.Android.csproj
DESKTOP_PROJ=src/design/App.Desktop/App.Desktop.csproj
TFM=net10.0-android
APK=src/design/App.Android/bin/Debug/$TFM/io.angor.app-Signed.apk

if [[ "${1:-}" != "--skip-android" ]]; then
    if adb devices 2>/dev/null | awk 'NR>1 && $2=="device"' | grep -q .; then
        echo "── Android device connected: build + install + launch ──"
        dotnet build "$ANDROID_PROJ" -t:Install -f $TFM -c Debug \
            -p:JavaSdkDirectory="$JAVA_HOME" -p:AndroidAttachDebugger=false
        adb shell monkey -p io.angor.app 1 >/dev/null
        echo "── Android app launched (io.angor.app) ──"
    else
        echo "── No Android device: building APK only ──"
        dotnet build "$ANDROID_PROJ" -t:SignAndroidPackage -f $TFM -c Debug \
            -p:JavaSdkDirectory="$JAVA_HOME"
        echo "── APK: $APK ──"
    fi
fi

echo "── Launching desktop app ──"
exec dotnet run --project "$DESKTOP_PROJ"
