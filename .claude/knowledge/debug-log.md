# Debug Log - AJ Tools

Bugs found and fixed, separate from `ajtools-conventions.md` (which is coding rules, not bug history).
Read this before investigating a new symptom - it may be a repeat. Append a short entry after every fix.

Format per entry: **Symptom (Ajmal's words)** -> **Root cause** -> **Fix** -> **Verified how** (live via
AutoDebugger, or code-review only) -> date.

## Log

### 2026-08-16 (Auto MEP Dimension: two ducts to one wall left two stacked dimensions — FIXED, suite 1.48.2)
- **Symptom (Ajmal's words)**: "dimention everyrun in the view tool its not understanding Already
  dimensioned earlier ... 2 duct need to do same wall so if the inside duct first dimention and 2 duct
  dimeiton if run it will come for that also but onld one also will be there so 2 diemntion will be
  there. and also for the if outside duct took first and dimeiton the inside duct no need to dimention
  becouse this already include."  Two requirements in one message: (a) the longer wall-inner-outer chain
  must REPLACE the short wall-inner dimension, and (b) a run already carried inside an existing chain
  must be SKIPPED.
- **Root cause — a placement test gating an identity question.** All three duplicate/supersede paths
  (`HasSimilarExistingDimension`, `FindSupersededOwnedDimensions`, and the in-run `createdRecords`
  filter) ran through `RecordsOverlap`, which required `Math.Abs(existing.RunCoord - proposed.RunCoord)
  <= bandTolerance * 1.25` — **187.5 mm** on the shipped 150 mm search band. `RunCoord` is where the
  dimension string sits **along the run**, and `TryCreateDimensionLine` places every chain through its
  own seed run's midpoint (`plan.Axis.Origin`, the seed's mid-length). Two parallel ducts of different
  lengths therefore put their strings **metres** apart down the run, so `RecordsOverlap` returned false
  and the real test — `AddsNothingNew`, a strict subset test over the references — was **never reached**.
  The machinery was all present and correct; a geometric pre-filter in front of it made it unreachable.
- **Why it looked like the "already dimensioned" check was broken**: the run-level gate
  (`ExistingDimensionCoversElement`) does work, so a second run adds nothing new. What Ajmal saw was the
  FIRST run leaving two dimensions and no later run ever able to tidy them up — `FindSupersededOwnedDimensions`
  is behind the same broken gate.
- **Fix** (`MepReferenceDimensionService` v1.1.0, `MepDimensionModels` v1.1.0):
  1. `RecordsOverlap` dropped the along-the-run station test entirely; it now only asks "same measuring
     direction, overlapping measured extent". Containment is decided by the references, where it belongs.
  2. `AddsNothingNew` gained an **element-identity fallback**. The face-key test compares
     `Reference.ConvertToStableRepresentation` strings, which is exact for two records built in the same
     collector pass but **cannot be relied on for a dimension read back out of the model** — Revit does
     not promise `Dimension.References` hands back the same string that was passed to `NewDimension`. The
     fallback compares `"linkInstanceId:elementId"` sets (via the existing `GetReferenceTargetKey`), which
     survives that round trip, and additionally requires the proposed span to sit INSIDE the existing one
     so the two halves of a `DimensionBothSides` run — same elements, opposite directions — are never
     mistaken for each other.
  3. `DimensionLineRecord.RunCoord` removed (its only reader was the deleted test) and `RunDirection`
     removed (set in three places, **read nowhere** — already dead before this change).
- **Deliberately unchanged**: `DimensionOwnership.IsOwned` still gates every delete, so a dimension drawn
  by hand is never removed however well it matches (Ajmal's rule, 2026-08-15).
- **Lesson — the general trap, and it is the third time this repo has hit this shape**: when a cheap
  geometric pre-filter guards an expensive identity/content test, the pre-filter silently decides the
  outcome. Ask what the guard is actually measuring: here "are these two strings drawn in the same place"
  was standing in for "does one of these document everything the other does", and those are different
  questions. Same family as the 1.47.2 sign error (a tie-break chosen on travel distance silently decided
  bend direction) and the 1.47.0 pair (two places holding one rule). **If a filter and the test behind it
  answer different questions, the filter must be provably weaker than the test, not merely plausible.**
- **Verified how**: code-review only, plus clean rebuilds at `Release` (2020/net472) and `Release R25`
  (.NET 8), zero errors / zero warnings, and `tools\verify-version-consistency.ps1` clean on all six
  references (it caught CHANGELOG.md and README.md still at 1.48.1). Deployed to the 2020 AppData payload
  `AJ Tools.20260816173635439`, manifest and DLL read back at **1.48.2.0**. **The AJ AI Bridge was NOT
  connected this session (ping refused), so nothing was checked against the live model, and Revit was open
  during the deploy — the running session is still on 1.48.1.** Ajmal restarts Revit and re-tests the
  two-duct case both ways round. **Not click-tested by me.** → 2026-08-16.
- **Released publicly the same session, at Ajmal's explicit choice** (asked test-first vs release-now; he
  picked release-now, having been told plainly it was not yet exercised in Revit — the same call he made
  for 1.48.1). Ran clean end to end: all 9 payloads read back at 1.48.2.0 before packaging, checksum
  matched, Actions published in well under a minute, and the **published asset was downloaded and
  re-hashed — byte-for-byte identical** to the local zip and to the published `SHA256SUMS.txt`
  (`3180be14…59e`). Release notes came through non-empty and correct. Source tag `v1.48.2`, installer tag
  `v1.48.2`, all three repos clean and in sync (AJ AI Brain needed nothing).
- **Deploy gotcha worth keeping — `Release R27` cannot be built from this shell with VS MSBuild.** It
  fails `NETSDK1045: The current .NET SDK does not support targeting .NET 10.0` because the machine-wide
  SDK on PATH is 9.0.317. `package.ps1` already knows this and routes the 2027 build through the
  **user-local** SDK at `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`, which has .NET 10. For a manual
  R27 build or deploy, use that dotnet.exe directly
  (`& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build "src\AJ Tools.csproj" -c "Release R27"`) —
  the VS MSBuild path in the 2026-08-05 note below works for every OTHER config but not this one.
- **Only three Revit versions are actually installed on this machine** (2020, 2024, 2027 — checked under
  `C:\Program Files\Autodesk\`), even though AppData carries manifest folders for all eight years because
  the deploy target creates them. All three were brought to 1.48.2.0; the unused year folders were left
  behind, which is why a version sweep across all eight always shows stale entries that do not matter.
  `package.ps1` builds all eight regardless, and that is what the public zip ships. → 2026-08-16.

### 2026-08-16 (Connect MEP Elements settings: Main tab clipped and the window could not be resized — FIXED, suite 1.48.1)
- **Symptom (Ajmal's words, with two screenshots)**: "chek this the main page and that reszing in the
  corner we can drain and resize that also not working." The Main tab's last card ("Warn me if the new
  run hits something") was cut off behind the footer, and dragging the window edge did nothing. The
  Advanced tab, in the same window, had ~150px of empty space.
- **Root cause — a height ESTIMATE written into the design, with no safety net.** 1.48.0 deliberately
  made this window `ResizeMode="NoResize"` at a fixed 560 x 700 with no ScrollViewer, and the XAML
  header comment recorded the reasoning: "Main currently needs about 490" of "roughly 500px usable".
  That number was estimated by reading the markup, never measured on screen, and it was ~60px short —
  radios, wrapped hint lines and card padding each cost a few px more than assumed. With no scrollbar
  AND no resize, there were two independent ways to see the hidden control and both had been removed
  in the same change, so the shortfall was invisible until Ajmal opened it.
- **Fix**: 560 x 780 default, `ResizeMode="CanResize"` with `MinWidth`/`MinHeight`, and each tab's
  content wrapped in a `ScrollViewer` (`VerticalScrollBarVisibility="Auto"`, horizontal disabled,
  `Margin="0,0,8,0"` on the inner StackPanel so cards clear the scrollbar). No control, label,
  tooltip or setting changed. This is exactly the pattern the other **seven** settings windows in the
  repo already use — checked by grep, not memory: CreateTags, FlowDirection, MepDimension,
  AutoDatumDimension, MepOpening, SmartMepTag, RevisionCloud and ArrangeTags are all
  `CanResize` + `SizeToContent="Manual"` + Min sizes, and the ones with long bodies scroll.
  SmartConnectWindow was the only exception in the suite.
- **Lesson**: a fixed-size window is only safe if the content height was MEASURED at 100% DPI, and
  even then it breaks at larger Windows text scaling. Keep the ScrollViewer as the safety net even
  when the content is expected to fit — a hidden control with no scrollbar and no resize grip gives
  the user no way to discover anything is missing. If a future window really must be fixed-size,
  measure it running, don't estimate it from the markup.
- **Caught in the same pass**: `tools\verify-version-consistency.ps1` reported the AssemblyInfo header
  and README.md still at **1.47.8** — the 1.48.0 change bumped the attributes and the changelog but
  not those two. Both corrected to 1.48.1. Worth running that script after every version bump; it
  exists precisely because these two are easy to miss.
- **Verified how**: clean build at `Release` (2020), zero errors / zero warnings, then deployed to the
  AppData 2020 payload `AJ Tools.20260816170942719` with the manifest and DLL read back at 1.48.1.0.
  Revit was open during the deploy, so **the running session is still on 1.48.0** — Ajmal restarts
  Revit, opens the settings window and confirms the Main tab shows the clash card and the window
  drags bigger. **Not click-tested by me.** → 2026-08-16.
- **Released publicly the same session, at Ajmal's explicit instruction** ("release it now", chosen over
  the recommended test-first option — he was told plainly the build had not been exercised in Revit).
  The public installer had been stuck at **v1.44.0** since 2026-08-13 while source ran to 1.48.1, so
  the release notes had to cover 1.45.0-1.48.1 in one entry, written from the FINAL behaviour rather
  than by concatenating the intermediate changelogs — 1.47.0 shipped batch pairing, routing modes and
  a "Neither" option that 1.47.5-1.48.0 then removed, so copying those bullets forward would have
  described features the download does not contain. Worth repeating on any future catch-up release.
  Ran clean end to end: package.ps1 built all 8 years (~9 min, `SkipAjToolsAutoDeploy` is built into
  it, so Revit staying open did not matter), all 9 payloads read back at 1.48.1.0, checksum matched,
  Actions published in 14s, and the **published asset was downloaded and re-hashed** — identical to
  the local zip. Source tag `v1.48.1`, installer tag `v1.48.1`, all three repos clean and in sync.
- **Process gotcha**: the shell's working directory persists between commands, so a `cd` into
  `AJ-Tools-Installer` earlier in the session made `.\dist\create-tag.ps1` fail with "does not exist".
  `Set-Location` back to the repo root before the source-repo steps in `RELEASE_PROCESS.md`.

### 2026-08-16 (Connect MEP Elements v3: Copy Workset had NEVER worked, plus a settings/behaviour rebuild — FIXED, suite 1.48.0)
- **The bug worth remembering**: `CopyWorkset` (and `CopyInstanceParameters` before it) copied
  `BuiltInParameter.ELEM_PARTITION_PARAM` through a helper that guards on
  `StorageType != StorageType.ElementId` and returns early. **Workset is Integer storage, not
  ElementId** - it holds the workset id as a plain int. So the guard tripped on every single call and
  the copy silently did nothing, in v2 as well as v3. Nothing threw, nothing warned, the setting
  looked live in the UI, and the build was clean the whole time.
- **Why it survived so long**: a helper named `CopyElementIdParameter` reads as obviously correct for
  a parameter that "points at a workset". The storage type is the thing that matters, and it is not
  visible at the call site. **Lesson: when copying a BuiltInParameter, confirm its StorageType from
  the API rather than inferring it from what the parameter conceptually refers to.** A guard that
  silently returns is invisible; prefer one that is impossible to get wrong, or verify the pairing.
- **Found by**: a 5-dimension multi-agent housekeeping audit (24 raised, 18 confirmed after
  adversarial verification). NOT by the build, and not by reading the diff - the call site looked
  perfectly reasonable.
- **Behaviour rebuilt in the same pass, at Ajmal's instruction** ("instead of creating new pipe or
  duct, stretch the elements like that we need"): `SmartConnectRoutingMode` deleted entirely. The tool
  now always stretches the picked elements; a piece is created only where nothing can be stretched -
  the bridging run across a crank, and the run up to flex/equipment (`CanTrimEnd` false).
- **The subtle half of that change**: `mayMove` conflated two different questions - CAN this end be
  stretched, and is it ALLOWED to be. With `MoveMode.FirstOnly`, a perfectly stretchable second
  element hit the insert path and got a new piece bolted onto it, which is precisely the behaviour
  being removed. Now split into `canTrim*` (physical) and `mayMove*` (permission): a locked-but-
  stretchable end refuses with a message naming the setting, and only a genuinely un-stretchable end
  gets a piece.
- **Also removed as dead**: `SmartConnectRoutePlan.FirstDirection`/`SecondDirection` and
  `ConnectionOutcome.Label` (all write-only), plus a `&& !result.Warnings.Any()` guard that suppressed
  the "built at X instead of your Y" notice whenever any unrelated warning was present.
- **Convention gap caught**: this window was the only tabbed window in the repo not calling
  `TabMotionHelper.AttachTabTransitions` - the other five all do. Worth checking that helper list when
  adding a TabControl to any AJ Tools window.
- **Verified how**: the workset storage-type claim checked against the real Revit 2020 API before
  fixing, not taken on the auditor's word. Clean builds at Release (2020) and R21/R24/R25/R26, zero
  warnings. **Not yet exercised in Revit** - Ajmal to test.

### 2026-08-16 (Connect MEP Elements: split button got stuck showing Settings as the default face — FIXED, suite 1.47.7)
- **Symptom (Ajmal's words)**: "after that can you change the button configuration - no, if i select
  the settings it will come like settings first, and i need the settings inside always, like [the]
  split button for the opening pane; create opening tool - so settings will be inside always, connect
  only it will come." He wanted the button to behave like the Opening panel's "Create Openings"
  split button, where the main face never changes away from the primary action.
- **Root cause**: Revit's `SplitButton.IsSynchronizedWithCurrentItem` defaults to `true`, which makes
  the button's TOP face permanently switch to whichever child was clicked most recently. `AddSmartConnectTool()`
  never set this to `false`, so opening the dropdown and clicking "Connect MEP Elements Settings" once
  made Settings the new default - the next plain click on the button ran Settings again, not Connect.
  `AddMepOpeningsTool()` had already solved this exact problem with a one-line
  `splitButton => splitButton.IsSynchronizedWithCurrentItem = false` configuration and a comment
  explaining why - the Connect MEP Elements button simply never got the same treatment when it was
  built.
- **Fix**: added the identical configuration line to `AddSmartConnectTool()`, matching the Opening
  tool's pattern exactly (including comment wording), so the top face is now permanently pinned to
  "Connect MEP Elements" regardless of which child ran last.
- **Lesson**: this is another instance of the pattern already logged twice above for this feature -
  the fix already existed elsewhere in the ribbon (the Opening panel), and the new tool just didn't
  inherit it. When building a split/pulldown button, check an existing one in the same ribbon for this
  exact `IsSynchronizedWithCurrentItem` setting before assuming the default behaviour is fine.
- **Verified how**: found by reading `AddMepOpeningsTool()` directly and comparing line-by-line
  against `AddSmartConnectTool()`, not from memory of how SplitButton works. Clean build, zero
  warnings. **Not yet exercised in Revit** - Ajmal to test after this deploy.

### 2026-08-16 (Connect MEP Elements: dead code left behind by the 1.47.5 removals — CLEANED UP, suite 1.47.6)
- **Prompted by Ajmal, directly**: "check entirely we remove something so with remove ites is there
  anything related with that removed feature settings remove. also maybe there is one settings is
  there that will work only if that remove features if we already revie that settings also we can
  remove am i right so if anything like that remove it." A genuine ask to sweep for orphaned code
  after a feature removal, not just trust that deleting the obvious call site was enough.
- **Method**: grepped for every remaining textual reference to what 1.47.5 removed (the auto-pairing
  algorithm and `SmartConnectMoveMode.None`) across the whole repo, then read every property/method
  those removals touched to check whether anything downstream still consumed it. Did not rely on
  memory of what I wrote - re-grepped each candidate's call sites fresh.
- **Found, all confirmed dead by an actual empty-caller-list grep, not assumed**:
  1. `ElementPair.Distance` (`SmartConnectCommand.cs`) - both construction sites passed a literal
     `0`, and nothing anywhere read `.Distance`. It existed solely for `BuildNearestPairs`' sort,
     which 1.47.5 deleted.
  2. `ShowSummary`'s `extraNotes` parameter and its 2-argument overload (`SmartConnectCommand.cs`) -
     existed only to carry the old "N selected elements left unpaired" batch message. Both remaining
     callers now pass a single-outcome list, so the 2-arg overload was only ever invoked with `null`.
  3. `TryGetBestOpenConnectorPair`, `AreDomainsCompatible`, `ComputeOrientationPenalty`
     (`SmartConnectConnectorUtils.cs`) - zero callers anywhere in the repo. This predates 1.47.5; it
     was left behind by the 1.47.0 route-builder rewrite, which built its own inline domain check
     directly in `SmartConnectRouteBuilder.cs` instead. Same class of problem, caught in the same pass
     because Ajmal asked for a thorough check, not a narrowly-scoped one.
- **Also fixed**: a code comment illustrating WPF text-wrapping still quoted the exact string of the
  deleted "Neither - leave both alone" radio option as its example; swapped for a string that still
  exists. Two file-header descriptions still said "batch" for settings that no longer batch anything.
- **Lesson**: deleting the call site that prompted a removal is not the same as confirming nothing
  else depended on what got removed. The right check is mechanical - grep for the removed name/type
  everywhere, then grep for every OTHER thing that removal's data flowed into, and confirm each one
  still has a real caller. "I don't remember anything else using it" is not verification.
- **Verified how**: build-clean confirmation only goes so far here, since orphaned-but-still-callable
  methods and always-null parameters both compile fine with zero warnings - the actual proof was the
  grep showing zero remaining call sites for each removed item, done before deleting, not after.
  Clean build across Release and R21-R26 confirms nothing broke; it does not by itself confirm the
  removed code was dead, which is why the grep step came first. **Not yet exercised in Revit**.

### 2026-08-16 (Connect MEP Elements: two overlapping settings could contradict each other — SIMPLIFIED, suite 1.47.5)
- **Symptom (found together, live testing)**: Ajmal ("still same") reported ducts still not extending
  as expected. Live inspection of his saved settings file showed `RoutingMode: 2` (Automatic) but
  `MoveMode: 3` (Neither - leave both alone). The tool followed MoveMode and inserted three new pieces
  even though RoutingMode said "Automatic," which reads as broken from the outside, because two
  settings governed overlapping ground and could point different ways.
- **Root cause**: `SmartConnectMoveMode.None` ("Neither") and `SmartConnectRoutingMode.OffsetWithTwoElbows`
  ("Never touch the picked pipes") both force `forceInsert = true` for the same reason - the user
  wants nothing they picked to move - via two unrelated settings that a user has no way to know are
  linked. Whichever one is set to the "don't touch anything" extreme wins, regardless of what the
  other says.
- **Fix, at Ajmal's explicit instruction** ("this no need and remove that related this settings
  also"): removed `SmartConnectMoveMode.None` outright, not just hid it. "Never touch the picked
  pipes" in Routing mode is now the ONE place that behaviour lives. `MoveMode` only ever offers Both /
  FirstOnly / SecondOnly now - all three assume at least one end may move, so there is no longer a
  second control that can silently override Routing's decision.
- **Backward compatibility**: `Sanitize()` already resets an out-of-range enum value to `Both` via
  `Enum.IsDefined` - removing value 3 makes any OLD settings file with `MoveMode: 3` fall into that
  same path automatically, no migration code required. Verified by reading the guard after the change,
  not assumed.
- **Companion simplification, same request**: also removed the nearest-open-end auto-pairing algorithm
  for a multi-element selection (`BuildNearestPairs`, `ClosestDistance`, `MaxPairDistanceMm`,
  `SingleUndoForBatch`) - Ajmal's words: "that connection method no need, you can remove - if i need i
  will select the pipe or what elements then it will connect." A pre-selection of exactly two elements
  now connects them directly with no matching; more than two asks him to narrow it down.
- **Lesson**: same shape as the two Routing-mode bugs already logged above - two settings (or two
  branches) governing the same underlying decision from different angles is where this feature keeps
  going wrong. Worth checking for on any future addition here: does a new setting duplicate ground
  that Routing mode or MoveMode already covers?
- **Verified how**: grepped for every remaining reference to the removed enum value and the removed
  batch functions before and after editing (both came back empty), then a clean full build across
  Release/R21-R26, zero warnings. **Not yet exercised in Revit** — Ajmal to test.

### 2026-08-15 (Connect MEP Elements: in-line gap always made a new duct instead of extending the real one — FIXED, suite 1.47.4)
- **Symptom (Ajmal's words, live in Revit)**: "in the revit sliting means not single cut. split means
  add a unian in the bitween liek that" → clarified on asking: "in this tool if there is extending
  duct that is just creating the duct that is not currect chek that." Two straight ducts, dead in
  line, with a gap between them: instead of stretching one of his existing ducts across the gap, the
  tool inserted a brand new third duct to bridge it.
- **Root cause**: `TryPlanParallelPair`'s Inline branch (the dead-in-line, no-bend-needed case)
  hard-coded `FirstShift = 0`, `SecondShift = 0`, `NeedsMiddleSegment = true` unconditionally. It
  never looked at `mayMoveFirst`/`mayMoveSecond` at all, so even with "Always cut the picked pipes
  back" selected and both ends free to move, it always built a new bridging segment spanning the
  whole gap and left both picked ducts untouched. The sibling branch for an offset crank
  (`ParallelOffset`) already called `TryDistributeShift` to split the required travel between
  whichever ends may move — Inline just never got the same treatment when it was written.
- **Fix**: Inline now calls the same `TryDistributeShift` when at least one end may move, stretching
  the real duct(s) to close the gap directly (no new element) - and falls back to a genuine new
  bridging piece only when neither end is allowed to move (equipment, flex, "Never touch the picked
  pipes"), where there is no existing element to stretch.
- **Lesson**: when two branches of the same decision tree solve visibly similar problems (bend vs. no
  bend), check that a capability added to one (respecting the move-mode setting) was actually carried
  into the other, not just copy-pasted structure with the capability silently missing. This is the
  second bug this feature has had from one branch getting a fix/feature the sibling branch didn't -
  worth specifically re-scanning sibling code paths after any planner change.
- **Verified how**: found by Ajmal running the live tool in Revit, not by review. Traced the exact
  code path against his description before touching anything. Clean build, zero warnings.

### 2026-08-15 (Connect MEP Elements: sign error folded the offset crank back on itself — FIXED, suite 1.47.2)
- **Symptom**: none visible. Found by a six-dimension multi-agent audit of freshly written code, not by
  a user report. 27 findings raised, 13 survived adversarial verification, all fixed.
- **Root cause (the one that mattered)**. In the parallel-offset planner, the required axial gap `r`
  can be taken off the offset in two ways: `axisOffset - r` or `axisOffset + r`. The code chose
  `Math.Abs(optionA) <= Math.Abs(optionB) ? optionA : optionB` — "move the ends as little as
  possible", which sounds obviously right and is completely wrong. The bridging vector is
  `between - d1*totalShift`, so its axial component is `axisOffset - totalShift` and the bend angle
  satisfies `tan(theta) = perpLen / (axisOffset - totalShift)`. **The SIGN of that term sets the
  angle, not its magnitude.** `optionA` gives `+r` → theta = the requested angle. `optionB` gives
  `-r` → theta = 180 - requested, i.e. the bridge doubles back over the run it just left. And
  `|optionB| < |optionA|` exactly when `axisOffset < 0` — which is every pair whose open ends have
  already passed each other, the ordinary overlapping crank. `ResultingAngleDegrees` was still set to
  the requested angle, so a 135° fold-back was reported to the user as 45°.
- **Why it hid**: the default angle is 90°, where `r = 0` makes both options identical. Only 45° and
  other custom angles broke, and only when the runs overlapped.
- **Why my own earlier reasoning missed it**: while writing `TryDistributeShift` I proved that only
  the SUM of the two shifts matters, not how it is split — which is true — and then used that to
  conclude the shift value itself was safe. The proof was about the split; it said nothing about
  which of the two candidate sums to pick.
- **Lesson — the general trap**: when a quantity can be reached two ways and you pick between them by
  "least movement" / "smallest change" / "nearest", check what that quantity actually *controls*. A
  tie-break chosen on one property (travel distance) silently decided a different property (bend
  direction). If a value feeds a trig relation, the sign is part of the answer.
- **Others fixed in the same pass**: the closest-approach solve divides by `1 - dot^2` and is
  ill-conditioned within ~2.6° of parallel (now screened by deflection and travel bounds); flex could
  never join rigid because `AreCompatible` compared raw `BuiltInCategory` and `OST_FlexDuctCurves !=
  OST_DuctCurves`, making the entire flex path unreachable; a geometry-fixed angle was reported as an
  angle "substitution" so every in-line route raised a false warning; `FallbackAngles` was sorted
  (destroying a deliberate try-order) and dropped on every window save.
- **Verified how**: the critical one re-derived by hand before applying the fix, not taken on the
  reviewer's word. Clean builds at Release (2020) and R21/R24/R25 (net48/.NET 8), zero warnings.
  **Not yet exercised in Revit** — Ajmal to test live.

### 2026-08-15 (Connect MEP Elements: a routing mode that could never be chosen, and angles that could never be built — FIXED, suite 1.47.0)
- **Symptom**: not reported by Ajmal — both found while studying the tool for a feature update. Neither
  throws, which is exactly why they survived from v1.0.0 (2026-03-25) to now.
- **Root cause 1 — "Offset + 2 Elbows" was dead code.** `SmartConnectSettingsService.Sanitize()` carried
  the line `result.RoutingMode = SmartConnectRoutingMode.SingleElbow;` unconditionally. `Sanitize` runs on
  **both** `Load()` and `Save()`, so the mode was overwritten coming and going and could never be selected.
  The window reinforced it by hardcoding `SelectedRoutingMode = SingleElbow` in its OK handler. ~200 lines
  of working offset-routing code in the builder were unreachable.
- **Root cause 2 — the settings window offered angles the builder rejects.** The service validated custom
  angles against `MinAllowedAngle = 5.0` / `MaxAllowedAngle = 175.0`, but
  `TryBuildSingleElbowMepCurveRoute` bailed out on anything over `90 + AngleToleranceDegrees` (92.5°). So
  an angle of, say, 120° could be typed, saved, persisted and selected — and then failed on *every*
  subsequent pick with "Single Elbow supports practical elbow angles up to 90 degrees." The two limits
  were written independently and never reconciled.
- **Fix**: removed the forcing line and let the mode persist (adding an `Auto` mode that tries trimming
  first, then inserting). Capped the settings range at the honest 5–90°, and clamp rather than discard an
  older saved angle above 90 so an existing settings file lands on 90 instead of silently resetting.
- **Lesson worth carrying**: both bugs are the same shape — **two places holding the same rule, neither
  aware of the other**. A validation range in the settings layer and a feasibility check in the geometry
  layer must come from one constant, or they drift apart silently. Same for an enum that a sanitiser is
  allowed to overwrite: a "sanitise" step that *sets* a value rather than *rejecting* an invalid one will
  quietly delete a feature.
- **Verified how**: code-review plus clean builds at Release (2020) and Release R25 (.NET 8), zero
  warnings. **Not yet loaded in Revit** — Ajmal to test the live behaviour.

### 2026-08-11 (Transfer Drafting Views / Transfer Legends create the view but it arrives EMPTY — FIXED, suite 1.42.1)
- **Symptom (Ajmal's words)**: "transfer drafting views its not working properly its creating the view in
  anothonr model but inside that drafting view its not creating we need that also chek inide the legents
  also is that same like that or its okkey". The view lands in the target project and looks right in the
  Project Browser — it is only empty once opened.
- **Root cause — a Revit API behaviour, not a logic error in the tool.** The document-to-document overload
  `ElementTransformUtils.CopyElements(sourceDoc, ids, targetDoc, transform, options)` copies a view's
  **shell only**. It does not carry the detail lines, text notes, filled regions or legend components drawn
  inside it. Nothing signals this: the call succeeds, returns an id, and the tool correctly reported
  "1 drafting view transferred". **The view-to-view overload**
  `CopyElements(View sourceView, ids, View destView, transform, options)` is the one that actually carries
  view-specific elements.
- **Measured live before writing a line of fix** (Revit 2020, Ajmal's own two open models `vg` +
  `MODEL PROJECT`): copying the 131-element drafting view `MEP_Text_Styles_Legend` with the exact call the
  tool makes returned **one** element id, and the new view read back holding **1** element — its internal
  `ExtentElem`. All 130 real items were left behind. The second pass then copied 130 and the view read back
  at **131/131** against the source.
- **A near-miss worth keeping**: the two passes were first tested as two separate transactions, but the real
  tool runs them in ONE. Re-tested single-transaction on purpose — **it works, and needs no
  `Document.Regenerate()`** between the passes; the second pass can see the view the first pass just
  created. Assuming either way round without testing would have been a coin flip.
- **Fix** (`TransferViewsCommandRunner.cs` v1.1.0): legends/drafting views are copied **one view per call**
  (so the returned id can be paired back to its source view — a bulk call gives a flat collection with no
  such guarantee), then their contents are copied into the new view with the view-to-view overload.
  `ExtentElem` is excluded by `Category != null`. Contents failing on one view adds a warning instead of
  failing the transfer, matching how sheet-placement failures already behave, and the report now states how
  many items were copied inside — an empty result can no longer look like a success.
- **Schedules deliberately untouched** — still one bulk copy. A `ViewSchedule`'s rows are generated from the
  target model's own elements, so there is nothing drawn inside to leave behind. Transfer Schedules never
  had this bug.
- **Legends: fixed by the same code path, but NOT live-verified — be honest about this.** Both kinds ran
  through the same single bulk call and both now run through the two-pass copy, so the defect and the fix
  are shared by construction. It could not be measured: **neither open model contains a single legend view,
  and the Revit API cannot create one** (there is no `Legend.Create`), so there was nothing to test against.
  Ajmal re-tests on a model that has legends.
- **Verified how**: live via AJ AI Bridge as above (both the broken behaviour and the fixed two-pass
  sequence), then `Release` (2020/net472) and `Release R25` (.NET 8) both rebuilt 0 errors / 0 warnings, and
  deployed to the 2020 AppData payload `AJ Tools.20260811190129404`, manifest + DLL read back at 1.42.1.0.
  **Every live test was reversed with Revit's native Undo** and both models confirmed back at their exact
  starting contents (`vg` 1 view/107 items, `MODEL PROJECT` 4 views/15+2+107+131). **Not click-tested through
  the real ribbon button** — needs a Revit restart to load 1.42.1. → 2026-08-11.

### 2026-08-05 (v1.40.6 released end-to-end — and RELEASE_PROCESS.md had stale paths that would have broken it)
- Full release run straight after the Game Mode fix below. Source repo committed (`b076bf6`) + tagged
  `v1.40.6` + pushed; installer repo committed (`1c15597`) + tagged + pushed to `main`; GitHub Actions
  published the public release, verified live via `Invoke-RestMethod` — "AJ Tools v1.40.6", draft false,
  both assets present (`AJ-Tools-v1.40.6.zip` 53.67 MB + `SHA256SUMS.txt`). All 8 payloads (2020-2027)
  read back at 1.40.6.0 before packaging. Both working trees clean afterwards, local HEAD == remote.
- **Stale-path trap in `RELEASE_PROCESS.md`, fixed in the same session**: steps 5 and 8 still said
  `Set-Location ..\AJ-Tools-Installer` and `-SourceRepoPath "..\AJ Tools"` / `Set-Location ..\AJ Tools`.
  That was correct only while the git working copy lived inside the `AJ Tools\` subfolder. Since
  2026-08-05 **the repo root IS the working copy** and `AJ-Tools-Installer` is a gitignored sibling
  checkout *inside* it, so those relative paths resolve to the wrong places. Now written as absolute
  paths, with a note that the source branch is `master` while the installer branch is `main` — they
  differ, and assuming one name for both is an easy way to push nothing. Also added the API
  verification step (`gh` is still not installed on this machine).
- **`msbuild` is not on PATH in this shell.** Find it with
  `& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"`
  → `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`.
- **The `Release R21`..`R27` configurations exist on the PROJECT, not the solution.** `AJ Tools.sln`
  only carries `Debug`/`Release`, so `msbuild "AJ Tools.sln" -p:Configuration="Release R25"` fails with
  MSB4126 "invalid solution configuration". Build the csproj directly for any R-config:
  `msbuild "src\AJ Tools.csproj" -p:Configuration="Release R25"`.
- **Known pre-existing warning, NOT from this release**: `Release R27` builds with 2 × CS0618 —
  `Space.Zone` is deprecated in Revit 2027 (`src\UI\LocationDataAssignerWindow.xaml.cs:1030`). The
  2020 and R25 configs are warning-free. Flagged to Ajmal, not fixed — it needs a version-safe helper
  around `GenericZone` and real API verification, which is its own task. → 2026-08-05.

### 2026-08-05 (Game Mode SELECTOR gun: camera flies/jumps on every shot — FIXED, suite 1.40.6)
- **Symptom (Ajmal's words)**: "in the game mode tool for the selection gun there is issue if i shoot
  that to anyting after that the camara is going flying or jumbing someting like that". Only the
  SELECTOR weapon; gun/laser/cleaner/snag were not reported.
- **First instinct was wrong, and checking it first saved the session**: "shot → camera rockets" reads
  exactly like an unclamped frame-delta game loop (slow frame → huge `dt` → player launched). It is not.
  `GameMotionEngine.Execute` already clamps `dt` to 0.005–0.25 s (`GameMotionEngine.cs:199-202`), so a
  slow frame cannot move the player far. **Rule out physics before touching it — the symptom lies about
  the layer.** Nothing in the selector branch (`GameMotionEngine.Extras.cs:159-196`) touches `position`
  either; it only calls `uidoc.Selection.SetElementIds`.
- **Root cause — an INPUT bug, and the trigger is Revit's own window chrome**: the selector is the only
  weapon that changes the Revit **selection**. Changing the selection makes Revit re-lay-out its chrome
  (the Options Bar slides in/out, the contextual "Modify | ..." tab swaps in), which changes what
  `UIView.GetWindowRectangle()` returns for the game view. The HUD follows that rectangle each tick
  (`GameHudWindow.xaml.cs` `OnTick` → `ApplyPixelRect(..., remember: true)`), which updates
  `_fullLeft/_fullTop/_fullRight/_fullBottom` — **and the mouse-look centre is derived from those four
  fields** (`OnGameMouseMove`: `centerX = (_fullLeft + _fullRight) / 2`). The physical pointer was still
  parked on the OLD centre from the previous `SetCursorPos`, and **nothing anywhere re-centred it when
  the rectangle moved**. So the next mouse move measured `(old centre − new centre)` as genuine aiming
  and applied it as yaw/pitch at `MouseDegPerPixel = 0.15`. A chrome shift of a few dozen px is several
  degrees; if the Properties palette opens too, `centerX` moves ~150 px = **~22° of instant yaw whip**.
  Hold a movement key while that happens and you genuinely fly off across the model.
- **Fix** (`GameHudWindow.xaml.cs` v1.9.5 + `GameHudWindow.Controls.cs`): (1) `ApplyPixelRect` re-centres
  the pointer whenever a *remembered* rectangle moves the centre and the look is active — it compares
  edge **sums**, so a resize about a fixed middle correctly does nothing. (2) `StartLook` centres the
  pointer *before* setting `_mouseLookActive`, so the move event WPF raises on `CaptureMouse()` can't be
  read as a turn. (3) `OnGameMouseMove` drops (and re-centres on) any single step beyond
  `MaxLookStepPx` = 400 px — a backstop for DPI changes / remote-session cursor warps, not the fix.
- **Deliberately NOT changed**: `UpdateViewRectangle`'s `uiView.ZoomToFit()` on rectangle change. It
  fires on every selector shot too (same trigger), but it is v1.38.2's aim/display sync fix — removing
  it would bring back "shots land beside the crosshair". It re-fits the picture; it does not move the
  eye, and `SetOrientation` re-applies the camera every frame regardless.
- **General lesson worth carrying**: in this HUD, *any* screen geometry the mouse-look centre is derived
  from is load-bearing. If a change moves `_full*` while the look is captured, it MUST re-centre the
  cursor in the same breath, or it silently becomes a camera input. Watch for this in any future work
  that repositions the overlay.
- **Verified how**: code-review + clean builds only — `Release` (2020/net472) and `Release R25` (.NET 8)
  both 0 errors / 0 warnings; `tools\verify-version-consistency.ps1` clean on all six references (it
  caught README.md still saying 1.40.5 — exactly what it was added for). Revit was closed, so the fix
  was rebuilt and **deployed** to the 2020 AppData payload (`AJ Tools.20260805192607865`, read back at
  1.40.6.0). **Not click-tested in Revit** — Ajmal re-tests by shooting a few elements with the SELECTOR
  and confirming the view stays put. → 2026-08-05.

### 2026-07-28 (Revision Cloud By Elements: cloud outline comes out tilted/angled on schematic symbols — DIAGNOSED (hypothesis), no fix applied yet)
- **Symptom (Ajmal's screenshot + follow-up clarification)**: ran Revision Cloud By Elements on an HVAC
  schematic (6 branch-end device symbols, each a box+arrows group) — got 6 separate small clouds, one
  hugging each device group. First read of the screenshot: flagged the *separate-clouds-per-group*
  behavior, but Ajmal corrected that this part is fine/expected ("if the one duct I am selecting, this
  comes aligned with the duct or elements" — the align-to-duct-angle feature is wanted when a real duct
  drives it). **The actual complaint is that the cloud outline itself is drawn at a tilted/diagonal
  angle, not aligned with any duct or anything else** — i.e. the tilt has no real element behind it.
  Two warning-triangle badges near the bottom trunk symbol still look unrelated to this tool — not
  investigated.
- **Separate-clouds-per-group part (confirmed by code review, still true, just not what he's asking about
  now)**: `OrthogonalOutlineBuilder.BuildOutlines` expands every selected element's box by Offset Distance
  (default 50mm), rasterizes to a 10mm grid, flood-fills into connected components, one cloud per
  component. Groups farther apart than the offset land in separate components -> separate clouds. Not a
  bug — no fix needed here unless Ajmal says otherwise.
- **Angle root cause — hypothesis, NOT yet confirmed live**: `CmdRevisionCloudByElements.CreateCloudsForSelection`
  computes exactly ONE shared "dominant axis angle" for the *entire* selection pass
  (`GetDominantAxisAngle`, doubled-angle average over every element's angle from
  `GeometryProjectionService.TryGetElementProjectedAxisAngle`), then builds every element's rectangle AND
  the final cloud polygon in that one rotated frame. `TryGetElementProjectedAxisAngle` takes a real
  duct/pipe's `LocationCurve` direction when available, but falls back to a point-family's
  `LocationPoint.Rotation`, and — critically — schematic symbols are very likely built from plain
  **detail lines/curve-based annotation** (the box outline, the crossing "X", the arrow chevrons), which
  are `LocationCurve` elements too. Any diagonal line in the selection (an X-mark stroke, an arrowhead
  chevron) feeds its own diagonal angle into the SAME shared average that decides the whole cloud's tilt
  — even though it isn't a duct at all. Because one `dominantAxisAngle` is shared across the whole pass,
  this would tilt ALL clouds built in that pass by the same amount — a falsifiable detail to check against
  what Ajmal actually sees (are all 6 clouds tilted by the same angle, or different amounts per group?).
- **Not confirmed live** — AJ AI Bridge not connected this session (`ping` failed twice, Revit not open),
  so the actual selected elements' real `Location` type/angle were not read back. Asked Ajmal to confirm
  the "same angle on all 6" detail and what exactly was selected (just the box/arrow symbols, or also
  duct/pipe/trunk elements) before proposing a fix.
- **Fix**: none applied yet — still confirming root cause, per Ajmal's "tell me the issue before doing
  anything." Likely direction once confirmed: exclude non-MEP-curve elements (detail lines, generic
  annotation symbols without real duct/pipe geometry) from the dominant-angle vote, so only real
  duct/pipe/tray runs can tilt the cloud away from 0/90 degrees — but not applied without his sign-off.
  -> 2026-07-28 (open).

### 2026-07-28 (HVAC Schematic: "The given key was not present in the dictionary" — OPEN, diagnostic build deployed)
- **Symptom (Ajmal's words: "IN THE HVAC SCHEMATIC ITS CAME WHY")**: Create HVAC Schematic From Model
  shows "An unexpected error occurred. / The given key was not present in the dictionary." Popup text is
  the OUTER catch of `HvacSchematicCommand.Execute` — so the crash is in analysis/layout, BEFORE the
  transaction (confirmed: no drafting view exists in the project afterwards; model untouched).
- **Reproduced live**: yes — clicking the real ribbon button in Revit 2020, Project1, 45 selected
  elements (6 air terminals, ducts, duct fittings, FCU-01 equipment) shows the error every time.
- **Ruled out (all verified live via AJ AI Bridge in the same session that crashes)**:
  - `UnitFormatUtils.Format(UT_HVAC_Airflow)` on this doc — works ("1342.2 L/s").
  - Whole analysis+layout pipeline replicated line-by-line in bridge scripts (traversal, edge
    evidence/votes, level ladder, AssignNetworks BFS, band indices, ChooseRoot, Dijkstra, children map,
    depths, subtree scores, continuation choice, tree positions, band lookup) — PASSES with both
    all-model seeding (37 nodes/33 edges/6 networks) and his exact 45-element selection
    (23 nodes/22 edges/1 network, root = FCU). Zero edge-endpoints missing from the node set.
  - Version drift — running session loaded 1.25.1 (session started 07-27 ~23:09; newest payload then),
    and 1.25.1 contains the same schematic code as current src (files last changed 07-24). All four
    schematic sources + RevitCompat + ElementIdHelper/Extensions read fully: no unguarded dictionary
    with an inconsistent key found by review.
- **Conclusion so far**: real button crashes, faithful replay of the same logic does not — the throw is
  somewhere the replay can't reach (only unreplicated pre-transaction bits: full LevelResolver host/
  owner-view/connector-context ladder, cosmetic label fallbacks — none look capable of KeyNotFound).
  Stopped guessing; instrumented instead.
- **Action taken (suite 1.25.7, command v1.1.0)**: both catch blocks in `HvacSchematicCommand` now show
  `BuildExceptionReport(ex)` — exception type + message + AJTools stack frames (file/line via PDB) +
  inner exceptions — instead of `ex.Message` alone. Built clean (0 err/0 warn), deployed to AppData
  2020 payload `AJ Tools.20260727232452691`, manifest verified pointing at 1.25.7.
- **Next step**: Ajmal reopens Revit 2020 → Project1 → same selection → run tool → the dialog now names
  the exact failing method+line; fix from there. Note: next session starts on 1.25.7, whose schematic
  code is otherwise identical to what crashed — if the error does NOT reappear after restart, suspect
  session-state (something cached in the old session) rather than the model.
- **Process note**: reproduced the click via desktop automation once — a stray second Revit (viewer
  mode) got launched and Ajmal stopped it; desktop automation is now off-limits (see cross-session
  memory `no-desktop-automation`). Ask him to click instead. -> 2026-07-28.

### 2026-07-28 (v1.25.6 shipped as a public release, same day as the audit)
- Ajmal asked to finish the remaining work (public installer release) right after the audit landed on
  master. Package rebuilt (all 8 payloads re-verified at 1.25.6.0), installer repo prepared/committed/
  tagged/pushed (`5c23b65`), source repo tagged and pushed (`v1.25.6`), both confirmed live via
  `Invoke-RestMethod` against api.github.com (installer release has both assets, source tag exists,
  master head matches). One false alarm: `sha256sum -c SHA256SUMS.txt` failed with "No such file or
  directory" — the checksum file has CRLF line endings so bash appended a stray `\r` to the filename
  it looked for. Not a real integrity problem — `sha256sum` on the zip directly reproduced the exact
  same hash recorded in the file. **When re-verifying a Windows-generated checksum file from bash, hash
  the file directly and compare the digest string — don't rely on `sha256sum -c` reading the file's own
  filename line back.**

### 2026-07-28 (v1.25.6 full-force UI audit — 18 fixes across 22 files, all mechanical checks now clean)
- **Symptom**: none — Ajmal asked to "check the entire UI with full force". Scripted checker
  (`ui_audit.py` pattern: resource-key resolution per merged dictionary, duplicate x:Key, root-attr
  StaticResource, Grid.Row/Column overflow, footers, IsCancel, taskbar caps) + targeted greps
  (owners, MessageBox, Application.Current, ribbon IDs/icons/tooltips).
- **Found + fixed**: (1) **12 modal windows shown with no owner** (could drop behind Revit): Duct
  Standards, Filter Pro, Linked ID Viewer, Linked Search, Pipe Sizing, Purge ×2, Revision Cloud
  Settings, Transfer View Templates, Apply Graphics, SharedParam→FamilyParam, Saved Scripts — all
  got `WindowInteropHelper`. (2) **5 borderless windows maximize over the taskbar** (Graphics
  Override + 4 View Crop; `WindowChromeHelper.ToggleMaximize` does NOT cap size) — fixed with the
  AboutWindow v1.19.1 two-liner. (3) **Esc-close missing** on MEP Opening Settings / Pipe Sizing /
  About Close buttons — `IsCancel="True"` added. (4) **success popup** in PipeSizingCsvExporter
  removed (pre-approved class). Left alone: AiShell's 2 MessageBox confirms (working confirmations).
- **Verified clean** (worth trusting next time, dated today): all StaticResource/DynamicResource
  refs resolve, no duplicate keys, no root-attr StaticResource, no grid overflows, footer everywhere,
  no duplicate ribbon IDs, all 34 icons exist, no empty tooltips, Application.Current only in comments.
- **Checker gotchas**: `{StaticResource {x:Type X}}` (BasedOn default-style) is VALID — whitelist it;
  bash for-loops split icon filenames containing spaces — use `while IFS= read`.
- **Verified how**: rebuild 2020 (0 err/0 warn) + R25 (0 err), all 8 years deployed and read back at
  1.25.6.0 (16/16). Pushed as 3930405. **Not click-tested in Revit; no new release tag** — v1.25.6
  rides on master until the next release.

### 2026-07-28 (v1.25.5 released end-to-end: local deploy + both GitHub repos)
- Full release run after the WPF conversion below: synced root → repo (31 src files + Directory.Build.targets,
  one-way drift only, verified empty re-diff), CHANGELOG updated both sides (root already had 1.25.1 —
  repo copy was one release behind; wrote 1.25.2–1.25.5 on top and copied the same file to both),
  built + deployed all 8 years locally (Revit closed; all 16 deploy points read back at 1.25.5.0 —
  note: AppData deploys live in timestamped `AJ Tools.<stamp>` folders, verify via the .addin manifest's
  Assembly path, not a fixed folder name), packaged (per-year payloads verified: sizes step by framework
  family, not 8 copies of 2020), installer repo release pushed (Actions auto-created the public release
  with zip + SHA256SUMS), source repo pushed + tagged. Commits: source 9cb0f1e + tag v1.25.5, installer
  3728202 + tag v1.25.5. `gh` CLI is NOT installed on this machine — use `Invoke-RestMethod` against
  api.github.com to verify releases/tags.

### 2026-07-28 (Smart MEP Tag Settings — last WinForms dialog converted to WPF; suite is now 100% WPF)
- **Symptom**: none — Ajmal asked "is there any UI balance not done like this"; this was the only one left.
- **Same anti-pattern, 4th instance**: "Enable at least one category" was checked *after* `ShowDialog()`
  returned → error popup, dialog closed, command cancelled, all edits lost. Now inline: Save disables
  with a message the moment the last tick is removed. The old grid's "Select a valid priority" failure
  is gone entirely — priority is a fixed-choice ComboBox, an invalid value can't exist.
- **Also**: no owner window (could drop behind Revit) → owned now; "Settings saved." popup dropped per
  house rule; added Tag all / Tag none. Save/state logic (`ResolveOffsetInternal` carry-over,
  first-enabled-offset rule) copied over unchanged — UI-only change, `SmartTagSettingsTracker` untouched.
- **New WPF technique used**: `DataGrid` with a checkbox `DataGridTemplateColumn` needs
  `UpdateSourceTrigger=PropertyChanged` on the binding + an `INotifyPropertyChanged` row model, with the
  window subscribed to each row's `PropertyChanged` to re-validate live. Before reading rows on Save,
  call `CommitEdit(DataGridEditingUnit.Row, true)` or an in-progress cell edit is silently lost.
- **Verified how**: code-review + clean rebuild only — Release (2020) 0 errors / 0 warnings; R25 rebuild
  clean of this file (its 216 CA1416 warnings gone, suite 490 → 274). **Not click-tested in Revit.**

### 2026-07-28 (Reassign Reference Level — UI bugs found while converting to WPF, all FIXED)
- **Symptom**: none reported — Ajmal asked to "check and fix" the Reassign Level UI after the credit-line
  pass. Same story as Arrange Tags below: the bugs came out of reading the dialog, not from a crash.
- **Bug 1 — same-level pick cancelled the whole command.** `TryPromptLevels` validated *after*
  `ShowDialog()` returned, so choosing the same level in both boxes closed the dialog, showed an error
  popup, returned false → `Result.Cancelled`, and the tool had to be relaunched from the ribbon. This is
  the **third** instance of the same anti-pattern in this codebase (Arrange Tags Settings had it too).
  **Rule going forward: validate inside the window, live, with the action button disabled — never after
  ShowDialog returns.** Fix: `Validate()` on every SelectionChanged, Run disabled while invalid.
- **Bug 2 — overlapping buttons.** "Reassign Elements" was at x=235 with Width=130 (right edge 365) while
  Cancel sat at x=350 — a 15 px overlap, present since v1.0.0. Moot now the WinForms form is gone.
- **Bug 3 — no owner window.** `form.ShowDialog()` with no owner + `ShowInTaskbar = false` meant the
  dialog could disappear behind Revit with no taskbar entry to get it back. Fixed via
  `WindowInteropHelper.Owner = uiapp.MainWindowHandle` (same fix as Arrange Tags Settings).
- **Bug 4 — clipping risk.** The intro sentence lived in a fixed 430x32 label inside a fixed 460x225
  `FixedDialog`; at larger Windows text scaling the third line had nowhere to go. Now wraps in a
  resizable window.
- **Also added (not bugs)**: a Swap button, and an up-front note that the scope is the WHOLE project and
  that hosted elements are skipped — both were previously only discoverable from the report *after* the
  run. The bulk-change confirmation with the element count is unchanged and still fires before any edit.
- **Scope held**: `ReassignLevelService` (eligibility, host-offset compensation, space copy) was not
  touched — this was a UI-only change.
- **Side benefit worth knowing**: deleting the WinForms form removed **all 192** of that file's CA1416
  warnings on R25 (682 → 490 suite-wide). Converting a WinForms dialog to WPF is the real fix for that
  warning class — see the conventions note. `CmdSmartMepTagSettings.cs` is the last one left (216).
- **Verified how**: code-review + clean rebuild only — `Release` (2020) 0 errors / 0 warnings, `Release
  R25` builds with no warnings from the changed files. **Not click-tested in Revit** (Revit was open, so
  no deploy). Ajmal still needs to run it once and confirm a real reassignment behaves as before.

### 2026-07-27 (Arrange Tags Settings — 3 latent bugs found while rebuilding the UI, all FIXED)
- **Symptom**: none reported — Ajmal asked to "make the Rearrange Tag settings window perfect". The bugs
  were found by reading the old WinForms dialog, not from a crash.
- **Bug 1 — a typo silently threw the whole entry away.** The old dialog validated *after*
  `ShowDialog()` returned, so any bad value closed the window, showed an error popup, and returned
  `Result.Cancelled` — the spacing was never saved and the user had to relaunch the tool from the ribbon.
  **Fix**: validation is now live and inline in the WPF window (`Validate()` on every keystroke), Save is
  disabled while the value is invalid, and the window never closes on a bad entry.
- **Bug 2 — comma-decimal locales could silently save a 10x spacing.** Input was parsed with
  `CultureInfo.CurrentCulture` but `TagArrangeSettings` writes/reads with `InvariantCulture`. On any
  Windows regional setting that uses comma as the decimal separator, `"12.5"` parses as **125** and is
  then stored as `125` — a 10x tag gap with no warning. **Fix**: `TryParseNumber` tries CurrentCulture
  first, then InvariantCulture, so both `12.5` and `12,5` are read correctly on any locale. Worth
  remembering as a general rule: **if a value is stored invariant, it must not be parsed culture-only.**
- **Bug 3 — a failed settings write was reported as success.** `TagArrangeSettings.SaveTagSpacingMm`
  swallows all exceptions by design, so an AppData permission problem produced a cheerful "saved" popup
  and the old value stayed in force. **Fix**: the command now reads the value back after saving and only
  reports success if it actually matches (the house "fresh read-back, never trust the write" rule applied
  to settings files, not just the model).
- **Also fixed, not bugs**: no range check at all (any positive number accepted → now 0.1–250 mm); the
  dialog had no owner window so it could drop behind Revit (now owned by `MainWindowHandle`); the tool
  demanded an open project even though the setting lives in AppData (now optional — the active view scale
  is used only for the live "on sheet vs in model" explanation text).
- **Verified how**: code-review + clean rebuild only — `Release` (2020) rebuild 0 errors / 0 warnings,
  `Release R25` (.NET 8) builds with no warnings from the changed files. **Not click-tested in Revit**
  (Revit was open, so no deploy). Ajmal still needs to open the window once and confirm the saved value
  survives a reopen.

### 2026-07-26 (ProgramData deploy manifest bug FIXED — closes the open item from 2026-07-21)
- **Symptom**: the 2026-07-21 entry below — all-users ProgramData deploy wrote DLL/PDB/Resources loose at
  `Addins\<year>\` root and the manifest pointed at a glued `AJ ToolsAJ Tools.dll` that never existed.
- **Root cause (sharpened from the original diagnosis)**: inside a target's `<ItemGroup>` item created via
  transform, `%(FullPath)` in the **metadata elements** binds to the SOURCE item being transformed
  (`DeployRoots`, whose FullPath keeps its trailing `\`), NOT to the new item's own path — while
  `%(AjDeployDirs.FullPath)` used later in the WriteLinesToFile *does* resolve the new item (no trailing
  separator). Two different resolutions, two different wrong paths: files copied to the Addins root,
  manifest pointing at the glued name. Proven with a standalone msbuild test project before fixing.
- **Fix** (`Directory.Build.targets`): DllPath/PdbPath/ResourcesPath metadata now insert
  `$(RevitAddinDeployName)\` explicitly (→ files land in `...\<year>\AJ Tools\`), AddinPath stays at the
  root (Revit only discovers manifests there), and the manifest `<Assembly>` now uses
  `%(AjDeployDirs.DllPath)` — the same metadata the Copy uses — so path drift is impossible by
  construction. Explanatory comment left in the targets file.
- **Not done**: stale loose `AJ Tools.dll`/`.pdb`/`Resources\` from old builds at the ProgramData root
  (years 2020/2024/2027) were NOT deleted — harmless, and deleting files outside the repo needs Ajmal's
  OK. Ask him whether to clean them on the next deploy-build session.
- **Verified how**: standalone msbuild path test (old expressions reproduce both wrong paths byte-for-byte;
  new expressions produce `...\<year>\AJ Tools\AJ Tools.dll` etc.), then — same day, after Ajmal closed
  Revit — a real full deploy of all 8 years and a fresh read-back of every folder: all 8 ProgramData
  `<year>\AJ Tools\AJ Tools.dll` exist at v1.25.1.0 and all 8 manifests' `<Assembly>` == that exact path;
  all 8 AppData manifests point at existing v1.25.1.0 payloads. (Pre-deploy listing also corrected an
  earlier note: ALL 8 years had the broken pattern, not just 2020/2024/2027 — the PR #17 full deploy had
  seeded every year.)
- **Watch-item created by the fix**: each year now has TWO valid manifests for the same AddInId
  (ProgramData all-users + AppData per-user) — pre-multiversion this same coexistence ran fine for
  months, and both deploy in lockstep from the same build, so versions can't diverge. But if a future
  mid-session build ever fails copying to ProgramData with a file-lock error, that means Revit loaded
  the ProgramData copy (which has no rename-locked-DLL trick like AppData does) — revisit then, likely
  by asking Ajmal whether to drop the ProgramData deploy entirely. → 2026-07-26.

### 2026-07-22 (Smart Selection ignored an element pre-selected before the tool was run)
- **Symptom (Ajmal's words)**: "after runing tool i can select referance eelement first but before ithe
  running tool if i select the item its not concidering that is referance" — pre-selecting an element in
  Revit, then clicking the Smart Selection button, did not use that element as the reference; the tool
  always forced a fresh interactive pick.
- **Root cause**: `CmdSmartSelection.Execute()` always called `PickReferenceElement()` (an interactive
  `Selection.PickObject` prompt) unconditionally — it never checked `uiDocument.Selection.GetElementIds()`
  for a pre-existing selection made before the command ran.
- **Fix**: added `GetPreSelectedReference(uiDocument)` — if exactly one categorized element is already
  selected when the command launches, it's used directly as the reference (skipping straight to the
  follow-up box-select stage); zero, more than one, or an uncategorized pre-selection all fall back to the
  original `PickObject` prompt unchanged. `src/Commands/CmdSmartSelection.cs` v1.1.1, suite v1.23.5.
- **Verified how**: code-review + clean compile only (Release/2020 baseline and Release R25, both zero
  errors/warnings from this file) — not yet live-tested by clicking the button in Revit.

### 2026-07-21 (ProgramData all-users deploy writes a broken addin manifest path — found while pulling/building PR #17)
- **Symptom**: none reported by Ajmal — found incidentally while rebuilding all 8 Revit-year configs
  (2020-2027) to deploy PR #17. `%ProgramData%\Autodesk\Revit\Addins\<year>\AJ Tools.addin` (the "all users"
  manifest, written by the `DeployAjToolsAddin` target in `Directory.Build.targets`) points its `<Assembly>`
  path at `...\Addins\<year>\AJ ToolsAJ Tools.dll` — "AJ Tools" and "AJ Tools.dll" glued together with no
  path separator, so the referenced file never exists.
- **Root cause**: `Directory.Build.targets`, `DeployAjToolsAddin` target. `DeployRoots` is defined in
  `src\AJ Tools.csproj` as `Include="$(ProgramData)\Autodesk\Revit\Addins\$(RevitVersion)\"` (trailing
  backslash). The item transform `@(DeployRoots->'%(FullPath)$(RevitAddinDeployName)')` builds the deploy
  folder as `...\<year>\` + `AJ Tools` = `...\<year>\AJ Tools` (a valid folder, no trailing slash). But then
  `<DllPath>%(FullPath)AJ Tools.dll</DllPath>` uses that same item's own `%(FullPath)` metadata (which never
  carries a trailing separator) and concatenates the filename directly onto it, producing
  `...\<year>\AJ ToolsAJ Tools.dll` instead of `...\<year>\AJ Tools\AJ Tools.dll`. Net effect: the `AJ Tools\`
  subfolder gets created and `RemoveDir`/`MakeDir`'d every build but is always left **empty** — the actual
  DLL/PDB Copy lands one level up, sitting loose as a sibling file misleadingly named exactly `AJ Tools.dll`
  (this second file is harmless on its own, just an orphaned copy - the manifest that's supposed to point at
  it is the broken part, since its path math is different again and resolves to neither of the two real
  locations).
- **Practical impact**: low. Revit's per-user `%APPDATA%\...\Addins\<year>\AJ Tools.addin` (the
  `AutoDeployRevitAddin` target, unaffected by this bug) is what actually loads for Ajmal day-to-day, and it
  deployed v1.24.0 correctly to all 8 years. The ProgramData manifest with the bad path just fails to load
  silently (or Revit logs a missing-assembly warning) - it doesn't crash Revit or block the working AppData
  copy. Confirmed present on this machine for years 2020, 2024, 2027 (the only years that ever had a
  ProgramData entry - unclear when/how those three specifically got seeded, possibly an older pre-refactor
  manual deploy).
- **Fix**: none applied yet - out of scope for the PR #17 pull/build/release task in progress. Needs a
  one-line change in `Directory.Build.targets`: give `AjDeployDirs`' `DllPath`/`PdbPath`/`ResourcesPath`
  metadata an explicit `\` between `%(FullPath)` and the filename (or make the `AjDeployDirs` Include end in
  `\` before the transform).
- **Verified how**: read-only investigation (`find -printf`, direct file inspection of the addin XML and
  folder contents) on Ajmal's real ProgramData folders; not yet fixed or rebuilt to confirm the fix.
  -> 2026-07-21.

### 2026-07-20 (Match Graphics: second run seems "stuck" on the first run's color — not a code bug)
- **Symptom (Ajmal's words, dictated)**: gray duct + red duct test. Run 1: pick gray duct as SOURCE,
  click red duct as TARGET -> turns gray; can keep clicking more targets continuously, all correctly turn
  gray, in that one run. Cancel the tool. Run 2: pick the red duct as the new SOURCE this time, click the
  gray duct as TARGET -> color does NOT change. Ajmal's own theory: the tool is permanently storing the
  very first run's source color forever, so every later run (even the 10th) reapplies that same original
  color no matter what new source is picked.
- **Root cause investigated**: full code review of `CmdMatchElementGraphics.cs`, `CmdMatchCategoryGraphics.cs`,
  `GraphicsSelectionService.cs`, and `GraphicsOverrideBuilder.cs` (the whole Match Graphics call chain) -
  **no static or cached field exists anywhere in this chain.** `sourceElementId` and `sourceSettings` are
  plain local variables read fresh via `View.GetElementOverrides`/`GetCategoryOverrides` at the very top of
  every single `Execute()` call (Revit creates a new command instance per click), and
  `GraphicsOverrideBuilder.Clone()` does a real deep copy (`new OverrideGraphicSettings(source)`), never a
  shared reference. So Ajmal's literal "stored forever" theory does not match what the code actually does.
- **Most likely real explanation instead (unconfirmed live - bridge wasn't connected this session)**: Match
  Element/Category Graphics is a paint-picker - it copies the source's *current* override onto the target,
  which **overwrites the target's own original value**. In a 2-element back-and-forth test like Ajmal's,
  run 1 (source=gray -> target=red) destroys the red duct's real override the moment it's applied, replacing
  it with a value-copy of gray's settings. So in run 2, re-picking that same duct as the "new red source"
  just re-captures the gray values that were written into it a moment earlier - not its original red -
  and applying that onto the original gray duct produces no visible change because the values are now
  identical. This can look exactly like "frozen on the first color forever" while the tool is actually
  reading live, current state correctly every time; the state itself was changed by the previous run.
- **Fix**: none applied - no defect found to fix. Recommended test to tell the two theories apart: repeat
  with **3 distinct elements** (don't reuse the same 2 elements' roles back-to-back) - e.g. gray A -> red B
  in run 1, then a still-untouched red C -> gray A in run 2. If that still fails to recolor A, this
  diagnosis is wrong and needs live tracing instead.
- **Verified how**: code-review only - AJ AI Bridge was not connected this session (Revit not open), so the
  actual live override values on Ajmal's real gray/red ducts were not read back to confirm. Flagged to
  Ajmal to re-test with 3 elements, or reconnect the bridge so this can be confirmed against live element
  state rather than staying theoretical. -> 2026-07-20.

### 2026-07-19 (Highlight Selection: insulation left gray instead of red)
- **Symptom ("if the duct i selected or pipe for this is there insulation that insulation olso i need to
  be red")**: after clicking the new Highlight Selection tool (shipped same day, v1.20.0) on a duct/pipe
  that has insulation wrapped on it, the insulation stayed gray instead of turning red with its host.
  **Root cause**: `DuctInsulation`/`PipeInsulation` are separate elements with their own `ElementId` -
  hosted on the duct/pipe but never part of Revit's own selection when you pick the duct/pipe itself. The
  tool's highlight set was built strictly from `UIDocument.Selection.GetElementIds()`, so the insulation
  fell into the "everything else" gray bucket by default. **Verified against the real API, not memory**:
  bridge wasn't connected this session, so reflected directly over the installed `RevitAPI.dll` (2020,
  2024, and 2027 - all three identical) instead of guessing: `Autodesk.Revit.DB.InsulationLiningBase`
  has a public static `GetInsulationIds(Document document, ElementId elemId) -> ICollection<ElementId>`
  - exactly the lookup needed, same signature across all three installed versions, so no version-safe
  compat helper was needed for this one. **Fix**: `CmdHighlightSelection.cs` v1.1.0 (suite 1.20.1) - new
  `AddHostedInsulation()` calls `GetInsulationIds` per already-highlighted host element (wrapped in
  try/catch per element - categories that don't support insulation just return nothing) and folds any
  resulting insulation id that's also present in the active view into the red set before the gray "rest"
  pass runs. **Scope note, not done**: only covers host-selected -> insulation-follows; selecting the
  insulation directly does not (yet) pull in its host duct/pipe, and duct/pipe **lining**
  (`InsulationLiningBase.GetLiningIds`, a separate but same-shaped API) was not touched since only
  insulation was reported - flagged here in case Ajmal wants either extended later, same technique both
  times. **Verified how**: `Release` (2020) and `Release R25` (.NET 8) both compile-only rebuilt
  (`-p:SkipAjToolsAutoDeploy=true`) - 0 errors on both, 0 new warnings (R25's pre-existing ~648
  CA1416/SYSLIB0023 platform-compat warnings in unrelated files are untouched). Not yet deployed/live-
  clicked - Revit needs to be closed for the next real deploy build, then Ajmal re-tests by selecting an
  insulated duct/pipe and clicking Highlight Selection again. -> 2026-07-19.
  **Same-day follow-up**: Ajmal pushed back that insulation isn't only on ducts/pipes themselves - he
  recalled Duct Accessories, Pipe Accessories, and Mechanical Equipment can carry it too, and asked to
  check rather than assume. AJ AI Bridge was still not connected, so checked via `WebSearch`/`WebFetch`
  against Autodesk's own Revit API docs instead of memory: the `DuctInsulation`/`PipeInsulation` class
  remarks (revitapidocs.com) literally say "*represents insulation applied to the outside of a given
  duct/pipe, **fitting**, or **accessory/content***" - confirming Duct/Pipe Accessories are official,
  documented insulation hosts, not an edge case Ajmal misremembered. Also found (same source): calling
  `GetInsulationIds` on an id that isn't a valid insulation host throws `ArgumentException` ("This id
  does not represent a valid host for insulation"). **Turned out no code change was actually needed**:
  re-checked `AddHostedInsulation()` from the fix above and confirmed it already calls
  `GetInsulationIds` on **every** highlighted element with no category filter at all, catching that exact
  `ArgumentException` generically and skipping - so it was already correct for accessories/fittings/
  equipment/anything, by construction, not by accident. Only action taken: strengthened the code
  comments in `CmdHighlightSelection.cs` (both the class Notes and the `AddHostedInsulation` method) to
  say explicitly *why* it must stay category-agnostic, backed by the doc quotes above, so a future pass
  doesn't "simplify" it into a hardcoded Duct/Pipe-only check. No version bump (doc-comment only, no
  behavior change); compile-verified clean afterward regardless. -> 2026-07-19.

### 2026-07-16 (AutoDebugger bridge: second chat window can't connect after first one goes idle)
- **Symptom ("i will chat in one window and if i finish i did not tell that i finished and i move to
  another chat for another work its not coming")**: pinging from a second chat window times out
  ("semaphore timeout period has expired") while a first chat window is still technically connected,
  even though Ajmal stopped using it and never explicitly disconnected. **Root cause**:
  `McpBridgeService` creates the named pipe with `maxNumberOfServerInstances = 1` (one slot, total) and
  (since v1.1.0) keeps a connected session's pipe open indefinitely between requests rather than
  reconnecting per call — see the `project_autodebugger_mcp` cross-session memory for the full
  architecture. Whoever connects first holds the only slot for their entire session; a
  forgotten-but-not-closed chat window blocks every other chat indefinitely. **Fix**
  (`McpBridgeService.cs` v1.4.0, suite 1.13.4): `HandleConnectionAsync`'s read loop now races
  `ReadLineAsync()` against a 3-minute `Task.Delay` (`IdleReleaseTimeout`); if the delay wins, the
  connection is dropped and the slot frees up for the next chat window. Confirmed safe for the idle
  session too — the Node MCP client (`mcp-server/index.js`, `getConnection()`) already detects a
  closed/destroyed socket and transparently reconnects on its own next call, so the idle chat sees no
  error, just a fresh connection next time it's used. **Verified how**: `Release` (2020) compile-only
  build (`-p:SkipAjToolsAutoDeploy=true`) — 0 errors, 0 warnings. Not version-sensitive API, so no
  additional newer-config build done. Not yet deployed/live-tested — Revit was live-connected to the
  bridge during this fix (mid-session), so deploy was held pending Ajmal's go-ahead (would overwrite a
  loaded add-in DLL).
  **Follow-up same day**: Ajmal clarified his real workflow is strictly sequential (finish everything
  in one chat, then move to the next - never two at once) and asked whether a new connection could
  just immediately take over instead of waiting out the 3-minute idle timer. Confirmed possible and
  built as `McpBridgeService.cs` v1.5.0 (suite 1.13.5): pipe instance count raised 1 -> 2 so
  `ListenLoopAsync` can keep one instance always listening for the next connect while another
  services the active chat; the moment a new client connects, the previous active pipe is disposed
  immediately (preempted, no wait) and the new one takes over. The 3-minute idle release from v1.4.0
  stays in place as a secondary fallback (covers an abandoned session with no one else trying to
  connect). Risk noted to Ajmal and accepted: if the preempted chat was genuinely still mid-script
  (not just idle), it loses its result and must retry - acceptable since he never runs two at once.
  Also confirmed via `RevitExecutionService.cs`'s `_isRunning` re-entrancy guard that Revit itself
  only ever runs one script at a time regardless, so true concurrent execution was never available to
  offer. **Verified how**: `Release` (2020) compile-only build, 0 errors, 0 warnings. Not yet
  deployed/live-tested - same live-bridge-in-use situation as v1.4.0. -> 2026-07-16.

### 2026-07-16 (AJ AI activity popup progress bar frozen — different bar from the one below)
- **Symptom (clarified after the first fix)**: Ajmal specified he meant the small floating popup that
  appears over Revit while AJ AI is working ("AJ AI is working" card, top-of-screen, non-modal) — its
  own thin progress bar never moves. Not the same as the AJ AI pane's execution bar (previous entry).
  **Root cause**: `AiTaskWarningBarService.EnsureBanner()` built the bar as a plain `Border` with a
  **hardcoded fixed `Width = 238`** — pure decoration, never bound to any value or animated in any way.
  Unlike the pane's bug, this one had no crash hiding it; it was simply never wired up in the first
  place. Also relevant: `BeginTask()`/`EndTask()` (called from `McpBridgeService` around every bridge
  call, ping or script) carry no real percentage — there's nothing genuine to bind a normal progress
  value to. **Fix** (`AiTaskWarningBarService` v1.4.0, suite 1.13.3): replaced the static fill with an
  indeterminate sweeping-highlight `Storyboard` (a short bright segment inside a `Canvas`, animating
  `Canvas.Left` from off-left to off-right, `RepeatBehavior.Forever`) — honest signal of "something is
  happening" rather than a fake percentage. Storyboard starts right after `_banner.Show()` and is
  explicitly `.Stop()`ped in the banner's `Closed` handler (mirrors the existing `_closeTimer` stop
  pattern) so a forever-repeating clock doesn't keep ticking after the popup closes. **Verified how**:
  `Release` (2020) rebuilt with deploy — 0 errors, 0 warnings, deployed to
  `AppData\...\Addins\2020\AJ Tools.20260716163009845\`. `Release R25` (.NET 8) force-rebuilt — 0
  errors; the only 4 warnings touching this file are pre-existing CA1416 `Screen.FromHandle`/
  `WorkingArea` platform-compat noise on the unchanged window-positioning line, not from the new
  animation code. Not yet live-tested — needs a Revit restart (Ajmal was actively modeling live during
  this fix; the running session and open model were not touched, only files on disk + a fresh
  timestamped Addins deploy folder). After restart: trigger any AutoDebugger bridge call and watch the
  popup's bottom edge — a bright segment should sweep left-to-right on a loop while it's visible.
  -> 2026-07-16.

### 2026-07-16 (AJ AI execution progress bar frozen)
- **Symptom ("in the ai aj progrss bar updae. not its static")**: the progress bar in the AJ AI pane
  never moves during a script run — sits at "Initializing... 0%" then jumps to "Done 100%".
  **Root cause (two layers)**: (1) `GeminiShellViewModel`'s `ReportProgress` callback marshalled UI
  updates via `System.Windows.Application.Current.Dispatcher` — but `Application.Current` is always
  null inside Revit (the exact root cause already found in `AiTaskWarningBarService` on 2026-07-12;
  this second call site was missed then). Worse than cosmetic: any AI-generated script that actually
  called `ReportProgress(...)` threw a NullReferenceException and failed/rolled back, so the auto-fix
  loop tended to regenerate scripts *without* progress calls — hiding the crash and leaving the bar
  static. (2) Even with a valid dispatcher, the script runs ON Revit's UI thread
  (`RevitExecutionService` blocks it in `task.Wait()`), so WPF never repaints mid-run — values can
  update but nothing visibly moves. **Fix** (`GeminiShellViewModel` v1.2.1, suite 1.13.2): capture
  `Dispatcher.CurrentDispatcher` at ViewModel construction, use it in the callback, and after setting
  the values pump an empty `Invoke` at `DispatcherPriority.Render` so the bar visibly repaints while
  the thread is otherwise blocked (Render priority doesn't process user input — no re-entrancy risk).
  Repo-wide sweep confirmed no other live `Application.Current` call remains in `src/`; rule promoted
  to `ajtools-conventions.md` § WPF inside Revit. **Verified how**: `Release` (2020) rebuilt — 0
  errors, 0 warnings; `Release R25` (.NET 8) rebuilt — 0 errors, all 632 warnings pre-existing CA1416
  noise (10 in this file are the old FolderBrowserDialog lines, untouched). Deployed as next-start
  payload 1.13.2 (AppData manifest updated; live session untouched). NOT yet live-tested — needs a
  Revit restart, then run any script that calls ReportProgress and watch the bar move. The null
  `Application.Current` root cause itself was live-verified 2026-07-12; the AutoDebugger bridge was
  unreachable during this fix (Revit busy/disconnected), so no fresh live check this session.
  Note: the small indeterminate spinner next to the status text will still freeze during script
  execution — inherent to the blocked UI thread, only ticks when ReportProgress pumps a repaint —
  flagged here so it isn't re-reported as a new bug. -> 2026-07-16.

### 2026-07-16 (MEP Color Data Standard rollout — 2 real bugs caught, not just cosmetic renames)
- **Symptom**: none reported by Ajmal — both bugs below were caught by AI cross-checking data instead of
  trusting names, while syncing `MEP Color Data Standard.xlsx` into the live model's Duct/Pipe System
  Types, Materials, and View Filters (full technique in `live-model/mep-color-standard.md`).
- **Bug 1 — Kitchen Exhaust (KED) duct system silently shared its Material with Return Air (RAD)**.
  **Root cause**: both system types' `Material` parameter resolved to the exact same `ElementId`
  (`HVAC_AC_Return Air Duct System_RAD`'s material) — invisible unless you compare resolved Material
  `ElementId`s across the whole set rather than trusting each type's own name. **Fix**: held the rename
  back and asked; Ajmal created a proper dedicated material and confirmed. **Verified how**: fresh
  read-back showed KED now resolves to its own distinct `ElementId` (1101895), matching its own name.
- **Bug 2 — filter graphic overrides had Projection Line/Pattern colors set but Cut Line/Pattern colors
  completely unset, on all 26 filters in "GROUND FLOOR HVAC"**. Plus 2 small RGB typos found in the same
  pass (Soil Pipe 166→165, Refrigerant Vapor 211→221 — one-digit manual entry mistakes). **Root cause**:
  whoever originally set these filter overrides only touched the Projection side; Revit does not default
  Cut colors from Projection colors, so any duct/pipe segment actually sliced by the plan view's cut
  plane would show default/black instead of its standard color. **Fix**: set `CutLineColor` and
  `CutForegroundPatternColor` (+ pattern Id + visibility) explicitly for all 26 filters, corrected the 2
  RGB typos. **Verified how**: fresh read-back, all 4 color slots (Projection Line, Cut Line, Surface
  Pattern, Cut Pattern) confirmed matching the Excel standard, 0 mismatches out of 26. -> 2026-07-16.

### 2026-07-13 (Duct tag leader clashing with its own tag)
- **Symptom ("look at the tags leader its clashing with own tag chek each and every tag")**: after
  live-tagging 38 ducts via `tag-elements-in-active-view.cs`, Ajmal reported the leader lines visually
  ran into their own tag's text. **Root cause**: the script reused `LeaderLogicService`'s default
  `minHorizontalStub` (152mm) as the elbow-push distance for the "same-X" guard case, but that constant
  is only a straight-line-risk threshold, not a real clearance distance — the project's actual duct tag
  family measures 395mm half-width around the head, well past 152mm. **Verified systemically, not
  assumed**: measured all 38 tags' real `get_BoundingBox(view)` against `TagHeadPosition`/`LeaderElbow`
  — 26 of 38 were genuinely clashing (a defect affecting most axis-aligned duct runs, not a rare edge
  case). **Fix**: deleted and recreated all 38 tags with the elbow push set to a measured-safe 550mm
  instead of the borrowed 152mm guard constant. **Verified how**: re-ran the identical clash check after
  the fix — 0 of 38 clashing. Recipe and `live-model/tagging.md` updated with the measurement technique so
  a different tag family/project gets re-measured instead of reusing this number blindly. -> 2026-07-13.

### 2026-07-13 (Revision auto-purge near-miss, sheet-date revision assignment)
- **Symptom**: while attaching the 8 newly-created "IFI" revisions to their matching sheets via
  `ViewSheet.SetAdditionalRevisionIds`, a fresh read-back showed the project's 2 pre-existing revisions
  (Seq 1 "2020/12/26, Rev. 01, by Alessio Orlando" and a blank Seq 2) had disappeared entirely — gone
  even from a raw `FilteredElementCollector(doc).OfClass(typeof(Revision))`, not just filtered out of a
  summary. **Root cause**: neither old revision was attached to any sheet or cloud; Revit appears to
  auto-purge such "orphan" revisions the next time something forces the revision sequence to recompute
  (see full technical detail in `live-model/revisions.md` § Revision sequence auto-purges unused revisions).
  **Handled per house rule**: stopped immediately, did not guess or silently recreate anything, reported
  the exact lost values to Ajmal and asked how to proceed before taking any further action. **Resolved**:
  Ajmal confirmed those 2 revisions were already stale/unwanted from his own earlier cleanup — no
  recovery needed, nothing further done. **Verified how**: live read-back confirmed the 8 real revisions
  remain correctly attached to their matching sheets; no code fix needed, this was model-data behavior,
  not an AJ Tools bug — captured as a knowledge gotcha instead. -> 2026-07-13.

### 2026-07-13 (Pin Elements scroll)

- **Symptom ("scroll is not working, i have to select the scroll bar and drag ... mouse scrolling is
  not working")** -> **Root cause**: `PinElementsWindow.xaml` wraps its two category `ListBox`es (Sheet
  Items, Model Items) in an outer `ScrollViewer` (added so the combined content can scroll once it
  exceeds the window's `MaxHeight`), but `ModernListBox`'s own control template (`ModernStyles.xaml`)
  embeds a second, inner `ScrollViewer` around every ListBox. WPF's `ScrollViewer` always marks a
  `MouseWheel` event as handled once it processes it - even when it has nothing to scroll internally -
  so the inner ScrollViewer silently swallowed the wheel event before it could bubble up to the outer
  one. Dragging the outer scrollbar thumb worked fine (a Thumb-drag, not a MouseWheel event), which is
  exactly the split Ajmal described. Likely went unnoticed until now because the Model Items list only
  just grew past the window's visible height after Grids/Levels were added as two more groups earlier
  today. **Scope check**: confirmed this nested-ScrollViewer pattern is specific to `PinElementsWindow` -
  every other AJ Tools window's lists sit directly in a sized Grid cell with no second outer
  ScrollViewer, so nothing else needed touching. **Fix**: added a `PreviewMouseWheel` handler on both
  ListBoxes that re-raises the event sourced at the ListBox itself, so it bubbles past the ListBox's own
  template (skipping its internal ScrollViewer) straight to the window's outer ScrollViewer. No change to
  the shared `ModernStyles.xaml`. **Verified how**: `Release` (2020) build clean, zero errors, deployed;
  not yet live-clicked in Revit. -> 2026-07-13.

### 2026-07-13

- **Symptom ("some tools are hidden and sometimes not coming properly, e.g. Colorize")** ->
  **Root cause**: Colorize (command, availability, two services, two UI files) had been hand-ported
  to C# on 2026-07-02, but landed only in the stale pre-multiversion `AJ Tools\` copy - never in the
  live, actually-built `src/` project. It could never appear on the ribbon no matter how many times
  the add-in was rebuilt, because its code simply wasn't part of what gets compiled/deployed. A repo
  sweep (diffing every `IExternalCommand` class against every `typeof(...)` reference in
  `RibbonManager.cs`/`AnnotationRibbonManager.cs`) found this is the only *fully missing* tool; one
  genuinely orphaned class also exists (`CmdQuickParallelDimension`, the pre-split plain command in
  `CmdQuickParallelDimension.cs`, superseded by `CmdQuickParallelCenterLineDimension` /
  `CmdQuickParallelFaceEdgeDimension` which are both wired) - harmless dead code, left alone since
  fixing it wasn't what was reported and touching it would have been unrelated scope. **Fix**: ported
  all 7 Colorize files into `src/` (`Commands/CmdColorize.cs`, `Commands/CmdColorizeAvailability.cs`,
  `Services/Colorize/ColorizeApplier.cs`, `Services/Colorize/ColorizeElementMatcher.cs`,
  `UI/ColorizeWindow.xaml(.cs)`, `Resources/Colorize.png`), added `ColorPalette.GetColorAt(index)`,
  and wired the button into `RibbonManager.cs`'s View panel next to Filter Pro (matching where Ajmal
  had already placed it in the old tree). The straight copy would NOT have compiled: live Filter Pro
  had evolved since 2026-07-02 and no longer exposes `FilterRuleBuilder.BuildRules`,
  `FilterApplier.BuildOverrideSettings`, or `FilterApplier.HasAnyGraphicsToggleEnabled` (Filter Pro's
  own overrides are now filter-centric, built inline in `FilterApplier.ApplyGraphicsToFilter`) - wrote
  small self-contained equivalents inside `ColorizeApplier.cs` instead of widening Filter Pro's
  internals for one caller. First rule-building pass introduced a real bug (caught before reporting
  done): the "Family + Type" virtual parameter needs TWO ANDed rules (family name AND type name), but
  the ported version only returned one, which would have colorized every element of a matching family
  regardless of type - fixed by returning `IList<FilterRule>` instead of a single `FilterRule`.
  **Verified how**: `Release` (2020) and `Release R25` (.NET 8) both built clean via
  `msbuild "AJ Tools.sln" -p:SkipAjToolsAutoDeploy=true` - 0 errors both configs; R25's ~630 CA1416/
  SYSLIB0023 warnings are pre-existing platform-compat noise in unrelated files (none reference
  Colorize). Not yet live-tested by clicking Shuffle Colors in Revit - build-verified only. -> 2026-07-13.

### 2026-07-12

- **Symptom ("can you make this warning beautiful")** -> **Root cause**: the working activity banner
  still used a plain yellow WarningBar-like surface, visually disconnected from AJ Tools. **Fix**:
  restyled it as a compact dark AJ Tools card with blue AI badge, live status dot, soft shadow, and blue
  progress accent; behavior stays non-blocking and model-safe. **Verified how**: `Release|x64` built and
  deployed with zero warnings/errors; the next-start payload is version 1.10.5. -> 2026-07-12.

- **Symptom ("small codes taking from this is time consuming")** -> **Root cause**: the visible
  `tools/invoke-revit-bridge.ps1` wrapper spawned a second `powershell.exe -File` process, which split
  multi-line C# passed through `-Code` into positional arguments. **Fix**: invoke the underlying helper
  directly and splat its parameters, preserving the complete code string in memory. **Verified how**:
  direct multi-line duct query returned `Count: 88` with no temporary `.cs` file. -> 2026-07-12.

- **Symptom ("forms.WarningBar same like this, AI working time only")** -> **Root cause**: AutoDebugger
  tasks had no visible Revit-wide indication, so work was only apparent in the AJ AI pane. **Fix**: added
  `AiTaskWarningBarService`, a non-modal yellow top-of-Revit banner, and bracketed each authenticated,
  validated non-ping bridge task in `McpBridgeService` with `BeginTask()` / `EndTask()` in a `finally`
  block. It closes automatically after success or failure and does not alter the model. **Root cause of
  the first fix failing**: Revit returns `null` for `System.Windows.Application.Current`, so the service
  silently skipped UI work. **Fix**: capture `Dispatcher.CurrentDispatcher` when `McpBridgeService` is
  created on Revit's UI thread and use that dispatcher for all banner work; fast requests remain visible
  for 0.8 seconds without delaying Revit work. **Verified how**: a live AutoDebugger diagnostic returned
  `WPF Application.Current is null`; `Release|x64` built/deployed with zero warnings/errors and the
  current-user manifest points to the unlocked 1.10.4 payload. Revit restart is required for live UI test.
  -> 2026-07-12.

- **Symptom ("check my AI tool, i changed a lot, any mistake, how to improve speed")** -> **Root cause**:
  no bug found in the 2026-07-11 AutoDebugger speed pass (`McpBridgeService.cs`, `RoslynService.cs`,
  `mcp-server/index.js`) — thread-marshalling to the Revit UI thread via `RevitExecutionService`'s
  `ExternalEvent`, the compiled-script cache, and the persistent pipe connection are all wired correctly.
  Only gap found was documentation drift: `AssemblyInfo.cs` was already bumped to 1.10.1.0 but
  `CHANGELOG.md`'s Unreleased section never got a line for the perf work. **Fix**: added the missing
  CHANGELOG bullet; no source change needed. **Verified how**: clean `Release` (2020) and `Release R25`
  builds, zero errors on both (R25's 99 warnings are pre-existing CA1416 platform-compat noise in
  unrelated WinForms files — `CmdReassignLevel.cs`, `CmdIntelligentTagArrangerSettings.cs`,
  `GraphicsOverrideWindow.xaml.cs` — not from this change); `node --check` on `index.js` passed;
  `.claude/tools/verify-knowledge-consistency.ps1` passed clean. -> 2026-07-12.
- Noted for a future speed pass (not applied, just an idea, flagged to Ajmal): the compiled-script cache
  in `RoslynService` is keyed on exact source text, so it mainly helps repeated/templated calls (`ping`,
  `model_summary`) — AI-authored one-off scripts differ in text almost every time and rarely hit it.
  A bigger win for those would be pre-warming Roslyn (compiling one trivial dummy script) right when
  "Connect AutoDebugger" is clicked, so the first real query of a session doesn't pay Roslyn's one-time
  JIT/assembly-load cost. Not implemented — ask before adding, since it changes `McpBridgeService.Start()`
  behavior and needs its own rebuild + live check.

### 2026-07-11

- **Symptom ("speed up program")** -> **Root cause**: each MCP call created a new named-pipe connection,
  and repeated Roslyn scripts rebuilt their options, syntax tree, and compilation every time. **Fix**:
  persist the authenticated pipe session, cache up to 64 safe compiled scripts, and retain live document
  reads on every call. **Verified how**: Release build completed with zero warnings/errors; a local pipe
  simulator confirmed two sequential MCP pings used one connection. Deployment/live add-in reload is still
  required before testing the new Revit-side loop in Revit. -> 2026-07-11.

### 2026-08-04

- **Symptom (would have broken the v1.39.1 release, caught pre-publish)**: `sha256sum -c SHA256SUMS.txt`
  failed with `No such file or directory` even though the hash was correct. **Root cause**:
  `AJ-Tools-Installer/tools/prepare-release.ps1` wrote the manifest with `Set-Content`, which emits CRLF
  on Windows PowerShell. `sha256sum` on the Ubuntu runner treats the trailing `\r` as part of the
  filename, so it looks for `AJ-Tools-v1.39.1.zip\r` and finds nothing — and
  `.github/workflows/publish-release.yml` runs exactly that check before publishing. The previous
  release's manifest was LF, so this was a silent regression, not a long-standing bug. **Fix**: write
  the bytes explicitly — `[System.IO.File]::WriteAllText($path, "$line`n", ASCII)` — plus a
  `.gitattributes` rule (`SHA256SUMS.txt text eol=lf`) so git can't reintroduce CRLF. **Verified how**:
  regenerated the manifest, ran `sha256sum -c` locally (`OK`), then confirmed the real
  "Publish Installer Release" workflow run completed with `conclusion: success` and both assets
  uploaded. -> 2026-08-04.

- **Symptom**: the Revit 2025+ configurations built with 11 `CA1416` warnings on Windows-only WinForms
  calls (`ColorDialog`, `FolderBrowserDialog`, `Screen`) while the 2020 baseline was warning-free — the
  tail of the count this project tracked down from 682 -> 490 -> 274 across v1.25.3-v1.25.5 by rewriting
  dialogs. **Root cause was never the call sites**: `src/AJ Tools.csproj` sets
  `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` because `AssemblyInfo.cs` is hand-maintained, and
  that suppresses the `[assembly: SupportedOSPlatform("windows")]` marker the SDK would otherwise emit
  for a `net8.0-windows` target. Without it the analyser assumes the assembly might run on Linux.
  **Fix**: declare the attribute manually at the foot of `AssemblyInfo.cs` under
  `#if NET5_0_OR_GREATER` (net472/net48 have no such attribute type). One line cleared all 11 — chasing
  individual call sites was never going to finish. **Verified how**: `Release` and `Release R25` both
  rebuilt 0 warnings / 0 errors. -> 2026-08-04.

- **Symptom (user-facing, found only because Ajmal asked "is everything updated in the installer
  too?")**: `AJ-Tools-Installer/README.md` advertised installer version `v1.25.0` and told users to
  extract `AJ-Tools-v1.13.5.zip`; `INSTALL.md` named `AJ-Tools-v1.25.0.zip`. None of those filenames
  existed in the repo, so anyone following the "Download This Repository" or manual-install steps hit
  a missing file. **Root cause**: `tools/prepare-release.ps1` stages the zip and checksum but only
  *prints* "next steps" — it never touched the docs, so every release since at least v1.13.5 left them
  behind (v1.25.6 didn't update them either; this was not introduced by v1.39.1). **Fix**: the script
  now stamps the version into `README.md` and `INSTALL.md` itself, rewriting only concrete version
  numbers via regex (`AJ-Tools-v\d+\.\d+\.\d+\.zip` and the `Current installer version:` line) and
  deliberately leaving the generic `AJ-Tools-vX.Y.Z.zip` placeholder in the Option 1 instructions
  alone. **Verified how**: re-ran the script, saw `Updated version references in README.md /
  INSTALL.md`, confirmed all three references now read 1.39.1, confirmed the placeholder survived, and
  re-checked `sha256sum -c` still passes. **Lesson**: a release script that ends in "Next steps: ..."
  is a list of things that will eventually be forgotten — automate the step instead of printing it.
  -> 2026-08-04.

- **Symptom**: "Auto Duct Dimension" ran over a whole view and finished in complete silence — no count,
  no message, no way to tell whether it did 40 dimensions or 4, or why anything was skipped. **Root
  cause**: `DuctReferenceDimensionReport` tracked created/skipped/failed with reasons and had a fully
  written `BuildSummary()` — and **nothing ever called it**. The service used only `report.HasActivity`
  to choose its `Result`. Repo-wide grep confirmed Create Tags, Stack Tags and Shared Parameter all show
  their summary; this one alone did not. **Fix**: the service now RETURNS the report and the command
  shows it (shared code must never raise its own dialog — a `TaskDialog` from a non-ribbon caller blocks
  Revit). Reporting is on by default and switchable in settings. **Lesson**: a report class with no
  call site is worse than none — it reads as "this is handled" in every later review. -> 2026-08-15.

- **Symptom**: dimensioning to an element inside a Revit link threw *"the references are not geometric
  references"* even though `Reference.CreateLinkReference(linkInstance)` returned a non-null reference
  and is present in the API right back to 2020. **Root cause**: `CreateLinkReference` produces a
  reference Revit accepts for face-based family placement but **not** for `NewDimension`. Its stable
  representation carries `RVTLINK/<linkTypeUniqueId>`; dimensioning needs a bare `RVTLINK`. **Fix**:
  rebuild the reference — `ConvertToStableRepresentation(hostDoc)`, reduce that segment, then
  `ParseFromStableRepresentation`. See `DimensionSource.PrepareForDimensioning`. **Verified how**: the
  rewrite was traced by hand on a real representation string and the API members were confirmed by
  reflection against the installed 2020 and 2024 `RevitAPI.dll`; **not yet run inside Revit**.
  **Lesson**: this is the Modeler-mindset case again — the API returned a perfectly valid-looking object
  and the obvious call was still wrong. -> 2026-08-15.

- **Symptom**: the duct dimension tool found no faces at all in a **Coarse** plan view. **Root cause**:
  `get_Geometry` with `Options.View = <coarse view>` returns a valid, non-null `GeometryElement` — full
  of `Line`s, because Revit draws MEP as single lines at Coarse, and containing no `Solid`. The
  model-geometry fallback was gated on `geometry != null`, so it never ran. **Fix**: gate the fallback on
  "did a usable Solid actually come back", then retry with model options and
  `DetailLevel = ViewDetailLevel.Fine` (`View` and `DetailLevel` cannot both be set on one `Options`).
  **Lesson**: "not null" is not "usable" — check for the thing you actually need. -> 2026-08-15.

- **Symptom**: a plan view with exactly two grids got two identical dimension strings stacked one above
  the other. **Root cause**: `AutoDimensionService` created the overall (first-to-last) row
  unconditionally; with two datums that is the same measurement as the individual chain, just one row
  further out. **Fix**: suppress the overall row at two datums, on by default and switchable. The same
  rewrite also isolated each row in its own transaction — previously one reference Revit refused threw
  out of the single shared transaction and the whole run placed nothing. -> 2026-08-15.

- **Symptom**: in Auto MEP Dimension, ticking "Other services" did nothing — a duct never picked up the
  pipe or cable tray crossing it — and the only thing that "worked" was ticking Pipes in section 1,
  which then dimensioned every pipe as a run in its own right. **Root cause**: the collector sourced its
  MEP-run references from `GetMeasuredCategories()`, i.e. the section 1 "Services to dimension" ticks,
  then dropped the seed's own category. On the shipped default (Ducts only) that left an EMPTY list, so
  the row shipped ticked and inert. Two lists were answering different questions with one set of data:
  section 1 is *what gets dimensioned*, section 2 is *what it is measured to*. **Fix**: the reference
  pass enumerates `SupportedMeasuredCategories` (all six) and splits same-vs-other by comparing against
  the seed run's category; section 1 no longer gates section 2. **Lesson**: when one setting silently
  constrains another, the inert control looks like a broken feature — and the user's workaround
  (ticking the category) makes a second, worse problem. -> 2026-08-15.

- **Symptom**: ticking only "Same service (duct to duct)" and saving came back with **Walls** switched
  on again, silently. **Root cause**: two guards disagreed. `MepDimensionSettings.Normalize()` required a
  target that is not None, not MepRun and not SameServiceRun, forcing Walls on when none existed; the
  window's `Validate()` excluded only MepRun, so Save stayed enabled. Normalize then rewrote the choice
  after the window closed. **Fix**: the window's guard now matches Normalize exactly, so the user is
  told "tick at least one reference" instead of having their choice quietly changed. **Lesson**: a UI
  validator and a model normaliser encoding the *same* rule in two places will drift — and when they do,
  the silent one wins. -> 2026-08-15.

- **Symptom**: "No grids or levels were found in this view to dimension", sending the modeller off to
  check crop, visibility, worksets and links — when the real cause was `GridScope = Do not dimension`
  set days earlier in a different project (the settings file is user-level and shared by every project
  and all three buttons). **Fix**: the service now detects "nothing is switched on for this button" before
  it collects anything and names the setting. **Lesson**: a "found nothing" message must be able to tell
  *searched and found nothing* apart from *never searched*. -> 2026-08-15.

### 2026-08-20

- **Symptom (Ajmal's words)**: the AJ Quick Menu wheel is "sometimes very slow and sometimes its not
  running the tool correctly". **Root cause — two separate faults, neither where the first guess
  pointed.** (1) *Not running*: the wheel posted every pick with `UIApplication.PostCommand`, and
  **Revit silently ignores a posted command whose `IExternalCommandAvailability` says no** — no
  exception, no return value, nothing. `QuickMenuLauncher.TryRun` therefore reported success and
  `CmdQuickMenu` returned `Result.Succeeded` while nothing whatsoever happened. **Five of the eight
  default wheel slots carry an availability class** (Unhide All + Highlight Selection via
  `CmdGraphicalViewAvailability`, Toggle Revit Links, Colorize, Filter Pro), so on a sheet, schedule,
  legend or any view where `AreGraphicsOverridesAllowed()`/`CanCategoryBeHidden` is false, most of the
  default wheel was dead and mute. (2) *Slow*: the wheel is `AllowsTransparency="True"`, which forces
  **software rendering of the whole window**, and it carried a `DropShadowEffect` blur re-applied on
  every hover change plus an opacity fade over the entire ring on open. **Fix**: new
  `QuickMenuAvailability` asks each button's *own* availability class and the wheel draws unavailable
  slots greyed out, unpickable, with the reason in the hub — the panel's behaviour, mirrored rather
  than re-implemented; `CanPostCommand` guards the launch so a refusal is explained; blur and fade
  removed in favour of a brighter fill and thicker stroke; close-on-lose-focus now arms on the pop-in's
  `Completed` instead of `ApplicationIdle` (it was arming before the window was really up, so a stray
  focus change swallowed the wheel); Enter/Space with nothing hovered and an out-of-range number key
  no longer close the wheel doing nothing. **Verified how**: `Release` (net472/2020), `Release R25`
  (net8.0-windows) and `Release R27` (net10.0-windows) all build 0 errors — R27 needs `dotnet build`,
  because VS2022's MSBuild is pinned to SDK 9 and cannot target net10.0 even though SDK 10 is
  installed. `verify-wpf-styles.ps1`, `verify-window-styles.ps1` and `verify-version-consistency.ps1`
  all pass. API facts (`PushButton.AvailabilityClassName`, `IExternalCommandAvailability`'s single
  method, `UIApplication.CanPostCommand`) were read from the installed Revit 2020 **and** 2027
  `RevitAPIUI.dll` with `ildasm`. **NOT tested inside Revit** — the AJ AI Bridge was not connected, so
  no live check of the greying or the launch was possible. **Lessons**: (a) a silent API no-op is the
  worst failure mode there is — `PostCommand` returning normally means nothing about whether the
  command ran; (b) the first review pass of the fix introduced a *new* slow path — the `CategorySet`
  passed to an availability class costs a full selection walk and was being rebuilt per slot, which
  would have made the wheel stall on a large selection, i.e. a performance bug inside a performance
  fix, caught by adversarial review before it shipped. -> 2026-08-20.
