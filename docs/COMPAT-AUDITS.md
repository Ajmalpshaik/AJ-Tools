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

## MEP Openings — audited 2026-07-23 — RESULT: fully compatible; two inline version branches moved into helpers

**Files covered (complete tool inventory):** `CmdCreateMepOpenings.cs`, `CmdMepOpeningSettings.cs`;
Services/MepOpenings: `MepOpeningService.cs` (~132 KB, the suite's largest service),
`MepOpeningSelectionFilter.cs`, `MepOpeningHostSelectionFilter.cs`,
`MepOpeningSourceSelectionFilter.cs`, `MepOpeningSettingsService.cs`; all 9 Models/MepOpenings
classes; UI: `MepOpeningSettingsWindow.xaml` + code-behind.

**Verified unchanged across all 8 versions (2020–2027):** all three
`doc.Create.NewOpening` overloads the tool uses — `(Wall, XYZ, XYZ)`, `(Element, CurveArray, bool)`,
`(Element, CurveArray, eRefFace)` with `eRefFace.CenterX/Y/Z`; `FamilySymbol.Activate/IsActive`;
`CurveArray`, `Arc.Create`, `Line.CreateBound`; `StructuralType.NonStructural`;
`Element.UniqueId`; all category/parameter enum values used (incl. `OST_CableTrayRun`,
`OST_CableTrayFitting`, `OST_Conduit`, `SCHEDULE_LEVEL_PARAM`, `INSTANCE_REFERENCE_LEVEL_PARAM`,
`FAMILY_LEVEL_PARAM`, `RBS_REFERENCE_INSULATION_THICKNESS`); linked-model handling
(`GetLinkDocument`, `GetTotalTransform` via `Instance`).

**Notable verified subtlety:** `doc.Create.NewFamilyInstance(XYZ, FamilySymbol, Level,
StructuralType)` — the overload MOVED from `Creation.Document` (2020–2023) to the base class
`Creation.ItemFactoryBase` (2024+). Call sites are unaffected (C# resolves it through inheritance
either way), but a naive single-type API diff would wrongly flag it as removed. Verified present in
every version on one of the two types.

**Also confirmed in the assemblies (suite-wide fact):** `DisplayUnitType` exists only in 2020–2021
and is REMOVED from 2022; `UnitTypeId` exists from 2021. Both `RevitCompat`'s 2022 switch and this
tool's former 2021 switch were valid; they produce identical values.

**Source changes (the first real code changes of the audit series — convention cleanups, identical
behaviour):**
- `MepOpeningSelectionFilter.IsCategory`: inline `#if REVIT2024...` ElementId branch replaced with
  the shared `ElementIdExtensions.LongValue()` helper.
- `MepOpeningService.MmToInternal`: inline `#if REVIT2021...` unit branch replaced with
  `RevitCompat.MmToInternal`.
Both previously compiled correctly on all 8 versions — the change removes duplicated version logic
from tool files per the project rule ("version-specific code lives ONLY inside helpers"); no
behaviour difference on any version.

**Build verification:** same limitation as the other audits — assembly-metadata verification only;
a real 8-configuration build still needs `build-all.ps1` on the local machine.

---

## Auto Dimensions — audited 2026-07-23 — RESULT: fully compatible, no code changes needed

**Files covered (complete tool inventory):** `CmdAutoDimensions.cs`;
`Services/AutoDimension/AutoDimensionService.cs`. UI is TaskDialog-based — no window.

**Verified unchanged across all 8 versions (2020–2027):**
`doc.Create.NewDimension(View, Line, ReferenceArray)` (defined on `Creation.ItemFactoryBase` —
present with identical signature in all 8), `Line.CreateBound`, `ReferenceArray` + `Append`,
`new Reference(Element)` for grid/level references, `Grid`/`Level` types,
`DatumPlane.GetCurvesInView(DatumExtentType, View)` with `DatumExtentType.ViewSpecific/Model`,
`ViewType.EngineeringPlan` (other view types verified in earlier audits), view direction/crop
properties, `Transaction`, `TaskDialog.Show`. No version boundary is crossed anywhere in this tool —
no ElementId numeric access, no unit API, no string filter rules, no tagging API.

**Source changes: none** — headers contain no stale compatibility claims.

**Build verification:** same limitation as the other audits — assembly-metadata verification only;
a real 8-configuration build still needs `build-all.ps1` on the local machine.

---

## Duct Standards Manager — audited 2026-07-23 — RESULT: fully compatible, no code changes needed

**Files covered (complete tool inventory):** `CmdDuctStandardsManager.cs`; Services/DuctStandards:
`DuctCollectorService.cs`, `DuctParameterWriter.cs`, `DuctRuleEngine.cs`, `DuctShapeService.cs`,
`DuctSizeService.cs`, `DuctStandardsConfigService.cs`, `DuctStandardsProcessor.cs`,
`DuctWeightService.cs`; Models: `DuctStandardsConfig.cs`; UI: `DuctStandardsManagerWindow.xaml` +
code-behind. Helpers read (unchanged): `RevitCompat`, `SharedParamUtils`, `TransactionHelper`.

**The one real version boundary this tool crosses — the 2022 ForgeTypeId transition — is entirely
routed through the existing helpers:** parameter spec/group tokens come from
`RevitCompat.SpecText/SpecNumber/GroupData`, definition creation from
`RevitCompat.CreateDefinitionOptions`, binding insert/re-insert from
`RevitCompat.InsertBinding/ReInsertBinding`, and labels from `SharedParamUtils`. The window and
`SharedParamUtils` use the project's guarded `AjSpec`/`AjGroup` alias pattern
(`#if REVIT2022_OR_GREATER → ForgeTypeId`, else legacy enums) — no unguarded `ForgeTypeId` anywhere.

**Verified unchanged across all 8 versions (2020–2027):** the whole shared-parameter workflow —
`Application.SharedParametersFilename` get/set, `OpenSharedParameterFile`,
`Creation.Application.NewInstanceBinding/NewCategorySet`, `Document.ParameterBindings` +
`ForwardIterator` (inherited from `DefinitionBindingMap` — present in all 8),
`DefinitionFile.Groups` / `DefinitionGroups.get_Item/Create` / `DefinitionGroup.Definitions` /
`Definitions.Create`, `CategorySet.Insert`, `Category.AllowsBoundParameters`,
`Settings.Categories.get_Item(BuiltInCategory)`; plus `Parameter.Set(double/int/string)`,
`FilteredElementCollector.FirstElement/OfCategory`, `BuiltInParameter.RBS_CURVE_HEIGHT_PARAM`
(width/diameter already verified in the Smart MEP Tag audit), and the FamilyParameter members
`SharedParamUtils` exposes (`GUID`/`IsShared`/`IsReporting`, `FamilyManager.Types`).

**UI:** plain WPF window (same ModernStyles pattern as Filter Pro) — identical on all four target
frameworks. Config persistence is JSON via Newtonsoft (framework-independent).

**Source changes: none** — headers contain no stale compatibility claims.

**Build verification:** same limitation as the other audits — assembly-metadata verification only;
a real 8-configuration build still needs `build-all.ps1` on the local machine.

---

## Pipe Sizing — audited 2026-07-23 — RESULT: fully compatible, no code changes needed

**Files covered (complete tool inventory):** `CmdPipeSizing.cs`; Services/PipeSizing:
`PipeSizingCalculator.cs`, `PipeSizingCsvExporter.cs`, `PipeSizingData.cs`,
`PipeSizingStateService.cs`; Models/PipeSizing: `PipeSizingResult.cs`, `PipeSizingState.cs`,
`PipeSizeOption.cs`, `PipeFixtureData.cs`; UI: `PipeSizingWindow.xaml` + code-behind.

**API surface:** this tool is a self-contained calculator — the ONLY Revit API it touches is the
thin command wrapper (`IExternalCommand`, `ExternalCommandData`, `ElementSet`, `Result`,
`TaskDialog.Show`, `OperationCanceledException`, `[Transaction(TransactionMode.ReadOnly)]`,
`[Regeneration(RegenerationOption.Manual)]`). All of those were already verified in the earlier
audits except `TransactionMode.ReadOnly`, now verified present in **all 8 versions (2020–2027)**.

**The 2021 units boundary does not apply:** the calculator performs its own metric/imperial math —
no `UnitUtils`, `DisplayUnitType`, or `UnitTypeId` anywhere in the tool. Services, models, and the
window use zero Revit API: plain WPF, `Microsoft.Win32.SaveFileDialog` (CSV export), and JSON state
via Newtonsoft — identical API surface on .NET Fx 4.7.2/4.8, .NET 8, and .NET 10.

**Source changes: none** — headers are accurate.

**Build verification:** same limitation as the other audits — assembly-metadata verification only;
a real 8-configuration build still needs `build-all.ps1` on the local machine.

---

## Ceiling Magnet — audited 2026-07-23 — RESULT: fully compatible, no code changes needed

**Files covered (complete tool inventory):** `CmdCeilingMagnet.cs`;
`Services/CeilingMagnet/CeilingMagnetService.cs`; helper `CeilingGridApiCompat.cs` (read, unchanged).
UI is TaskDialog-based (command links) — no XAML/WinForms window.

**The one real version boundary — `Ceiling.GetCeilingGridLines` — verified in the assemblies, and
`CeilingGridApiCompat`'s gate is exactly right:** the method is absent in 2020–2024 and present in
2025–2027 reference assemblies. The helper compiles the call out below `REVIT2025_OR_GREATER` and
still probes by reflection at runtime on 2025+ (because the method arrived in point release 2025.3 —
an unpatched 2025.0–2025.2 install lacks it even though the reference assembly has it). Falls back
silently to the manual-click path — never crashes.

**Verified unchanged across all 8 versions (2020–2027):** `CeilingType` /
`HostObjAttributes.GetCompoundStructure` / `CompoundStructure.GetLayers` /
`CompoundStructureLayer.MaterialId`; `Material.SurfaceForegroundPatternId`;
`FillPattern.Target/GetFillGrids`, `FillGrid.Offset/Angle`, `FillPatternTarget.Model`;
`Options` (+ `ViewDetailLevel.Fine`), `Element.get_Geometry`, `Solid.Faces`,
`PlanarFace.FaceNormal`, `Face.Project/Area`; `ElementTransformUtils.MoveElement`;
`RevitLinkInstance.GetLinkDocument` + `GetTotalTransform` (inherited from `Instance` — present in
all 8); `Transform.OfPoint/OfVector/Inverse/Origin`; `Reference.ElementId/LinkedElementId`;
Selection API (`PickObject`/`PickObjects`/`PickPoint`/`GetElementIds`, `ObjectType.Element/
PointOnElement/LinkedElement`, `ISelectionFilter`); instance `TaskDialog` with
`AddCommandLink`/`TaskDialogCommandLinkId`/`TaskDialogResult.CommandLink1-2`.
Unit conversion goes through `RevitCompat.MmToInternal/InternalToMm` (the 2021/2022 unit-API
switch lives in that helper).

**Source changes: header notes only.** Both file headers still said the tool "uses UnitUtils with
DisplayUnitType — revisit for 2021+ ForgeTypeId builds", but the code had already been converted to
RevitCompat in an earlier pass — the stale notes were corrected (no code touched).

**Build verification:** same limitation as the other audits — assembly-metadata verification only;
a real 8-configuration build still needs `build-all.ps1` on the local machine.

---

## Smart MEP Tag — audited 2026-07-23 — RESULT: fully compatible, no code changes needed

**Files covered (complete tool inventory):**
`CmdSmartMepTag.cs`, `CmdSmartMepTagSettings.cs` (WinForms settings dialog); Services/SmartTag:
`SmartMepTagService.cs`, `SmartTagPlacementEngine.cs`, `SmartTagReportGenerator.cs`,
`SmartTagSettingsTracker.cs`, `SmartTagTelemetryTracker.cs`, `AnnotationBox.cs` (pure math, no API),
`AnnotationSpatialIndex.cs`; Models: `SmartTagSettingsState.cs`. Shared dependency exercised and
read (not modified): `Services/LeaderLogic/LeaderLogicService.cs`.

**The one real version boundary this tool crosses — IndependentTag's API generations — verified
in the assemblies, and `TagCompat`'s switch point is exactly right:**

| IndependentTag members | 2020 | 2021 | 2022 | 2023–2027 |
|---|---|---|---|---|
| Old single-reference: `LeaderEnd`, `LeaderElbow`, `GetTaggedReference()`, `TaggedLocalElementId` | ✔ | ✔ | ✔ | ✘ removed |
| Multi-reference: `GetTaggedReferences()`, `GetLeaderEnd(ref)`, `SetLeaderElbow(ref, xyz)`, `GetTaggedLocalElements()` | ✘ | ✘ | ✔ (added) | ✔ |

`TagCompat` compiles the old members below `REVIT2023_OR_GREATER` and the new members from 2023 —
both paths only ever touch members that exist in their version range (2022 has both generations,
so either choice is valid there; the helper's pre-2023 choice preserves 2020-identical behaviour).
Every leader/tagged-reference call site routes through `TagCompat` or `LeaderLogicService` (whose
primary path is TagCompat; its reflection probes are runtime-only fallbacks that cannot break a
compile). No direct call to any removed member exists in the tool.

**Verified unchanged across all 8 versions (2020–2027):**

- `IndependentTag.Create(Document, ElementId viewId, Reference, bool, TagMode, TagOrientation, XYZ)`
  — the overload the tool uses — present in all 8 (the `tagTypeId` overload also exists in all 8).
- `IndependentTag`: `TagHeadPosition` get/set, `HasLeader` get/set, `LeaderEndCondition` get/set,
  `CanLeaderEndConditionBeAssigned`.
- Enums: `TagMode.TM_ADDBY_CATEGORY`, `TagOrientation.Horizontal/Vertical`,
  `LeaderEndCondition.Attached/Free`; all 14 `BuiltInCategory` values used (6 MEP model categories,
  6 tag categories, OST_Dimensions, INVALID); all 5 `BuiltInParameter` values used
  (VIEWER_ANNOTATION_CROP_ACTIVE, CURVE_ELEM_LENGTH, RBS_CURVE_DIAMETER_PARAM,
  RBS_CURVE_WIDTH_PARAM, RBS_PIPE_DIAMETER_PARAM).
- `Reference(Element)` ctor; `View.RightDirection/UpDirection/ViewDirection/CropBox/CropBoxActive/Scale`;
  `Element.IsHidden/GetTypeId/ChangeTypeId/get_BoundingBox(View)`; `Document.GetDefaultFamilyTypeId`;
  `Family.FamilyCategory`; `Duct`/`Pipe`/`MEPCurve`/`LocationCurve`/`LocationPoint`; `Outline`;
  `Curve.GetEndPoint/Evaluate`; `Line.Direction`; `Transform` (Identity/CreateTranslation/
  CreateRotationAtPoint/OfPoint); `TransactionGroup`/`SubTransaction`;
  `FilteredElementCollector.OfCategory`.
- The single `new ElementId(...)` call uses the `BuiltInCategory` constructor (present in all 8);
  all numeric id work goes through `.IntValue()`.

**UI:** the settings dialog is WinForms (`UseWindowsForms` is enabled suite-wide in the csproj) —
identical API surface on .NET Fx 4.7.2/4.8, .NET 8, and .NET 10. The `#if` blocks in the services
are all `#if DEBUG` (diagnostics), not version branches — version-specific code lives only in the
helpers, per the project rule.

**Source changes needed: none** — headers contain no stale compatibility claims.

**Incidental observation (does not affect this tool):** the 2024 assembly has an extra
`IndependentTag.Create(Document, ElementId, ElementId, Reference, XYZ, double)` overload that is
absent again in 2025+ — a reminder not to adopt one-version-only overloads outside helpers.

**Build verification:** same limitation as the other audits — assembly-metadata verification only;
a real 8-configuration build still needs `build-all.ps1` on the local machine.

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
