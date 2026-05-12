# Android Emulator Integration Testing Plan

Use [docker-android](https://github.com/HQarroum/docker-android) to run the Angor app in a containerized Android emulator and validate it against the local test signet stack.

## Prerequisites

- **KVM required**: The docker-android image needs `/dev/kvm` for hardware acceleration. Docker Desktop on Windows does not expose KVM natively. Use WSL2 with nested virtualization, a Linux VM, or a Linux CI runner.
- **x86_64 APK**: Debug builds target x86_64 by default, which matches the emulator architecture.

## Phase 1 — Enable KVM in WSL2

1. Update WSL: `wsl --update`
2. Ensure Intel VT-x / AMD-V is enabled in BIOS.
3. Add to `%USERPROFILE%\.wslconfig` if needed:
   ```ini
   [wsl2]
   nestedVirtualization=true
   ```
4. Verify inside WSL2:
   ```bash
   ls -la /dev/kvm
   docker run --rm --device /dev/kvm alpine ls -la /dev/kvm
   ```

## Phase 2 — Run the Android Emulator Container

Pull the pre-built image and start the emulator:

```bash
docker pull halimqarroum/docker-android:api-33

docker run -d --name android-emu \
  --device /dev/kvm \
  -p 5555:5555 \
  -e MEMORY=4096 \
  -e CORES=2 \
  -e DISABLE_ANIMATION=true \
  halimqarroum/docker-android:api-33
```

Connect ADB and verify:

```bash
adb connect 127.0.0.1:5555
adb devices
```

## Phase 3 — Build and Deploy the Angor App

Build the APK:

```bash
dotnet build src/design/App.Android/App.Android.csproj \
  -f net9.0-android -c Debug
```

Install and launch on the emulator:

```bash
adb install <path-to-apk>/io.angor.app.apk
adb shell monkey -p io.angor.app 1
```

Optionally use [scrcpy](https://github.com/Genymobile/scrcpy) to observe the UI remotely.

## Phase 4 — Connect Emulator to the Test Signet Stack

The existing `docker-compose.yml` creates the `angor-test-net` network. Connect the emulator container so the app can reach signet-node, mempool, relays, and faucet:

```bash
docker network connect angor-test-net android-emu
```

Configure the app inside the emulator to point at the docker service hostnames/ports (see `docker/README.md` for the endpoint map).

## Phase 5 — Add Android Emulator to Docker Compose

Add as a service in `src/design/App.Test.Integration/docker/docker-compose.yml`:

```yaml
android-emulator:
  image: halimqarroum/docker-android:api-33
  devices:
    - /dev/kvm
  ports:
    - "5555:5555"
  environment:
    - MEMORY=4096
    - CORES=2
    - DISABLE_ANIMATION=true
  networks:
    - angor-test-net
```

Add a helper script that:
1. Waits for the emulator to finish booting (`adb wait-for-device && adb shell getprop sys.boot_completed`).
2. Installs the APK.
3. Launches the app.

## Phase 6 — Automated Test Execution (Future)

Choose a test framework for driving the Android UI:

| Option | Pros | Cons |
|--------|------|------|
| **Appium + .NET** | C# tests, same language as codebase, rich UI assertions | Extra infrastructure (Appium server) |
| **ADB shell scripting** | Lightweight, no extra deps | Limited to basic smoke checks |
| **Separate xUnit suite** | Familiar test runner | Need Appium or similar driver underneath |

Initial goal: deploy APK, verify app starts, check basic navigation works.

## Risks & Considerations

- **KVM in WSL2** is not guaranteed — depends on hardware, Windows version, and WSL kernel version. Fall back to a Linux VM if unavailable.
- **Performance** — emulator in Docker in WSL2 adds two virtualization layers; expect slowness.
- **CI integration** — standard GitHub Actions `ubuntu-latest` runners lack KVM. Requires self-hosted runners or the `reactivecircus/android-emulator-runner` action (macOS runners with HAXM).
- **Google Play Services** — if the app needs them, use `IMG_TYPE=google_apis_playstore` when building the docker-android image and provide matching ADB keys in the `keys/` directory.
