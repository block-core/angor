# Avalonia 12 Upgrade Plan — `src/design/`

Upgrading from Avalonia 11.3.12 to Avalonia 12.0.2 to unlock 3x Android performance (42→120 FPS scrolling, 4x faster startup, 20x lower idle CPU).

**Target version:** 12.0.2 (released April 28, 2026) — three patch releases in one month with ~208K combined downloads. Includes important Android-specific fixes (crash on surface destroyed, text selection, back button for API 33+). Production-ready.

**Scope:** `src/design/` and its dependencies (`src/sdk/`, `src/shared/`). The `src/avalonia/` project is explicitly excluded — it has its own Zafiro dependencies and will be handled separately if needed.

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

**Avalonia core (bump `$(AvaloniaVersion)` property to 12.0.2):**
- `Avalonia` → 12.0.2
- `Avalonia.Themes.Fluent` → 12.0.2
- `Avalonia.Fonts.Inter` → 12.0.2
- `Avalonia.Desktop` → 12.0.2
- `Avalonia.iOS` → 12.0.2
- `Avalonia.Browser` → 12.0.2
- `Avalonia.Android` → 12.0.2
- `Avalonia.Headless` → 12.0.2
- `Avalonia.Headless.XUnit` → 12.0.2 (note: now requires xUnit v3)

**ReactiveUI ecosystem:**
- `ReactiveUI` → 23.2.1 (required by ReactiveUI.Avalonia 12.0.1)
- `ReactiveUI.Avalonia` → 12.0.1
- `ReactiveUI.SourceGenerators` → check for compatible version

**Projektanker.Icons.Avalonia:**
- Currently on 9.6.2 (netstandard2.0, depends on Avalonia >= 11.2.8)
- Used directly in `src/design/` for FontAwesome icons throughout the UI
- Evaluate if it works with Avalonia 12 as-is — likely compatible since it targets netstandard2.0
- If incompatible, check for a v12-compatible release or consider alternative icon solutions

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
| Projektanker.Icons.Avalonia no v12 release | Medium | May work as-is (netstandard2.0); check for updated release if not |
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

---

## Phase 7: Post-Upgrade Runtime Issues (DISCOVERED DURING TESTING)

After completing Phases 1-5, the app builds cleanly and launches but the desktop window renders nothing visible (transparent/black with no UI controls). Window chrome appears, title bar correct, but the entire ShellView's content area is empty.

### 7a. Root cause RESOLVED — `Svg.Controls.Skia.Avalonia` 11.x incompatible with Av12

**Symptom:** Entire `ShellView` rendered as transparent. No exceptions. No log warnings (Avalonia logs at `Warning` level over Visual/Layout/Control/Binding areas were clean).

**Investigation:** Bisect-by-stripping. Replaced `MainWindow` content with `<Border Background="HotPink"/>` → rendered. Therefore bug is inside `ShellView`. Stripped `ShellView` to a Panel + Grid skeleton with HotPink/DeepPink/White borders, then added pieces back one row/column at a time. Sidebar fine. ModalOverlay/ToastOverlay (incl. `<i:Icon>`, ModalBackdrop, ToastBackground, ToastShadow) all fine. ContentControl with bound HomeView — fine. **Adding the desktop header back → transparent.** Stripped header to `<TextBlock>` only → still transparent. Replaced DockPanel with `<Border>+<TextBlock>` → still transparent. **Removed the `<Svg Path="/Assets/logo.svg"/>` → rendered correctly.**

**Root cause:** `Svg.Controls.Skia.Avalonia` 11.3.6.2 references `Avalonia.Base 11.3.6.0` / `Avalonia.Controls 11.3.6.0`. In Av12, the package's internal `<Svg>` control template silently fails to apply, and the failure cascades up the visual tree, making the entire containing UserControl render zero pixels — with no exception or log warning.

**Fix:** Bump `Svg.Controls.Skia.Avalonia` from `11.3.6.2` → `12.0.0.6` in `src/Directory.Packages.props`. Done.

### 7b. ReactiveUI 23.x requires explicit initialization

**Symptom:** App crashes on first `WhenAnyValue` call with:
```
System.InvalidOperationException: ReactiveUI has not been initialized.
You must initialize ReactiveUI using the builder pattern.
```

**Root cause:** ReactiveUI 23.x removed implicit auto-init. Must be wired into the AppBuilder explicitly.

**Fix:** Add `.UseReactiveUI(b => b.WithAvalonia())` to `Program.cs:BuildAvaloniaApp()`. Requires `using ReactiveUI.Avalonia;`. Done.

### 7c. Pattern: silent transparent rendering = check third-party Av11 packages

Two third-party packages caused the same class of bug during the upgrade:
1. `Projektanker.Icons.Avalonia` 9.x — fixed by switching to `Optris.Icons.Avalonia` 12.0.6 (the same package republished under a new owner with Av12 support)
2. `Svg.Controls.Skia.Avalonia` 11.3.6.2 — fixed by bumping to 12.0.0.6

Both packages had Av11-only internal control templates that broke under Av12 with no exception, no log warning, and produced silent-transparent rendering of the entire enclosing visual tree. **Lesson for future Avalonia majors: when faced with silent transparent rendering and clean logs, audit all third-party control packages first** — pick out anything whose dll references `Avalonia.Base v<old>.0.0` and look for a major-version bump on NuGet.

### 7d. Items investigated but NOT confirmed as causing the rendering bug

These were examined during diagnosis. Some were tested and ruled out as the cause of the transparent-rendering bug — but the underlying Av12 issues are still real and should still be fixed for performance/correctness reasons unrelated to rendering.

#### `VisualBrush` regression — [AvaloniaUI/Avalonia#20515](https://github.com/AvaloniaUI/Avalonia/issues/20515)

`VisualBrush` with `TileMode="Tile"` + `DestinationRect` regressed in Avalonia 11.3.10 and is **still broken in 12.x** (open `bug` + `regression` label). Affects `TiledLogoBrush` in `Colors.Core.axaml:156` and `:314`.

**Tested:** Neutralizing `TiledLogoBrush` (VisualBrush → SolidColorBrush) in isolation did NOT fix the transparency bug — so this is not the cause of Phase 7's primary symptom. But the brush still won't render its tiled pattern correctly under Av12. **TODO:** Replace with `ImageBrush` pointing at a pre-rendered PNG, or a `DrawingBrush` (subject to leak below).

#### `DrawingBrush` memory leak — [#21049](https://github.com/AvaloniaUI/Avalonia/issues/21049)

`AppTextureBrush` (`Colors.Core.axaml:443`) is a `DrawingBrush` used as a full-screen overlay. **Tested:** Neutralizing it (DrawingBrush → SolidColorBrush) did NOT fix the transparency bug. But it still leaks memory in Av12. **TODO:** Replace with a tiled `ImageBrush` pointing at a pre-baked 4x4 PNG.

#### `IActivityApplicationLifetime` for Android

`App.axaml.cs:69` only checks for `ISingleViewApplicationLifetime`. Av12 Android uses the new `IActivityApplicationLifetime` with a `MainViewFactory` property. Not related to the desktop rendering bug, but required for Android to launch correctly:

```csharp
else if (ApplicationLifetime is IActivityApplicationLifetime activity)
    activity.MainViewFactory = () => new ShellView();
```

#### Compiled bindings now default

Av12 sets `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` as the new default. Every `{Binding}` without an `x:DataType` ancestor will silently fail to compile. **Tested:** This was NOT the cause of the transparent-rendering bug (the bound `ContentControl` to `CurrentSectionContent` worked fine in our stub). But it's still worth auditing `.axaml` files for `{Binding}` without `x:DataType` to catch any silently-broken bindings the user might encounter elsewhere.

#### 932 `Color`-as-`Brush` usages

Audit was a non-issue. Av12 docs don't list it as breaking. Stylistic only.

### Remaining Phase 7 work

1. ~~Bump `Svg.Controls.Skia.Avalonia` to 12.0.0.6~~ ✅ done
2. ~~Wire `UseReactiveUI` in `Program.cs`~~ ✅ done
3. ~~Add `IActivityApplicationLifetime` branch to `App.axaml.cs:69` for Android~~ ✅ done
4. ~~Replace `TiledLogoBrush` (#20515 workaround) — pre-baked `ImageBrush` of `/Assets/logo-tile-80x86.png` (160x172 @ 2x for HiDPI)~~ ✅ done
5. ~~Replace `AppTextureBrush` (#21049 leak) — pre-baked `ImageBrush` of `/Assets/app-texture-4x4.png` (16x16 @ 4x for HiDPI)~~ ✅ done
6. Optional audit: `{Binding}` usages without `x:DataType`
7. Install Android workload for .NET 10, deploy to device, measure FPS
