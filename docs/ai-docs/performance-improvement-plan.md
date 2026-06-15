# Performance Improvement Plan — `src/design/App`

Investigation identified six categories of issues causing UI sluggishness, especially on mobile (Android/iOS). Each fix is scoped as its own PR.

---

## PR 1: Virtualize FundersView signature list

**Priority:** Critical
**Impact:** Largest single improvement for mobile scroll performance

The FundersView renders all signature cards at once using `ItemsControl` with a `StackPanel` — no virtualization, no pagination. Each card template is ~260 lines of XAML with multiple Buttons, Viewboxes, Paths, Icons, and nested panels.

**Plan:**
- Replace `ScrollableView > StackPanel > ItemsControl` with a `DockPanel` layout
- Header + filter tabs go in `DockPanel.Dock="Top"` (fixed, don't scroll)
- Replace `ItemsControl` with `ListBox` + `VirtualizingStackPanel` that owns its own scroll viewport
- Style `ListBox` to remove selection chrome (transparent selection brush, no focus adorner)
- Make expand/collapse binding-driven (`IsExpanded` property on `SignatureRequestViewModel`) instead of code-behind visual tree walking — virtualized containers get recycled, so imperative state is lost on scroll
- Adjust responsive padding logic in code-behind to target the new layout

**Files:**
- `App/UI/Sections/Funders/FundersView.axaml`
- `App/UI/Sections/Funders/FundersView.axaml.cs`
- `App/UI/Sections/Funders/SignatureRequestViewModel.cs` (add `IsExpanded` reactive property)

**Note:** Grid views (FindProjects, Portfolio, MyProjects) already paginate at 4 items per batch on mobile, so virtualization there is lower priority.

---

## PR 2: Remove transitions on list item templates

**Priority:** High
**Impact:** Eliminates per-frame composition overhead during touch scrolling

`TransformOperationsTransition` and `BoxShadowsTransition` on card `Border` elements inside DataTemplates fire on pointer-over during scrolling, causing GPU work every frame.

**Plan:**
- Remove `TransformOperationsTransition` and `BoxShadowsTransition` from card borders in:
  - `PortfolioView.axaml` (lines 363–368)
  - `FundersView.axaml` (lines 333–337)
- Optionally replace with a lightweight opacity change for hover feedback
- FundersView already strips transitions on mobile via a `.Mobile` style class — extend that pattern to PortfolioView

**Files:**
- `App/UI/Sections/Portfolio/PortfolioView.axaml`
- `App/UI/Sections/Funders/FundersView.axaml`

---

## PR 3: Fix blocking `.GetAwaiter().GetResult()` in ShellViewModel

**Priority:** High
**Impact:** Eliminates UI freeze at startup

`ShellViewModel.cs` line 200 calls `_store.Load<PrototypeSettingsData>(SettingsKey).GetAwaiter().GetResult()` synchronously in the constructor. If `IStore.Load` hits disk I/O, this blocks the UI thread and causes visible startup lag (potential ANR on Android).

**Plan:**
- Make settings loading async — either via a separate async init method or `Observable.StartAsync`
- Ensure the UI renders a default/loading state until settings are available

**Files:**
- `App/UI/Shell/ShellViewModel.cs`

---

## PR 4: Add `IDisposable` + `CompositeDisposable` to ViewModels

**Priority:** High
**Impact:** Prevents subscription accumulation over time as users navigate between sections

Only `FundersViewModel` implements `IDisposable` with proper `CompositeDisposable`. Nine other ViewModels with active subscriptions never dispose them, causing leaked handlers that accumulate on navigation.

**Plan:**
- Add `IDisposable` with `CompositeDisposable` to:
  - `ShellViewModel`
  - `FindProjectsViewModel`
  - `PortfolioViewModel`
  - `MyProjectsViewModel`
  - `SettingsViewModel`
  - `PaymentFlowViewModel`
  - `InvestPageViewModel`
  - `FundsViewModel`
  - `SendFundsModalViewModel`
- Wire existing `WhenAnyValue`/`Subscribe` calls with `.DisposeWith(disposables)`
- Unsubscribe event handlers (e.g., `SignatureStatusChanged`, `CollectionChanged`) in `Dispose()`

**Files:**
- All ViewModels listed above

---

## PR 5: Add decode size limiting to image loading

**Priority:** Medium
**Impact:** Reduces memory usage and decode time, especially for banner images on mobile

`ImageCacheService.cs` decodes images at full resolution (`new Bitmap(fs)` / `new Bitmap(ms)`) without specifying `DecodePixelWidth`/`DecodePixelHeight`. On mobile, large banner images consume excessive memory and slow down rendering.

**Plan:**
- Add `DecodePixelWidth`/`DecodePixelHeight` parameters when creating `Bitmap` instances
- Use appropriate sizes: e.g., 640px for banners, 128px for avatars
- Consider making decode size configurable per call site

**Files:**
- `App/UI/Shared/Helpers/ImageCacheService.cs`

---

## PR 6: Add throttle/debounce to collection change handlers

**Priority:** Medium
**Impact:** Prevents redundant work when collections update rapidly

Several handlers fire on every individual collection mutation without debouncing:

- `FindProjectsViewModel.cs:429` — `CollectionChanged += (_, _) => UpdateHasInvestedFlags()` fires on every Add/Remove/Reset
- `PortfolioViewModel.cs:521` — `WalletsUpdated.Subscribe` triggers full `LoadInvestmentsFromSdkAsync()` reload with no throttle
- `ShellView.axaml.cs:202-257` — multiple `WhenAnyValue` + `CombineLatest` chains trigger layout recalculations without debounce

**Plan:**
- Convert event handlers to reactive streams with `.Throttle()` or `.Debounce()`
- For `CollectionChanged`: use `Observable.FromEventPattern` + `.Throttle(TimeSpan.FromMilliseconds(250))`
- For `WalletsUpdated`: add `.Throttle(TimeSpan.FromMilliseconds(500))` before triggering reload
- Review `ShellView.axaml.cs` subscriptions for missing throttle on layout-affecting chains

**Files:**
- `App/UI/Sections/FindProjects/FindProjectsViewModel.cs`
- `App/UI/Sections/Portfolio/PortfolioViewModel.cs`
- `App/UI/Shell/ShellView.axaml.cs`
