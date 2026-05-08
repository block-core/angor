# Avalonia 12 Upgrade Plan — `src/design/`

Upgrading from Avalonia 11.3.12 to Avalonia 12.0 to unlock 3x Android performance (42→120 FPS scrolling, 4x faster startup, 20x lower idle CPU).

---

## Phase 1: .NET 10 Target Framework Update

**Why:** Avalonia 12 requires .NET 10 for Android/iOS targets. Desktop can use .NET 8+, but for consistency move everything to net10.0.

**Tasks:**
- Update `global.json` to .NET 10 SDK
- Update all csproj files in `src/design/` from `net9.0` → `net10.0`
- Update `src/design/App.Android/App.Android.csproj` from `net9.0-android` → `net10.0-android`
- Update `src/design/App.iOS/App.iOS.csproj` from `net9.0-ios` → `net10.0-ios`
- Update `src/design/App.Desktop/App.Desktop.csproj` from `net9.0` → `net10.0`
- Note: `src/shared/` (net8.0) and `src/webapp/` (net8.0) stay on their current targets — they don't depend on Avalonia 12

**Files:**
- `global.json`
- All `.csproj` files in `src/design/`
- `src/sdk/` projects that target net9.0 → net10.0

---

## Phase 2: Update Package Versions in Directory.Packages.props

**Avalonia core (bump `$(AvaloniaVersion)` property):**
- `Avalonia` → 12.0.0+
- `Avalonia.Themes.Fluent` → 12.0.0+
- `Avalonia.Fonts.Inter` → 12.0.0+
- `Avalonia.Desktop` → 12.0.0+
- `Avalonia.iOS` → 12.0.0+
- `Avalonia.Browser` → 12.0.0+
- `Avalonia.Android` → 12.0.0+
- `Avalonia.Headless` → 12.0.0+
- `Avalonia.Headless.XUnit` → 12.0.0+ (note: now requires xUnit v3)

**ReactiveUI ecosystem:**
- `ReactiveUI` → 23.2.1 (required by ReactiveUI.Avalonia 12.0.1)
- `ReactiveUI.Avalonia` → 12.0.1
- `ReactiveUI.SourceGenerators` → check for compatible version

**Zafiro packages (all → 52.x for Avalonia 12):**
- `Zafiro.Avalonia` → 52.0.3
- `Zafiro.Avalonia.Generators` → 52.x (check latest)
- `Zafiro.Avalonia.Dialogs` → 52.x
- `Zafiro.Avalonia.Icons.Svg` → 52.x (if still used)

**Icon package migration:**
- Zafiro 52.x migrated from `Projektanker.Icons.Avalonia` to `Optris.Icons.Avalonia`
- Either: (a) migrate to Optris alongside Zafiro, or (b) keep Projektanker separately if it gets a v12 release
- Evaluate if `Projektanker.Icons.Avalonia` 9.6.2 (netstandard2.0, depends on Avalonia >= 11.2.8) works with Avalonia 12 as-is — it might since it's netstandard

**Other third-party packages:**
- `Avalonia.Labs.Panels` → check for 12.x release
- `PanAndZoom` → check for 12.x release
- `AsyncImageLoader.Avalonia` → check for 12.x release
- `Svg.Controls.Skia.Avalonia` → needs SkiaSharp 3.0 compatible version
- `AVVI94.Breakpoints.Avalonia` → check compatibility
- `AvaloniaUI.DiagnosticsSupport` → may be removed (Avalonia 12 dropped `Avalonia.Diagnostics`)

**Files:**
- `src/Directory.Packages.props`

---

## Phase 3: Android Initialization Changes

**Why:** Avalonia 12 changed the Android init pattern. `AvaloniaMainActivity<TApp>` is now non-generic `AvaloniaMainActivity`, and a new `AvaloniaAndroidApplication<TApp>` class is required.

**Tasks:**
- Update `MainActivity.cs`: change base class from `AvaloniaMainActivity<App>` to `AvaloniaMainActivity`
- Add/update `MainApplication.cs`: inherit from `AvaloniaAndroidApplication<App>`
- Review `OnCreate` and `OnBackPressed` overrides for API changes

**Files:**
- `src/design/App.Android/MainActivity.cs`
- `src/design/App.Android/MainApplication.cs` (may need to create)

---

## Phase 4: API Breaking Changes

### 4a. DataValidationErrors
- `DataValidationErrors` attached properties moved from a standalone class to the base `Control` class
- Found in: `NumericUpDown.axaml` (lines 22, 54), `CalendarPicker.axaml` (lines 10, 14)
- Fix: update XAML references per the migration guide

### 4b. Clipboard API
- `IClipboard` / `IDataObject` replaced with `IAsyncDataTransfer`
- Audit `ClipboardHelper` and any `TopLevel.Clipboard` usage

### 4c. Compiled Bindings
- Already enabled (`AvaloniaUseCompiledBindingsByDefault=true`) — no change needed
- But v12 makes this the default, so check for any bindings that fail to compile

### 4d. Gesture Events
- `Gestures.PointerPressed` etc. moved to `InputElement`
- Search for `Gestures.` usage and update

### 4e. Focus Events
- `GotFocusEventArgs` → `FocusChangedEventArgs`
- Search for any handlers using `GotFocusEventArgs`

### 4f. Avalonia.Diagnostics Removal
- `Avalonia.Diagnostics` package removed from Avalonia 12
- Currently using `AvaloniaUI.DiagnosticsSupport` — check if this still works or needs replacement
- May need to conditionally include diagnostics only in debug builds

**Files:**
- Various AXAML and code-behind files (identified by build errors after package update)

---

## Phase 5: xUnit v3 Migration (Test Projects)

**Why:** `Avalonia.Headless.XUnit` 12.x requires xUnit v3.

**Tasks:**
- Update `xunit` from 2.9.2 → 3.x
- Update `xunit.runner.visualstudio` to v3-compatible runner
- Update test syntax if needed (xUnit v3 has minor API changes)
- Update `FluentAssertions` if needed for xUnit v3 compatibility

**Files:**
- `src/design/App.Test.Integration/App.Test.Integration.csproj`
- Test files if API changes are needed

---

## Phase 6: Build, Fix, Verify

**Tasks:**
1. `dotnet restore src/App.sln`
2. `dotnet build src/App.sln` — fix all build errors iteratively
3. `dotnet test` — run all test projects
4. Deploy to Android device and verify:
   - Startup time improvement
   - Scroll smoothness (the main goal)
   - Touch interactions
   - No visual regressions
5. Deploy to Desktop and verify no regressions

---

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Projektanker.Icons.Avalonia no v12 release | Medium | May work as-is (netstandard2.0); or migrate to Optris with Zafiro |
| Third-party packages without v12 support | Medium | Check each before starting; fork if needed |
| SkiaSharp 3.0 breaking changes in SVG rendering | Low | `Svg.Controls.Skia.Avalonia` may need update |
| xUnit v3 test migration effort | Low | Mostly mechanical changes |
| .NET 10 SDK availability | Low | Already available as of early 2026 |
| `src/shared/` and `src/sdk/` compatibility | Low | These stay on net8.0/net9.0; only src/design/ moves to net10.0 |

## Expected Outcome

- **Scrolling**: 42 → 120 FPS on Android (the "tiny jumps" issue resolved)
- **Startup**: ~4x faster on Android with NativeAOT
- **Idle CPU**: 20x reduction (better battery life)
- **Rendering**: up to 1,867% improvement in complex scenes
- **Built-in navigation**: potential future simplification of shell/section navigation
