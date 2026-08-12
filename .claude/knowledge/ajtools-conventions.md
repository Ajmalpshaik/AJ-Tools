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
  - **Never pipe a script that WRITES FILES through `Select-Object -First N`.** `-First` stops the
    upstream command the moment it has N items, by throwing `StopUpstreamCommandsException` — which
    kills the producing script mid-run. Measured 2026-08-12 publishing AJ Connect's tool folder:
    `publish-tools.ps1 ... | Select-Object -First 4` wrote the `.ajtool` fine, then was terminated
    while `index.json` was still open, leaving a **0-byte file that shipped to the live website**.
    The script exited 255 and that was the only warning. Use `Where-Object` to filter output, or
    capture to a variable and slice it afterwards — never `-First` on a side-effecting command.
  - **`Out-File -Encoding utf8` writes a BOM on Windows PowerShell.** Harmless for a human-read file,
    a real defect for anything a parser consumes: a leading U+FEFF makes strict JSON parsers fail
    with "unexpected character", which reads as broken data rather than a broken file. Found on the
    live site the same day, serving `index.json` as `﻿[...]`. Use
    `[System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding($false)))` — no BOM
    on any PowerShell version, so it doesn't depend on which one runs it.
  - **A 200 response only proves a file EXISTS.** Both bugs above passed a `curl -o /dev/null -w
    "%{http_code}"` check and were only caught by fetching the body and parsing it. For any published
    artifact, verify the CONTENT, not the status code.
  - **`Invoke-WebRequest -UseBasicParsing` returns `.Content` as a STRING for text content types and
    a BYTE ARRAY for binary ones** (`application/octet-stream`, which is what GitHub Pages serves an
    unknown extension like `.ajtool` as). A verification script that assumes one shape silently
    reports empty data for the other — this produced two false "signature invalid" results before the
    harness itself was the thing at fault. Branch on `$c -is [byte[]]`. AJ Connect itself is
    unaffected: `HttpClient.GetStringAsync` always returns a string.
  - **Never feed `Get-Content -Raw` straight into `ConvertTo-Json`** — use
    `[System.IO.File]::ReadAllText((Resolve-Path $p).Path)`. `Get-Content` decorates every string it
    returns with ETS note properties (`PSPath`, `PSParentPath`, `PSProvider`, `ReadCount`), and
    `ConvertTo-Json` at any depth > 1 serialises the **decorated object** rather than the text — the
    field comes out as `{"value":"…","PSPath":{…}}` instead of a string. Measured 2026-08-12 building
    the connector's tool publisher: a **1,910-character script produced a 2.2 MB payload**, and the
    consuming C# (expecting a plain string) could not have read it at all. It is silent — the script
    reports success, `$text.Length` is correct, and only the serialised output is wrong. Cast other
    values with `[string]` for the same reason.
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

**The ProgramData auto-deploy was BROKEN and is now disabled (found 2026-08-12)**
- `DeployAjToolsAddin` copied only `$(TargetPath)`, the PDB and `@(Content)` — the DLL and the icons.
  It **never copied the NuGet dependencies** (Newtonsoft.Json, Roslyn, AvalonEdit, CommunityToolkit),
  so it produced a **7.7 MB install missing everything the AI shell and Web Panel need** — and
  registered it under the **same AddInId** as the complete AppData one. Two registrations for one
  add-in, and no way to tell from the outside which Revit was loading.
- Found while clearing disk space: ProgramData held a "1.43.0" with no dependencies, AppData a
  complete 1.42.1. The obvious move — keep the newer-looking one — would have **deleted the working
  install and kept the broken one**. Always check a deploy is COMPLETE (dependencies present), not
  just newer.
- Disabled by commenting out `RevitAddinDeployName` + `DeployRoots` in `src/AJ Tools.csproj`.
  `AutoDeployRevitAddin` (AppData) copies `$(OutputPath)**\*.*` — everything — and is the deploy that
  always actually worked. All-users installs still go through `dist\install-all-users.cmd`, which
  ships a complete payload. To re-enable, restore both entries AND fix the target to copy
  dependencies, or the same half-install returns.

**Every build left a payload folder behind — now swept automatically (2026-08-12)**
- `AutoDeployRevitAddin` must create a fresh `AJ Tools.<timestamp>\` each build (a running Revit locks
  the current DLL), but nothing ever removed the old ones. Measured: **75 abandoned folders,
  2,086 MB**, accumulated since July.
- The target now sweeps them, keeping the newest 2 (live + one rollback). Best-effort and
  `ContinueOnError` — a folder a running Revit still holds open just fails and gets swept on a later
  build. Never let housekeeping fail a build.

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

**WPF motion / Storyboard rules (established on the About window entrance+exit pass, 2026-08-05)**
- **A `Button` that is BOTH `Click="Handler"` (where the handler calls `Close()`) AND `IsCancel="True"`
  raises `Closing` TWICE per click.** The Click handler closes, then WPF's cancel-button logic sets
  `DialogResult = false`, whose setter calls `Close()` again. This breaks any single-flag
  "already handled" guard in `Closing`: the second pass sails through and the window dies mid-animation.
  **Fix**: two flags, not one — `_isExitPlaying` (a close is in flight, keep cancelling) and
  `_isReadyToClose` (the animation's own completion callback asked for the real close, let it through).
  `AboutWindow.AboutWindow_Closing` is the reference implementation. Applies to any window that needs to
  defer its own close — exit animations, save prompts, async cleanup. Caught by reasoning through the
  close path before shipping, not by hitting it live.
- **A `Storyboard` stored in a `ResourceDictionary` can be frozen**, and a frozen `Freezable` throws on
  both `+= Completed` and `.Stop()`. **Always `.Clone()` a storyboard resource before adding handlers or
  keeping a reference to it.** Cloning also lets one storyboard resource be retargeted per call via
  `Storyboard.SetTarget(clone, element)` — that is how all five About sections share one swap animation.
- **Never set a `RenderTransform` through a `Style` `Setter` when the elements must animate
  independently** — a Setter's value is a single shared instance, so all N elements get the SAME
  transform object and move as one block instead of staggering. Declare the transform inline on each
  element (see the five nav buttons in `AboutWindow.xaml`).
- Entrance storyboards belong in `<Window.Triggers>` on `Window.Loaded`, declared **inline** rather than
  via `{StaticResource}` — see the root-element resource-lookup crash noted above. Storyboards that code
  must reach (exit, swap, ambient) live in `Window.Resources` and are fetched at runtime with
  `FindResource`, which is safe because the dictionary is fully populated by then.
- An infinite (`RepeatBehavior="Forever"`) ambient animation must be `.Stop()`ed when the window closes.
  Revit is a long-lived process; a live clock holds a reference to the target element and the dead window
  with it, so repeated opens accumulate leaks.
- `x:Name` on a transform nested inside a `<TransformGroup>` DOES register in the window namescope and
  generate a field — verified in the generated `obj\R2020\Release\UI\AboutWindow.g.cs`. Targeting a named
  transform (`Storyboard.TargetName="ShellScale"`, `TargetProperty="ScaleX"`) is far more readable than
  the indexed path form, and works.

**Progress reporting in a long tool — `ProgressReporter`, and why it is NOT a background thread (v1.40.1)**
- **Never move Revit work to a worker thread to "keep the UI responsive."** The Revit API must be called
  on Revit's own UI thread; a background thread throws or corrupts the document. Progress reporting here
  means the work stays exactly where it is and the window repaints part-way through it.
- Setting `ProgressBar.Value` inside a UI-thread loop changes the number but **paints nothing** — the loop
  starves WPF's render pass. After updating the values, pump the dispatcher with an empty `Invoke` at
  `DispatcherPriority.Render`. (Same technique already recorded for the AI shell's progress bar.)
- **Render priority, specifically.** `Input` sits BELOW `Render`, so a Render pump repaints *without*
  processing clicks or keystrokes — the user cannot re-enter the loop by clicking a button half way
  through a delete. A `DoEvents`-style pump at Input priority WOULD allow exactly that re-entrancy.
- Throttle to ~33 ms between repaints, but always paint the first and last item so the bar visibly starts
  empty and finishes full. Measured cost: 500 reports = 86 ms total, against one trial-delete transaction
  per item — noise.
- **Wire it in without breaking callers**: give the service method an OPTIONAL `Action<int,int>` callback
  defaulting to `null`, and wrap the invocation in try/catch so a reporting fault can never abort the
  work. Existing callers then compile and behave unchanged.
- **Adding the progress row to a window**: put the new rows INSIDE an existing inner grid, never as a new
  row on the window's root `Grid` — that shifts every `Grid.Row` index below it (same trap as the credit
  footer). Keep both elements `Collapsed` when idle so the window looks unchanged.
- First use: `PurgeUnusedElementsWindow` scan. `UnusedElementPurgeService.Scan()` trial-deletes every
  candidate inside a rolled-back transaction, which is the longest silent freeze in the suite. The other
  long tools (the two other Purge windows, Transfer Views, the tagging services) are the same shape and
  can reuse this helper unchanged.

**The suite version lives in SIX places and WILL drift — run `toolserify-version-consistency.ps1`**
- The six: `AssemblyInfo.cs` header `Version :`, its `[assembly: AssemblyVersion]` and
  `[AssemblyFileVersion]` attributes, its newest changelog entry, `CHANGELOG.md`'s newest `## [x.y.z]`,
  and `README.md`'s "Current suite version". The AssemblyVersion attribute is the source of truth.
- **Proof this needs a script, not discipline**: an audit caught `README.md` ten versions behind
  (1.39.1 vs 1.40.3) on 2026-08-05. It was fixed by hand — and drifted AGAIN one version later in the
  same session, because the next bump updated AssemblyInfo and CHANGELOG but not the README. A hand-fix
  does not solve a recurring drift.
- Run it after every version bump, before committing: exit 0 = all six agree. It also reports the
  deployed DLL version, informational only (it lags whenever a bump happened after the last deploy).

**A dispatcher pump lets the X button and Esc through — a busy window MUST veto its own close (v1.40.4)**
- `Dispatcher.Invoke(..., DispatcherPriority.Render)` on the calling thread waits by pushing a **nested
  dispatcher frame**, and that frame runs a **real Win32 message loop**. Measured 2026-08-05 both ways:
  a `DispatcherOperation` queued at `Input` priority does NOT run during the loop, **but** a posted
  `WM_CLOSE` fires the window's `Closing` DURING it. So "Render priority means input can't be processed"
  is only half true, and the dangerous half is the untrue one.
- **Consequence**: any window that pumps to repaint progress can be closed mid-run by the title-bar X or
  Esc, while its loop keeps running underneath. **Disabling the buttons does not cover this** — neither
  the X nor Esc goes through a button. Guard `Closing` on a busy flag, and subscribe that guard
  **before** `WindowMotionHelper.AttachStandardExit` so the veto is already on the event args when the
  motion helper runs. `PurgeUnusedElementsWindow` / `PurgeUnplacedViewsWindow` are the reference.
- `AttachStandardExit` now returns early when `e.Cancel` is already true, so a veto from any other
  handler (busy guard, unsaved-changes prompt, validation refusal) is respected instead of being
  overridden by the animation. `toolserify-exit-motion.ps1` has a permanent case for this.
- **Nulling `FocusVisualStyle` obliges the template to draw its own focus ring.** `ModernListCheckBox`
  and `ToggleSwitchCheckBox` inherited `{x:Null}` from `ModernCheckBox` without one, leaving five
  controls with no keyboard marker at all (found by audit, fixed v1.40.4). Check this whenever a style
  sets `FocusVisualStyle="{x:Null}"`.

**Exit animations — `DialogResult` does NOT survive a cancelled close (measured 2026-08-05, v1.40.0)**
- **The finding, and it is the whole reason exit motion is dangerous**: an exit animation must cancel the
  window's own `Closing`, animate, then re-issue the close — and **WPF discards `DialogResult` when a
  close is cancelled**. Measured on real dialogs: set `DialogResult = true`, cancel the `Closing`, close
  again → `ShowDialog()` returns **False**. Every AJ Tools command is written as
  `if (window.ShowDialog() == true) { …do the work… }`, so the naive implementation makes **every Run
  button behave like Cancel** — window opens, window closes, tool silently does nothing, no error
  anywhere. This is a silent, data-shaped failure; never "check it by eye".
- **The fix**: capture `window.DialogResult` **before** setting `e.Cancel = true`, and restore it when the
  animation finishes — assigning it re-issues the close by itself. Wrap both in try/catch: the setter
  throws on a modeless window, where a plain `Close()` is correct instead.
- **Three flags, not one.** `IsExitPlaying` (a close is in flight → keep cancelling), `IsReadyToClose`
  (our own callback asked for the real close → allow), `IsFinished` (issue the close exactly once, since
  the animation's `Completed` and the backstop timer both land there). The two-flag version from the
  About window pass is not enough once a timer is involved.
- **Always arm a backstop timer BEFORE starting the animation.** If the animation never completes the
  window can never close, and a dialog the user cannot dismiss is far worse than no animation. The close
  must never depend solely on an animation completing.
- **Audit `Closing` handlers before attaching an exit to a new window**: cancelling makes `Closing` fire
  2–3 times, so any existing handler runs that many times. As of 2026-08-05 only `PipeSizingWindow` has
  one (`SaveState()`, a full overwrite — idempotent, so repeats are harmless). A handler that appends,
  prompts, or counts would break. Also check nothing outside the window calls `Close()` on it and then
  depends on it being gone (only the AI toast/banner do that, and neither carries this helper).
- `tools\verify-exit-motion.ps1` runs the **real** helper against real dialogs and asserts the returned
  result matches a no-animation control for Run(true), Cancel(false), plain `Close()`, and the
  Click+`IsCancel` double-close. Run it after ANY change to `WindowMotionHelper`.

**Window motion is TWO tiers — do not collapse them into one (2026-08-05, suite v1.39.3)**
- **Working-dialog tier (the default, 33 windows)**: `WindowMotionHelper.AttachStandardEntrance(this)`,
  one call right after `InitializeComponent()`. Content fades in over 220 ms while rising 12 px over
  280 ms, `CubicEase` `EaseOut`. Any new AJ Tools window should get this line — that is the house default.
- **Showcase tier (AboutWindow only)**: the staged ~750 ms entrance with the nav cascade. **Never copy
  this onto a working dialog.** A settings window opened many times a day must feel instant; a staged
  reveal there reads as waiting, not polish. This distinction is the whole point of the split — if a
  future pass "unifies" the two it will make the everyday tools feel slower.
- **Excluded**: `GameHudWindow` (real-time overlay with its own code-behind animation, frame budget
  matters) and the AiShell warning bar (already animated).
- **`Window.Opacity` only has a visual effect when `AllowsTransparency="True"`** — WPF needs a layered
  window for it. Only 7 of 35 AJ Tools windows set that, so the helper animates the window's **root
  content element** instead, which works on every window regardless of chrome style. Don't "simplify"
  it to `Window.Opacity`: it will silently do nothing on most windows.
- **Exit added 2026-08-05 (v1.40.0), on Ajmal's explicit go-ahead** — `WindowMotionHelper.AttachStandardExit(this)`,
  150 ms fade + 6 px sink, `CubicEase` `EaseIn` (exits accelerate, entrances decelerate), on the same 33
  windows. It is shorter than the entrance on purpose. **Read the `DialogResult` section above before
  touching it** — the naive version silently turns every Run button into Cancel.
- The helper skips any window whose root already carries a `RenderTransform`, and every failure path
  restores the window to fully visible — motion can never stop a window from opening.

**Interaction motion lives in the shared style dictionaries — two rules that keep it safe (2026-08-05, suite v1.39.4)**
- Hover / press / focus / enable-disable motion belongs in `src/UI/ModernStyles.xaml` (29 windows) and
  `src/AiShell/Views/SoftUiStyles.xaml` (3 windows), as animated `Trigger.EnterActions` /
  `ExitActions` — **not repeated per window**. One edit there lands everywhere. Only a window carrying
  its own local styles and inheriting nothing (AboutWindow, GraphicsOverrideWindow,
  GameKeySettingsWindow, GameHudWindow) needs its own copy.
- **Rule 1 — never animate `Background` or `Foreground` in a shared style.** A running animation outruns
  a locally-set value, so any window that colours a control from code-behind would break. Real cases in
  this repo: `AboutWindow.ShowSection` sets nav-button `Background`/`Foreground` for the active item, and
  `PipeSizingWindow.ApplyToggleBrush`/`ResetToggle` sets both on its mode buttons. Animate an **overlay
  element's `Opacity`** or a **`RenderTransform`** inside the `ControlTemplate` instead — both are
  template-internal, so no code-behind can collide with them. Keep colour changes as plain `Setter`s: a
  Setter correctly loses to a code-behind local value, which is exactly what's wanted.
- **Rule 2 — one trigger per animated property.** Give hover, press and focus each their own overlay
  element. Several of these states are true at once (hovering while pressed, pressing while focused), and
  if two triggers animate the same property, whichever storyboard ran last wins — behaviour that then
  depends on trigger declaration order and breaks silently when someone reorders them.
- A `RenderTransform` declared **inside a `ControlTemplate`** is safe to animate per control — the
  template's visual tree is instantiated per instance, so each control gets its own transform. This is
  the opposite of the Style-`Setter` case noted below, where one shared instance is handed to every
  element. `Storyboard.TargetName` inside `ControlTemplate.Triggers` resolves in the template namescope,
  so two different templates may reuse the same part name (`HoverGlow`) without clashing.
- House timing (from the `motion-design` skill): hover in 90 ms / out 160 ms, press 110 ms, release and
  settle 200–240 ms, focus ring 140 ms, enable/disable fade 120 ms, dropdown-arrow rotation 180 ms.
  All decelerate through one shared `CubicEase EaseOut` exposed as the resource key `MotionEaseOut`.
  Same "working dialog" philosophy as the window-entrance tier: quick enough to feel instant.
- **Selection stays instant, deliberately** (list items, combo items, tab headers). Selection is often set
  programmatically in bulk when a window loads, so animating it turns a Purge/Transfer list of hundreds
  of pre-ticked rows into a wave. Hover animates; selection does not.
- Before adding motion to any shared style, check that nothing reaches into the template from code —
  `GetTemplateChild` / `Template.FindName`. **This is no longer "zero" (corrected by audit, 2026-08-05):**
  `TabMotionHelper.cs` looks up `PART_SelectedContentHost` on a `TabControl`, added the same day the
  original note was written. So renaming or removing that part silently kills tab motion in all five
  tabbed windows. Re-run the grep before restructuring any template rather than trusting a stale count.
- **Tick boxes / radio buttons / the toggle switch are templated as of v1.40.2** (`ModernCheckBox`,
  `ModernRadioButton`, `ToggleSwitchCheckBox`). They were setter-only until then, drawing raw Windows
  chrome inside the soft UI. They stay **keyed, never implicit**: an implicit `TargetType="CheckBox"`
  style would also capture the `CheckBox` that `DataGridCheckBoxColumn` generates (Duct Standards has
  one), where the animated tick would replay on every scroll. Every original Setter is preserved so no
  layout moved. Checked first that nothing uses `IsThreeState`/`IsChecked="{x:Null}"`, so the templates
  need no indeterminate visual — re-check that if one is ever added.
- Both dictionaries now define the same `MotionEaseOut` key with the same timings **on purpose**, so the
  AI shell and the tool windows feel like one product. Retune both together or neither. The four
  standalone windows that received motion (About, Graphics Override, Game Key Settings) each declare
  their own `MotionEaseOut` with the same curve — they merge nothing, so there is no shared key to
  reach. Game HUD does NOT declare one: it was left untouched and has no motion styles at all.
- **"One trigger per animated property" is the rule that actually bites.** It was violated once during
  the v1.39.6 pass (the Graphics Override colour swatch had hover AND press driving one shared
  `ScaleTransform`): press, then drag off the control, and BOTH exit animations fire — whichever lands
  last wins, so the control can be stranded mid-state. Fixed with two transforms in a `TransformGroup`,
  one per trigger. Any control where two states can be true at once needs this.
- **Motion goes on what the user CHANGED, never on what merely appeared.** Selection in the shared lists,
  and the tick in Graphics Override's category checkboxes, stay instant — those containers are created
  and recycled in bulk by a virtualized `ListBox`, so an animated state would replay on every scroll and
  read as flicker. The standalone `CutLinkCheckBoxStyle` tick does animate, because it is a single
  checkbox the user clicks. Judge each case by "does this fire on user action, or on materialization?"
- **Not every window needs motion — but get the REASON right.** `GameHudWindow` is excluded because it
  is a real-time overlay with its own code-behind animation and a frame budget. **CORRECTION (audit,
  2026-08-05):** an earlier version of this note claimed it "has every element set
  `IsHitTestVisible="False"` … a pure non-interactive overlay". That is WRONG and backwards. Its root
  `RootGrid` carries `Background="#01000000"` — 1/255 alpha — precisely so it *does* receive every click
  and key over the whole Revit view; `PlayLayer` has no hit-test attribute either; and `PauseLayer` is a
  click-to-resume surface with `Cursor="Hand"` wired to `PreviewMouseDown`. Only the "no `<Button>`"
  half was true. Do not reason about the HUD's input from that old claim.
- **Two windows keep instant hover colours on purpose, for two different reasons.** Graphics Override:
  its hover steps carry meaning and a neutral wash can't reproduce them (12% white over the danger fill
  `#5B1C1C` gives `#692C2C`, nowhere near the intended `#8B2B2B`). AboutWindow: `ShowSection()` sets the
  active nav button's `Background`/`Foreground` from code-behind, so those must stay Setters. Both get
  motion through transforms instead — About's sidebar slides, Graphics Override's controls dip and grow.

**Verifying a window that keeps its styles in its own `<Window.Resources>` — `tools\verify-window-styles.ps1`**
- The pack-URI trick used by `verify-wpf-styles.ps1` only reaches standalone `ResourceDictionary` files,
  and instantiating one of these windows directly would drag in Revit. This script instead lifts the
  `<Window.Resources>` block out of the XAML **source**, re-parses it as a standalone dictionary with
  `XamlReader.Parse`, then applies every `Style`/`ControlTemplate` to a real control and forces
  `ApplyTemplate()`. 35 styles across About / Graphics Override / Game Key Settings pass as of v1.39.6.
- It discovers what to test from each entry's `TargetType`, so **new styles are covered automatically** —
  there is no list to keep in sync, unlike the shared-dictionary script.
- Works because a `<Window.Resources>` block never references code-behind (no `Click=` handlers live in
  it). If a future window puts something code-behind-dependent in its resources, that entry will fail to
  parse — that is a finding, not a script bug.

**Verify a restyled window WITHOUT launching Revit — `tools\verify-wpf-styles.ps1` (built 2026-08-05)**
- A clean `msbuild` proves the XAML parses; it does **not** prove the styles work. BAML compilation never
  resolves `{StaticResource}` against the runtime resource tree, so a missing key compiles fine and throws
  `XamlParseException` the first time a template is applied — the failure mode that took the whole add-in
  down during `OnStartup` in v1.16.0. The script closes that gap: it loads both compiled dictionaries out
  of the built DLL by pack URI and forces every style to build its `ControlTemplate`, which is the moment
  WPF resolves the resources inside it. All 28 styles pass as of v1.39.5.
- Run it after ANY edit to `ModernStyles.xaml` / `SoftUiStyles.xaml`, and add new style keys to its lists:
  `powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File tools\verify-wpf-styles.ps1` (exit 0 = clean).
  **`-STA` is required** — WPF refuses to create controls on an MTA thread.
- **PowerShell trap that cost a debug cycle**: `ResourceDictionary` implements `IDictionary`, so
  `$rd.Source = $uri` adds a dictionary **entry** named "Source" instead of setting the property, and the
  dictionary silently loads with 1 key and no error. Set it through reflection:
  `[System.Windows.ResourceDictionary].GetProperty('Source').SetValue($rd, $uri, $null)`.
  Same trap applies to any `IDictionary`-derived WPF type touched from PowerShell.
- A style that sets no `Template` (colours only) legitimately builds no visual tree outside a real window —
  don't score that as a failure; the script checks the style's own and inherited setters first.

**`ProgressBar` can be safely retemplated — the control does the width math (verified 2026-08-05)**
- An earlier note in `SoftUiStyles.xaml` claimed a custom `ProgressBar` template "needs real Track-width
  math to avoid silently showing wrong progress". **That is wrong** — corrected in place. Reflection over
  the real `PresentationFramework.dll` shows `ProgressBar` declares
  `[TemplatePart] PART_Track / PART_Indicator / PART_GlowRect` and sizes the indicator itself in its
  private `SetProgressBarIndicatorLength()`. A custom template only has to use those part names.
- Measured on the house bar (`ModernStyles.xaml` v1.3.0) to confirm: on a 200 px track, values 25/50/100
  give exactly 50/100/200 px of indicator.
- The one real caveat: **indeterminate**. WPF's default chrome carries its own sliding-glow animation, and
  a custom template loses it — supply your own (the house template pulses the indicator's opacity). This
  is why the AI shell's `ProgressBarStyle` deliberately keeps default chrome: `AiShellView`'s busy strip is
  `IsIndeterminate="True"` and its animation already works.

**Tab-change motion — `TabMotionHelper`, and the routed-event trap it exists for (2026-08-05, v1.39.7)**
- `TabMotionHelper.AttachTabTransitions(this)`, one call after `InitializeComponent()`, gives every
  `TabControl` in the window a fade (180 ms) + 8 px rise (220 ms) on tab change. Wired into the five
  tabbed windows: Colorize, Duct Standards Manager, Filter Pro, Graphics Override, Location Data
  Assigner. Deliberately shorter than the 220/280 ms window entrance — a window opens once, a tab is
  clicked repeatedly in one sitting.
- **The trap: `Selector.SelectionChanged` is a ROUTED event.** A `ComboBox`/`ListBox` *inside* a tab
  bubbles its own selection change up to the `TabControl`, so a naive handler replays the entire tab
  transition every time the user picks a value from a dropdown inside the tab — and four of these five
  windows are full of dropdowns. Guard by requiring `ReferenceEquals(e.OriginalSource, tabControl)`.
  Never set `e.Handled` — these windows have their own `SelectionChanged` logic that must still run.
  `tools\verify-tab-motion.ps1` is the regression guard: it asserts a dropdown change does NOT animate
  while a tab change does, and that neither selection is disturbed.
- **`PART_SelectedContentHost` is a `ContentPresenter` in all three cases** — the default WPF `TabControl`
  template, `ModernStyles.xaml`'s implicit style (setters only, so still the default template), and
  `GraphicsOverrideWindow`'s own custom template. Verified at runtime 2026-08-05, which is why one helper
  covers every tabbed window. A future window that templates its `TabControl` with a different part name
  just gets no transition — the helper finds nothing and does nothing.
- Attaches by walking the visual tree **on `Loaded`** (templates are guaranteed applied by then) rather
  than by `x:Name`, so no XAML has to change and a window with two `TabControl`s gets both.
- **Show/hide panel transitions were considered and rejected** (Reassign Level's scope toggle, View Crop
  options, Purge lists): most of those windows are `SizeToContent`, so animating a panel in or out makes
  the whole window resize mid-animation. If one is ever wanted, give that window a fixed size first.

**Rounded-corner windows — `CornerRadius` does NOT clip children (found on the About window, 2026-08-05)**
- **A WPF `Border` draws its own rounded corner but does not clip its child content to it.** Any child
  that reaches a window corner paints a SQUARE corner straight over the curve. On the About window the
  sidebar had `CornerRadius="22,0,0,22"` so the LEFT corners looked right, while the header and footer
  bars had none — so the top-right and bottom-right rendered square inside a rounded outline.
  **Fix**: give every corner-touching child its own matching `CornerRadius`. Cheaper and far more
  predictable than the `OpacityMask` + `VisualBrush` clipping trick, which also costs a full-surface
  composite every frame — bad when the window already animates its opacity.
- **Concentric radius rule: inner radius = outer radius − BorderThickness.** The child is laid out
  inside the shell's border, so against a `CornerRadius="22"` + `BorderThickness="1"` shell the children
  must use **21**, not 22. Using 22 bleeds a hairline of child colour outside the shell's curve.
- **`ResizeMode="CanResizeWithGrip"` is wrong for a rounded window** — the dotted grip is drawn by the
  window chrome at the square bottom-right, outside the curve. Use `CanResize`: resizing from every edge
  and corner still works, only the glyph goes.
- **A maximized `WindowStyle="None"` + `AllowsTransparency="True"` window must flatten its corners to 0**,
  or the desktop shows through all four. Hook the window's `StateChanged` (NOT the maximize button's
  Click) so header double-click, Win+Up, and any external restore are all covered. See
  `AboutWindow.AboutWindow_StateChanged`.
- `WindowState == System.Windows.WindowState.Maximized` — fully qualify, same reason as the
  `Visibility` CS0176 note above (`Window` has an instance property of the same name).
- **`WindowChromeHelper.ApplyStateChrome(window, rootBorder)` is now the single place that reconciles a
  borderless window's shell with its state** (shadow margin + corner radius). It remembers each border's
  design radius in an attached property, so a window with a different radius keeps its own. Call it from
  the window's **`OnStateChanged` override**, not only from the maximize button — `ToggleMaximize` is
  bypassed entirely by Win+Up and top-edge snap. Don't re-implement this per window.
- **Audit result, 2026-08-05 — do not re-audit this from scratch.** All 38 XAML files under `src/` were
  checked. `AllowsTransparency="True"` matched 10, but **3 are false positives**: `ModernStyles.xaml` and
  `SoftUiStyles.xaml` (ResourceDictionaries) and `LinkedSearchWindow.xaml`, where it sits on a `Popup`
  inside a dropdown template — that window uses standard OS chrome and has no custom corners at all.
  Of the 7 real `WindowStyle="None"` windows, **AboutWindow was the only one with the clipping defect**.
  The others are correct for a specific reason each: `GameHudWindow` (root `Padding="12,8"` insulates the
  corners, `NoResize`), `GraphicsOverrideWindow` (`CornerRadius="0"` by design), and the 4 View Crop
  windows (every filled `Border` already rounds itself; their title bar is a `Background="Transparent"`
  hit-test `Grid`, which paints nothing and so cannot square a corner). The View Crop four DID need the
  maximize fix above — they all call `WindowChromeHelper.ToggleMaximize` from a real maximize button.

**Skill routing note (2026-08-05)**
- The `ui-ux-pro-max:ui-styling` plugin skill is React / Tailwind / shadcn only — **not applicable to this
  repo**, and `revit-ui-design` explicitly forbids a web UI stack for Revit add-ins. For AJ Tools window
  work use `revit-ui-design` (Neumorphism + Claymorphism + Neon Blue house style) and, for animation,
  the `motion-design` skill. Don't burn a turn loading `ui-styling` for WPF.

**Build command correction — the .sln only knows `Release`/`Debug` (found 2026-08-05)**
- `AJ Tools.sln` carries ONLY `Debug|Release` x `Any CPU|x64|x86`. The per-version configs
  (`Release R21` … `Release R27`) are **project-level**, from `Directory.Build.props`. So the
  Definition-of-done command in CLAUDE.md works for the 2020 baseline but **fails for every newer
  config** with `MSB4126: The specified solution configuration "Release R25|x64" is invalid`.
- Build newer versions against the **csproj**, not the sln:
  `msbuild "src\AJ Tools.csproj" -p:Configuration="Release R25" -p:Platform=x64 -p:SkipAjToolsAutoDeploy=true`
- `msbuild` is not on PATH on this machine. Resolve it with
  `& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"`
  → currently `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`.
- **`Release R27` cannot be built on this machine right now**: the installed .NET SDK is 9.0.316 and R27
  targets `net10.0-windows` → `NETSDK1045: The current .NET SDK does not support targeting .NET 10.0`.
  This is an environment gap, not a code fault — don't chase it as a regression. R25 (`net8.0-windows`)
  is the newest config that currently compiles here.

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


**Reaching AJ Tools from outside Revit — the Web Panel rules (established v1.43.0, 2026-08-12)**
- **A localhost `HttpListener` needs NO admin rights and NO URL ACL — only the wildcard does.** Measured
  on Ajmal's machine as a standard non-admin user: `http://localhost:5599/` starts fine; `http://+:5599/`
  throws `Access is denied`; a raw `TcpListener` on 127.0.0.1 also works. `McpBridgeService`'s header note
  used to state the opposite as the reason the AJ AI bridge chose a named pipe — **corrected in place**.
  The pipe is still right for the AI bridge (no port to pick, unreachable from a browser by construction),
  but never repeat the old claim as a reason to rule out a loopback HTTP server. A localhost-only prefix
  also raises no Windows Firewall prompt, because it is not reachable from another machine.
- **Names, not code.** The browser sends a tool **id** from a fixed registry compiled into the add-in
  (`WebPanelToolRunner.RegisteredTools`) — never C#, a path, or a script. So the worst a hostile page can
  do is press a button that is already on the ribbon. This is deliberately narrower than the AJ AI bridge,
  which *does* accept code (guarded by `GeneratedCodeSafetyValidator`). **Do not widen the registry to
  "run whatever was sent" when the download-tools-from-a-website idea gets built** — that step needs a
  signature check (Ajmal signs each tool, the connector verifies before running) designed first, or a
  hacked website becomes code execution on every colleague's PC.
- **Two defences, because each alone has a hole**: a per-session token injected into the served page, AND
  an `Origin` header check. A hostile page in another tab cannot read the token (CORS blocks it reading
  the response) but could otherwise fire blind requests — the Origin check stops exactly that. Neither
  stops another *program* running as Ajmal; nothing can, and the named pipe has the same property.
- **Serve the page from the listener itself.** Same origin means no CORS and no mixed-content fight — the
  problem an https website would hit trying to reach `http://localhost`. The page then asks `/api/tools`
  for its buttons rather than hardcoding them, so adding a registry entry makes a button appear with no
  HTML edit. Keep that property; it is what the "post a tool, everyone gets it" plan rests on.
- **One logic, two front doors.** A tool reachable from both the ribbon and the panel keeps its model work
  in a service that **returns** its report (`UnhideAllService` → `UnhideAllResult.Summary`) and shows
  nothing. The command turns that into a `TaskDialog`; the panel returns it as JSON. **A `TaskDialog`
  raised from shared code is the trap here** — triggered from a browser it appears on the Revit screen and
  blocks Revit until somebody physically walks over and clicks it, while the person watching the browser
  sees nothing. Any further tool added to the panel must be split this same way first.
- `ProcessStartInfo(url) { UseShellExecute = true }` — required to open a URL at all on .NET 8
  (Revit 2025+), where the plain `Process.Start(string)` overload defaults `UseShellExecute` to false and
  throws on anything that is not a real executable path. Identical behaviour on .NET Framework, so one
  code path covers 2020–2027.
- Port is picked by **binding**, walking a range (48210–48229) and keeping the first that starts —
  checking a port then binding it is a race. Two Revit versions open at once therefore each get their own.

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

**Every AJ Tools UI surface, and the two a `.xaml` sweep MISSES (enumerated 2026-08-05, v1.40.5)**
- **35 XAML `<Window>` files** under `src/` — 33 carry `AttachStandardEntrance` + `AttachStandardExit`;
  `AboutWindow` has its own staged pair; `GameHudWindow` is excluded (real-time overlay, frame budget).
- **1 dockable `UserControl`** — `AiShell/Views/AiShellView.xaml`. Styled via `SoftUiStyles`, no entrance
  motion by design (it is constructed during Revit's `OnStartup`; a fault there kills the whole add-in).
- **2 windows built entirely in C# with NO `.xaml`** — `AiShell/Helpers/BridgeStatusToast.cs` and
  `AiShell/Services/AiTaskWarningBarService.cs`. **These are the blind spot**: every sweep that finds
  windows by globbing `*.xaml` misses them, which is exactly how `BridgeStatusToast` went through the
  whole motion pass with no animation at all. When auditing UI coverage, search for `new Window` /
  `: Window` in `.cs` files that have no matching `.xaml`, not just the XAML.
- **Zero WinForms UI remains** anywhere in `src/` (confirmed 2026-08-05).
- `BridgeStatusToast` animates `Window.Opacity` **directly** — correct there because it sets
  `AllowsTransparency = true`. Do not copy that to a normal window: most AJ Tools windows do not set it,
  which is the whole reason `WindowMotionHelper` animates the root content element instead.

**Adding an AI provider to the "C#" shell — the five places (done 4x: Gemini, OpenAI, Claude, NVIDIA)**
`IAiProviderService` is a 3-member contract (`ProviderName`, `SendMessageAsync`, `IsConfigured`), so a new
provider is additive and touches exactly five files. In order:
1. `AiShell/Services/<Name>ApiService.cs` — the service itself.
2. `AiShell/Configuration/AiShellConfig.cs` — `Encrypted<Name>ApiKey` + `Set/Get` pair (DPAPI, per-user)
   and a `<Name>Model` default.
3. `AiShell/ViewModels/AiShellViewModel.cs` — field, ctor param, load-on-construct, one line in
   `GetActiveService()`, `Is<Name>Selected`, key/model properties, and the `SaveSettings()` writes.
   Also add `OnPropertyChanged(nameof(Is<Name>Selected))` to the `SelectedProvider` setter — forgetting
   it means the Settings panel never shows when that provider is picked.
4. `AiShell/Views/SettingsWindow.xaml` + `.xaml.cs` — dropdown entry, key `PasswordBox` +
   `PasswordChanged` handler + the `ShowKeyToggle_Click` `Tag` case, model picker.
5. `AiShell/DockablePane/AiShellPaneProvider.cs` — construct it and pass it to the ViewModel ctor.

`ErrorCorrectionService` needs **no** change — it takes whichever `IAiProviderService` it is constructed
with, so it follows `GetActiveService()` automatically. `SelectedProvider` is a plain string compared in
`GetActiveService()`; the string in the XAML `ComboBoxItem` must match it exactly.

**TRAP: `SoftComboBoxStyle` cannot be made editable (found 2026-08-05, v1.41.0)**
`AiShell/Views/SoftUiStyles.xaml` replaces the `ComboBox` `ControlTemplate` and that template contains
**no `PART_EditableTextBox`** — only a hit-test-disabled `ContentSite` presenter. So setting
`IsEditable="True"` on any ComboBox using this style renders a control with **nothing to type into**, and
it fails silently: it builds clean, `verify-wpf-styles.ps1` still passes (the template itself is valid),
and the damage only shows when a user tries to type. `ModernStyles.xaml` should be assumed to have the
same shape until checked.
**Do not "just add the part" to the shared dictionary.** `SoftUiStyles` is merged by `AiShellView`, which
`AiShellPaneProvider` constructs during Revit's `OnStartup` — a fault there takes the WHOLE add-in down,
not just that pane (it did once, v1.16.0). The house pattern when a free-text-plus-shortlist picker is
needed is **two controls**: a non-editable ComboBox of the shortlist and a plain `SoftTextBoxStyle`
TextBox, both bound `TwoWay` to the same property. Picking fills the box; typing anything else just
leaves the ComboBox unselected, which is the correct display for "custom value". See the NVIDIA model
picker in `SettingsWindow.xaml` for the worked example.

**Reasoning models are not drop-in chat models (found 2026-08-05, v1.41.0)**
When a provider's default model *thinks* before answering (GLM, DeepSeek-R1, Qwen thinking modes), four
settings copied from a chat-model service are wrong, and all four fail in ways that look like something
else:
- **Timeout.** The other three services share a 60s `HttpClient`. A reasoning model can exceed that
  legitimately, and `HttpClient` reports its own timeout as `TaskCanceledException` — identical to the
  user pressing Stop unless `cancellationToken.IsCancellationRequested` is checked. Give the service its
  own client (raising the shared one would slow every provider) and catch that case explicitly.
- **`max_tokens`.** Reasoning tokens spend the SAME budget as the answer, so a cap sized for a chat
  model truncates the generated script *after* the allowance went on thinking. Check
  `finish_reason == "length"` and say so plainly rather than returning an empty/half reply that fails
  later as a confusing compile error.
- **`temperature`.** Clamping it (the 0.2 `OpenAiApiService` uses) degrades the reasoning chain. Use the
  provider's own published sample value.
- **`seed`.** NVIDIA's sample sets `seed=42`. Never copy that here — a fixed seed makes a retry return
  the IDENTICAL broken script, and the auto-fix retry loop depends on a retry being a fresh attempt.
Reasoning text arrives either in a separate `reasoning_content` field (clean — the normal parse already
skips it) or inlined as `<think>…</think>` (must be stripped, or `CodeExtractionHelper` reads it as script).
