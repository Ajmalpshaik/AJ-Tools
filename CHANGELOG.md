# Changelog

This changelog tracks tagged AJ Tools source milestones. Public installer downloads are published separately in `AJ-Tools-Installer`.
Release tags should use `vX.Y.Z`. Older legacy tags with other formats remain in repository history.

## [Unreleased]

## [1.40.3] - 2026-08-05

- **Changed**: The tick boxes inside the scrolling lists (View Crop target views, Section Mark views) now
  match the rest of AJ Tools too. Their tick appears instantly rather than animating, on purpose - those
  rows are rebuilt as you scroll, so an animated tick would flicker down the list.
- **Added**: **Purge Unplaced Views** now shows the same progress bar and live count as Purge Unused
  Elements. These two are the only tools that genuinely sit silent for seconds, because both have to try
  deleting every candidate and undo it again to find out what Revit will release.
- **Changed**: The boxes you type in on Graphics Settings Manager now light up on hover and focus like
  every other field in the suite. They were the last ones that didn't.
- **Note**: Three things were deliberately left alone. Sliding panels open and shut inside a window would
  make those windows resize while animating, because they size themselves to their contents. The AJ AI
  docked panel has no arrival animation because that one file loads during Revit's startup - a fault
  there stops the whole add-in loading, which has happened before, and a fade on a panel isn't worth
  that risk. And the tick column in the Duct Standards table keeps its own behaviour.

## [1.40.2] - 2026-08-05

- **Changed**: **Tick boxes and radio buttons now look like AJ Tools.** They were the last thing in the
  suite still drawing plain grey Windows squares inside otherwise soft, rounded, blue windows - about 90
  of them across 21 windows. A tick box is now a rounded box that fills blue with the tick scaling in;
  a radio button is a matching circle with a dot that pops in. Both light up on hover, press in when
  clicked, and fade when they grey out.
- **Changed**: The three "overwrite existing" options (Filter Pro, Transfer Views, Transfer View
  Templates) are now real **on/off switches** with a knob that slides across, matching the switch in
  Graphics Settings Manager. They were always named switches internally; they just never looked like one.
- **Note**: Nothing moved. All the original spacing and sizing is kept, so every window lays out exactly
  as before - only the boxes themselves are redrawn.
- **Note**: The tick column in the Duct Standards table is deliberately left as it was. It is inside a
  scrolling table, where an animated tick would replay every time a row scrolled into view.

## [1.40.1] - 2026-08-05

- **Added**: **Purge Unused Elements** now shows a progress bar and a running count while it scans -
  "Checking element 340 of 1,200..." - instead of freezing behind an hourglass with no idea how long is
  left. This was the longest silent wait in the suite: the scan has to try deleting every candidate and
  undo it again, just to find out what Revit will actually let go.
- **Note**: The scan itself is not faster, and it does not run in the background. Revit only allows its
  own work on its own thread, so the scan runs exactly where it always did - the window just repaints
  part-way through now instead of looking dead. Same elements checked, same answers.
- **Note**: You still cannot click anything mid-scan. That is deliberate: the repainting is done in a way
  that redraws the window without accepting clicks, so a button press cannot land in the middle of a scan
  or a delete.
- **Note**: The progress row only appears while something is running and takes no space otherwise, so the
  window looks exactly as it did before.

## [1.40.0] - 2026-08-05

- **Added**: Windows now close with a short fade and a slight sink, instead of vanishing. It is quicker
  than the opening motion on purpose - about a seventh of a second - so closing feels decisive rather
  than slow. All 33 tool windows. The About window keeps its own longer closing motion.
- **Important**: **Every window still returns exactly the same answer as before.** Click Run and the tool
  runs; click Cancel and nothing happens. This was measured on every combination rather than eyeballed,
  and there is now a permanent check that re-proves it.
- **Fixed before it ever shipped**: the obvious way to build a closing animation would have **broken
  every Run button in the suite**. To animate on close, the window has to stop itself closing, play the
  animation, then close for real - and Windows throws away the window's answer when a close is stopped
  that way. Every tool asks "did the user click Run?" before doing anything, so the answer would have
  come back as "no" every time: the window would open, close, and the tool would silently do nothing,
  with no error to tell you why. The answer is now saved before the close is stopped and put back
  afterwards.
- **Safety**: if the animation ever fails to finish, a timer closes the window anyway. A window you
  cannot close would be far worse than a missing animation, so closing never depends on the animation.
- **Note**: Checked first that no window does anything on closing that would be harmed by running twice.
  Only Pipe Sizing does anything at all - it saves your entries - and saving the same entries twice
  changes nothing.

## [1.39.7] - 2026-08-05

- **Added**: Switching tabs no longer hard-cuts. The new panel fades in while rising slightly, so it
  reads as the panel arriving rather than the window flicking to something else. It affects the five
  windows that have tabs: Colorize, Duct Standards Manager, Filter Pro, Graphics Settings Manager and
  Location Data Assigner.
- **Note**: It is quicker than the window-opening motion on purpose. A window opens once; a tab gets
  clicked over and over while you work.
- **Note**: Picking a value from a dropdown *inside* a tab does not replay the transition. Windows
  treat a dropdown's selection change as if it came from the tab strip, so the obvious way to build this
  would make the whole panel re-animate every time you chose a value from a list - and four of these five
  windows are full of dropdowns. There is now a permanent check in place so that stays fixed.
- **Note**: Nothing about how the tabs work changed - same tabs, same order, same content, and any
  existing logic that runs when you switch tab still runs.

## [1.39.6] - 2026-08-05

- **Added**: The last four windows that have their own separate styling now react to the mouse too, so
  the whole suite is consistent.
- **Added (About window)**: The sidebar items slide a little to the right as the mouse passes over them
  and press in when clicked; the two footer links lift slightly; the window buttons press in. The
  currently-selected sidebar item never showed any hover reaction before - now it does.
- **Added (Graphics Settings Manager)**: This is the biggest one - 26 separate styles. Buttons press in,
  show a ring when the keyboard is on them, and fade when they grey out. The colour swatches **grow**
  under the pointer instead of changing colour, because tinting a swatch would misrepresent the colour
  you are about to pick. The dropdown arrow turns, dropdown rows and category rows fade their highlight,
  tab headers fade, the transparency slider handle grows when you grab it, and **the on/off switch now
  slides across instead of jumping from one end to the other.**
- **Added (Game Key Settings)**: This window had no styling at all, so its buttons were plain square
  Windows buttons that turned Windows-blue on hover regardless of their own colour. They now match the
  rest of AJ Tools and press in properly. Setting a key still works exactly as before, amber prompt and
  all.
- **Note (Game Mode HUD)**: Nothing changed, and that is the correct answer rather than an oversight -
  the HUD is a see-through overlay with no buttons in it at all, so there is nothing to hover or press.
  Its own animation was left untouched.
- **Note**: The Graphics Settings Manager keeps its own hover colours rather than the standard glow. Its
  hover colours mean something there (the red Reset button in particular goes to a distinctly brighter
  red), and the standard glow could not reproduce that. So on that window hover still changes colour and
  what was added is the pressing, focus and sliding.
- **Note**: The tick boxes in the long category list still tick instantly, on purpose. They are in a
  scrolling list, so an animated tick would replay every time a row scrolled into view and look like
  flickering down the list.
- **Added (internal)**: A second checking script, `tools/verify-window-styles.ps1`, for windows that keep
  their styles inside themselves. It pulls the styles out of the window file, rebuilds them on their own
  and forces each one to run. 35 checked across the three windows, all pass.

## [1.39.5] - 2026-08-05

- **Added**: The AJ AI shell now reacts the same way the tool windows do. Its buttons glow on hover and
  press in when clicked, the prompt box and the API key box light up with a blue edge when you hover or
  click into them, and the dropdown arrow turns to point up while the list is open. Same speed and feel
  as the rest of the suite, so it doesn't feel like a separate program.
- **Added**: The AI dropdowns now show a blue edge when you hover over them. Before this they gave no
  sign at all that they could be clicked.
- **Changed**: The AI shell's three button colours (blue, grey, amber) now all behave identically. The
  amber one used to dim on hover while the other two changed colour - now all three glow the same way.
  The colours themselves land within a shade of what they were.
- **Note**: The AI progress bars are deliberately untouched. The thin busy strip that runs while the AI
  is thinking uses Windows' own sliding animation, and swapping that for a hand-written one would risk
  breaking something that already works.
- **Added (internal)**: A checking script, `tools/verify-wpf-styles.ps1`. Building the add-in proves the
  window styles are *spelled* correctly but not that they *work* - that only shows up when a window
  opens, and this exact gap once stopped AJ Tools loading at Revit startup. The script now loads the
  built file and forces all 28 styles to build, catching that in seconds without opening Revit. All 28
  pass.
- **Fixed (note only)**: A comment in the AI style file claimed a custom progress bar would show wrong
  progress. That was checked against the real Windows library and is not true - and the new progress bar
  from 1.39.4 was measured showing exactly the right length at 25%, 50% and 100%. The note now records
  the real reason that one bar was left alone.

## [1.39.4] - 2026-08-05

- **Added**: Buttons now react when you touch them. Hovering brings up a soft glow, pressing pushes the
  button in slightly and it springs back when you let go. Small and quick - under a fifth of a second -
  so it feels responsive rather than slow.
- **Added**: When a button turns grey because something isn't filled in yet (and back again when it is),
  it now fades instead of flicking. Same for the Run button on every settings window.
- **Added**: Text boxes light up with a blue edge that fades in when you hover over them, and a stronger
  one when you click into them.
- **Added**: The little arrow on a dropdown now turns to point up while the list is open, and back down
  when it closes.
- **Added**: Rows in lists, items in dropdowns and tab headers fade their highlight in as the mouse
  passes over them instead of flashing.
- **Added**: The minimise, maximise and close buttons at the top of a window press in when clicked. The
  close button still turns red on hover exactly as before.
- **Changed**: Progress bars now use the AJ Tools look (blue, rounded) instead of the old Windows one.
  The only tool with a progress bar today is Location Data Assigner.
- **Note**: This was done once in the shared theme file, so all 29 windows that use it got it together.
  Nothing about how any tool works changed - same buttons, same results, same validation. No window was
  made non-resizable, and no button changed size or moved.
- **Note**: Selected rows still highlight instantly. A list that opens with hundreds of rows already
  ticked would otherwise animate as a wave, which would look worse, not better.

## [1.39.3] - 2026-08-05

- **Added**: Every AJ Tools window now opens with a short fade and a small upward settle instead of
  snapping onto the screen. It is deliberately quick - about a fifth of a second - so a window you open
  twenty times a day still feels instant. 33 windows in total.
- **Note**: The About window keeps its longer, staged entrance. That one is opened occasionally and can
  carry a slower reveal; the same timing on a settings dialog would feel like waiting rather than
  polish. Game Mode's HUD is left alone because it already has its own animation.
- **Note**: Opening motion only. Closing is untouched, so nothing changed about how a window returns its
  result, validates, or cancels. No window was made non-resizable.

## [1.39.2] - 2026-08-05

- **Added**: The About window now animates. It fades in while growing and rising gently into place, then
  the logo, the AJ TOOLS wordmark, the content area, the five sidebar buttons and the two footer links
  arrive one after another. Closing it sinks and fades out, faster than it came in. Switching sections
  slides the new panel up instead of hard-cutting, and the green "System Operational" dot breathes
  slowly. Entrances slow down as they land, the exit speeds up as it leaves.
- **Fixed**: The About window's top-right and bottom-right corners looked square inside a rounded
  outline. A rounded panel in WPF does not clip what sits inside it, so the header and footer bars were
  painting square corners straight over the curve; both now round themselves. The inner pieces also use
  a slightly tighter radius so they sit truly inside the outer edge instead of bleeding a hairline past
  it, and the dotted resize grip that sat outside the curve is gone. **The window is still fully
  resizable from every edge and corner.**
- **Fixed**: The four View Crop windows kept their rounded corners while maximized, which let the
  desktop show through all four corners. They now square off when maximized and round again when
  restored - including when maximized with Win+Up or by snapping to the top edge, not just via the
  maximize button. Handled once in the shared `WindowChromeHelper` so every custom-chrome window
  behaves the same.
- **Note**: No tool behaviour changed, and no window was made non-resizable. A project-wide check of all
  38 UI files found About was the only window with the corner-clipping problem - the others were already
  correct.

## [1.39.1] - 2026-08-04

- **Fixed**: The Revit 2025, 2026 and 2027 builds now compile with zero warnings, matching the 2020
  baseline. They had been emitting 11 platform-compatibility warnings on Windows-only dialogs
  (colour picker, folder picker, screen bounds) because the assembly was never marked Windows-only -
  the marker a `net8.0-windows` build normally carries is suppressed by this project maintaining its
  own AssemblyInfo. The add-in only ever runs inside Revit on Windows, so the marker is now declared
  explicitly, guarded so the .NET Framework builds (Revit 2020-2024) are untouched.
- **Changed**: The AJ AI Bridge builds its per-session token with `RandomNumberGenerator.Create()`
  instead of the obsolete `RNGCryptoServiceProvider`. Identical cryptographic strength, same 24-byte
  token, same format - existing connections and discovery files are unaffected.
- **Note**: No tool behaviour changed in this version. This is the first release to carry the work
  from 1.26.0 onward, which had been built and running locally but never published.

## [1.39.0] - 2026-07-30

- **Added**: Game Mode laser now also shows the element's System and Level in its identity line
  (e.g. "Duct - Default - Size 400x250 - SAD 5 - Level 03 - Id 919700"). Works for linked elements
  with their own model's names; non-MEP elements without a system simply omit it. The cleaner /
  snag / selector messages and the snag punch-list report gain the same detail automatically.

## [1.38.4] - 2026-07-29

- **Changed**: Game Mode full audit pass, zero behaviour change - no logic bug found. Ribbon tooltip
  now covers the SELECTOR weapon and professional mode (N); a dead helper left over from the removed
  measuring feature was deleted; missing per-file changelog entries were stamped; eight stale header
  comments (deleted features, old folder layout) were corrected; and this changelog was backfilled -
  it had stopped at 1.25.8 while the suite moved on to 1.38.3 (every entry from 1.26.0 below was
  written in this pass, from the AssemblyInfo version history).

## [1.38.3] - 2026-07-29

- **Changed**: Game Mode - final weapon color scheme applied everywhere (crosshair, beams, glows,
  texts): GUN amber, LASER green, CLEANER black & white, SNAG red, SELECTOR blue.

## [1.38.2] - 2026-07-29

- **Fixed**: Game Mode aim/display sync - shots could land beside the crosshair when Revit had
  2D-zoomed/panned the perspective view; the game now re-fits the picture (ZoomToFit) on start, on
  resume and on every window resize so the crosshair and the real aim always match.
- **Changed**: The crosshair is colored by the active tool - the weapon indicator that survives
  professional mode.

## [1.38.1] - 2026-07-29

- **Changed**: Game Mode teleport visual finalized to the approved VR reference - solid green
  ballistic arc dropping onto a landing disc drawn flat on the floor.

## [1.38.0] - 2026-07-29

- **Added**: Game Mode PROFESSIONAL MODE (N, remembered across sessions) - no gun ever shows,
  presentable in meetings, every tool keeps working with beams rising from the bottom of the view.
- **Added**: Game Mode 5th weapon SELECTOR - the shot selects the element in Revit's live selection
  (shot again unselects); the selection stays after exiting the game, ready for editing.

## [1.37.0] - 2026-07-29

- **Removed**: Game Mode measuring (not working properly) - the laser keeps its live distance in mm
  and element identity.
- **Changed**: J now resets ALL element colors in the game view (existing Reset tool's approach), so
  snag marks from earlier sessions clear too.
- **Added**: Remappable keys - pause (Esc) then S opens the new Key Settings window; every game
  action can be given a new key, saved to AppData. Esc, mouse, wheel, 1-9 and arrows stay fixed.

## [1.36.4] - 2026-07-29

- **Fixed**: Game Mode CLEANER rifle rotated and repositioned so barrel and beam form one straight
  line to the crosshair.

## [1.36.3] - 2026-07-29

- **Changed**: Game Mode SNAG MARKER now shows Ajmal's blaster picture - every weapon has its own
  gun: pistol (gun + laser), rifle (cleaner), blaster (snag).

## [1.36.2] - 2026-07-29

- **Changed**: Game Mode CLEANER weapon now shows Ajmal's rifle picture instead of the pistol.

## [1.36.1] - 2026-07-29

- **Changed**: Game Mode saved positions are now unlimited - B keeps counting past 9, the tour (O)
  visits every slot, the left-side list shows them all (slots above 9 are tour-only).

## [1.36.0] - 2026-07-29

- **Added**: Game Mode round 11 - true rubber-band measure line (superseded - removed in 1.37.0),
  SNAG MARKER weapon (paints red + punch-list report saved to Documents\AJ Game Snags on exit),
  tour mode (O), compass + level line, crouch (hold C), live speed dial (+/-), flashlight night
  mode (V), and a synthesized gunshot sound (M mutes).

## [1.35.1] - 2026-07-29

- **Removed**: Game Mode follow-me section box (X) - the game is back to zero undo entries in every
  mode.

## [1.35.0] - 2026-07-29

- **Changed**: Game Mode teleport reworked to the Autodesk Workshop XR feel - hold T for a glowing
  jump arc with landing ring and distance, release to go.
- **Added**: Left-side SAVED POSITIONS panel listing every B-saved spot with its coordinates in mm.

## [1.34.1] - 2026-07-29

- **Changed**: Game Mode restructure, zero behaviour change - everything now lives in one folder
  (src/GameMode) with one small file per feature (partial classes), per Ajmal's own request.

## [1.34.0] - 2026-07-29

- **Added**: Game Mode "add all" round - teleport (T), saved positions (B / 1-9), photo mode (K, to
  Pictures\AJ Game Photos), CLEANER weapon (temporary hide, U restores), live clear-height readout,
  follow-me section box (removed again in 1.35.1), and toast messages.

## [1.33.0] - 2026-07-29

- **Added**: Game Mode scroll-wheel holster (gun slides away; scroll up or click to draw), and the
  measure card's axis breakdown (Total, X/Y/Z deltas and plan distance in mm).

## [1.32.2] - 2026-07-28

- **Fixed**: Game Mode laser/bullet/flash now start at the gun picture's real barrel tip (they were
  starting from the striker plate at the rear - Ajmal spotted it immediately).

## [1.32.1] - 2026-07-28

- **Fixed**: Game Mode gun picture now displays exactly as generated - no flip, no tilt.

## [1.32.0] - 2026-07-28

- **Added**: Game Mode laser measuring, BIM-360 style - hold on the first face, release on the
  second; Total / Horizontal / Vertical in mm (superseded - removed in 1.37.0).

## [1.31.1] - 2026-07-28

- **Changed**: Game Mode HUD gun is now Ajmal's own pistol picture (vector pistol stays as
  fallback); freshly created "AJ Game View" views come with Crop View and crop boundary already OFF.

## [1.31.0] - 2026-07-28

- **Changed**: Game Mode weapon + speed rework after Ajmal's first play - hold left-click for
  automatic fire with impact sparks, right-click switches GUN/LASER, the laser continuously shows
  distance + element identity, realistic pistol redraw, sprint raised to 3x.

## [1.30.0] - 2026-07-28

- **Added**: New tool - AJ Game Mode ("Game" panel, AJ Tools tab). First-person walkthrough in a
  real Revit perspective view ("AJ Game View"): WASD + mouse-look, gravity, stairs, sprint, jump,
  doors with E, windows by jumping, fly (F), ghost (G), respawn (R); transparent HUD overlay with
  crosshair, gun, tracer and laser rangefinder identifying the element hit. Camera moves create no
  undo entries and no model changes; the only model change ever is creating the game view itself.

## [1.29.1] - 2026-07-28

- **Fixed**: Stack Tags' first-click tags now use Rearrange Tags' exact leader routine (no leader
  end-condition fallback). Stack Tags moved into the Create Tags pulldown.

## [1.29.0] - 2026-07-28

- **Added**: New tool - Stack Tags (AJ Annotation, Tags panel): select MEP elements, click once, a
  tag per eligible element is created and stacked there; click again to relocate the whole stack.

## [1.28.0] - 2026-07-28

- **Added**: 9 new tools across the Transfer and Purge pulldowns (Manage panel) - Transfer
  Schedules / Legends / Drafting Views, Purge Unused View Templates / Filters / Groups, and Purge
  Unplaced Schedules / Legends / Drafting Views - all built on two new shared engines, with the
  probe-before-delete safety net deciding what Revit really allows.

## [1.27.0] - 2026-07-28

- **Added**: New tool - Create Tags (AJ Annotation, Tags panel): select MEP elements, click a
  location for each in turn; creates fresh tags with L-shaped leaders, auto-skipping already-tagged,
  too-short and vertical runs. Own Settings window (categories + minimum length).

## [1.26.0] - 2026-07-28

- **Added**: Reassign Reference Level now has a Selected Elements scope alongside Whole Project -
  pre-select elements and only those move to the chosen level; the option is disabled with an
  explanatory tooltip until something eligible is selected.

## [1.25.8] - 2026-07-28

- **Changed**: HVAC Schematic error dialogs now name the exact failing method and line (exception
  type + trimmed stack trace) instead of only a bare message — kept as a standing diagnostic even
  after the v1.25.7 crash below was root-caused, since a bare message alone had proven too vague
  to act on.

## [1.25.7] - 2026-07-27

- **Fixed**: HVAC Schematic crash, "An unexpected error occurred. The given key was not present in
  the dictionary.", firing on almost every run (any selection producing at least one leaf node in
  the schematic tree — nearly always, including a single isolated element). Root cause in
  `SchematicLayoutEngine.AssignTreePositions`: a variable was pre-set to the "no continuation
  child" sentinel (-1) then passed as the out-parameter of a `Dictionary.TryGetValue` call —
  `TryGetValue` always overwrites its out-parameter even on a failed lookup, so the -1 default was
  silently replaced with 0 for every leaf node, then treated as a real element id and looked up,
  which threw. Now checks `TryGetValue`'s return value first, matching the already-correct pattern
  used a few methods away in `GetChildOrder`. No behaviour change beyond removing the crash.

## [1.25.6] - 2026-07-28

- **Fixed**: Full UI audit pass over every window in the suite. Twelve tool windows (Duct Standards,
  Filter Pro, Linked ID Viewer, Linked Search, Pipe Sizing, both Purge tools, Revision Cloud
  Settings, Transfer View Templates, Apply Graphics, Shared Param to Family Param, Saved Scripts)
  were shown without a Revit owner and could drop behind the Revit window - all now properly owned.
  Five borderless windows (Graphics Override and the four View Crop windows) could maximize over the
  Windows taskbar - now capped to the working area. Esc now closes MEP Opening Settings, Pipe Sizing
  and the About window.
- **Removed**: Pipe Sizing CSV export's "Report saved successfully." popup (silent success is the
  house style; the failure message stays).
- **Checked clean**: every XAML resource reference resolves, no duplicate resource keys, no layout
  grid overflows, credit footer present in every window, no duplicate ribbon button IDs, all ribbon
  icons present, no empty tooltips.

## [1.25.5] - 2026-07-28

- **Changed**: Smart MEP Tagging Settings rebuilt as a themed WPF window - the last WinForms dialog in
  the suite is gone, every AJ Tools window is now themed WPF. Live inline validation (unticking every
  category disables Save with a message instead of closing the dialog and cancelling the command),
  priority is a fixed-choice dropdown so an invalid value is impossible, window owned by the Revit
  main window. Added "Tag all" / "Tag none" buttons. Saved-settings shape and the offset carry-over
  logic unchanged.
- **Removed**: The routine "Settings saved." success popup, per the house no-success-popup rule.

## [1.25.4] - 2026-07-28

- **Changed**: Reassign Reference Level's level picker rebuilt as a themed WPF window (reassignment
  logic untouched). Picking the same level in both boxes is now caught live inline with the Run button
  disabled - previously it closed the dialog, showed an error popup and cancelled the whole command.
  Added a "Swap" button and an up-front note that the scope is the whole project and hosted family
  instances are skipped. The bulk-change confirmation with the element count still fires before any
  edit.
- **Fixed**: The old dialog's "Reassign Elements" button overlapped Cancel by 15 px; the dialog had no
  owner window so it could drop behind Revit; the fixed-size intro text could clip at larger Windows
  text scaling.

## [1.25.3] - 2026-07-27

- **Added**: The credit line "Created & All Rights Reserved @ Ajmal P.S." now appears in every window
  in the suite - added as a bottom-centred footer to 18 windows that had none, kept exactly where it
  already was in the 9 windows that had it, and normalised to the standard wording in 3 windows that
  had drifted into their own variants (Graphics Override, Pipe Sizing, About).
- **Fixed**: Reassign Level's overlapping Save/Cancel buttons (superseded by the v1.25.4 WPF rebuild).

## [1.25.2] - 2026-07-27

- **Changed**: Arrange Tags Settings rebuilt as a themed WPF window (was a plain WinForms prompt):
  live inline validation instead of losing the entry on a typo, quick preset buttons
  (6/8/10/12/15/20 mm), "Reset to default", and a live explanation of what the spacing means on the
  printed sheet vs in the model at the current view scale.
- **Fixed**: On comma-decimal Windows regional settings the old dialog could read "12.5" as 125 and
  silently save a 10x tag spacing - both decimal formats now parse correctly. Added a 0.1-250 mm
  range check (any positive number was accepted before). The save is now verified by read-back, so a
  failed settings write is reported instead of silently showing success. The window is owned by the
  Revit main window and no longer requires an open project.

## [1.25.1] - 2026-07-26

- **Improved**: Highlight Selection now completes the insulation story in both directions: selecting
  insulation or lining directly also turns its host duct/pipe red (previously only host-selected ->
  insulation-follows worked), and highlighted hosts now pull in their duct LINING alongside insulation
  (lining was previously left gray). API members (`InsulationLiningBase.GetLiningIds` /
  `.HostElementId`) verified against the real installed RevitAPI.dll on 2020/2024/2027, not the NuGet
  reference package alone.
- **Fixed**: The all-users ProgramData deploy (`Directory.Build.targets`) wrote its files loose at the
  shared `Addins\<year>\` root and its .addin manifest pointed at a glued
  "`AJ ToolsAJ Tools.dll`" path that never existed (bug found 2026-07-21, fix applied now). DLL, PDB,
  and Resources now land inside the `AJ Tools\` subfolder and the manifest is generated from the exact
  same path the DLL is copied to, so they can never drift apart again. The everyday per-user AppData
  deploy was never affected. Takes effect on the next non-skip build; stale loose files from old
  builds are not auto-deleted.
- **Removed**: The orphaned `CmdQuickParallelDimension` command class (the pre-split original,
  superseded by the CenterLine/FaceEdge pair, wired to no ribbon button since the split - confirmed by
  a fresh repo-wide reference sweep). The two live Quick Parallel Dimension commands are unchanged.

## [1.25.0] - 2026-07-25

- **Added**: Anthropic (Claude) as a third AI provider option in the C# AI pane, alongside Gemini and
  OpenAI - same settings pattern (encrypted API key, model dropdown: claude-opus-4-8 default,
  claude-sonnet-5, claude-haiku-4-5, claude-fable-5), raw HttpClient against the Messages API, no new
  NuGet dependency.
- **Changed**: Settings window's API key fields are now masked (PasswordBox with a "👁 show/hide"
  toggle) instead of plain text - the key itself was always encrypted at rest and sent only in an
  HTTPS header (never logged, never seen by the AI model), but the Settings UI previously displayed
  it in the clear on screen.
- **Added**: Standalone "Saved Scripts" ribbon button (AI Assistant panel) - browse, pin, and run any
  .cs file in the configured Scripts Folder from its own window, reachable whether or not the C# pane
  is open, same as "Run Pinned". Moved out of the C# pane's "Saved Scripts History" expander, which no
  longer exists in the pane.

## [1.24.0] - 2026-07-21

- **Added**: Two RevitPythonShell-equivalent pieces in the C# pane, ported to sit alongside the
  existing AI Generate/Run workflow rather than replace it: a "Live Console" - type one raw C# line,
  press Enter, it runs immediately against the live document with variables kept alive for the next
  line (the actual "interactive shell" the reference tool is named for) - and a one-click "Snoop
  Selection" that dumps every instance/type parameter of the current selection to the Output panel,
  read-only, no code required.
- **Added**: "📌 Pin" on each Saved Scripts History row + a new "Run Pinned" ribbon button (AI
  Assistant panel) - pin one saved script for a one-click, no-code re-run. The safe, statically-
  compiled alternative to RevitPythonShell's "deploy script as ribbon button", which relies on
  runtime IL emission (System.Reflection.Emit) to generate a new type per script.
- **Added**: Ctrl+Space in the C# pane's Code Editor opens a curated Revit API completion list
  (types, members, globals, and a couple of common snippets) - RevitPythonShell's "autocompletion",
  scaled to a static list instead of a live semantic model so it needs no Roslyn workspace plumbing.
- **Added**: Live Console command history now persists across Revit sessions, and a new "📋 Send
  Last Line to Code Editor" button carries a console line (and its error, if it failed) into the
  main editor/prompt so the AI can extend or fix it using the existing Generate flow.

## [1.23.1] - 2026-07-20

First tagged release since v1.13.5 - bundles ten days of accumulated work: two new tools, a Ceiling
Magnet rework, the About window overhaul, and the AJ AI branding/UX pass below.

- **Fixed**: Smart Selection swapped its multi-pick window/crossing/click loop (needed an explicit
  Finish/Enter to end) for a single one-shot window/crossing box-select that completes the instant the
  drag ends, per live-testing feedback.

## [1.23.0] - 2026-07-20

- **Added**: Smart Selection tool (AJ Tools tab, Modify panel) - pick one reference element, then
  window, crossing, or click-select more elements in the view; only elements sharing the reference
  element's category are added to the selection, everything else is skipped automatically. Read-only
  (selection state only, no model changes, no undo step).

## [1.22.0] - 2026-07-20

- **Changed**: Elements to Ceiling Grid (Ceiling Magnet) now offers both the original one-at-a-time
  workflow and the newer window-select-then-loop workflow side by side via a choice dialog, instead of
  replacing one with the other.

## [1.21.0] - 2026-07-20

- **Changed**: Ceiling Magnet reworked - elements to snap are now window/click multi-selected once up
  front, then the tool repeats a ceiling-plus-anchor-point round (Esc to finish), each round snapping
  only that batch's elements sitting over the picked ceiling using its real solid geometry, not a
  bounding-box guess.

## [1.20.2] - 2026-07-19

- **Changed**: new About icon (Ajmal's own artwork), used for both the ribbon button and the About
  window's taskbar icon.

## [1.20.1] - 2026-07-19

- **Fixed**: Highlight Selection left a selected duct/pipe's insulation gray instead of red; insulation
  is now colored along with its host element.

## [1.20.0] - 2026-07-19

- **Added**: Highlight Selection tool (View panel) - colors the current selection red and every other
  element in the active view gray, for instant visual identification.

## [1.19.1] - 2026-07-19

- **Changed**: About window overhaul - real taskbar/window icon, a Minimize button, a maximize-size
  fix, the house Neon Blue accent color, and a content accuracy pass (real ribbon list, real recent
  updates, real license terms).

## [1.19.0] - 2026-07-19

- **Changed**: AJ AI's diff-highlight now also covers Run Code's auto-fix loop, not just Generate.
- **Added**: Prompt and code editor content auto-saves (2s debounce) and restores after a Revit crash.

## [1.18.0] - 2026-07-18

- **Changed**: AJ AI's Prompt box no longer clears after a successful generate.
- **Added**: changed lines are highlighted in the code editor after an incremental-edit generate.

## [1.17.1] - 2026-07-18

- **Changed**: "Generate C# Code" button resized to match the other buttons; added a Stop button beside
  it, visible while busy.

## [1.17.0] - 2026-07-18

- **Added**: "Generate C# Code" now sends the code already in the editor as context, so a follow-up
  prompt edits the existing script instead of always generating an unrelated fresh one.

## [1.16.2] - 2026-07-18

- **Fixed**: three visual issues from the first live launch - clipped button labels, Settings
  ComboBoxes ignoring their background color, and a cramped Output console layout.

## [1.16.1] - 2026-07-18

- **Fixed**: a real startup crash (AiShellView/SettingsWindow StaticResource lookups failing before
  their Resources dictionary was populated) that crashed Revit's OnStartup entirely. Confirmed live.

## [1.16.0] - 2026-07-18

- **Changed**: AJ AI's Settings moved out of an inline collapsible panel into its own popup window;
  shared visual style resources extracted into one file.

## [1.15.2] - 2026-07-18

- **Changed**: restyled the AJ AI "C#" dockable pane to the house Soft Revit UI look (Neumorphism +
  Claymorphism, Neon Blue, dark theme). Fixed a ProgressBar fill-width bug caught while building it.

## [1.15.1] - 2026-07-18

- **Fixed**: AJ AI ON/OFF icons re-supplied as proper transparent PNGs.
- **Changed**: pane label shortened to "C#".

## [1.15.0] - 2026-07-18

- **Changed**: rebranded the AI Assistant panel's two buttons - the chat/generation pane is now "C#
  with AI", the MCP bridge toggle is now just "AJ AI" with an icon that swaps between connected/
  disconnected states.

## [1.14.0] - 2026-07-18

- **Added**: "AJ AI Bridge" ribbon button (AI Assistant panel) to connect/disconnect the live-Revit MCP
  bridge directly, replacing the equivalent control that lived inside the AJ AI chat panel.

## [1.13.11] - 2026-07-18

- **Changed**: renamed the AJ AI pane's live-Revit MCP bridge from "AutoDebugger" to "AJ AI Bridge"
  everywhere - UI text, named-pipe protocol, file names, MCP server registration, the tool names Claude
  calls, and ~25 knowledge/skill files.

## [1.13.10] - 2026-07-18

- **Changed**: renamed the AI shell's internal branding away from "Gemini"/"Gemini Shell" to "AJ AI"
  everywhere except the actual Gemini provider name/API service, which correctly still says Gemini.

## [1.13.9] - 2026-07-18

Full code review + security hardening pass over the AJ AI (Gemini Shell) subsystem and its
companion AutoDebugger MCP server:

- **Security**: the AI safety validator only blocked killing Revit itself
  (`Process.GetCurrentProcess().Kill()`); a script could still kill *any other running program* on
  the machine without tripping a single check. Widened to block any `.Kill(` call.
- **Fixed**: `McpBridgeService.Start()` leaked a named-pipe handle on every failed start attempt.
- **Fixed**: the MCP server's own response timeout (65s) was stale against Revit's execution
  backstop (raised to 80s in a previous pass) — a script still legitimately finishing between
  65-80s was incorrectly reported to the AI agent as timed out. Raised to 90s.
- **Fixed**: the AJ AI activity banner window had `AllowsTransparency` set to `False` while using a
  transparent background and no window chrome — WPF can't render true transparency without it, so
  the banner likely showed as a solid black box instead of the intended floating card with a
  shadow.
- **Fixed**: the Gemini model-lookup call didn't respect the Stop button's cancellation token.
- Reviewed and found already solid: the script cancellation/timeout chain, infinite-loop
  protection, DPAPI-based API key encryption at rest, and the busy-state re-entrancy guards.

## [1.13.8] - 2026-07-18

Third cleanup pass, acting on the items v1.13.7 had deliberately deferred:

- **Perf**: `SmartMepTagService.MarkDenseZones` and `SmartTagPlacementEngine`'s parallel-group check
  both replaced an O(n^2) full pairwise scan with the existing `AnnotationSpatialIndex` as an X/Y
  coarse pre-filter. The original exact 3D distance check is still applied to every result, so
  behavior is unchanged, just faster on models with many tags.
- **Deduped**: the ~150-line duplicated leader-probing reflection block that `SmartTagPlacementEngine`
  and `IntelligentTagArrangerService` had each reimplemented is now shared via `LeaderLogicService`.
  The one deliberate behavioral difference between the two tools was preserved.
- **Reorganized**: `SharedParamUtils.cs` now holds only genuinely shared helpers; the Shared Param to
  Family Param conversion's own snapshot/restore logic moved into its Service, the only place that
  used it.
- **Fixed**: AJ AI safety validator now also blocks `using static` and `using X = Y;` type-alias
  directives, closing the specific bypass documented in v1.13.6/v1.13.7's notes.
- Evaluated and deliberately left as-is, each for a documented reason (see
  `src/Properties/AssemblyInfo.cs` for detail): `DuctShapeService`'s reflection-based shape read,
  `LocationDataAssignerWindow.xaml.cs`'s embedded business logic, Colorize/FilterPro's near-identical
  load methods (real behavioral drift found between them), `FilterProState`/`FilterSelection`'s
  property overlap, and `FilterCategoryItem`/`PatternItem`/`GraphicsIdOption`'s identical wrapper
  shape.

## [1.13.7] - 2026-07-18

Second cleanup pass, acting on the items v1.13.6 had deliberately deferred:

- **Fixed**: AJ AI's `task.Wait()` now has a hard backstop instead of no timeout at all - narrows
  (does not fully close) the freeze risk for a script that never yields at a loop checkpoint.
- **Fixed**: Gemini API key now sent via a header instead of a URL query param, matching the OpenAI
  client's existing approach.
- **Fixed**: a naming collision between two unrelated `DuctSelectionFilter` classes (not a live bug,
  a future trap) - renamed one to `DuctCurveOnlySelectionFilter`.
- **Extracted**: the four Commands that still had their full tool logic inline instead of a Service
  (Ceiling Magnet, Reassign Level, Arrange Text in Box, Force Tag Leader L-Shape) each now have a
  proper Service backing them; the Commands are thin wrappers.
- **Deduped**: the four config-store classes' identical config-path builder, and
  AnnotationRibbonManager's 28 repeated icon-loading blocks.
- Still deferred (see `src/Properties/AssemblyInfo.cs` for the full list): two O(n^2) hot loops in
  the tag-placement tools, a duplicated leader-probing block between two Services, Colorize/FilterPro
  duplication, `LocationDataAssignerWindow.xaml.cs`'s embedded business logic, and the AI safety
  validator's remaining text-matching (not AST/semantic) limitation.

## [1.13.6] - 2026-07-17

Full repo structure/cleanliness review plus a full code review pass (Core, Helpers, Commands, all
Services, Models, UI, and the AJ AI/GeminiShell subsystem), then acted on the safe/verifiable
findings. See `src/Properties/AssemblyInfo.cs` for the full itemized list. Summary:

- **Fixed**: AJ Annotation ribbon typo ("Auto Dimention" -> "Auto Dimension", visible on the tab).
- **Fixed**: AJ AI safety validator now blocks `#r`/`#load` script directives (previously a full,
  undetected bypass of every other safety check) and reflection-based indirect member access, and
  covers a few more dangerous APIs (SmtpClient, Dns, Ping, Process.Kill, Environment.FailFast).
- **Fixed**: AJ AI script execution now always completes its Task even if the failure-path
  transaction rollback itself throws (previously could hang the AJ AI pane on "busy" forever).
- **Fixed**: a real null-reference risk in Revision Cloud By Elements when no view is active.
- **Removed**: ~15 confirmed-unused classes/methods (verified unused repo-wide before deletion).
- **Cleaned up**: a couple of small duplicated helpers (ribbon panel lookup, a ViewCrop geometry
  check) consolidated into their existing shared helpers; 6 previously-silent empty catch blocks
  now document why the failure is safe to ignore instead of swallowing it invisibly.
- Not done this pass (needs a Revit/Visual Studio environment to verify safely, so left for a
  follow-up rather than guessed at blind): the larger structural duplication in a few tools
  (Ceiling Magnet, Force Tag Leader L-Shape, Reassign Level, Arrange Text in Box all still have
  their full logic inline instead of in a Service), a couple of O(n^2) hot loops in Smart MEP Tag /
  Intelligent Tag Arranger, and moving the AI safety validator from text-matching to a real
  AST/semantic scan.

## [1.13.5] - 2026-07-16

Catch-up release: everything built in the working source tree since v1.11.3, pushed to GitHub in one
batch (nothing here was released one version at a time — the working folder had moved on to 1.13.5
before this sync).

- **Multi-version build**: one codebase now builds Revit **2020 through 2027** from a single project,
  via root `Directory.Build.props`/`.targets` (configs `Release`/`Debug` for 2020 through `Release
  R27`/`Debug R27`, frameworks net472 / net48 / net8.0-windows / net10.0-windows, per-version `obj`
  isolation so builds don't clash). 2020 remains the tested baseline.
- **AJ AI (GeminiShell) can now run live against Revit**: a local named-pipe bridge
  (`mcp-server/`, `tools/invoke-revit-bridge.ps1`) lets an AI session run C# directly against the open
  Revit document, with reflection/assembly-loading and destructive operations blocked by design.
  Includes a non-modal "AJ AI is working" activity banner, an append-only audit log of every request
  at `%AppData%/AJTools/autodebugger-audit.jsonl`, a compiled-script cache, connection speed-ups
  (persistent pipe, Roslyn pre-warm), an instant handoff so a second chat window can take over from an
  idle one instead of waiting out a timeout, and locked-DLL-safe deployment (each build publishes to a
  fresh AppData payload folder so it can deploy while Revit still has the previous build loaded).
- Fixed two frozen progress bars in the AI shell: the pane's own execution bar (was bound to
  `Application.Current`, always null inside Revit) and the floating activity popup's bar (was a static
  fixed-width element with nothing driving it) — both now genuinely animate while a script runs.
- **Version-safe API hardening** for 2024+/2026+/2027 builds: `ElementIdHelper.FromInt` (the
  `ElementId(int)` constructor is gone in real Revit 2027), `IsDefinedBuiltInCategory` /
  `IsDefinedBuiltInParameter` (Int64 enum widening in 2024+), and category-based dimension collectors
  (2025+ `LinearDimension`).
- Ceiling Magnet: on Revit 2025.3+ now reads the ceiling's real grid lines
  (`Ceiling.GetCeilingGridLines`) for exact tile size and anchor, with a safe fallback to the original
  pattern-based method everywhere else.
- Added the **Arrange Text in Box** tool (AJ Annotation tab, new "Text" panel), ported from the pyRevit
  "Text Box Arrange Loop" script.
- The version-numbering mismatch noted in earlier entries (working tree at 1.10.0 vs GitHub tag
  v1.11.3) is resolved as of this release — the working tree's own version (1.13.5) is now the
  reconciled number going forward.

## [1.11.3] - 2026-07-07

- Fixed Revit startup dependency resolution so bundled DLLs such as `CommunityToolkit.Mvvm.dll` load from the AJ Tools install folder.
- This prevents the Revit 2024 `OnStartup` failure seen when the Gemini Shell dockable pane asks for `CommunityToolkit.Mvvm`.
- Fixed modern Revit packaging so Revit 2025-2027 payloads include copied NuGet dependency DLLs and `.deps.json` companion files.

## [1.11.2] - 2026-07-06

- Optimized `IndependentTag` compatibility so Revit 2022 and newer use the cleaner reference-based tag and leader APIs.
- Updated the L-Shape Leader command to use direct `IndependentTag` APIs first and keep reflection only as a fallback.
- Revit 2020-2021 keep the legacy tag API path required by their older Revit API surface.

## [1.11.1] - 2026-07-06

- Split Revit 2020-2024 into separate .NET Framework package payloads built against matching Revit API reference packages.
- Revit 2020 now targets .NET Framework 4.7.2, while Revit 2021-2024 target .NET Framework 4.8.
- The installer now prefers exact per-year payload folders before the old shared `2020-2024` fallback.
- Release packaging now produces API-specific payloads for all supported Revit versions from 2020 through 2027.

## [1.11.0] - 2026-07-06

- Added modern Revit builds for Revit 2025-2026 on .NET 8 and Revit 2027 on .NET 10.
- Added versioned installer payload folders for the modern Revit runtimes.
- Updated installer packaging to deploy the matching payload for Revit 2020-2027.

## [1.10.1] - 2026-07-06

- Updated the installer to stage AJ Tools folders and `.addin` manifests for Revit 2020-2027.
- Revit 2025-2027 now receive installer entries, while still reporting `NEEDS_REVIEW` until the separate modern .NET/Revit API build is completed.

## [1.10.0] - 2026-07-03

- Added the **MEP Openings** split-button workflow in the MEP panel.
- Added Opening Settings for element-specific shape, buffer, insulation, and merge-distance rules.
- Added Create Openings for selected pipes, ducts, cable trays, and conduits in current-model walls, floors/slabs, and beams.
- Verified a clean Release build against the Revit 2020 API.

## [1.9.1] - 2026-07-02

- Fixed Colorize shuffle behavior so repeated shuffles stay in the window and apply immediately.
- Removed the Colorize rule-type step so selected values are matched with Equals.
- Fixed shared fill-pattern visibility handling used by Colorize and Filter Pro shuffle colors.

## [1.9.0] - 2026-07-02

- Added the **Colorize** tool in the View panel for per-view element overrides by category or parameter values.
- Reused Filter Pro category, parameter, value, rule, and override engines without creating persistent view filters.
- Ported and hardened the retired pyRevit Colorize workflow.

## [1.8.0] - 2026-07-01

- Full project audit: added the **Pipe Sizing** tool (MEP panel) for domestic water pipe sizing from fixture units, system type, pipe material, and velocity limit.
- Hardened the AJ AI shell with `GeneratedCodeSafetyValidator` (blocks process/registry/network/reflection/unmanaged/file-delete calls in AI-generated scripts and flags destructive Revit operations for user confirmation before running), plus activity logging.
- Fixed a dead ribbon wiring gap: `CmdPurgeUnusedFamilyParametersAvailability` existed but was never assigned to its button, so "Purge Family Parameters" stayed clickable outside the Family Editor. It is now wired in.
- Fixed the About panel's inconsistent "Aj tool" ribbon label.
- Removed 8 orphaned icon resources and a stray local dev script/screenshot that had no code references.
- Verified a clean Release/x64 build (zero errors, zero warnings) against the Revit 2020 API.

## [1.7.0] - 2026-07-01

- AJ Annotation tab refactor/audit: full metadata blocks across every Dimensions, Auto Duct Dimension, Tags, Duct Flow, Revision Cloud, and Text tool; single-undo grouping for Copy Dimension Text, Copy Text, and continuous Revision Clouds; About and both ribbon-builder files standardized. All tool behaviour unchanged.

## [1.6.0] - 2026-07-01

- Modify / MEP / Coordination / Data / Manage / Family panels refactor/audit: full metadata blocks across every tool in these panels; Match Elevation now a single undo step; Reassign Level gains a Full-Project bulk-edit confirmation; version-safe ElementId access (Linked ID Viewer, Reassign Level); Duct Standards no-document path cancels cleanly with a project guard; removed loose scratch scripts from src. All tool behaviour unchanged.

## [1.5.4] - 2026-06-30

- Datums panel refactor/audit: full metadata blocks across all datum tools, removed success popups (silent success), single-undo batch for window-select Flip Bubbles, Family-Editor guards, and de-duplicated reset logic. Datum behaviour unchanged.

## [1.5.3] - 2026-06-30

- Graphics panel refactor/audit: single-undo TransactionGroup for both Match tools, view-scoped Reset Element Graphics in View, full metadata blocks, and 2024+ ElementId readiness. Graphics behaviour unchanged.

## [1.5.2] - 2026-06-27

- View Crop tool refactor/audit pass: shared helpers, bulk-edit confirmation, ElementId helper for 2024+ readiness. Behaviour of View Crop unchanged.

## [1.5.1] - 2026-06-24

- Integrated the AJ AI (Gemini Shell) tool into the main AJ Tools ribbon under a new "AI Assistant" panel, fixing visibility issues caused by an empty standalone tab.
- Fixed MSBuild compilation errors related to legacy PackageReference restores in the zero-warnings 2020 project configuration.

## [1.5.0] - 2026-05-30

- Added Search and Sort functionality for Categories and Parameters in the Filter Pro tool.
- Modernized ListBox and ListBoxItem styling in the shared UI components.
- Removed `CaseSensitive` checkbox logic from `FilterProWindow` in favor of more robust search/sort.
- Various minor stability fixes in `AutoDimensionService`, `LeaderLogicService`, and `SectionMarkVisibilityService`.

## [1.4.9] - 2026-05-25

- Added new **Section Mark Visibility** tool to automatically manage section visibility in plan views based on Sheet Number filters or placement status.
- Upgraded **View Crop** tool with persistent settings memory, custom diagnostics windows, support for coordination models, and integrated annotation crop configuration.
- Standardized namespaces, project files, and references for a zero-warnings compile on Revit 2020.


## [1.4.8] - 2026-05-17

- Fixed `Transfer View Templates` so the `Copy From` and `Copy To` document dropdowns show readable Revit document names instead of the internal `DocumentOption` type name.
- Verified the repository as a C# Revit add-in source repo with no pyRevit extension structure present.
- Cleaned local generated build outputs and confirmed the Release build succeeds with Revit 2020 API references and .NET Framework 4.7.2.

## [1.4.7] - 2026-05-14

- Added separate `Purge Unplaced 3D Views` and `Purge Unplaced Sections` tools under the AJ Tools Purge menu with preview, selection, confirmation, delete probing, transaction rollback, and final purge reporting.
- Added the separate `AJ Annotation` ribbon tab with `Duct Reference Dimension` and `Active View Duct Dimensions` tools.
- Updated Reset Graphics behavior so category reset uses all overridable active-view categories and element reset scans document elements safely.
- Cleaned startup logging so AJ Tools writes a temp log only when ribbon startup fails.
- Fixed generated `.addin` XML in the shared build target.

## [1.4.6] - 2026-05-10

- Reduced the `Apply Graphics` startup window size again for smaller screens.
- Added a compact tabbed layout so graphics settings and category selection are separated without increasing the default window height.
- Kept a visible custom title-bar close button, cancel behavior, and resize support for the WPF settings window.

## [1.4.5] - 2026-05-10

- Reduced the `Apply Graphics` default window size for smaller screens while keeping the settings area scrollable.
- Restored native Windows close and resize behavior so the standard title-bar close button remains visible.
- Clamped the startup window size to the available screen work area before showing the dialog.

## [1.4.4] - 2026-05-09

- Rebuilt `Apply Graphics` around a dark, compact settings manager with separate apply actions for element and category overrides.
- Added best-effort last-used settings memory for colors, patterns, line weights, transparency, halftone, cut-link state, and selected categories.
- Added active-view graphics override validation across Graphics commands and aligned `Reset Element Graphics in View` with the shared Graphics transaction flow.

## [1.4.3] - 2026-05-07

- Restored preset color buttons in `Apply Graphics`, but scoped each preset row to its own color field so preset clicks never spill into other targets.
- Changed `Apply as Category Graphics` to use the same selected-element source as `Apply as Element Graphics`, then derive selectable categories only from those selected elements.
- Kept direct Projection / Surface and Cut editing, and preserved linked-cut behavior when `Use Projection / Surface settings for Cut` is enabled.

## [1.4.2] - 2026-05-07

- Removed the unused `Preset Target` UI and quick-preset dependency from `Apply Graphics`.
- Renamed the combined Apply Graphics modes to `Apply as Element Graphics` and `Apply as Category Graphics`, and fixed apply-mode label visibility in the dark theme.
- Kept direct editable projection/surface and cut color controls, while preserving linked-cut behavior when `Use Projection / Surface settings for Cut` is enabled.

## [1.4.1] - 2026-05-07

- Combined Element Graphics and Category Graphics into one `Apply Graphics` tool with a shared UI mode switch.
- Added category selection inside the Apply Graphics window and removed the separate element/category apply commands from the ribbon.
- Fixed the `Use Projection / Surface settings for Cut` behavior so linked cut settings mirror line, pattern, weight, and fill settings correctly and unlink cleanly for manual editing.

## [1.4.0] - 2026-05-07

- Added the HVAC Schematic tool to create drafting-view schematics from selected ducts, air terminals, and mechanical equipment.
- Refined Ceiling Magnet selection and transaction flow for ceiling-grid snapping, including linked-ceiling support handling.
- Reorganized the AJ Tools ribbon layout and standardized metadata headers for the HVAC schematic and related touched files.

## [1.3.9] - 2026-05-06

- Cleaned the Graphics Tools command group: Apply Graphics, Match Graphics, and Reset Graphics.
- Added shared command context validation and summary transaction handling for graphics apply/reset flows.
- Standardized production metadata headers for Graphics Tools commands, services, models, and WPF files.
- Kept normal-success runs quiet and preserved the existing Revit graphics override behavior.

## [1.3.8] - 2026-05-06

- Cleaned the Toggle Link command and added standardized production metadata.
- Added validation before changing Revit Links category visibility in the active view.
- Kept Toggle Link normal-success runs quiet and scoped to Revit 2020 / .NET Framework 4.7.2.

## [1.3.7] - 2026-05-06

- Standardized production metadata headers for all View Crop C# and XAML files.
- Standardized production metadata header for the Unhide All command.
- Kept View Crop and Unhide All runtime logic unchanged from the previous releases.

## [1.3.6] - 2026-05-06

- Cleaned the Unhide All command and removed old debug-style comments.
- Fixed Unhide All to pass only elements permanently hidden in the active view to Revit's `UnhideElements` API.
- Kept Temporary Hide/Isolate reset behavior and normal-success runs quiet.

## [1.3.5] - 2026-05-06

- Cleaned the View Crop command flow, shared target-view selection path, and command result handling.
- Improved View Crop WPF labels, spacing, validation feedback, and normal-success dialog behavior.
- Confirmed the View Crop cleanup remains scoped to Revit 2020 and .NET Framework 4.7.2.

## [1.3.4] - 2026-04-19

- Refactored the About command to use a dedicated WPF About window.

## [1.3.3] - 2026-04-19

- Added pin tools and family parameter purge tools.
- Updated related UI and supporting code paths.

## [1.3.2] - 2026-04-13

- Added manual Smart MEP tag priority controls in the settings UI.

## [1.3.1] - 2026-04-13

- Improved Smart MEP tag priority placement behavior.
- Added telemetry support for Smart MEP tag priority handling.

## [1.3.0] - 2026-04-12

- Added a new tool suite.
- Retired the floor-plan import module.

## [1.2.1] - 2026-04-07

- Updated the About tool to a dedicated About window.
- Added clickable LinkedIn and email links.
- Added support for `Resources/AboutPhoto.png` in the packaged payload.

## [1.2.0] - 2026-04-07

- Added the Smart MEP Tag workflow.
- Added the Arrange Tags workflow.
- Updated shared leader logic and related ribbon assets.
