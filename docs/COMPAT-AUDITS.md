# Multi-version compatibility audits (Revit 2020–2027)

One section per tool, newest first. Each audit checks every Revit API type, method,
property, and enum the tool touches against the **real per-version RevitAPI /
RevitAPIUI reference assemblies** (Nice3point.Revit.Api NuGet packages — the same
ones the build uses), not just documentation. Method presence *and* full parameter
signatures are decoded from assembly metadata.

Verification method: `dnfile`-based ECMA-335 metadata audit script run against
RevitAPI.dll / RevitAPIUI.dll for all eight versions (2020.2.60, 2021.1.50,
2022.1.80, 2023.1.90, 2024.3.40, 2025.4.50, 2026.4.10, 2027.1.0).

---

## Colorize — audited 2026-07-23 — RESULT: fully compatible, no code changes needed

**Files covered (complete tool inventory):**
`CmdColorize.cs`, `CmdColorizeAvailability.cs`; Services/Colorize: `ColorizeApplier.cs`,
`ColorizeElementMatcher.cs`; UI: `ColorizeWindow.xaml` + code-behind; ribbon wiring in
`RibbonManager.AddColorizeTool()`. Shared dependencies exercised by Colorize and read as part of
this audit (not modified): `FilterProDataProvider`, `FilterApplier.GetSolidFillId`,
`FilterRuleCompat`, `ColorPalette`, `GraphicsElementService.ApplyOverrides`,
`GraphicsCommandService.ExecuteSummaryTransaction`, `GraphicsOperationSummary` (plain model).

**API surface:** Colorize reuses almost the entire Filter Pro surface verified in the section
below (rule factories via FilterRuleCompat, OverrideGraphicSettings setters, category/parameter
utilities, TaskDialog, ViewType/StorageType enums). Members unique to Colorize, verified present
with identical signatures in **all 8 versions (2020–2027)**:

- `FilteredElementCollector(Document, ElementId)` view-scoped constructor (2027 adds an extra
  3-arg overload; the 2-arg one is unchanged) and `FilteredElementCollector.ToElementIds()`
- `View.SetElementOverrides(ElementId, OverrideGraphicSettings)`
- `Transaction.RollBack`, `Document.ActiveView`
- `Autodesk.Revit.Exceptions.OperationCanceledException` (type)
- RevitAPIUI: `UIApplication.MainWindowHandle` (used with WPF `WindowInteropHelper` to parent the
  window to Revit — `System.Windows.Interop` is standard WPF on all four target frameworks)

**Version boundaries crossed:** the same three as Filter Pro (string rule factory 2023/2026,
`ElementId.IntegerValue` removed 2026, `ElementId(int)` removed 2026) — all already handled by
`FilterRuleCompat` / `ElementIdHelper` / `ElementIdExtensions`. Colorize contains no direct calls
to any removed member.

**Source changes needed: none** (its headers, written 2026-07-13, contain no stale compatibility
claims, unlike the older Filter Pro headers corrected in this same audit branch).

**Build verification:** same limitation as Filter Pro below — assembly-metadata verification only;
a real 8-configuration build still needs `build-all.ps1` on the local machine.

---

## Filter Pro — audited 2026-07-23 — RESULT: fully compatible, no code changes needed

**Files covered (complete tool inventory):**
`CmdFilterPro.cs`, `CmdFilterProAvailability.cs`; Services/FilterPro: `FilterApplier.cs`,
`FilterCreator.cs`, `FilterProDataProvider.cs`, `FilterProHelper.cs`, `FilterProStateTracker.cs`,
`FilterReorderer.cs`, `FilterValueKeyMatcher.cs`; Models: `FilterProState.cs`, `FilterSelection.cs`,
`FilterValueItem.cs`, `FilterParameterItem.cs`, `FilterCategoryItem.cs`, `FilterValueKey.cs`,
`SpecialParameterIds.cs`, `RuleTypes.cs`, `ColorPalette.cs`, `ApplyViewItem.cs`, `PatternItem.cs`;
UI: `FilterProWindow.xaml` + code-behind; ribbon wiring in `RibbonManager.AddFilterProTool()`.

**Verified unchanged across all 8 versions (2020–2027):**

- `ParameterFilterRuleFactory`: int, double+tolerance, and ElementId overloads of
  Equals/NotEquals/Greater/GreaterOrEqual/Less/LessOrEqual, plus
  `CreateHasValueParameterRule` / `CreateHasNoValueParameterRule` — identical signatures in all 8.
- `OverrideGraphicSettings`: all setters/getters the tool uses (projection/cut line colour,
  surface/cut foreground pattern id + colour, halftone).
- `View`: `GetFilters`, `AddFilter`, `RemoveFilter`, `GetFilterOverrides`, `SetFilterOverrides`,
  `GetFilterVisibility`, `SetFilterVisibility`, `AreGraphicsOverridesAllowed`, `IsTemplate`,
  `ViewTemplateId`, `ViewType`.
- `ParameterFilterElement.Create` (both overloads), `SetCategories`, `SetElementFilter`.
- `ParameterFilterUtilities.GetAllFilterableCategories` / `GetFilterableParametersInCommon`.
- `LabelUtils.GetLabelFor(BuiltInParameter)` (the legacy enum overloads that were removed in
  2021+/2022+ are not used by this tool).
- `ElementId(BuiltInParameter)` constructor (used for ALL_MODEL_FAMILY_NAME / ALL_MODEL_TYPE_NAME).
- Enums: all used values of `ViewType` (incl. Internal/ProjectBrowser/SystemBrowser),
  `BuiltInParameter` (4 used values), `StorageType`.
- RevitAPIUI: `TaskDialog.Show` (2- and 3-arg), `IExternalCommand`, `IExternalCommandAvailability`,
  `UIDocument.ActiveView/Document`, `UIApplication.ActiveUIDocument`,
  `PushButton.AvailabilityClassName`, `TaskDialogResult/CommonButtons`,
  `TransactionMode.Manual` / `RegenerationOption.Manual`.

**Version boundaries that DO affect this tool — all already handled by existing helpers:**

| API change | Real boundary (verified in assemblies) | Handled by |
|---|---|---|
| String rule factory 3-arg (caseSensitive) overloads | Present 2020–2025, REMOVED 2026; 2-arg added 2023 | `FilterRuleCompat` (switches at `REVIT2023_OR_GREATER`) |
| `ElementId.IntegerValue` | Present 2020–2025, REMOVED 2026 | `ElementIdHelper` / `ElementIdExtensions.IntValue()` (switch to `.Value` at 2024) |
| `ElementId(int)` constructor | Present 2020–2025, REMOVED 2026 (`long` ctor added 2024) | `ElementIdHelper.FromInt` (switches at 2024); only caller is `SpecialParameterIds` |
| `BuiltInParameter` enum widened Int32→Int64 (2024) | 2024+ | `ElementIdHelper.IsDefinedBuiltInParameter` (reflection-based, no #if needed) |

Note: earlier notes elsewhere in the repo said `ElementId(int)` was "removed by 2027" — the
reference assemblies show it was already gone in 2026. Harmless either way, because the helper
switches to the `long` constructor at 2024.

**XAML / UI:** plain WPF only (Window, TabControl, ListBox with virtualization, styles from
`ModernStyles.xaml`) — identical API surface on .NET Fx 4.7.2/4.8, .NET 8, and .NET 10. No
version-specific XAML needed.

**Project wiring:** the tool has no project-file machinery of its own; it is compiled by the single
shared `src/AJ Tools.csproj`, which takes `RevitVersion` / `TargetFramework` / `REVITxxxx`
constants entirely from `Directory.Build.props`. No duplication, no conflict.

**Improvement opportunity (NOT applied — behaviour must stay identical on 2020):**
Revit 2021 added `View.GetOrderedFilters()` and `Get/SetIsFilterEnabled()`. On 2021+,
`FilterReorderer` could read the true filter order instead of maintaining its per-view order cache
(`_lastKnownOrderByView`). Not applied because the current remove-and-re-add approach works on all
8 versions, and using the new API only on 2021+ would make behaviour differ between versions.

**Known accepted limitation (all versions, unchanged):** ElementId values are compared/dedup-keyed
as `int` via `IntValue()`. Revit 2024+ ids are 64-bit; the repo-wide documented decision (see
`ElementIdHelper.cs`, `FilterValueKey.cs`) is that real project ids stay well within int range.

**Build verification:** not possible in the cloud session that produced this audit — the sandbox
has no .NET SDK and its network policy blocks the SDK download (`builds.dotnet.microsoft.com`
denied). Compatibility was instead verified at assembly-metadata level as described above. A real
8-configuration build (`build-all.ps1`) still needs to be run on the local machine.
