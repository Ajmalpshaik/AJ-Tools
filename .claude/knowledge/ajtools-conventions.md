# AJ Tools — Conventions & Decisions Log

This file is the shared memory for the `ajtools-build` and `ajtools-debug` skills. It exists so that
every new build or debug session already knows what's been decided before, instead of Ajmal having to
explain the same thing twice. Both skills read this file before starting work, and append to the **Log**
section at the bottom after finishing.

If you're an editor reading this: this file is meant to grow. Don't be shy about adding a new dated Log
entry — but if something in "Established Conventions" turns out to be wrong or outdated, correct it in
place rather than leaving stale info sitting next to the new truth.

## Established Conventions

**Branding & metadata**
- Display name is **"AJ Tools"** (with a space) — ribbon text, transaction names (`"AJ Tools - <Tool>"`), assembly name, `.addin` Name. Namespace is `AJTools` (no space). Don't rename to "AJ-Tools".
- Every command file uses the full `#region Metadata` block (see `Properties/AssemblyInfo.cs`, `Helpers/ElementIdHelper.cs` for the template) — Tool Name, File Name, Purpose, Author, Version, Created/Updated dates, Target Revit, Framework, Platform, Dependencies, Input/Output, Notes, Changelog, License, Repo.
- Suite version lives in `Properties/AssemblyInfo.cs`. Bump the patch digit for a fix/refactor with no new tool; bump minor when a tool is added.

**Active project location**
- The live multi-version project is `D:\Ajmal\Revit Addins\src\AJ Tools.csproj` (repo root `src/`), driven by root `Directory.Build.props`/`.targets`. It builds all Revit **2020 → 2027** from configs `Release` (2020), `Release R21..R24`, `R25`/`R26`, `R27`.
- The old single-version tree at `D:\Ajmal\Revit Addins\AJ Tools\` and anything under `_backup\pre-multiversion-*` are stale copies — never edit those.
- **Proof case this matters**: Colorize was hand-ported to C# on 2026-07-02 but landed only in the
  stale `AJ Tools\` tree, never in live `src/` — it silently could never appear on the ribbon no matter
  how many times the add-in was rebuilt, since its code wasn't part of what gets compiled. Found and
  fixed 2026-07-13 (see `debug-log.md`). When Ajmal reports a tool "missing" or "not coming up", check
  whether the command class actually exists under root `src/` (and is wired into a `RibbonManager.cs`/
  `AnnotationRibbonManager.cs` `typeof(...)` reference) before assuming a build/deploy problem.
- **The reverse can also happen**: on 2026-07-16 a GitHub catch-up sync found the **MEP Openings** tool
  (shipped in GitHub tag v1.10.0, still fully wired in the `AJ Tools\` git-repo tree) completely absent
  from root `src/` — not renamed, genuinely never carried over when root became the live tree. It was a
  clean restore (no missing dependencies) once found. Root `src/` is live for day-to-day work, but it is
  **not automatically a superset of what's on GitHub** — before assuming a tool doesn't exist, or before
  any GitHub sync, check both directions. Full mechanics of the sync (and why the two trees drift for
  weeks at a time) are in the cross-session memory `feedback-git-sync-gap`, not duplicated here.
- SDK-style csproj with globbing — don't add manual `<Compile Include>` entries, new `.cs`/`.xaml` files are picked up automatically.
- **`dist/package.ps1` gotchas found rebuilding multi-version packaging (2026-07-16):**
  - Never use `[ordered]@{}` with integer keys for a lookup table in PowerShell — `OrderedDictionary`
    exposes a positional `this[int index]` indexer alongside the normal `this[object key]` one, and an
    integer key silently resolves as an out-of-range **position**, not a dictionary key, returning
    `$null` with no error. A plain `@{}` (Hashtable) only has the key indexer and works correctly. This
    bug made every "different" per-version build in `package.ps1` silently rebuild the 2020 config 8
    times over — always verify a build script's chosen output folder name for the actual version, not
    just that "Build succeeded" was printed once per loop iteration.
  - The VS-bundled `MSBuild.exe` only resolves the machine-wide .NET SDK (9.x here) and cannot target
    `net10.0-windows` (Revit 2027) even though a .NET 10 SDK is installed user-locally at
    `%LOCALAPPDATA%\Microsoft\dotnet`. Route the 2027 build through that local `dotnet.exe` directly
    (`dotnet build ... -c "Release R27"`) instead of MSBuild — it resolves its own bundled SDK correctly.
  - The `Release R21`…`R27` configurations exist only in the csproj (via `Directory.Build.props`), NOT in
    `AJ Tools.sln` — building the .sln with `-p:Configuration="Release R25"` fails with MSB4126. Build
    `src\AJ Tools.csproj` directly for any R-config; only plain `Release`/`Debug` work at solution level.
  - **"Warning-free" is true for `Release` (2020) only** (measured 2026-07-27): a clean rebuild of
    `Release` gives 0 warnings, but `Release R25` (.NET 8) gives ~650 pre-existing **CA1416**
    ("only supported on windows") warnings — every WinForms call in `CmdSmartMepTagSettings.cs` (202)
    and `CmdReassignLevel.cs` (178), plus AiShell files (~270). They are noise, not bugs, and they
    disappear from a file the moment its WinForms UI is replaced by WPF. So on any R-config, judge a
    change by *warnings from the files you touched*, not by the total — and don't "fix" the total by
    suppressing CA1416; convert the offending dialog to WPF instead.
  - **MSBuild gotcha (proven 2026-07-26, the ProgramData deploy-path bug)**: inside a target's ItemGroup,
    when an item is created via a transform (`Include="@(Src->'...')"`) its **metadata elements'**
    `%(FullPath)` binds to the SOURCE item being transformed — but a later `%(NewItem.FullPath)` reference
    resolves the NEW item's own path. Mixing the two silently produces two different base paths from what
    reads like one. When a generated path must match a copy destination, derive both from the same custom
    metadata value instead of re-concatenating.

**Verifying the real Revit API across versions (no Revit launch needed)**
- 2020/2024 (.NET Framework era): PowerShell `[Reflection.Assembly]::ReflectionOnlyLoadFrom` over
  `C:\Program Files\Autodesk\Revit <year>\RevitAPI.dll` works for full member-signature checks.
- 2027 (.NET 10 era): that fails from Windows PowerShell (netfx can't map net10's core types). Working
  fallbacks, in order of strength: (a) MetadataLoadContext net472 build ships inside the .NET SDK at
  `sdk\<ver>\Sdks\Microsoft.NET.Sdk\tools\net472\` (loading it into PS 5.1 needs its buddy DLLs resolved
  from the same folder — non-trivial); (b) raw byte scan of the real 2027 DLL for the exact member-name
  strings (metadata names are stored as plain UTF-8 — a distinctive name found/absent is strong existence
  evidence), combined with a `Release R27` compile against the reference package. Used (b) for the
  Highlight Selection lining work, 2026-07-26.

**Version-safe API helpers — always route through these, never call the raw Revit API at the call site:**
- `ElementIdHelper.GetIntegerValue(id)` / `.IntValue()` / `.LongValue()` instead of `ElementId.IntegerValue` (removed in 2026). `WorksetId.IntegerValue` is a different type — leave that one alone.
- `ElementIdHelper.FromInt(value)` instead of `new ElementId(someInt)` — the plain-`int` constructor is gone from the real Revit 2027 API (even though the NuGet reference package still exposes it and compiles fine — don't trust the NuGet package or web docs alone on version-gap questions like this; load the real installed `RevitAPI.dll` to verify if in doubt).
- `RevitCompat` for anything ForgeTypeId-related (spec/group/unit values, shared-param creation, unit conversion) — the old `ParameterType`/`BuiltInParameterGroup`/`DisplayUnitType`/`UnitType` enums are removed 2022+/2025+.
- `TagCompat` for `IndependentTag` leader/tagged-element members (single-ref API removed 2023).
- `FilterRuleCompat` for `ParameterFilterRuleFactory` string rules (`caseSensitive` arg removed 2023, old overload deleted 2026).
- `ElementIdHelper.IsDefinedBuiltInCategory(id)` / `IsDefinedBuiltInParameter(id)` instead of a raw `Enum.IsDefined(typeof(BuiltInCategory/BuiltInParameter), someInt)` — throws a type-mismatch exception in Revit 2024+ because both enums widened Int32→Int64 that year. Compiles fine everywhere, breaks only at runtime on 2024+.
- Dimension collectors: filter by `.OfCategory(BuiltInCategory.OST_Dimensions).WhereElementIsNotElementType()`, not `.OfClass(typeof(Dimension))` — Revit 2025+ returns linear dimensions as a `LinearDimension` subclass that an exact-type filter silently misses.
- `UIView.GetWindowRectangle()`: the returned `Rectangle` type's namespace is a version gap —
  `Autodesk.Revit.UI.Rectangle` does NOT compile against the 2020 API (CS0234) even though newer
  versions/docs place it there. Declare the result with `var` and use `.Left/.Top/.Right/.Bottom`
  (raw device pixels; negative on a second monitor) — compiles and works on every version.

**Tag & leader logic**
- All tag placement / L-shaped leader tools must use `LeaderLogicService` (`src/Services/LeaderLogic/LeaderLogicService.cs`) for elbow computation — a specific 3-case elbow logic (Normal L-shape, Guard 1 same-X push, Guard 2 same-Y no-elbow) in view-space coordinates that must stay consistent across every tool. Instantiate `LeaderLogicService(view)`, use `ComputeElbow(headModel, leaderEndModel)` or `ApplyLeaderLogic(...)`. For stacking tools use `GetT1()` / `GetE1()`.
- Tag tools (Smart MEP Tag, tag arranger) still place one tag per element — Revit 2022+ supports multi-leader tags (`MultiLeader`), but switching to that is a workflow decision that changes how many tags get created. Flag it to Ajmal before touching; don't switch unprompted.
- **Rearrange Tags' click behaviour is whole-batch-per-click, not per-click-per-tag (verified by reading
  `IntelligentTagArrangerService.TryArrangeAtPoint`, 2026-07-28)**: every single `PickPoint` click
  re-arranges ALL selected tags into a fresh vertical stack starting at that point (nearest tag to the
  click assigned first, the rest step above/below by the configured spacing) — it does not place one
  tag per click. Clicking again doesn't add to the stack, it relocates the whole thing from scratch
  (each click's `Transaction` commits independently, so Esc just stops the "try another base point"
  loop). Matters for any future request that says "like Rearrange Tags" — that phrase means whole-batch-
  per-click, not one-item-per-click. Stack Tags (`StackTagsService.cs`) reuses exactly this mechanic for
  freshly-created tags: first click creates, every later click moves what it already created.
- **Which tag family to use, when not told a specific one (Ajmal, 2026-07-13)**: use whatever Revit
  itself would use tagging manually — `Document.GetDefaultFamilyTypeId(category.Id)` — not a guessed
  generic name like "M_Duct Size Tag", and not AJ Tools' own Smart MEP Tag Settings (a separate,
  AJ-Tools-specific enable/priority setting, not the same thing). Full technical detail + a related
  `IndependentTag.Create()` type-argument gotcha found while fixing this in `live-model/tagging.md` §
  Finding the RIGHT tag family.
- **Duct tag SIDE must be consistent by orientation, not alternated (Ajmal, 2026-07-13,
  "documentation looking quality")**: same-pattern ducts (e.g. two mirrored branches of the same size)
  must have their tags on the SAME side — never one above and one below just because of placement
  order. Rule confirmed via two reference screenshots: **horizontal-in-view duct runs always tag
  below; vertical-in-view runs always tag to the right.** Any resulting overlap gets resolved by
  pushing tags apart afterward (see the tag-vs-tag/tag-vs-duct resolution notes below), never by
  varying which side a tag starts on. Applies generally to duct tagging, not just the request it came
  from — same status as the horizontal/1000mm scope rule above.
- **Which LEFT/RIGHT the leader extends toward = real flow direction, not position (Ajmal, 2026-07-13,
  confirmed directly)**: for horizontal ducts, the leader/text must extend toward the downstream side —
  read from the duct's own `Connector.Direction` (In/Out), not a geometric guess (nearest riser,
  group centroid, etc. were all tried first and superseded — see `live-model/tagging.md` § Leader side
  should follow REAL flow direction for the full progression and why each earlier version broke).
  **Exception confirmed 2026-07-14**: Supply Air ducts specifically must point the OPPOSITE of their
  real flow direction; Return Air and Exhaust Air keep the real flow direction as-is. Standard drafting
  convention (draw supply/return as visual opposites for quick distinction at a glance), identified via
  `Duct.MEPSystem.SystemType == DuctSystemType.SupplyAir` — NOT `MechanicalSystemType` (a plausible-
  looking but wrong enum type name; verify the actual property type via reflection before trusting an
  enum name, same lesson as everywhere else in this project).
- **Check `view.Scale` FIRST, before computing any tag clearance — every time (Ajmal, 2026-07-13,
  standing rule)**: every mm-based clearance (elbow push, resolver margins, base offset) must scale
  with the view's current scale, computed as one shared ratio right at the start, not hardcoded. A
  clearance measured/tuned at one scale silently breaks the moment the view scale changes — this
  already caused a real regression (546 own-leader-clashes) once. Full detail in `live-model/tagging.md`
  § Check view.Scale FIRST.
- **Duct tagging scope rule (Ajmal, 2026-07-13)**: when tagging ducts, only tag **horizontal runs** —
  skip vertical risers — and only runs **1000mm or longer**; shorter stubs don't get tagged. Applies
  generally to duct tagging, not just the one request it came from. Default in
  `.claude/scripts/recipes/tag-elements-in-active-view.cs` (`onlyHorizontalRuns = true`,
  `minLengthMm = 1000.0`) — still restate/confirm before running per "every number is per-request", in
  case a future request explicitly overrides it.
- **Create Tags' vertical-run skip is deliberately WIDER than Smart MEP Tag's own (Ajmal, 2026-07-28)**:
  Smart MEP Tag only excludes vertical *ducts* (`IsVerticalDuct` in `SmartMepTagService.cs`, duct
  category only); Create Tags excludes vertical ducts, pipes, AND cable trays (`IsVerticalMepCurve` in
  `CreateTagsService.cs`, same Z-direction-over-0.95 technique, generalized to any `MEPCurve`). This is
  intentional, confirmed via AskUserQuestion — not a bug. Don't "fix" one to match the other without
  checking with Ajmal first. Likewise, Create Tags' minimum-length threshold is a real user-editable
  setting (`CreateTagsSettingsWindow`, mm field); Smart MEP Tag's own equivalent thresholds
  (`MinDuctWidth`/`MinPipeDiameter`/`MinCurveLength`) are still hardcoded constants in
  `SmartMepTagService.cs`, not exposed in its own Settings window — the two tools' settings are
  independent of each other. Full story in `ajtools-conventions-log.md` 2026-07-28.

**Credit line — every window carries it (Ajmal's rule, 2026-07-27)**
- Exact text, no variations: **`Created & All Rights Reserved @ Ajmal P.S.`**
  (in XAML: `Created &amp; All Rights Reserved @ Ajmal P.S.`). No year, no "(c)", no "AJ Tools" suffix —
  three windows had drifted into their own wording and were normalised back.
- **Where**: bottom-centred footer, `TextDisabled` brush, `FontSize="11"`, `Margin="0,0,0,8"`.
  Some older windows carry it as a header subtitle instead (Colorize, Filter Pro) — **leave those where
  they are**; Ajmal's instruction was "if it's already somewhere, keep the same location". Only add the
  bottom footer where there is none.
- **How to add it without breaking a layout**: do NOT add a row to the window's existing root `Grid` —
  every `Grid.Row` index below it would shift. Wrap the root layout in a `DockPanel` and dock the footer
  `Bottom`; the existing Grid becomes the last child and fills the rest. Bump a fixed `Height`/`MinHeight`
  by ~24 px at the same time, or the footer eats the button row.
- AiShell windows merge `SoftUiStyles.xaml`, which has **no `TextDisabled`** — use `MutedBrush` there.
  Always confirm the brush key exists in the dictionary that window actually merges: a missing
  `StaticResource` compiles fine and only blows up at runtime (see the resource-lookup note below).
- (Historical: the last two WinForms dialogs carried it as a grey label until both were converted to
  WPF on 2026-07-28 — every window now uses the standard XAML footer.)

**Validate inside the window, never after `ShowDialog()` (rule from 3 real bugs, 2026-07-27/28)**
- The pattern to kill: dialog closes → code checks the value → shows an error popup → returns false →
  command returns `Result.Cancelled`. The user loses everything they typed and has to relaunch from the
  ribbon. Found in Arrange Tags Settings (bad spacing) and Reassign Level (same level in both boxes).
- The house pattern instead: a private `Validate(out …)` called from every relevant event
  (`TextChanged` / `SelectionChanged`) **and** from the action button's click. It sets an inline
  `ErrorText` TextBlock (`AccentDanger`) and toggles `RunButton.IsEnabled` — no popup, window stays open.
  The calling command still re-checks after `ShowDialog()` returns, as a cheap belt-and-braces guard.
- Every modal window shown from a command needs
  `new WindowInteropHelper(window) { Owner = uiapp.MainWindowHandle }`, or it can drop behind Revit —
  worse when the window also sets `ShowInTaskbar="False"`, since there is then no way to get it back.

**Converting a WinForms dialog to WPF (done twice: Arrange Tags Settings, Reassign Level)**
- Worth doing beyond looks: each converted file also drops its entire CA1416 warning block on the .NET 8
  configs (Reassign Level alone was 192 of them, 682 → 490 suite-wide). **Done for all three as of
  v1.25.5 (2026-07-28): Arrange Tags Settings, Reassign Level, Smart MEP Tag Settings — zero WinForms
  dialogs remain in the project.** R25's residual ~274 CA1416 warnings all sit in AiShell/AvalonEdit-era
  files (RevitApiCompletionData, TextMarkerService, AiShellView code-behind, etc.) plus
  GraphicsOverrideWindow code-behind — nothing dialog-shaped left to convert.
- Recipe: new `UI/<Area>/<Name>Window.xaml(.cs)` merging `../ModernStyles.xaml`, root `DockPanel` with the
  credit footer docked `Bottom`, a `PanelCard` Border for the body, expose the chosen values as
  `{ get; private set; }` properties read by the command after `DialogResult == true`. Keep the window
  pure UI — no collectors, no transaction; the command keeps owning the model work.

**WPF inside Revit**
- **A `WindowStyle="None"` custom-chrome window that lets the user maximize (double-click header, a
  maximize button, etc.) will draw over the Windows taskbar** unless you cap its size — WPF only
  respects the taskbar automatically for the default chrome. Fix: bind `MaxWidth`/`MaxHeight` to
  `{x:Static SystemParameters.MaximizedPrimaryScreenWidth}` / `MaximizedPrimaryScreenHeight` in XAML
  (those two system metrics already exclude the taskbar) — no code-behind needed. Fixed in
  `AboutWindow.xaml` (v1.19.1, 2026-07-19); any other custom-chrome window that supports maximize
  (e.g. a future `SettingsWindow`/`AiShellView` restyle) should get the same two lines.
- **Never use `System.Windows.Application.Current` — it is always null inside Revit** (Revit hosts WPF
  without creating a WPF `Application` object). Capture `Dispatcher.CurrentDispatcher` in the
  constructor of whatever is created on Revit's UI thread and use that captured dispatcher for all UI
  marshalling (pattern: `AiTaskWarningBarService`, `AiShellViewModel`). This has caused two real
  bugs (invisible activity banner 2026-07-12, frozen AJ AI progress bar 2026-07-16) — as of 2026-07-16
  no live `Application.Current` call remains in `src/`; don't reintroduce one.
- **AJ AI provider naming (fixed 2026-07-25, v1.25.0 merge):** the provider-key string stored in
  `AiShellConfig.SelectedProvider` and shown in the Settings dropdown is `"Claude"` (alongside
  `"Gemini"` / `"OpenAI"`), while all internal member names stay `Anthropic*` (`AnthropicApiService`,
  `IsAnthropicSelected`, `AnthropicModel`, `EncryptedAnthropicApiKey`). Default model
  `claude-sonnet-5`. Don't reintroduce `"Anthropic"` as a key string — the local and GitHub lines once
  implemented this same feature with the two different keys and had to be merged by hand.
- **A script/work item running on Revit's UI thread blocks WPF repainting** — updating a bound property
  mid-run isn't enough to make the UI visibly change; pump the dispatcher at `DispatcherPriority.Render`
  (an empty `Invoke`) after setting the values so the repaint happens now. Render priority does not
  process user input (Input priority is lower), so there's no re-entrancy risk from the pump.
- **Never put `Background`/`Foreground`/any `{StaticResource ...}` directly on a XAML root element's own
  attributes (`<UserControl ...>`, `<Window ...>`) when the resource is declared in that same root's own
  `.Resources`.** WPF processes a root element's own attributes before its `Resources` dictionary is
  populated, so the lookup always fails: `XamlParseException` → `Cannot find resource named 'X'.
  Resource names are case sensitive.` This crashed Revit's `OnStartup` outright the first time
  `AiShellView`/`SettingsWindow` were restyled (2026-07-18, suite 1.15.2/1.16.0 → fixed in 1.16.1) —
  `AiShellPaneProvider` constructs the view unconditionally, so a bad root-level `StaticResource` there
  takes the whole add-in down at startup, not just that one window. **Fix**: set
  `Background`/`Foreground`/etc. one level down, on the first child element (e.g. the outer `Grid`), not
  on the `UserControl`/`Window` tag itself — a child element correctly resolves `StaticResource` against
  its parent's `Resources`, only the root element referencing its own can't. **This is exactly the kind
  of bug a clean `msbuild` build cannot catch** — BAML compilation doesn't evaluate `StaticResource`
  lookups against the runtime resource tree, only `InitializeComponent()`/`LoadBaml()` at actual runtime
  does. Any new WPF UserControl/Window in this project needs an actual Revit launch to catch this class
  of error, not just a compile check.
- **A RadioButton/CheckBox wired with `Checked="Handler"` in XAML, on the same element that also sets
  its own `IsChecked="True"`, fires `Handler` synchronously during `InitializeComponent()`'s BAML walk —
  before any later-declared `x:Name` siblings exist yet.** If `Handler` touches those siblings (e.g. a
  scope toggle that shows/hides other named panels), it NullReferenceExceptions on every single window
  open, not just an edge case. Found while adding the Reassign Level scope toggle (2026-07-28, suite
  v1.26.0) — caught by reasoning through the load order before it ever shipped, not by hitting the crash
  live. **Fix**: don't wire `Checked`/`Unchecked` via the XAML attribute when the handler reaches outside
  that one element; attach it in code-behind after `InitializeComponent()` returns instead, then call the
  update method once manually for the initial state.
- **A bare `Visibility.Visible`/`Visibility.Collapsed` fails to compile (CS0176) inside any class that
  itself has a `Visibility` property — i.e. any `Window`/`UserControl`/other `UIElement`.** `UIElement`
  already declares an *instance* property literally named `Visibility`, which shadows the enum type name
  in that class's scope, so the compiler resolves the bare name to `this.Visibility` first and then fails
  to find `.Visible`/`.Collapsed` on it. This one WAS caught live, by the first Release build, while
  adding the same Reassign Level scope toggle. **Fix**: fully qualify as
  `System.Windows.Visibility.Visible`/`.Collapsed` any time this kind of assignment is written inside a
  `UIElement`-derived class.

**Feature-folder layout for big multi-file tools (Ajmal's own rule, 2026-07-29)**
- A large tool (Game Mode is the template) lives in ONE folder under src/ (like AiShell), NOT spread
  across Commands/Services/UI subfolders - and each feature gets its own small .cs file, using
  partial classes for what is logically one class (engine core + Movement/Measure/Extras; window core
  + Controls/Weapons/Render/Photo). Ajmal asked for this explicitly ("each feature separate file so
  editing will be easy"). ALL instance fields/consts of a partial class stay in the CORE file.
  Keep namespaces unchanged when only relocating files - zero ripple.
- **After MOVING a .xaml/.xaml.cs pair to a new folder, the next build fails with CS0103 on every
  x:Name field** ("The name 'PlayLayer' does not exist...") from the `_wpftmp` markup-compile pass -
  the per-version obj folder still holds the old generated files. Fix: delete `src\obj\R<year>` for
  the configuration being built and rebuild. Hit and solved 2026-07-29 (110 phantom errors -> 0).

**Real-time camera + raycast techniques (from Game Mode; ALL verified live on Revit 2020, 2026-07-28)**
- `View3D.SetOrientation` needs NO transaction: it behaves as navigation (like Revit's own orbit/walk),
  not a model change — writes apply immediately, persist on read-back, create ZERO undo entries, and
  cost well under 1 ms each. Verified with `Document.IsModifiable == false` at write time, so no hidden
  wrapper transaction was helping. Per-frame camera animation is therefore free of undo pollution —
  pair each write with `UIDocument.RefreshActiveView()` to repaint. (Web samples wrap SetOrientation in
  a transaction — unnecessary, at least on 2020.)
- `ReferenceIntersector` accepts a PERSPECTIVE `View3D` (constructor and `FindNearest` both fine, hit
  results identical to an ortho view, ~0.1 ms per hitting ray) — no hidden-ortho-companion-view
  workaround needed. It only reports elements VISIBLE in that view, so VG/filters/hide/section-box
  double as collision filters for free. Set `FindReferencesInRevitLinks = true` and resolve link hits
  via `(hit as RevitLinkInstance).GetLinkDocument().GetElement(reference.LinkedElementId)`. Rebuild the
  intersector after visibility changes (Game Mode rebuilds every ~4 s).
- Reusable real-time frame-loop shape (any future live tool): a modeless WPF overlay window owns a
  DispatcherTimer and raises ONE ExternalEvent per tick — multiple `Raise()` calls before execution
  coalesce into one, so the loop naturally throttles to Revit's idle pump. ALL Revit API work lives in
  the handler; the window only reads/writes a plain shared-state object (same UI thread, no locking).
  For pixel-exact overlay placement + FPS mouse-look, stay in device pixels end-to-end
  (`GetWindowRectangle` → `SetWindowPos`, `GetCursorPos`/`SetCursorPos`) and no DPI conversion is
  ever needed, even on mixed-DPI dual monitors.

**Selection-based tool scope — reuse this shape rather than reinventing it**
- When a tool needs both a "whole project/model" mode and a "just what I selected" mode (first done for
  Reassign Level's Selected Elements scope, v1.26.0, 2026-07-28): read `uidoc.Selection.GetElementIds()`
  **before** opening any modal WPF window, never from inside one — a modal window blocks Revit's UI, so
  there is no way to prompt `PickObjects` once it's open. Pass the pre-selection count (and however many
  of those are actually eligible for this tool) into the window's constructor; give the window a scope
  toggle that shows/hides the mode-specific fields.
- Don't let the selection-scope option lead to a dead-end Run click when nothing eligible is selected —
  disable that option with an explanatory tooltip instead (same "validate inline, never after
  `ShowDialog()`" house rule, just applied to an entire scope choice instead of one field). This mirrors
  the existing "pre-selection, else prompt" pattern already used by
  `CmdRevisionCloudByElements.GetSelectedElements`/`CmdCeilingMagnet`/`CmdForceTagLeaderLShape`, just
  without the `PickObjects` fallback (not needed once the window can simply tell the user to select
  first and reopen).

**Ribbon & shared helpers**
- Ribbon is built in `Core/RibbonManager.cs` (+ `Core/AnnotationRibbonManager.cs`), not in `App.cs`.
- **A split button with a permanent default face and a secondary action tucked in the dropdown**
  (e.g. Opening panel's Create Openings / Opening Settings, AI Assistant panel's Run Pinned / Saved
  Scripts): give `CreateSplitToolSpec` an `Action<SplitButton> configureSplitButton` that sets
  `splitButton.IsSynchronizedWithCurrentItem = false` - per `RevitAPIUI.xml` this pins the top face
  to the FIRST-added child forever (that child also runs directly on a single click), and every other
  child is reachable only by opening the dropdown arrow. Whichever child should be the permanent default
  must be added first. **Do not** try to make the top face track "whichever child ran last" by setting
  `IsSynchronizedWithCurrentItem = true` and having each child set `SplitButton.CurrentButton = self` in
  `Execute()` - that was tried first (2026-07-21) and reverted the same day: Ajmal watched it live and
  didn't want the top face changing depending on what was clicked. Confirmed twice now (Opening, then
  Run Pinned/Saved Scripts) that "always-there default + dropdown-only secondary" is the wanted pattern
  here, not last-used tracking - default to this reading if a future request sounds like "make X always
  there, put Y in the pulldown." Full story in `ajtools-conventions-log.md` 2026-07-21.
- Shared validation: `Helpers/ValidationHelper.cs`. Dialogs: `Helpers/DialogHelper.cs` (namespace `AJTools.Utils`).
- Icons load from `src/Resources/*.png` via `Helpers/IconLoader.cs` (32px large / 16px small, filename is the
  only key — same file feeds both ribbon managers). As of 2026-07-19 there are **43 unique icon files**
  across both tabs, and **5 of them are reused as-is by two unrelated tools**: `apply.png` (Apply Graphics +
  Pin/Unpin Elements), `Flowdirectioncreate.png` (HVAC Schematic + Duct Flow Annotations),
  `Dimensions by Line.png` (Auto Duct Dimension + Quick Dimension), `copyswaptext.png` (Copy/Swap Text Notes +
  Arrange Text in Box), `Arrange Tag.png` (Rearrange Tags + Center Room Tags). Redrawing one of these files
  changes the icon for both tools at once — if Ajmal wants them visually distinct, that needs a new filename
  wired into whichever `RibbonManager.cs`/`AnnotationRibbonManager.cs` call currently points at the shared
  file, not just a new image.

**Shared mode-enum tool families — reach for this when a 3rd/4th near-identical tool variant is asked for**
- Established shape (Purge Unplaced Views was first; Purge Unused Elements and Transfer Views now follow
  it too): one enum naming each variant + an `internal static class ...Extensions` giving it its display
  text (tool title, noun singular/plural, description, transaction name), one shared window class that
  reads everything through those extension methods, one shared collector/service, and thin one-line
  command classes that just call the shared runner with their own enum value. Adding a 5th variant later
  should mean touching the enum + collector, not writing a whole new window/service from scratch.
- **The mode/kind enum itself must be `public`, even though everything else in its file
  (`...Extensions`, item/status/result model classes) stays `internal`.** A `public partial class Window`
  (required for XAML/BAML) cannot have a constructor parameter of a less-accessible type — an `internal
  enum` there is CS0051 ("Inconsistent accessibility"), caught only at compile time, not by XAML tooling.
  Easy to miss because the extensions class two lines below it in the same file correctly stays
  `internal` — don't copy that modifier onto the enum by habit.
- When one variant's "is this safe to delete" question can't be answered with 100% certainty by static
  analysis alone (e.g. Purge Unused View Templates can't see whether a template is silently set as
  Revit's own default-for-new-views), don't try to special-case every edge case in the scan — lean on the
  existing "probe the delete inside a rolled-back transaction, let Revit's own dependency graph decide"
  pattern instead. It's already proven (`UnplacedViewPurgeService`) and generalizes to any `Element`, not
  just `View`.
- Sheet-placement bookkeeping differs by what's being placed: a normal view/legend/drafting view uses
  `Viewport` (`ViewId`/`SheetId`/`GetBoxCenter()`); a schedule uses `ScheduleSheetInstance`
  (`ScheduleId`/`OwnerViewId`/`Point`) — note the sheet-id property is named **`OwnerViewId`** on
  `ScheduleSheetInstance`, NOT `SheetId` like `Viewport` has. A legend can be placed on several sheets (or
  the same sheet more than once) at once — record every existing placement as a list before deleting an
  overridden element, never assume at most one.
- All of `Viewport.Create(doc, sheetId, viewId, XYZ)`, `ScheduleSheetInstance.Create(doc, sheetId,
  scheduleId, XYZ)`, `View.GetFilters()`, `ViewSchedule.IsTitleblockRevisionSchedule` /
  `.IsInternalKeynoteSchedule`, `GroupType.Groups` (a `GroupSet` with `.IsEmpty`/`.Size`), and
  `BuiltInCategory.OST_IOSModelGroups`/`OST_IOSDetailGroups` were confirmed present against the real
  installed Revit 2020 `RevitAPI.dll` (2026-07-28) before use — none of them are in this file's known
  version-gap list above, so no `#if` branching was needed for any of them.

**AJ AI Bridge (live Revit session testing)**
- `mcp__aj-tools-aj-ai__ping` / `run_csharp` let Claude run C# directly against Ajmal's live, open Revit document — only works from a session running locally on Ajmal's PC (not a remote/cloud sandbox).
- The bridge **blocks reflection and assembly-loading** as a safety guard — you cannot use it to reach into an AJTools internal (non-public) class to test its private logic directly. Only plain Revit API calls are allowed. If you need to exercise a command's real internal logic, either replicate the behavior with plain Revit API calls, or ask Ajmal to click the button himself once you've set up the preconditions.
- Posting one of AJ Tools' own ribbon buttons programmatically (`RevitCommandId.LookupCommandId(...)` + `UIApplication.PostCommand(...)`) does **not** reliably work for commands nested inside a `PulldownButton` — `PushButton.GetCommandId()` doesn't exist in this API version, and manually constructed `CustomCtrl_%...` lookup strings didn't resolve in testing. Don't spend time re-guessing this; treat it as unsolved.
- Destructive ops (Delete/Purge/file writes) are refused by the bridge unless explicitly allowed — this is intentional, don't try to route around it.


## Log — moved

The dated history (what was decided/built on which day, and why) now lives in
[`ajtools-conventions-log.md`](ajtools-conventions-log.md). It's history, not rules — read it only when you
need the story behind a decision or what happened on a given date. **The rules above are what a build or
debug task needs.**

New dated entries go in the log file; new *rules* go in the conventions above — one fact, one home.

## Build environment: which toolchain builds which Revit version (verified 2026-08-04)

Three separate traps, all environmental rather than code problems — check these before concluding a
build is broken:

1. **The `.sln` only defines `Debug`/`Release`.** The `Release R21`..`R27` configurations live in
   `Directory.Build.props`, not in the solution. Building `"AJ Tools.sln" -p:Configuration="Release R25"`
   fails with `MSB4126: invalid solution configuration`. Multi-version builds must target the project
   file directly: `msbuild "src/AJ Tools.csproj" -p:Configuration="Release R25"`.
2. **`C:\Program Files\dotnet` is broken on this machine** — `hostfxr.dll` fails to load with
   `HRESULT: 0x800700C1` (architecture mismatch). Anything resolving to it dies. The working SDK is the
   per-user one at `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe` (Ajmal has no admin rights, so SDKs
   install there). `dist/package.ps1` already knows this and reaches for the local dotnet for the .NET 10
   (Revit 2027) build — so the packaging script succeeds where a plain `dotnet build` fails.
3. **VS2022's MSBuild resolves SDK 9.0.316**, which cannot target `net10.0-windows`, so R27 reports
   `NETSDK1045` under msbuild even though SDK 10 is installed. Not a code error. Build R27 through
   `package.ps1` or the local dotnet directly.

**Known non-clean configurations** (both pre-existing, deliberately NOT fixed — each is a project-wide
API migration needing Ajmal's sign-off, same class as the pending `ElementIdHelper` migration):

- `Release R21` (Revit 2021+): 76 `CS0618` warnings, all the pre-ForgeTypeId unit API —
  `DisplayUnitType`, `UnitType`, `UnitUtils.Convert*(…, DisplayUnitType)`,
  `Units.GetFormatOptions(UnitType)`, `LabelUtils.GetLabelFor(DisplayUnitType)`,
  `FormatOptions.DisplayUnits`. Obsolete but still functional.
- `Release R27` (Revit 2027): 1 `CS0618` — `Space.Zone` is deprecated in 2027 in favour of
  `GenericZone`. One call site: `src/UI/LocationDataAssignerWindow.xaml.cs`.

`Release` (2020 baseline) and `Release R25` both build **0 warnings / 0 errors** and must stay that way.
