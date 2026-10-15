# AJ Tools — Conventions Log (history)

> Dated history behind the rules in [`ajtools-conventions.md`](ajtools-conventions.md). Newest entries first.
> Read this only for the story behind a decision, or what happened on a date — not for the rules themselves.

### 2026-10-15 (Smart MEP Tags "Selected elements only" — still v1.58.0, same PR)

Ajmal: *"add the tag selected elements only option"* (my suggestion after the restructure). Built as a
"Tag: Whole view / Selected elements only" choice in Smart MEP Tagging Settings — the same two-way
choice Revit's own Tag All dialog offers — stored as `smarttag_selected_only`, default whole view.
Chosen over "use the selection automatically when there is one" because a stray selection would then
silently shrink a run; the explicit choice plus a clear "nothing is selected" stop cannot surprise.
Selecting a link tags inside it; on Revit 2023+ Tab-picked linked elements count too
(`Selection.GetReferences`). New file `Step2_FindElements/SmartTagSelectionFilter.cs`. Built only.

### 2026-10-15 (Smart MEP Tags restructured — still v1.58.0, same PR)

Ajmal, after the linked-model work: *"you can completely restructure this smart tag tool… the
algorithm it is very difficult to find and edit. You can make separate code for this algorithm."*
`SmartMepTagService.cs` (2,100 lines) and `SmartTagPlacementEngine.cs` (1,600) became step folders
with one job per file, a `SmartTagTuning.cs` holding every placement number, a
`SmartTagPlacementContext` replacing the 17-value parameter list, and the "where does the tag go"
routine split into three named stages. Map in `ajtools-conventions.md` ("Smart MEP Tags code map").

**Kept to a pure restructure** — no tag lands anywhere different. Proven by a script comparing every
routine's code lines old-vs-new after a declared list of intended substitutions; zero unexplained
differences; all 8 configs built. Folded into 1.58.0 because that version is not released yet.
"Add more things connected" from the same message was read as permission, not a spec — nothing new
was added on it; asked Ajmal what he wants next instead of guessing.

### 2026-10-14 (Smart MEP Tags tags linked models — v1.58.0)

Ajmal's request, dictated: a tick box in Smart Tag so it "searches for the linked element", so
mechanical equipment in a linked model can be tagged; and a better settings window. Read as ONE global
tick box applying to the categories already ticked (not a per-category column) — the simplest thing
that matches "one more tick box". Off by default, so nothing changes for anyone who does not tick it.

**Built**: `SmartTagLinkedElementCollector` (new file) finds loaded links shown in the view and their
elements per ticked category; `SmartMepTagService` folds them into the same filter loop as host
elements with their geometry already in host coordinates; `SmartTagPlacementEngine` tags them through
a link reference; the report says how many linked elements were found and tagged. Settings window: the
tick box, an "In links" count, a live "x of y categories ticked" line, run options grouped in one card,
"Advanced" renamed "Size limits". Setting stored as `smarttag_include_linked` in `TagClash.config`.

**Decisions worth keeping**: (1) the old code comment called "never tag elements from linked models" a
hard constraint — it was the tool's original scope, not a rule Ajmal set, and his request supersedes
it; the default stays off. (2) Linked candidates are compared by the candidate object / (link, id) key,
never a bare ElementId — the engine had four places (`taggedGroupMembers`, the parallel-group self
check, the model-obstacle lookup, the midpoint lookup) that would have silently confused a linked fan
coil with a host duct sharing its id. (3) 2020–2022 use an estimate of what the view shows; the report
says so instead of pretending it is exact.

**Also found**: the Linux web container CAN build AJ Tools (`dotnet-sdk-10.0` from Ubuntu +
`EnableWindowsTargeting`) — the old "cannot build" note was corrected in place. All 8 configs built.
NOT run in Revit; Ajmal to test on 2020 (estimate path) and, if he can, a 2023+ version.

### 2026-08-19 (Quick Menu customise becomes the wheel itself, drag-and-drop — v1.53.0)

Ajmal ran 1.52.0 and came straight back: *"in the settings window same quick menu view also I need so I
can drag and drop the tools in there and easy for the working"*. The customise window had been showing a
numbered list of slots, which is fine to read but nothing like the thing it configures.

The slot list was replaced with the wheel, drawn in the settings window's own code-behind using the same
geometry and palette as `QuickMenuWindow` — slot 1 at the top, the rest clockwise, wedges hit-tested by
angle with the shapes themselves `IsHitTestVisible=false`. Three drags do all the arranging: list → wedge
fills a slot, wedge → wedge swaps two, wedge → list empties one. The drop target lights green so the
landing spot is never ambiguous.

**Deliberately kept**: the Set / Clear slot / Move buttons, now driven by the slot picked on the wheel.
Dragging is the nicer path, not the only one — removing the buttons would have made a fiddly drag the
sole way to do anything. "Move up"/"Move down" became "Move back"/"Move on", because up and down mean
nothing on a ring.

**The one real trap** (now a rule in `ajtools-conventions.md`): `DoDragDrop` blocks until the drop
completes and eats the mouse-up, so `_dragFromSlot` had to be reset *before* the call. Resetting it
afterwards, or in a mouse-up handler, leaves it armed and the next ordinary click starts a phantom drag.

**Duplication accepted knowingly**: the wedge geometry now exists in two files. Both metadata blocks say
so, because the alternative — extracting a shared wheel control — is a bigger refactor of a tool that has
not yet had its first live run, and the two must simply be kept in step until that is worth doing.

Clean on Release (2020), R25, R26 and R27 (R27's only warning is the pre-existing `Space.Zone` one).
Deployed to Revit 2020. **Not loaded in Revit by the assistant** — Ajmal tests it.

### 2026-08-19 (Quick Menu — Revit's own commands can sit on the wheel, still v1.52.0)

Ajmal read the first version back and asked the obvious question: *"in the customise window can I add my
custom tool AND Revit's one also?"* — his own AJ Tools buttons were there, Revit's own commands were not.
So they were added, into the same unreleased 1.52.0 rather than a second version bump, because 1.52.0 has
never shipped and would otherwise read as a release that lacked them.

**Launching Revit's own command turned out to be the easy half** — `RevitCommandId.LookupPostableCommandId`
hands the id straight over, no `CustomCtrl_%...` string to read or rebuild, and `PostCommand` is the same
call either way. `QuickMenuLauncher` branches once on `entry.Source` and everything downstream is shared.

**The real trap was the version matrix, and it is worth remembering beyond this tool.** The tempting way to
offer Revit's commands is to list the useful ones — `PostableCommand.ThinLines`, `PostableCommand.PurgeUnused`
and so on. That would have compiled on Revit 2020 and then failed the build on some of the other seven
configurations, because Autodesk adds and removes enum members every release. Walking the enum by name at
run time (`Enum.GetNames` + `Enum.Parse`) means only the enum's *type* appears in compiled code, one source
file serves 2020 → 2027, and each version automatically lists exactly the commands it has. The same reasoning
applies to any version-varying Revit enum.

Two smaller decisions worth keeping:
- **Saved keys got a prefix.** A slot now holds either a command class name or `revit:Undo`. The colon is
  illegal in a C# class name, so the two can never be confused and files saved by the earlier build still load.
- **A "Show" filter, not one long list.** Revit publishes on the order of a thousand commands; dropped into
  one list they would have buried Ajmal's own forty-odd tools. Show = All / AJ Tools only / Revit commands
  only, with the search box narrowing whichever is showing (search also matches the unspaced command name,
  so "visibilitygraphics" finds "Visibility Graphics").

Revit gives add-ins **no artwork** for its own commands, so those wheel slots show the name with no icon —
the wheel already handled a null image, so nothing needed changing there.

Still not compiled and still never opened in Revit — same Linux container, same limitation as the 18th.

### 2026-08-18 (Quick Menu — the game-style tool wheel, v1.52.0)

Ajmal sent a screenshot of a video-game radial "quick menu" (weapon wheel, eight wedges round the mouse
pointer) and asked for the same thing inside Revit, customisable, running the tool that is clicked.

**The one real engineering question was how a wheel slot runs a tool**, and it has exactly one supported
answer: `UIApplication.PostCommand`. Ruled out first, on the way there — `ExternalCommandData` cannot be
constructed, so calling another command's `Execute` directly is impossible; `PushButton` has no click
method. That single constraint shaped the design: the wheel is **modal** (`ShowDialog`) because the
posting command must still be inside its own `Execute()` when the choice comes back.

**The id string is READ, not composed.** Not
guessed: `RibbonItem` carries a non-public `getRibbonItem()` that returns the `Autodesk.Windows` item,
whose `Id` is the `CustomCtrl_%...` string. Reflection, so no `AdWindows.dll` reference is added to an
eight-configuration build. Composed strings are kept only as a fallback. Rule written up in
`ajtools-conventions.md`.

**Nothing about the tool list is hard-coded** — the catalog walks `GetRibbonPanels` / `GetItems` and
reads each button's own `ClassName`, label, tooltip and icon, so a tool added in a later release turns
up on the customise list by itself. Slots are saved by **class name, not label**, so renaming a ribbon
button does not wipe Ajmal's layout.

**Geometry was verified without Revit.** No WPF on Linux, so the wedge maths (angles, radii, label
placement) was re-implemented in Python and rendered to PNG for every slot-count/size combination. That
caught two real defects before they shipped: an empty slot list indexing `_slots[0]` that does not
exist, and label blocks sized off the ring thickness that spilled across the wedge dividers — the tight
spot is the block's INNER corners, because a wedge narrows towards the hub. Known residual: at **12
slots** the empty corners of a label's bounding box graze the divider lines. Harmless (centred text puts
no ink there) and left alone rather than over-tuned geometry that cannot be seen running.

**NOT BUILT, NOT RUN IN REVIT.** Written in the Linux web container — no msbuild, no Revit API
assemblies, no WPF. Needs a Windows build (`Release` + one newer config) and a live open before it is
trusted. Two things a compile cannot catch and a live open must: the settings window's
`../UI/ModernStyles.xaml` merge (a wrong resource URI only throws when the window opens), and whether
`PostCommand` really fires from a modal dialog's caller on his Revit 2020.

**Ribbon**: new "Quick" panel, placed FIRST on the AJ Tools tab, one split button (Quick Menu /
Customise) following the established permanent-default-face pattern. Everything else shifts one panel
right — flagged to Ajmal in case he wants it elsewhere.

### 2026-08-13 (Web Panel removed the day after it shipped — v1.44.0)

Ajmal did not want it: *"web panel i dont whant i told you to remove"*. It went the same way the spoken
voice went in v1.42.0 — **code deleted, not switched off**: `src/WebPanel/` (4 files, 1,101 lines), the
ribbon button, and the startup/shutdown wiring in `App.cs`. Nothing remains that can open a port, and
there is no setting to find. `CLAUDE.md` now carries the guardrail that AJ Tools does not serve a web
page and must not grow one back unasked.

Worth being honest about why it had to be said twice: he had already asked, in the same breath as asking
about AJ Connect — *"confom that aj connect in the revvit aj tools also as separate also i dont what so
remove it"*. That was read as AJ Connect only, and the Web Panel — the same browser idea living **inside**
AJ Tools rather than beside it — was left in place. **When he names a capability, he means it wherever it
lives, not just the artefact that carries that name.**

Kept deliberately:
- **`UnhideAllService`.** Split out of `CmdUnhideAll` to give the panel a UI-free way in. UI-free model
  work stands on its own, and it is the worked example for the "one logic, two front doors" rule that
  still applies to any future non-UI caller.
- **The measured findings**, reframed in `ajtools-conventions.md` as history rather than live design.
  One of them — that a localhost `HttpListener` needs **no** admin rights and no URL ACL, only the
  wildcard prefix does — had corrected a claim this repo had written down wrongly in an older service
  header. That finding is worth more than the code was, so it outlived it.

**Two sweep hazards found while removing it, both worth more than this feature:**

1. **A raw NUL byte made `grep` silently skip this very file.** Line 723 documented a corrupted NUL
   separator by *containing* one, so ripgrep/grep classified all 2,111 lines as binary and printed
   `Binary file ... matches` with no lines — hiding the entire Web Panel history section from every
   keyword sweep. Replaced with an escaped `\0`. **A "Binary file matches" line in a text-file search is
   a failed read, not a result. Never treat it as "nothing there".**
2. **Five parallel sweeps mapped the whole repo and all five missed the installed copies.** They
   inventoried `src/`, `dist/`, docs and the published zip — but not
   `%APPDATA%\...\Revit\Addins\<year>\AJ Tools\`, which is where the add-in Ajmal actually runs lives.
   Every one of those was still the pre-removal build, so the button would have stayed on his ribbon
   while every in-repo check reported the feature gone. **Deleting a feature from the source does not
   remove it from the machine — the install is a separate place and has to be re-checked by name.**

### 2026-08-13 (v1.43.0 shipped, and two traps found while shipping it)

**v1.43.0 had been fully prepared but never shipped.** All six version references agreed, the
CHANGELOG entry was written and dated, the Web Panel was tested in Revit — and there was no tag and no
release. The public download sat at v1.42.1 while the source said 1.43.0. Worth remembering as a
failure mode of its own: **"version bumped and changelogged" is not "released", and nothing in the repo
warns you about the gap.** The version-consistency checker passes happily, because every file it checks
agrees with every other — it has no idea whether any of it was ever published.

Two traps found while publishing it:

1. **`gh` IS installed and authenticated on this machine.** `RELEASE_PROCESS.md` step 9 says it is not,
   and routes you through `Invoke-RestMethod` against the public API to confirm a release. That line is
   stale — the whole release was driven with `gh` (`gh release list/view/download`, `gh run watch`).
   Corrected in the document.
2. **Do not verify the release-notes extraction locally with GNU Awk — it lies.** The publish workflow
   pulls the release body out of `CHANGELOG.md` with an awk dynamic regex, `"^## \\[" version "\\] - "`.
   Under **GNU Awk 5.3.2** (this machine's Git Bash) that `\[` is treated as a plain `[`, so the pattern
   becomes a character class and matches nothing — the extraction returns **0 bytes**. It does this for
   **every** version, including ones already published with correct notes. On the Ubuntu runner it works
   fine. The danger is obvious: run it locally, see 0 bytes, conclude your changelog entry is malformed,
   and "fix" a file that was already correct. **The control test is the fix** — run the same extraction
   against a previously published version (`1.42.1`). If that returns 0 bytes too, it is your local awk,
   not your entry.
3. **The shipped `install.cmd` could not install Revit 2025/2026/2027 — FIXED, shipped in v1.43.1.**
   `dist\install.ps1`
   still carries a pre-multiversion guard: it treats the package as "a .NET Framework/Revit 2020-2024
   build", installs the **root** `AJ Tools.dll` (the 2020 net472 build), and **skips 2025-2027** with a
   `NEEDS_REVIEW` warning. It never looks at `Payload\<year>\` at all — the per-Revit builds that have
   shipped in every zip since the multi-version backbone landed (2026-07-06). Consequences for anyone
   installing a release the documented way: **2025/2026/2027 get nothing**, and 2021-2024 get the 2020
   net472 build instead of their own net48 one. `INSTALL.md` meanwhile advertises "payloads for Revit
   2020-2027", so the docs and the installer disagree. Verified against the v1.43.0 package on
   2026-08-13; the payloads themselves are correct and complete (all eight at 1.43.0, right framework
   each: net472 / net48 / net8.0 / net10.0). Fixed the same day in v1.43.1: installable versions are
   now read from what `Payload\` contains rather than a list in the script, so it cannot drift from
   the package again. **The lesson is the shape of the bug, not the bug** — a hardcoded capability
   list in the installer silently outlived the build system that made it true, and nothing failed
   loudly: the build was green, the package was correct, the docs promised 2020-2027, and only an
   actual install on a 2025+ machine would have shown it. Anything that hardcodes what the product
   supports should be derived from the product instead.
   **Verify a release by installing from the built zip, not by reading the script.** v1.43.0 was
   published having verified the payloads (all eight present, right framework each) but never having
   run its own installer — which is precisely the step that would have caught this. v1.43.1 was
   verified by extracting the published zip and running its `install.ps1` the way a user does.

### 2026-08-12 (Installer and publishing defects — all found by checking output, not code)

Three defects worth remembering, none of which a code review would have caught:

1. **An installer that destroyed a working install.** It removed the add-in registration BEFORE
   copying new files, so a copy that failed on a DLL Revit had locked left the machine with nothing.
   Hit for real mid-session. Rule: never destroy a working install before the replacement is in
   place; and locked files can be **renamed** even when they cannot be deleted, which is how you
   update while Revit is open. Directly relevant to this repo's own installer.
2. **A 0-byte file published live**, because the publishing command was piped through
   `Select-Object -First 4` — which terminates the upstream script. It exited 255 and that was
   ignored.
3. **A BOM in machine-read JSON** from `Out-File -Encoding utf8`.

All three passed an HTTP 200 check. **A 200 proves a file exists, not that anything is in it.** The
rules that came out of 2 and 3 are in `ajtools-conventions.md`.

### 2026-08-12 (Web Panel — AJ Tools buttons in a browser, v1.43.0)

Ajmal's idea, refined over several messages, and worth recording because the first version of it that
got discussed was the wrong shape. He initially sounded like he wanted the ribbon moved into a browser;
pushing back on that was correct (for a one-click tool like Unhide All the browser is strictly slower —
alt-tab out, click, alt-tab back). But that was not his idea. What he actually wants is: **the user
installs ONE connector once, then every tool is posted to a website and appears for everybody with no
reinstall, ever.** That is a genuinely better architecture, and the value is not "browser instead of
ribbon" — it is *shipping tools without redeploying a DLL*. Lesson: when a request keeps coming back in
slightly different words, the disagreement is usually about what was heard, not about what is right.

Built this session as the proof: `src/WebPanel/` (service, tool runner, page, ribbon command), plus
`Services/UnhideAll/UnhideAllService.cs` extracted out of `CmdUnhideAll`. Rules that came out of it are
in `ajtools-conventions.md` § "Reaching AJ Tools from outside Revit" — localhost `HttpListener` needs no
admin (measured, and it **corrected a wrong claim written earlier in this repo** that had been
used as the reason to rule HTTP out); names-not-code; token + Origin; serve the page from the listener;
one-logic-two-front-doors; `UseShellExecute` on .NET 8; bind-to-find-a-free-port.

Deliberately NOT built yet, and each for a reason:
- **Downloading tool code from a website.** This is the actual goal, but it turns a hostile/hacked
  website into code execution on every colleague's machine. Needs signing designed first. The registry
  approach shipped here is the safe half, and is genuinely useful on its own.
- **Its own icon.** Borrowed an existing icon pair with a TODO in `RibbonManager.AddWebPanelTool`.
- **Any tool beyond Unhide All.** Each needs the service split first, which is the real per-tool cost.

Status: **live-verified the same day.** Builds clean on Release (2020) and R25, all six version
references agree, deployed, and then actually exercised against Revit 2020 (model "Project1", view
"1 - Mech"): ribbon button → server on 48210 → browser opens → `/api/context` returned the real version,
model and active view through the ExternalEvent path → Ajmal confirmed Unhide All from the browser
changes the live model. Wrong token = 401, foreign `Origin` = 403, both confirmed by request rather than
by reading the code.

**A false alarm worth remembering**: the first report was a browser showing `ERR_CONNECTION_REFUSED` at
`localhost`. Nothing was broken — the URL had no port on it (`localhost` alone is port 80) and the
server had not been started yet, because it only starts when the ribbon button is pressed. Diagnosed by
checking three facts rather than guessing: the discovery file did not exist, nothing was listening on
48210–48229, and Revit's process start time was *later* than the DLL's write time (so the new add-in
really was loaded). Check those three before suspecting the code — "connection refused" on a
port-less localhost URL is almost always "not started" or "wrong address", not a defect.

### 2026-08-11 (Releasing is AUTOMATED — do not publish by hand, it breaks the pipeline)

**Read this before any release.** Learned by getting it wrong on v1.42.0 and failing the CI run.

Releases are published by a GitHub Actions workflow in the **separate installer repo**, not by hand and
not from this repo:

- Repo: `Ajmalpshaik/AJ-Tools-Installer` (cloned locally at `D:\Ajmal\Revit Addins\AJ-Tools-Installer`).
  This code repo (`AJ-Tools`) carries **tags only** — it has old releases up to v1.3.4 from April 2026,
  but every version since publishes on the installer repo instead.
- Workflow `.github/workflows/publish-release.yml`, triggered by pushing a `v*` tag. It:
  1. `awk`s `CHANGELOG.md` for a `## [X.Y.Z] - YYYY-MM-DD` section and turns it into the release notes
     (`Released: <date>` + the section body). **An empty result fails the job on `test -s`.**
  2. Verifies `releases/AJ-Tools-vX.Y.Z.zip` and `releases/SHA256SUMS.txt` exist and that
     `sha256sum -c` passes.
  3. Creates *or updates* the release via `softprops/action-gh-release`, attaching both files.

**So the correct order is:** run `dist\package.ps1 -Version X.Y.Z` → copy the zip into the installer
repo's `releases/` (it holds **one** zip at a time, replacing the previous) → write `SHA256SUMS.txt`
**with an LF ending** (`.gitattributes` enforces it; Linux CI runs `sha256sum -c` and CRLF breaks it) →
add the `## [X.Y.Z]` changelog section → commit and push → **then** push the tag.

**What went wrong:** the release was created manually with `gh release create` and the files uploaded
directly. That published a release that *looked* right while the repo held none of it, and the tag push
fired the workflow, which failed at the notes step because no changelog section existed. Repaired by
committing the payload properly and moving the tag onto that commit — the workflow then updated the
same release in place, so the URL never changed. **A hand-made release that bypasses a pipeline is not a
shortcut; it is a release with no record behind it and a red X on the repo.**

**Also worth knowing:** `dist\package.ps1` reads `${env:ProgramFiles(x86)}` to find `vswhere`. That
variable is absent in some non-interactive shells (an automation shell here), and the script dies in
under a second with a null-argument error on `Join-Path`. The script is fine — set the variable before
launching rather than "fixing" it.

### 2026-07-29 (Game Mode round 20 — v1.38.3, Ajmal's final weapon color scheme)
- Ajmal's fixed color mapping, applied everywhere the weapon color appears (crosshair, laser beam/
  dot/readouts, gun accent stripes, muzzle glows, weapon text): GUN amber #FFC53D, LASER green
  #3CE24A (the beam itself changed red->green), CLEANER black & white (white crosshair bars with
  1.2px black strokes), SNAG red #FF3B30, SELECTOR blue #00C8FF. This is a standing scheme - don't
  reshuffle without his say. Deployed 1.38.3 (payload 20260729192048418), Revit closed.

### 2026-07-29 (Game Mode round 19 — v1.38.2, aim/display sync fix + tool-colored crosshair)
- Ajmal confirmed the VR teleport ("yes like this I need") and reported a REAL live bug: on a fresh
  game start, shooting a duct sometimes acted on a DIFFERENT element - and opening the Properties
  palette (which shrinks the view) fixed it. Root cause: Revit 2D-zooms/pans perspective views like
  a photo, so the rendered picture's centre can drift off the camera axis; our rays are always
  camera-centred, so crosshair and true aim diverge until Revit re-fits the view. FIX: the engine
  calls UIView.ZoomToFit on the game view on the first frame, on every resume from pause (new
  ResyncViewQueued input set by EnterPlay) and on every window resize. RULE for any future
  crosshair-style tool over a perspective view: force ZoomToFit before trusting screen-centre aim.
- Crosshair now wears the active tool's color (blue/red/amber/magenta/green) - doubles as the
  weapon indicator in professional mode where no gun shows.
- v1.38.2 built clean (2020 0/0; R25 0/0 GameMode), deployed payload 20260729191446480. Revit
  open - restart needed.

### 2026-07-29 (Game Mode round 18 — v1.38.1, VR-style teleport visual per reference image)
- Ajmal pasted a VR teleport screenshot (thick green ballistic arc + landing disc flat on the
  floor) - "like this we need, generate a sample then we will finalize". Rendered the sample with
  the real HUD math (pistol muzzle start, cubic bezier cresting then dropping onto the aim point,
  double flattened ellipse disc + dot reading as floor-flat), sent it, and implemented identically
  in the same round: TeleportArc now solid 5.5px #3CE24A cubic bezier (was thin dashed quadratic to
  a round ring), landing marker = flattened 68x24 ring + 34x12 inner + flat dot. Deployed 1.38.1
  (payload 20260729191101116); Revit open, restart needed. Awaiting his finalize/tune call.

### 2026-07-29 (Game Mode round 17 — v1.38.0, professional mode + selector gun)
- Ajmal: the guns are fun but not showable to a manager/meeting - he needs a no-gun version, and
  the holster wasn't enough (shooting/clicking brought the gun back). New PROFESSIONAL MODE (N,
  remappable): no weapon visuals ever; all tools work identically with beams from the bottom of
  the view; flash/recoil/holster disabled; persisted in AppData ajgame-prefs.txt (GamePrefs.cs) so
  it survives sessions - one press before a meeting, done.
- Mid-turn addition: SELECTOR as 5th weapon (green) - the shot toggles the element in the live
  Revit selection (uidoc.Selection.SetElementIds; no transaction, no undo entries, linked refused);
  selection SURVIVES game exit -> walk, shoot-select, exit, edit. Weapon cycle now:
  pistol GUN / LASER / rifle CLEANER / blaster SNAG / SELECTOR.
- v1.38.0 built clean (2020 0/0; R25 0 errors, 0 GameMode warnings), deployed payload
  20260729184340747, Revit closed. Not yet click-tested.

### 2026-07-29 (Game Mode round 16 — v1.37.0, measure removed + full graphics reset + remappable keys)
- Ajmal: measuring "not working properly, remove that" - deleted wholesale (Measure partial file,
  projection/calibration code, all measure state/UI). Honest note: the rubber-band projection was
  the round-11 experiment; removal accepted without argument per the ship-what-he-wants rule.
- J is now a FULL "Reset Element Graphics in View" inside the game (FilteredElementCollector over
  the game view -> default OverrideGraphicSettings per element, failures skipped - the existing
  Reset tool's proven approach). Catches red snag marks left by EARLIER sessions, which the old
  tracked-ids-only clear could not. U unchanged (temporary-hide reset).
- REMAPPABLE KEYS, his spec ("press what key, that will save"): GameKeyBindings (defaults + AppData
  ajgame-keys.txt, ForceSet/TrySet with duplicate rejection) + GameKeySettingsWindow (pause -> S;
  click a key button, press the new key; inline errors for reserved/duplicate keys; Cancel restores
  the opening snapshot). ALL letter actions route through the bindings' reverse map in the rebuilt
  Controls partial; Esc/mouse/wheel/1-9/arrows stay hardwired. Settings window styling is
  deliberately self-contained (no merged dictionaries -> no StaticResource runtime risk).
- v1.37.0 built clean (one leftover MeasureHold reference caught by the compiler; 2020 0/0, R25 0
  errors 0 GameMode warnings), deployed payload 20260729155812595. Revit open - restart needed.

### 2026-07-29 (Game Mode round 15 — v1.36.4, rifle/beam alignment fix)
- Ajmal from the previews: snag blaster "perfect"; cleaner rifle "gun and line slightly need to
  rotate" - the rifle barrel sat at ~33 degrees while the beam to the crosshair ran at ~17. Fix in
  two coupled moves: bake a 16-degree rotation into GameRifle.png (muzzle re-tracked to 0.028/0.147)
  AND reposition so the muzzle lands back on the ~17-degree line (display 280, bleedR 119 - the
  stock/arm now bleeds off the right edge, FPS-natural; bleedB -5). Barrel + beam now read as one
  straight line (verified in preview 2, sent).
- Note for future gun art: rotation direction in this GDI+ pipeline is empirically INVERTED vs the
  documented "positive = clockwise" (the pistol's +60 and the rifle's +16 both rotated counter-
  clockwise on screen) - always do a small-angle test or check the output bbox before trusting the
  sign. Deployed 1.36.4 (payload 20260729153821306), Revit closed.

### 2026-07-29 (Game Mode round 14 — v1.36.3, blaster art for the SNAG MARKER + both previews)
- Ajmal supplied "SNAG gun .png" (blue/orange toy blaster - fitting for a marking tool). Same
  pipeline as the rifle: transparency already real, residual green keyed, RGB blanked under alpha,
  trim, muzzle tracked to (0.022, 0.087) at the orange tip. New SnagImage in the HUD; weapon cycle
  now shows pistol (gun/laser) / rifle (cleaner) / blaster (snag), with glow/flash/tracer/laser
  following the active muzzle. Deployed 1.36.3 (payload 20260729153212075, both PNGs verified in
  payload, Revit closed). Rendered + sent both placement previews per his ask; both clean.

### 2026-07-29 (Game Mode round 13 — v1.36.2, rifle art for the CLEANER weapon)
- Ajmal supplied rifle.png (Y:\Ajmal Ps\icon) for the CLEANER. Discovery that re-reads earlier
  rounds: these AI gun exports ALREADY carry true alpha transparency - the "green background" seen
  in image previews is residual RGB *under* A=0 pixels (the preview renderer flattens alpha). So the
  pistol's chroma-key war was partly self-inflicted; for the rifle, processing = key residual green
  + blank RGB under transparency (prevents green edge tint when WPF scales) + trim, orientation
  untouched (it is already a proper FPS behind-view aiming up-left). Muzzle at the flash hider,
  fractions (0.034, 0.095). LESSON: check the alpha channel FIRST before fighting a "background".
- Weapon-visual swap: CLEANER now shows the rifle (pistol hidden), glow/flash/tracer/laser all
  follow the active weapon's muzzle; pistol returns on GUN/LASER/SNAG. Deployed 1.36.2 (payload
  20260729152813906, GameRifle.png verified in payload). Revit open - restart needed.

### 2026-07-29 (Game Mode round 12 — v1.36.1, unlimited saved positions for the tour)
- Ajmal: "Tour mode not 1 2 3 - until how much we have" = no slot limit. B now counts up forever
  (10, 11, 12...) instead of rotating over 1-9; the tour visits every saved slot sorted; the list
  shows all (slots >9 marked "tour only" since the number keys physically stop at 9). Deployed
  1.36.1 (payload 20260728225413900), Revit open - restart needed.

### 2026-07-29 (Game Mode round 11 — v1.36.0, rubber-band measure + "add all" #2)
- Ajmal corrected the measure UX: he wants a GREEN laser anchored to the FIRST face, stretching to
  the second face with the dimension riding on the line (true BIM-360 rubber band), not just numbers
  in a card. Solved the "project a 3D point to screen in a perspective view" problem that was earlier
  declared impossible: the engine publishes the camera basis each frame, computes view-direction
  tangents for the anchored point(s), and converts tangent->pixels using a calibration from
  UIView.GetZoomCorners (guarded try/catch + sanity bounds; falls back to an assumed 55-degree FOV
  when corners are unavailable/nonsense). Locked measurements stay glued to both faces while walking.
  Anchor-accuracy is THE thing to verify live; numbers are exact regardless.
- "Add all" #2 delivered: SNAG MARKER 4th weapon (red SetElementOverrides in the game view via
  language-safe solid-fill lookup FillPatternElement.GetFillPattern().IsSolidFill; punch list with hit
  positions in mm; J clears; report .txt written from the ENGINE inside Stop() - still valid API
  context - to Documents\AJ Game Snags, path appended to the end-screen reason); tour mode (O, flies
  through saved slots, saved look adopted at each stop, any move key cancels, HUD parks the mouse via
  hud.TourRunning); compass + level line (project north assumed +Y; levels cached once); crouch (C in
  walk = 1000 mm eye, collision ray heights scale with current eye height); speed dial (+/- keys,
  x0.4..x3.0); flashlight (V, HUD RadialGradient vignette); synthesized gunshot WAV built in memory
  (no sound files; SoundPlayer; M mutes; CA1416 pragma same policy as photo capture).
- v1.36.0 built clean FIRST TRY on a ~15-file round (2020 0/0; R25 0 errors, 0 GameMode warnings),
  deployed payload 20260728223045475. Revit was open (01:02 session) - restart needed.

### 2026-07-29 (Game Mode round 10 — v1.35.1, section box removed on request)
- Ajmal: "X no need follow mode remove that" - the follow-me section box (the one v1.34.0 feature
  that committed transactions) is fully deleted: input flag, engine fields/toggle/maintenance/
  ApplySectionBox, X key, help line, tooltip mention. Game Mode is back to ZERO undo entries in
  every mode. Deployed 1.35.1 (payload 20260728221207558) while he was playing - loads next restart.
- He asked for a fresh feature-idea list; offered: marker/snag gun with exported report, compass +
  level display, crouch, live speed dial, tour mode through saved positions, flashlight night mode,
  shot sounds. Awaiting his pick.

### 2026-07-29 (Game Mode round 9 — v1.35.0, Workshop-XR teleport + saved-positions panel)
- Ajmal live-tested teleport ("that is working") and referenced Autodesk Workshop XR: he wants the
  VR-style two-stage jump - see an arc to where you're going, then confirm. Implemented as HOLD T =
  glowing dashed jump arc (2D quadratic bezier on the HUD from bottom-centre to the crosshair - the
  true 3D-projected arc is still impossible without the view's FOV) + pulsing landing ring + "Release
  T - jump N mm"; RELEASE T = confirm and go; gray arc + "aim at a surface" when invalid. T no longer
  teleports instantly on press. Engine feeds the aim target every frame while T is held, any weapon.
- Second ask: saved positions must be VISIBLE - new left-side "SAVED POSITIONS" panel listing each
  B-saved slot with number + X/Y/Z in mm. Selection = pressing the number (list rows are NOT
  clickable on purpose: the mouse is captured for FPS look while playing, so number keys are the
  only selection that works without breaking mouse-look). Rebuilt via a BookmarksVersion counter.
- v1.35.0 built clean (2020 0/0; R25 0 errors, 0 GameMode warnings), deployed payload
  20260728215928156, Revit closed. Awaiting live test of the arc feel.

### 2026-07-29 (Game Mode round 8 — v1.34.1, one-folder restructure per Ajmal's own idea)
- Ajmal proposed the architecture himself: "keep it entirely in one folder... each feature separate
  .cs file... editing also it will be easy, am I right??" - confirmed and done. Everything moved to
  src/GameMode/ (old Commands/Services/UI GameMode subfolders deleted); the two big classes split
  into per-feature PARTIAL files: engine core + .Movement + .Measure + .Extras; HUD core + .Controls
  + .Weapons + .Render + .Photo. 13 files, code byte-identical (only `partial` added), namespaces
  deliberately unchanged so RibbonManager/nothing else needed touching. Promoted to a standing rule
  in ajtools-conventions.md (feature-folder layout). Clarified honestly to Ajmal: file layout does
  NOT change Revit speed (same single DLL) - the win is editing/removal clarity.
- New build gotcha captured as a rule: moving a .xaml pair leaves stale generated files in the
  per-version obj folder -> 110 phantom CS0103 errors on every x:Name field from the _wpftmp pass;
  deleting src\obj\R<year> fixed it instantly (0/0).
- v1.34.1 built clean (2020 0/0 after obj clean; R25 0 errors, 0 GameMode warnings after its own obj
  clean), deployed payload 20260728215037356. Behaviour identical to v1.34.0 - still awaiting
  Ajmal's live test of the round-7 features.

### 2026-07-29 (Game Mode round 7 — v1.34.0, "add all": all six offered extras built)
- Ajmal accepted the whole idea list with "add all let me see". Built in one round: T teleport to the
  aimed point; B + 1..9 in-session saved positions (look direction restored via a HUD look-override
  handshake - the HUD owns yaw/pitch, so the engine REQUESTS the change instead of writing it);
  K photo mode (HUD collapsed for one frame, view area captured with System.Drawing CopyFromScreen in
  raw device px, saved to Pictures\AJ Game Photos); CLEANER as a third weapon in the right-click cycle
  (temporary-hide the element hit, host elements only, U restores; try-without-transaction first with
  transaction fallback, since Revit was CLOSED and HideElementsTemporary's transaction requirement
  could not be live-verified - flagged to Ajmal as the thing to watch); live clear-height readout
  (floor-ray + ceiling-ray per frame, mm); X follow-me section box (10x10x7 m, re-centred every 2.5 m,
  one committed transaction per re-centre - the ONLY Game Mode feature that adds undo entries, said so
  in its toast and to Ajmal).
- CA1416 precedent: the photo capture's System.Drawing calls triggered 10 new CA1416s on R25 - exactly
  the known-noise class. Resolution per the "judge by warnings from files you touched" rule: a tightly
  scoped #pragma warning disable CA1416 with a written justification at the call site (a Revit add-in
  is Windows-only). NOT a blanket suppression; the pre-existing 274 stay untouched.
- v1.34.0 built clean (2020 0/0; R25 0 errors, 0 GameMode warnings), deployed payload
  20260728213624328, Revit closed. Not live-tested; cleaner-hide and section-box transaction paths are
  the first things to verify in play.

### 2026-07-29 (Game Mode round 6 — v1.33.0, scroll holster + XYZ measure breakdown)
- Ajmal (dictated): "if i sroll button use that gun will go inside... i can shool but laser fetur will
  wrrok" = scroll wheel puts the gun AWAY (slides off-screen, no shooting) while laser/measuring keep
  working - beam then rises from the bottom of the view like a handheld pointer. Scroll up (or a
  click) draws it back. Wheel is ignored during a held measurement so it can't spoil one.
- "while holding that will show one lase z axis and x and y" = the measure card now shows the
  BIM-360-style axis breakdown: Total on top, then X / Y / Z deltas + plan distance, all mm.
- He also asked for MORE feature ideas ("tell me what ever") - offered in chat: teleport-to-laser-
  point, save/jump positions, photo mode, shoot-to-hide (temporary hide), live headroom readout,
  follow-me section box. Awaiting his pick; none built yet.
- v1.33.0 built clean (2020 0/0; R25 0 errors), deployed payload 20260728212615935, Revit closed.
  Not yet live-tested.

### 2026-07-28 (Game Mode round 5 — v1.32.1, gun orientation corrected per Ajmal's reference)
- Ajmal pasted the gun image itself and said "your generated is wrong, this is the gun" — he wants the
  picture EXACTLY as generated: no horizontal flip, no tilt. All my careful FPS-composition reasoning
  (flip so the muzzle faces the crosshair, tilt to aim) was overridden by the owner's visual intent.
  LESSON for future art integration: when Ajmal supplies artwork, ship it verbatim first (background
  removal/trim only) and let HIM ask for pose changes — don't "improve" orientation unprompted.
- Now: key+trim only -> 805x779, muzzle fractions (0.650, 0.343), display height 300, corner bleed
  40/60. Laser/tracer still start exactly on the muzzle. Preview 2 rendered + sent; deployed as
  v1.32.1 (payload 20260728201944224, un-flipped PNG verified in payload by dimensions). Not yet
  live-tested.

### 2026-07-28 (Game Mode round 4 — v1.32.0, laser measuring + preview-driven gun retune)
- Ajmal asked for BIM-360-style measuring on the laser ("press and hold and from one face to another
  face distance we can measure"). Implemented as hold-and-release: LMB down in laser mode captures
  point A = the exact 3D ray hit on the first face; while holding, a green HUD card live-shows Total /
  Horizontal (plan) / Vertical (level difference) in mm; release locks the card until the next press.
  Engine-side state machine (UpdateMeasurement in GameMotionEngine), pure reads, works on linked
  elements. Deliberately NOT drawn as a 3D rubber line: projecting a model point to screen pixels in a
  perspective view needs the view's FOV, which the API doesn't expose cleanly - numbers are exact, the
  visual line was skipped (noted honestly to Ajmal).
- Mid-round Ajmal asked to SEE the gun before testing ("show me how it will come... so I can tell
  corrections"). Rendered a mock preview PNG locally (System.Drawing: fake view background + the real
  HUD placement math + crosshair + laser) and sent it with SendUserFile. The preview immediately
  exposed two real problems before any Revit launch: the gun ate half the screen and the arm's cut end
  floated mid-view. Fixed by shrinking (display height 340 -> 260) and tucking deeper into the corner
  (bleed 25/45 -> 70/90). LESSON worth repeating: for any HUD/overlay visual, render a cheap mock
  preview with the REAL placement math and look at it before shipping - it catches composition bugs a
  compile never will.
- v1.32.0 built clean (2020: 0/0; R25: 0 errors, 0 GameMode warnings), deployed (payload
  20260728201604003, DLL 1.32.0.0, GameGun.png in payload, Revit closed). Measuring not yet
  live-tested by Ajmal.

### 2026-07-28 (Game Mode round 3 — v1.31.1, Ajmal's own gun picture + crop-off fix)
- Ajmal generated a gun image via AI (prompt supplied by Claude), dropped it at `Y:\Ajmal Ps\icon\gun.png`
  (1024x1024, solid green background despite asking for transparency - typical AI-generator behaviour).
  Processed locally, no cloud: chroma-key on greenness (G - max(R,B), full cut >=25, feather 10-25 with
  the green channel clamped to max(R,B) to kill fringes), content-crop, horizontal flip, 60-degree
  tilt so the barrel aims at the crosshair, content-crop again -> `src/Resources/GameGun.png`. The
  muzzle pixel (742,512 in the source) was tracked mathematically through every transform, ending at
  fractions (0.558, 0.147) of the final image - hardcoded in GameHudWindow so flash/tracer/laser start
  exactly at the barrel. C# helper compiled inline in PowerShell via Add-Type (LockBits, ~1 s for 1 MP;
  a raw PowerShell pixel loop would take minutes).
- Composition lesson: a SIDE-VIEW gun+arm picture (arm trailing behind the barrel) can never make the
  classic corner-FPS pose (barrel up-left AND wrist down-right are ~270 degrees apart; this art has
  them ~30 degrees apart). Chose barrel-at-crosshair and accepted the arm's clean diagonal cut - reads
  fine in game. If Ajmal ever regenerates, a BEHIND-the-gun view would allow the true FPS pose.
- HUD loads `Resources\GameGun.png` beside the deployed DLL at runtime; the vector pistol stays as
  automatic fallback when the file is missing (old payloads keep working).
- Crop fix per Ajmal's live report: freshly created "AJ Game View" perspective views now get
  `CropBoxActive = false` and `CropBoxVisible = false` inside the creation transaction (he was
  switching both off manually per model - the UI allows it on perspective views, so the API does too).
  Reused views stay untouched.
- v1.31.1 built clean (2020: 0/0; R25: 0 errors, 0 GameMode warnings) and deployed (payload
  20260728200723714, DLL 1.31.1.0, GameGun.png verified in payload, Revit was closed). Ajmal has been
  play-testing between rounds - v1.30.0 ran live successfully ("this is grate game"); v1.31.x weapon
  rework + this round not yet re-tested.

### 2026-07-28 (Game Mode round 2 — v1.31.0, Ajmal's feedback after first play)
- Ajmal played v1.30.0 ("this is grate game") and asked for: hold-to-shoot continuous fire, a splash
  effect on bullet impact, right-click to switch gun↔laser (drop the L key), move the element-identify
  off the shot and onto the laser (live distance + element name together), a more realistic gun, and
  faster Shift running.
- All delivered in v1.31.0: auto-fire ~7.7 shots/s while left button held; 8-spark + ring splash timed
  ~100 ms after the shot so it lands with the bullet; right-click weapon toggle with the gun's accent
  stripe/muzzle glow turning red in laser mode; laser now feeds a LIVE identity line every frame
  (Describe(includeDistance:false) under the big mm distance); pistol redrawn as a realistic side-view
  (slide/serrations/ejection port/sights/hammer/trigger guard/raked grip) drawn FLAT in a nested canvas
  and rotated 45° toward the crosshair — far easier to design than drawing polygons directly in
  diagonal space; sprint factor 2.2→3.0.
- Design shift worth remembering: gun shots no longer touch the engine at all (pure HUD fun — no
  ShootQueued flag, no per-shot Revit ray), while the laser owns ALL identification, continuously.
  One weapon = play, the other = measure/inspect. Cleaner than mixing both into the shot.
- Built clean (2020: 0/0; R25: 0 errors, 274 = unchanged baseline, 0 from GameMode files), deployed
  (AppData payload 20260728185727425 + ProgramData, both 1.31.0.0, Revit was closed). Still not
  click-tested live — v1.31.0 will be Ajmal's first hands-on of the new weapon behaviour.

### 2026-07-28 (AJ Game Mode — first-person walkthrough game, suite v1.30.0)
- Ajmal's ask (dictated): a fun tool — run it and a 3D view opens where he can walk inside the model
  "same like game": walk/jump/fly, walls and slabs block him, doors passable by pressing a button near
  them, windows passable by jumping, a ghost mode through everything, all Revit VG/filters still
  working, plus a gun with a laser and visible bullets — and it must stay easy to delete later.
- Built fully self-contained. REMOVAL = delete `Commands/GameMode/`, `Services/GameMode/`,
  `UI/GameMode/`, `Resources/GameMode.png`, and the one "Game" panel block in `RibbonManager.cs`
  (PanelKey.Game entries + BuildGamePanel + AddGameModeTool, v1.13.0) — plus optionally the
  "AJ Game View" perspective view the tool creates once per model.
- Architecture: a REAL Revit perspective view ("AJ Game View") is the game world, so every Revit view
  control (VG, filters, hide/isolate, section box, display style) applies natively — including to
  collision, because ReferenceIntersector only sees what the view shows. A transparent WPF overlay
  sits pixel-exact over the view (device px end-to-end, no DPI math) capturing WASD + FPS mouse-look;
  one ExternalEvent per HUD timer tick runs physics + camera + rays. Shooting identifies the element
  hit (category, family/type, Size, distance mm, Element ID, linked marker) — doubles as a genuine
  inspection tool; the laser is a live mm rangefinder. Esc pauses to a small pill so Revit stays
  usable mid-game (change VG, then click to resume).
- Key API findings promoted to RULES in `ajtools-conventions.md`: SetOrientation is transaction-free
  navigation (zero undo entries); ReferenceIntersector works on perspective views (+ link resolution
  + visibility-respect); UIView.GetWindowRectangle's Rectangle namespace is a 2020 version gap
  (declare with `var`).
- Method note: the risky APIs were verified live on Revit 2020 BEFORE any
  code was written (camera-write persistence with IsModifiable=false proving no wrapper transaction,
  perspective-vs-ortho raycast parity, timings) — the whole design stood on measured facts, not docs.
- Sandbox reality check that shaped the design: the open test model (Project1) has NO walls/floors/
  doors — so Walk mode hovers when no floor exists below instead of falling forever, and collision
  treats "everything visible" as solid rather than assuming architecture categories exist.
- Status: builds clean (2020: 0 errors/0 warnings; R25: 0 errors, 0 new warnings, 274 = known AiShell
  baseline), deployed for Revit 2020 (AppData payload `AJ Tools.20260728182409073` + ProgramData
  subfolder, both DLLs read back 1.30.0.0). NOT yet click-tested live — needs a Revit restart to load
  v1.30.0. Known open risk for the first live run: whether RefreshActiveView repaints smoothly enough
  per frame on a big model (fallback if the picture lags: raise GameTuning.FrameIntervalMs).

### 2026-07-28 (Transfer + Purge — 9 new tools, two new shared engines)
- Ajmal's ask (dictated, garbled): "in the transfer tool andd transfer shedule transfer legents transfer
  drafting view like that also, and in purge tool andd unsued view template unused filters or do you have
  any anothor idea" — extend Transfer (today: View Templates only) to also handle Schedules, Legends,
  Drafting Views; extend Purge with Unused View Templates and Unused Filters; open invitation for more.
- Confirmed via AskUserQuestion before building: (1) the 3 new Transfer tools should match Transfer View
  Templates' override behaviour (delete + copy + re-point), not ship a simpler copy-only v1; (2) "all type
  of thing what you have in your mind" — build every extra idea proposed (Purge Unplaced
  Schedules/Legends/Drafting Views AND Purge Unused/Empty Groups), not just the 2 explicitly asked for.
- Built as two new shared engines rather than duplicating near-identical logic per kind — see the new
  "Shared mode-enum tool families" rules in `ajtools-conventions.md` for the pattern itself; this entry is
  just the story of applying it:
  - **Transfer**: `TransferViewKind` (Schedule/Legend/DraftingView) + `TransferViewsWindow` +
    `TransferViewsCommandRunner` + `TransferElementCollector` (`Models/Transfer`, `Services/Transfer`,
    `UI/Transfer`). Existing `CmdTransferViewTemplates`/`TransferViewTemplatesWindow` deliberately left
    untouched — its override target (`View.ViewTemplateId`) is a different shape to these three
    (Viewport/ScheduleSheetInstance sheet-placement recreation), so folding it into the same engine
    wasn't worth the regression risk to a working, versioned tool for zero behaviour gain.
  - **Purge Unused** (new family, different shape to "unplaced" — not-referenced-anywhere rather than
    not-on-a-sheet): `UnusedElementPurgeMode` (ViewTemplates/Filters/Groups) + `PurgeUnusedElementsWindow`
    + `UnusedElementCollector` + `UnusedElementPurgeService` (`Models/Purge`, `Services/Purge`,
    `UI/Purge`). Filters mode scans `View.GetFilters()` across BOTH regular views and view templates
    (either can carry its own filter list); View Templates mode's "used" check is deliberately only a
    first pass — the real safety net is the same rolled-back-Delete probe `UnplacedViewPurgeService`
    already used, since a template silently set as Revit's own default-for-new-views isn't visible
    through the public API the same way a direct `ViewTemplateId` reference is. *(Corrected 2026-10-12:
    it IS visible — `ViewFamilyType.DefaultTemplateId` — and the probe cannot catch it, because nothing
    else is deleted. See "Shared mode-enum tool families" in ajtools-conventions.md.)*
  - **Purge Unplaced family extended in place**: added Schedules/Legends/DraftingViews to the existing
    `UnplacedViewPurgeMode` enum. Schedules are placed via `ScheduleSheetInstance` (not `Viewport`), so
    they got their own placed-id set (`GetPlacedScheduleIds`) and candidate check
    (`IsUnplacedScheduleCandidate`); Legends/DraftingViews reuse the existing Viewport-based
    `GetPlacedViewIds`/`IsUnplacedViewCandidate` completely unchanged.
    `PurgeUnplacedViewsWindow`/`.xaml.cs` needed **zero** code changes — it was already fully mode-driven
    through the enum's own extension methods, which is exactly the payoff of that shape.
  - Repurposed the dormant `cmbKindFilter`/`lblKindFilter` (present in the Purge window pattern since the
    original Unplaced Views build, but always hidden and never actually wired to filtering) for real use
    in the new Purge Unused Groups tool: Model Groups and Detail Groups are scanned together in one pass,
    and the combo lets Ajmal filter the grid between them.
- **Enum accessibility gotcha, found by the very first build of this session**: `TransferViewKind`
  started life as `internal enum` (matching habit from other internal model types) and failed with
  CS0051 the moment `TransferViewsWindow`'s `public` constructor took it as a parameter — full story and
  the fix are now in `ajtools-conventions.md`'s new "Shared mode-enum tool families" section, since it'll
  bite again on the next mode-enum family if not written down.
- Both Transfer and Purge top-level ribbon methods renamed now that they hold more than their original
  single tool (`AddTransferViewTemplatesTool` → `AddTransferTools`, `AddPurgeFamilyParametersTool` →
  `AddPurgeTools`); `BuildManagePanel`'s call site updated to match. All 9 new leaf tools reuse existing
  icon files (`Transfer View Template.png`, `Remove.png`) and the existing
  `CmdPurgeUnplacedViewsAvailability` class for ribbon availability — same shared-icon/shared-availability
  pragmatism already established elsewhere in this project, not something new.
- Suite bumped to v1.28.0 (9 new tools). Release (2020) and Release R25 (.NET 8) both 0 errors / 0
  warnings from any touched file (R25's pre-existing ~650 CA1416 AiShell-era warnings are unrelated
  noise, documented earlier in this log). Revit 2027 not separately compiled — none of the APIs used
  here appear in this project's known version-gap list, so this was judged low-risk, but it's an honest
  gap Ajmal should know about. **Not yet click-tested in Revit** — Revit wasn't open this session, so
  nothing here has been exercised against a live document yet. Still to
  try live: a two-project Transfer for all 3 new kinds (with and without override, watching the
  sheet-placement restore, especially a Legend placed on more than one sheet), and all 6 new Purge
  previews/deletes, especially Purge Unused Groups' kind filter.

### 2026-07-28 (Reassign Reference Level — added a Selected Elements scope, dictated request)
- Ajmal's ask, dictated and garbled ("howle elements i need one more that selected element... i can
  select the elements and... move to that mvig level"): the tool already reassigns MEP curves/
  free-standing families/spaces from one level to another across the **whole project** (FROM level ->
  TO level) - he wanted a second mode where he selects specific elements himself and just picks a
  single TO level, no FROM needed.
- Design landed on: reuse the house "pre-selection, else prompt" pattern already used elsewhere
  (`CmdRevisionCloudByElements.GetSelectedElements`, `CmdCeilingMagnet`, `CmdForceTagLeaderLShape`) but
  simplified to pre-selection only - Revit's selection is read once in `Execute()` **before** the WPF
  picker window opens (a modal WPF window blocks Revit's UI, so `PickObjects` can't run once it's open).
  A radio toggle in the same `ReassignLevelWindow` switches Whole Project (existing FROM/TO combos) vs
  Selected Elements (FROM/Swap collapse, just a TO combo + an "N of M selected" summary). When nothing
  eligible is selected, the Selected Elements radio is disabled with an explanatory tooltip instead of
  allowing a dead-end Run click - same "validate inline, never after ShowDialog()" house rule, just
  applied to an entire scope option instead of one field.
- Service layer: added `CollectCandidatesFromSelection` (same eligibility rules as the existing
  `CollectCandidates`, keyed by explicit ids) and `ReassignElementToLevel` (reads each element's OWN
  current level as FROM via a new `GetCurrentLevelId` helper, since one selection can span several
  levels at once) - both additive, `CollectCandidates`/`ReassignElement` untouched, so the Whole Project
  path is byte-for-byte unchanged. "Already on the TO level" is tracked as its own outcome, not a
  failure - a mixed selection where some elements are already correct just skips them silently.
- **Two new WPF gotchas found while building the scope toggle** (now in `ajtools-conventions.md` under
  "WPF inside Revit" since they're generic, not specific to this tool):
  1. Wiring a RadioButton's `Checked` event via a XAML attribute on the SAME element that also sets
     `IsChecked="True"` fires the handler synchronously during `InitializeComponent()`'s BAML walk -
     before later-declared `x:Name` siblings exist yet. A handler that touches those siblings (as the
     scope-visibility updater does here) would NullReferenceException on every single window open, not
     just an edge case - caught before it ever shipped by reasoning through WPF's load order rather than
     by hitting the crash live. Fixed by attaching the handler in code-behind after
     `InitializeComponent()` returns, then calling the update method once manually for the initial state.
  2. A bare `Visibility.Visible`/`Visibility.Collapsed` inside a `Window`/`UserControl` class fails to
     compile (CS0176: "member cannot be accessed with an instance reference") - `UIElement` already
     declares an INSTANCE property literally called `Visibility`, which shadows the enum type name
     inside that class scope. This one WAS caught live, by the first Release build. Must fully qualify
     as `System.Windows.Visibility.Visible`/`.Collapsed` inside any class that itself has a `Visibility`
     property (i.e. any `UIElement`).
- Suite bumped to v1.26.0 (new capability on an existing tool, not a fix - same bump-size logic already
  used for the file-level Swap-button addition in v1.3.0/suite-v1.25.4). Release (2020) and Release R25
  both 0 errors / 0 warnings, no new warnings from any touched file. **Not yet click-tested in Revit** -
  Ajmal still needs to try both scopes live (see `debug-log.md` if anything comes back wrong).
- **Found mid-task**: another session was concurrently adding the Create Tags tool to this same live
  `src/` tree (see the entry below) and bumped the suite on to v1.27.0 on top of this one, cleanly -
  both changelog entries and both ribbon-manager edits landed without clobbering each other. Worth
  Ajmal knowing two sessions were touching the same files at once today, even though it resolved clean.

### 2026-07-28 (Stack Tags fix — first-click leader technique didn't actually match Rearrange Tags)
- Ajmal tested v1.29.0 live: element selection worked, but told me to "check the Rearrange Tags logic,
  everything in the first click needs to be like that in Stack Tags — now it's not the same." No error
  message, no screenshot — just "it's not right," which meant re-diffing my own code against
  `IntelligentTagArrangerService.cs` line by line rather than trusting my earlier reasoning that they'd
  match.
- **Real bug found**: Stack Tags' MOVE path (2nd+ clicks) already matched Rearrange Tags exactly (plain
  `ComputeElbow` + `TrySetLeaderElbow`). The CREATE path (1st click per element) didn't — it called
  `SmartTagPlacementEngine.ApplyLeaderBehavior` (Smart MEP Tag's own leader routine) instead, which does
  two things Rearrange Tags deliberately never does: nudges the elbow outside the tag's own text
  bounding box (`AdjustElbowOutsideTextBoundsRight`), and falls back to toggling the leader end
  condition if the plain elbow-set fails (`TrySetLeaderElbowPreserveCondition`) — Rearrange Tags'
  `TryApplyLShapeLeader` has an explicit comment against exactly that: "Keep L1 exactly as-is: do not
  toggle leader end condition as fallback." Borrowing the "obviously related" sibling tool's leader code
  was the wrong call here — Stack Tags' whole point is to feel identical to Rearrange Tags, not to
  Smart MEP Tag, even though both are "attach a leader to a tag" problems on the surface.
- **Lesson**: when a request says "make X work exactly like Y," don't assume a nearby, already-reused
  helper is close enough just because it solves the same general problem — trace the SPECIFIC target
  tool's logic all the way through and match it deliberately, especially the parts that read like
  refinements (a comment saying "don't do X" is a signal that X was tried and rejected once already).
- Fix: new local `ApplyFreshLeader` in `StackTagsService.cs` — same plain elbow technique as Rearrange
  Tags, keeping only the L1 rollback-probe (a Revit API read quirk affecting any freshly-created tag,
  not a style choice, so worth keeping regardless of which tool's "style" is being matched).
  `CreateTagsService.cs` (the other sibling) was NOT changed — it's deliberately modeled on Smart MEP
  Tag's leader technique, not Rearrange Tags', and Ajmal never asked for that one to change.
- Also moved Stack Tags off its standalone ribbon button into the Create Tags pulldown as a third child
  (Create Tags / Stack Tags / Create Tags Settings), per Ajmal's request.
- Shipped as v1.29.1 (fix + ribbon move, not a new tool — patch bump). Builds re-verified clean on both
  configs, zero warnings from the touched files.

### 2026-07-28 (Stack Tags — Create Tags' sibling, one click stacks the whole batch)
- Same session as Create Tags below. Ajmal's follow-up (again dictated/broken English) initially read
  as "another Create Tags variant" but turned out to be a genuinely different click model: "select the
  items, all tags come on the clicking point, same like Rearrange Tags — instead of selecting tag we
  are selecting the elements." Re-read `IntelligentTagArrangerService.TryArrangeAtPoint` closely before
  building anything, since the request only makes sense once you know exactly what Rearrange Tags'
  single click actually does.
- **Correction to my own earlier mental model**: I'd assumed Rearrange Tags' click loop was "one click
  moves one tag." It isn't — every single click re-arranges the ENTIRE selected batch into a fresh
  vertical stack starting at that point (nearest-tag-to-target assigned first, then the rest step
  above/below by the configured spacing). Clicking again doesn't add to the stack, it RELOCATES the
  whole thing from scratch. Worth remembering next time a request references "like Rearrange Tags" —
  the signature behaviour is whole-batch-per-click, not per-click-per-item.
- Stack Tags reuses this exact mechanic but for elements that don't have tags yet: first click CREATES
  a tag per eligible element and arranges them; every later click MOVES the tags already created in
  this run to the new stack position (does not create duplicates). Implemented as one unified
  create-or-move step per candidate, keyed off whether that candidate already has a tag from earlier in
  the same run.
- **Bug avoided, not just fixed**: a tag created mid-click that then gets rolled back (because a LATER
  candidate in that same click failed) would leave a stale ElementId sitting in the "already created"
  tracking dictionary if that dictionary were updated live. Fixed by collecting each click's new tag IDs
  into a local, throwaway dictionary and only merging them into the persistent one after that click's
  Transaction actually commits — the persistent map can never point at an element Revit just deleted.
- Refactored `CreateTagsService.cs`'s eligibility/matching logic (SkipTally, BuildEligibleCandidates,
  IsVerticalMepCurve, FindNearestCandidate, DistanceInView) out into a new shared
  `CreateTagsEligibilityFilter.cs` so Stack Tags calls the identical rules instead of a second copy —
  same reasoning as reusing Smart MEP Tag's methods for Create Tags itself. No behaviour change to
  Create Tags from this move (rebuilt clean immediately after the extraction, before writing Stack Tags,
  to isolate the refactor from the new feature).
- No new Settings window: category enable/disable and minimum length come from Create Tags Settings;
  stack spacing comes from the EXISTING Arrange Tags Settings (`TagArrangeSettings.GetTagSpacingMm()`,
  already used by Rearrange Tags) — reused as-is, not duplicated.
- **Concurrent-edit note**: found the suite version had moved to v1.28.0 (9 new Transfer/Purge tools)
  from a different session while this one was in progress — didn't touch that work, just re-read the
  current AssemblyInfo.cs before bumping again rather than assuming my own last-known version was still
  current. Shipped as v1.29.0.
- Builds verified same as Create Tags: Release (2020) and Release R25 both 0 errors; R25's warning count
  (274) is the same pre-existing AiShell/AvalonEdit baseline, confirmed none of it traces to
  CreateTags*/StackTags* files.

### 2026-07-28 (Create Tags — new manual pick-and-tag tool, built from a dictated request)
- Ajmal described it in plain/broken language: a tool like Rearrange Tags' select-then-pick-point UX,
  but for CREATING tags (select an MEP element, click a location, tag appears there) instead of moving
  existing ones — plus the same skip rules he remembered from Smart MEP Tag (already tagged, too short,
  vertical). Confirmed via AskUserQuestion before building: multi-select + click-loop (not one-at-a-time),
  vertical-skip widened to duct+pipe+cable tray (Smart MEP Tag itself only checks ducts), and — the one
  that changed scope — the minimum-length threshold and category list must be a proper Settings window
  (like Smart MEP Tag Settings), not a value locked in during the conversation.
- **Found while checking**: Smart MEP Tag Settings' own window (category grid + priority) does NOT
  actually expose a minimum-length field today — `MinDuctWidth`/`MinPipeDiameter`/`MinCurveLength` are
  `private static readonly`/`const` fields hardcoded inside `SmartMepTagService.cs`, not read from
  `SmartTagSettingsState`. Create Tags Settings is the first tool in the suite to make that number
  user-editable — worth remembering if Ajmal later asks "why can't I change Smart MEP Tag's own
  1000mm cutoff the same way."
- Reuse over rewrite: widened exactly 4 methods from `private static` to `internal static` (zero logic
  changes) so the new tool could call proven code instead of duplicating it —
  `SmartMepTagService.CollectAlreadyTaggedElementIds/GetCurveLength/GetElementMidpoint` and
  `SmartTagPlacementEngine.ApplyLeaderBehavior`. `RunPreFlightChecks` and `SelectTagFamilies` were
  already `public` — reused as-is, no visibility change needed. Verified both builds (Release/2020 and
  Release R25) stayed at 0 warnings/0 errors after the widening, confirming Smart MEP Tag's own
  behaviour is untouched.
- **Standing note, not just this build**: Create Tags' vertical-run skip is deliberately BROADER than
  Smart MEP Tag's own (duct+pipe+cable tray vs. duct-only) — this is an intentional divergence Ajmal
  confirmed, not an inconsistency to "fix" by making them match. New rule added to ajtools-conventions.md's
  Tag & leader logic section so a future session doesn't reconcile them by accident.
- Shipped as suite v1.27.0. New files: `CmdCreateTags.cs`, `CmdCreateTagsSettings.cs`,
  `Services/CreateTags/CreateTagsService.cs`, `Services/CreateTags/CreateTagsSettingsTracker.cs`,
  `Models/CreateTags/CreateTagsSettingsState.cs`, `UI/CreateTags/CreateTagsSettingsWindow.xaml(.cs)`.
  Wired into `AnnotationRibbonManager.cs`'s Tags panel as a standalone pulldown (the existing 3-item
  stack — Smart MEP Tags/Rearrange Tags/L-Shape Leader — was already full) using the placeholder
  `cursor.png` icon (already used for pick-driven tools elsewhere in the suite, first use on AJ
  Annotation) — say if a dedicated icon is wanted instead. Not yet live-tested in Revit.

### 2026-07-26 (v1.25.1 maintenance pass — Ajmal delegated "do whatever for good")
- Asked what to do next, Ajmal delegated the choice entirely ("do whatever for good... you are my coding
  partner"). Chosen default: close out the known open items that don't need Revit, rather than another
  audit pass (whole repo was audited 3x on 17-18 Jul) or guessing at a new tool.
- Shipped as suite v1.25.1: (1) ProgramData deploy manifest path fix in `Directory.Build.targets` —
  root-caused deeper than the 2026-07-21 entry via a standalone msbuild path test, see debug-log.md;
  (2) removed the orphaned `CmdQuickParallelDimension` class (file kept — it also holds the two LIVE
  CenterLine/FaceEdge commands, a fresh reference sweep confirmed only the plain one was dead);
  (3) Highlight Selection v1.2.0 — both directions of the insulation story (wrap-selected → host follows
  via `HostElementId`; hosts now pull duct lining via `GetLiningIds` alongside insulation). API verified
  on real RevitAPI.dll 2020/2024 (full reflection) + 2027 (byte-scan of metadata names — netfx PowerShell
  can't reflect the .NET 10 DLL; technique now in ajtools-conventions.md).
- Builds: Ajmal asked "why not 2020 to 2027", so the FULL 8-config sweep was run (all compile-only,
  csproj direct — R-configs must be built against the csproj, not the .sln; rule added to conventions):
  2020 0 err/0 warn; R21 0/38 (all CS0618 deprecated-enum notices inside RevitCompat's own old-version
  code paths — by design, the helpers exist to call the old API on old versions); R22 0/8 (same kind);
  R23 0/0; R24 0/0; R25 0/756 and R26 0/756 (pre-existing CA1416-family platform noise); R27 via
  user-local dotnet 0 errors/1516 warning lines (≈ the same 756 printed twice in dotnet's summary
  block). Zero warnings from the changed files on every one of the 8. Revit was open the whole
  session → compile-only at first; later the same day Ajmal closed Revit and approved deploy — all 8
  years deployed and read-back verified (see debug-log 2026-07-26 for the verification detail and the
  dual-manifest watch-item). Still pending: Ajmal's live click-test of Highlight Selection with
  insulation/lining, and his decision on deleting the stale loose files at each ProgramData year root.

### 2026-08-20 (release v1.56.0 — first public download since 1.52.0)

- Four source versions had piled up unreleased (1.53.0 Quick Menu drag-to-arrange, 1.54.0 the
  greyed-out-slot fix, 1.55.0 two Revit sessions, 1.56.0 project targeting). Shipped as one
  **v1.56.0** download, so the installer CHANGELOG entry covers **everything since 1.52.0**, not just
  the last version — that is what the user actually receives when they upgrade.
- Followed `RELEASE_PROCESS.md` end to end. The step that needs a human and has no guard rail is
  **writing the installer `CHANGELOG.md` by hand**: `prepare-release.ps1` updates `README.md` and
  `INSTALL.md` only, yet step 7 stages CHANGELOG anyway, so a forgotten entry publishes silently with
  empty notes. Verified after publishing instead of assuming: **3,815 characters of notes on the
  release page.**
- **Revit 2027 still cannot be built by Visual Studio's MSBuild** — it resolves only the machine-wide
  .NET 9 SDK and R27 targets `net10.0-windows`. `package.ps1` already handles this via
  `Resolve-DotNetPath`; a manual R27 build must use `dotnet build`, not `msbuild`. Hit and re-confirmed
  this session.
- Verified rather than assumed, in this order: all **8 payload DLLs report 1.56.0**; the published
  SHA256 matches the zip; the workflow run succeeded; and finally the asset was **downloaded from the
  release page and `sha256sum -c` run against it** — the only check that proves what a user gets.
- The 54 MB zip trips GitHub's 50 MB advisory warning on push. Expected, not an error, same as previous
  releases; no LFS migration needed.

### 2026-08-20 (history scrub: every outside name removed from the repo AND its git history)

- Ajmal's instruction: *"do not mention any thing that we took from this web site or repo... the words
  also remove... remove his name and do not use like that."* Working files were cleaned first (script
  headers, one sourcing-history file deleted in full, nine passages in this log, and notes in two
  tool/source files).
- **Then the git history, on his explicit go-ahead.** The trigger for doing it at all: this repo is
  **PUBLIC**, not private — `CLAUDE.md` had said "private" since 2026-08-05 and that was wrong, so the
  old commits were readable by anyone. `git filter-repo` purged `history.md` from every tree and
  replaced every outside name in **file contents and commit messages**, then a force-push.
  **266 commits and all 57 tags survived**; nothing squashed. `git bundle` backups taken and verified
  first, in `D:\Ajmal\_repo-backups-2026-08-20\`.
- **The trap, recorded because it nearly bit:** one of the removed names is a **substring of the
  ordinary English word "canonical"**, which appears in this repo too — a plain search-and-replace
  would have silently mangled it. The rules were word-boundary regexes, unit-tested against
  "canonical" before being run. Do the same if this is ever repeated: write the test first.
- Zero forks on this repo or `AJ-Tools-Installer`, and the installer repo never
  contained any of it. The stale `AJ Tools\` clone was scrubbed too (182 commits) — it is a full copy
  on disk, even though it stays gitignored and must never be pushed from.
- Still outstanding: GitHub keeps unreferenced commits reachable by direct SHA until it
  garbage-collects. Ask GitHub Support to purge if that matters.

### 2026-07-21 (Correction: split buttons should have a FIXED default, not last-used tracking)
- Right after building the Run Pinned/Saved Scripts split button (entry below), launched Revit
  (computer-use, screen access granted by Ajmal) to visually verify all four ribbon changes from today.
  Confirmed live: Filter Pro/Colorize/Highlight Selection stacked correctly, Section Mark Visibility on
  the AJ Annotation Tags panel, and the Opening split button's dropdown correctly listed Create Openings
  (current/highlighted) + Opening Settings. While clicking through the Opening dropdown to confirm it
  reverts back to Create Openings afterward, the button's default face changed to "Opening Settings" -
  the exact "tracks last used" behavior that had just been built. Ajmal, watching, immediately said he
  wants Create Openings/Run Pinned to be the **permanent** default, with Opening Settings/Saved Scripts
  reachable **only** via the dropdown - never swapping the main face.
- **Fix**: both split buttons now set `IsSynchronizedWithCurrentItem = false` instead of `true`. Per
  `RevitAPIUI.xml`: "If it is false the first listed PushButton... executes this PushButton when
  clicked... the items in drop down list can only be executed by opening the drop down list." This is
  exactly the wanted behavior, and simpler than what was built - no static button/SplitButton captures
  needed, no `CurrentButton` assignment in each command's `Execute()` (setting `CurrentButton` while
  `IsSynchronizedWithCurrentItem` is false actually throws `InvalidOperationException`, so the sync code
  from earlier today had to come out, not just get bypassed). Removed: `App.MepOpeningSplitButton` /
  `CreateOpeningsButton` / `OpeningSettingsButton` / `RunPinnedSplitButton` / `RunPinnedButton` /
  `SavedScriptsButton` statics, and the `SetAsCurrent*Button()` methods in all four affected commands
  (`CmdCreateMepOpenings`, `CmdMepOpeningSettings`, `RunPinnedScriptCommand`, `ShowSavedScriptsCommand`).
- Updated the reusable-pattern rule in `ajtools-conventions.md` to describe the fixed-default version
  and explicitly warn against re-attempting the last-used-tracking version for a similarly-worded future
  request - it sounds plausible from wording alone ("last used one comes first") but was tried, shown to
  Ajmal, and rejected in favor of the simpler always-fixed version.
- Verified: clean build on Release (2020, deployed to AppData) and Release R25 (compile-only), 0
  errors/0 warnings in every touched file. Redeployed to the live AppData Addins folder - Revit was
  closed at the time (Ajmal closed it after seeing the wrong behavior), so no file lock. Not yet
  re-verified live after this specific fix - Ajmal needs to reopen Revit and confirm Create Openings and
  Run Pinned now stay put no matter what gets run from their dropdowns.

### 2026-07-21 (Run Pinned / Saved Scripts combined into a split button, same pattern as Opening)
- Immediately after the Opening split button change below, Ajmal asked for the identical treatment on
  the AI Assistant panel: "same like that run pinned... always there... saved script move to the
  pulldown list." Applied the exact same recipe (see the new rule in `ajtools-conventions.md` § Ribbon &
  shared helpers): `AddRunPinnedTool()` replaces the old two separate `AddRunPinnedScriptTool()` /
  `AddShowSavedScriptsTool()` top-level buttons with one `CreateSplitToolSpec` call - Run Pinned added
  first (default face), Saved Scripts in the dropdown, new `App.RunPinnedSplitButton` /
  `RunPinnedButton` / `SavedScriptsButton` statics, `CurrentButton` set at the top of each command's
  `Execute()`. No changes needed to `CreateSplitToolSpec` itself - the `configureSplitButton` overload
  added for Opening was already generic.
- Confirms the pattern documented in `ajtools-conventions.md` is genuinely reusable, not one-off.
- Found the same "header Version field lagging behind the changelog's actual top entry" drift again,
  this time in `RunPinnedScriptCommand.cs` (header said 1.0.0, changelog already had v1.1.0 at the top).
  Corrected while bumping to 1.2.0. Worth watching for elsewhere - this is now the second time in one
  day this exact drift showed up (see the AssemblyInfo.cs one in the entry below).
- Verified: clean build on Release (2020) and Release R25, 0 errors/0 warnings in every touched file.
  Not yet tested live in Revit.

### 2026-07-21 (View panel decluttered; Opening split button now tracks last-used)
- Ajmal's request arrived heavily garbled by dictation ("filter pro and colorize and hught seklection
  now pushbutton... selction visiblity tool move aj anotation tag panel... opening tool ti will be
  change olways last too used one will come first..."). Decoded by reading the actual ribbon code first
  rather than guessing from the words alone - "selction visiblity" turned out to be "Section Mark
  Visibility" (a real tool), and "opening tool" turned out to be the existing MEP Openings split button,
  once its two children (Create Openings / Opening Settings) were found in `AddMepOpeningsTool()`.
- **Change 1**: Filter Pro, Colorize, and Highlight Selection - previously 3 separate large top-level
  buttons in the View panel - now a single small stacked group (`AddStackedTools`), matching the
  existing View Crop/Unhide/Toggle Links stack. Exactly 3 items, which is what `AddStackedTools` (a thin
  wrapper over `RibbonPanel.AddStackedItems`) supports.
- **Change 2**: Section Mark Visibility moved off the AJ Tools tab entirely, onto the AJ Annotation
  tab's Tags panel (`AnnotationRibbonManager.cs`). Same command class, same `AvailabilityClassName`,
  same icon - only the ribbon registration moved. `AnnotationRibbonManager.cs` builds buttons with a
  lower-level direct `PushButtonData` + `RibbonPanelHelper.ApplyIcons` + `panel.AddItem()` pattern (no
  `TopLevelToolSpec` abstraction like `RibbonManager.cs` has) - matched that style rather than importing
  the other file's helper classes.
- **Change 3 - new reusable pattern: "split button remembers which child was used last".** The Opening
  split button previously always opened with "Opening Settings" as the top face (its first-added child)
  no matter which one was actually run - `SplitButton.IsSynchronizedWithCurrentItem` defaults to `true`
  per the real `RevitAPIUI.xml` doc comments (verified via reflection + the XML doc file next to
  `RevitAPIUI.dll`, not from memory), but nothing was ever setting `CurrentButton` after a child ran, so
  it never actually changed. Fix: `CreateSplitToolSpec` gained an optional `Action<SplitButton>
  configureSplitButton` parameter (new overload, existing 3 callers unaffected); `AddMepOpeningsTool()`
  uses it to capture the SplitButton + explicitly set `IsSynchronizedWithCurrentItem = true`, and
  captures each child `PushButton` via the existing per-child `afterCreate` hook - all three go into new
  `App.MepOpeningSplitButton` / `CreateOpeningsButton` / `OpeningSettingsButton` statics (cleared on
  `OnShutdown`). `CmdCreateMepOpenings`
  and `CmdMepOpeningSettings` each set `SplitButton.CurrentButton = <themselves>` as the very first line
  of `Execute()` - runs before any validation/cancellation path, so the ribbon face updates even on a
  cancelled pick. "Create Openings" was also reordered to be added first, so a fresh Revit session opens
  with it as the default face (a SplitButton's initial `CurrentButton` is the first item in its dropdown
  list) rather than "Opening Settings". **Reusable for any future split button Ajmal wants this on** -
  the `configureSplitButton` hook + a pair of static button refs + a one-line `CurrentButton = self` at
  the top of each child command's `Execute()` is the whole pattern.
- **Found and fixed in passing**: `Properties/AssemblyInfo.cs`'s `[assembly: AssemblyVersion]` /
  `[assembly: AssemblyFileVersion]` attributes were already sitting at `1.24.0.0` with no matching
  changelog entry (changelog topped out at v1.23.1) - the same category of drift a 2026-07-19 session
  (v1.20.0) already had to fix once. Re-synced to `1.23.2.0` to match this session's actual patch bump;
  flagged to Ajmal rather than assumed to be deliberate, since nothing in this file documents a
  "reserve a version ahead of time" convention.
- Verified: clean build on Release (2020) and Release R25, 0 errors/0 warnings in every file touched by
  this change (R25 has pre-existing CA1416/SYSLIB0023 platform-compat warnings in unrelated files -
  `CmdIntelligentTagArrangerSettings.cs`, `GraphicsOverrideWindow.xaml.cs`,
  `TextMarkerService.cs`, `SavedScriptsWindow.xaml.cs` - none of them touched here). Not yet tested live
  in Revit - Ajmal needs to open Revit and confirm the ribbon looks and behaves as intended.

### 2026-07-21 (Saved Scripts moved out of the C# pane into its own standalone ribbon tool)
- Ajmal asked to move "Saved Scripts History" (previously a collapsible expander inside the C# pane
  showing scripts saved to the Scripts Folder, with Pin/Run buttons) into its own tool, "same like run
  pinned" - i.e. a standalone ribbon button reachable whether or not the C# pane is open, matching
  `RunPinnedScriptCommand`'s existing design (IExternalCommand, no dependency on the WPF ViewModel).
- Built `ShowSavedScriptsCommand` (IExternalCommand) + `SavedScriptsWindow` (plain code-behind, no
  ViewModel) - opens a window that scans `AiShellConfig.ScriptsFolderPath` fresh from disk every time
  (never a cached in-memory list), shows folder path + Browse/Refresh, and per-row 📌 Pin / ▶ Run.
  New ribbon button "Saved\nScripts" added next to "Run\nPinned" in the AI Assistant panel.
- **Decision: extracted `RunPinnedScriptCommand`'s run logic into a shared `public static
  RunScriptFile(...)` instead of duplicating it.** `RunPinnedScriptCommand`'s own file comment already
  explained why the header-stripping is kept as an independent small copy (must not depend on the
  ViewModel) - but the actual "validate safety -> confirm risky -> run in TransactionGroup -> report"
  logic is bigger and riskier to fork into two copies that could silently drift. Both
  `RunPinnedScriptCommand.Execute()` and `SavedScriptsWindow`'s "▶ Run" button now call the same method.
- Removed the "Saved Scripts History" expander from `AiShellView.xaml` entirely (not just collapsed),
  and removed the now-dead `SavedHistory`/`PinScriptCommand`/`RunFromHistoryCommand`/
  `PinnedScriptDisplayText`/`RefreshScriptsList` (+ its header-parsing helpers) from
  `AiShellViewModel.cs` - `ScriptsFolderPath` (used by Settings' Browse button) is the only piece of
  this feature still owned by the pane's ViewModel.
- Verified: clean build on Release (2020) and Release R25, 0 errors/0 warnings on both. Not yet tested
  live in Revit.

### 2026-07-21 (API key leak check on the AI pane's provider settings - found and fixed one real gap)
- Ajmal asked, right after the Anthropic addition below, whether adding a new provider created any
  chance of the API key leaking. Investigated every code path that touches a key rather than just
  reassuring him:
  - **At rest**: confirmed by reading the real `%AppData%\AJTools\AiShellConfig.json` on his machine -
    genuinely DPAPI-ciphertext (`ProtectedData.Protect`, `CurrentUser` scope), not plaintext. Tied to
    his Windows login; unreadable by another Windows account on the same PC or if the file is copied
    to another machine (`Unprotect`'s catch block fails cleanly rather than crashing or leaking).
  - **In transit**: HTTPS only, key sent in an HTTP header (`x-api-key` / `Authorization`), never in
    the request body the model itself reads - so Gemini/OpenAI/Claude can never see or echo the key
    back into a response, chat history, or a saved script.
  - **Logging**: `AiShellActivityLogger` only logs provider name + truncated prompt/error text
    (file's own header already says "Never logs API keys"); `AiShellConfig`'s error logger only logs
    `ex.Message` on a decrypt failure. No code path writes the raw key to any log file.
  - **Found one real gap**: `SettingsWindow.xaml`'s three API-key fields were plain `TextBox` controls
    - the key was shown in cleartext on screen the whole time you're in Settings (a real risk during
    screen-share or over-the-shoulder viewing), even though it was already safe everywhere else.
- **Fix applied**: swapped to `PasswordBox` (added a new `SoftPasswordBoxStyle` in `SoftUiStyles.xaml`
  mirroring `SoftTextBoxStyle`'s look, since `PasswordBox` needs its own `TargetType`-matched style/
  template) with a per-field "👁 show/hide" button that swaps in a read-only reveal `TextBox` on
  demand, so the key stays masked by default but can still be visually double-checked. `PasswordBox
  .Password` isn't a bindable dependency property, so `SettingsWindow.xaml.cs` syncs it manually via
  `Loaded` + `PasswordChanged` handlers instead of a normal `{Binding}`.
- Verified: clean build, 0 errors/0 warnings, on both Release (2020) and Release R25.

### 2026-07-21 (Added Anthropic/Claude as a third AI provider option)
- Ajmal asked in passing ("in this c# can we add this claude also api or login like that") whether Claude
  could be added to the AI pane alongside Gemini and OpenAI. Built `AnthropicApiService.cs` following the
  exact `IAiProviderService` pattern the other two already use — raw `HttpClient` POST to
  `api.anthropic.com/v1/messages` with `x-api-key` + `anthropic-version: 2023-06-01` headers, no official
  Anthropic SDK dependency added.
- **Decision: raw HttpClient over the official Anthropic C# SDK, even though one exists.** The Anthropic
  API skill's own guidance says default to the official SDK when one exists for the language - but this
  project multi-targets net472 through net10.0-windows (8 Revit-year configs), and adding a new NuGet
  package that must support all of those is a real risk the existing Gemini/OpenAI services already
  avoided by using plain `HttpClient`. Matching that established in-repo pattern won over the generic
  skill default here - flagged to Ajmal rather than assumed.
- Model dropdown offers `claude-opus-4-8` (default), `claude-sonnet-5`, `claude-haiku-4-5`, `claude-fable-5`
  - same shape as the existing OpenAI model dropdown (cheap/fast to flagship). API key stored encrypted the
  same way as the other two providers (Windows DPAPI, `AiShellConfig`).
- `SelectedProvider` is a plain string switch (`"Gemini"` / `"OpenAI"` / `"Anthropic"`) on
  `AiShellViewModel.GetActiveService()` - no factory/enum, matching how the existing two-provider switch
  was already written.
- Verified: clean build on Release (2020, net472) and Release R25 (net8.0-windows), 0 errors/0 warnings on
  both. Not yet tested live in Revit with a real Anthropic API key.

### 2026-07-20 (Ceiling Magnet: kept BOTH old and new workflows, mode-choice dialog)
- Same day, right after the select-elements-first rework below: Ajmal tried it live and reported the
  original one-at-a-time flow doesn't come back for the next room anymore ("select element is not
  comming") - not a bug, that was the deliberate point of the rework. But then: "no sorry before was
  good and this also I need so keep this also and previous also in the same tool... keep this both."
  He wants BOTH workflows available in the same command, not one replacing the other.
- Added `AskElementPickMode()` to `CmdCeilingMagnet.cs` (v1.5.0): a `TaskDialog` with two
  `CommandLink`s shown at the very start of `Execute()` - "Pick one at a time" (restores the exact
  original v1.3.0 logic: one ceiling, then elements picked one-by-one, all to that same grid) vs
  "Window-select multiple at once" (the v1.4.0 rework: batch-select once, then repeat ceiling+point
  rounds). `Execute()` just dispatches to `RunOneAtATimeMode` or `RunWindowMultiSelectMode` based on the
  choice; Cancel/close on the dialog cancels the whole command.
- `RunOneAtATimeMode`/`SnapElementsOneAtATime` is a byte-for-byte restoration of the pre-rework logic
  (including its own `ShowSummary(CeilingGridDefinition, SnapSummary)` overload with the single-grid
  mm/angle detail line) - nothing about that path changed, it was fully replaced then fully restored
  the same day. `ShowSummary` is now overloaded by parameter type (`int` round count vs
  `CeilingGridDefinition`) since the two modes report meaningfully different things.
- **Lesson for future rework requests**: when Ajmal says "before was good AND this also I need," the
  right move is a mode choice inside the same tool, not picking one workflow over the other - don't
  assume a UX request to change how a tool starts is a request to *replace* the old way permanently.
- Suite bumped 1.21.0 -> 1.22.0. Verified: clean `msbuild` at `Release` (2020, deployed - Revit wasn't
  running, checked via `tasklist` first) and `Release R25` (net8.0-windows, csproj-direct). Not yet
  live-tested by Ajmal in Revit past his first report; that's what prompted this follow-up.

### 2026-07-20 (Ceiling Magnet: select-elements-first, repeat ceiling+point rounds)
- Ajmal (dictated, broken English) wanted the Elements to Ceiling Grid tool's pick order flipped: select
  all the elements to snap ONCE via a window/click multi-select up front, then repeat a "pick ceiling,
  pick anchor point, it arranges" round for as many rooms as he likes, Esc to finish the whole thing —
  instead of the old order (pick one ceiling first, then elements one-by-one after).
- `CmdCeilingMagnet.cs` (v1.4.0): `PickElementBatch` now reuses the current selection if Ajmal already
  had one, otherwise prompts one `uidoc.Selection.PickObjects` (native window/click multi-select, Finish
  or Enter to confirm, Esc cancels the whole command at this one stage). `RunCeilingRounds` then loops a
  ceiling pick + (real-grid-or-manual) anchor point, snaps, and repeats — Esc on either pick inside a
  round ends the loop and keeps whatever rounds already ran; a wrong (non-ceiling) pick just shows an
  error and retries the same round, it does not end the session. One aggregate summary (ceilings
  processed + totals) replaces the old single-ceiling grid-detail popup.
- **Confirmed with Ajmal**: when round 2, 3, etc. pick a different ceiling, only the elements from the
  original batch that actually sit over THAT ceiling get snapped — not a re-snap of everyone in the
  batch. This is what makes one big multi-room selection usable room-by-room in a single command run.
- New `CeilingMagnetService.FilterElementsOverCeiling` (v1.1.0) does the per-round filtering — per the
  Modeler mindset house rule, this reads the ceiling's *real solid geometry* (its largest horizontal
  top/bottom `PlanarFace`, found via `get_Geometry`) and tests each element's plan point against that
  face with `Face.Project(...) != null` (returns null outside the trimmed face — an exact containment
  test, correct even for L-shaped/non-rectangular ceilings), not a rough bounding-box guess. Falls back
  to the ceiling's bounding box only if no usable solid/face is found. Same local/host transform pattern
  already used elsewhere in this file (linked-ceiling geometry stays in its own local coordinates; the
  element's host-space point is brought into that frame via `TransformToHost.Inverse` before testing).
- Suite bumped 1.20.2 -> 1.21.0 (new capability/workflow within an existing tool, not a pure fix —
  matches the precedent set by v1.18.0/v1.19.0's "two improvements" minor bumps).
- Verified: clean `msbuild` at `Release` (2020 baseline, net472) and `Release R25` (net8.0-windows,
  built directly against `src/AJ Tools.csproj` since the .sln itself only knows the plain `Release`
  config — the multi-version configs are csproj-level, not in `AJ Tools.sln`). Zero warnings from either
  touched file at both configs (grepped explicitly); R25 does show ~648 pre-existing warnings but they're
  all in the unrelated AiShell subsystem (CA1416/SYSLIB0023 analyzer noise specific to the .NET 8
  target), not introduced by this change. This tool's interactive PickObjects/PickPoint flow needs
  real mouse input in Revit — not yet live-tested
  by launching Revit; report this honestly until Ajmal tries it.

### 2026-07-19 (About button icon swap)
- Ajmal dropped new artwork at `Y:\Ajmal Ps\icon\about.png` (a purple question-mark badge, 32x32 ARGB)
  and asked to use it for the About button. Confirmed same 32x32 size as the old icon (a low-res 8bpp
  indexed question-mark) before swapping.
- Copied straight over `src/Resources/About.png` (same filename) - no code change needed anywhere,
  since both the ribbon button (`RibbonManager.cs` `AddAboutTool()`) and the About window's own taskbar
  icon (`AboutWindow.xaml.cs`, `IconLoader.LoadLarge("About.png")`, added v1.19.1) already load that one
  file by name. Confirmed the deployed Resources copy is byte-identical to Ajmal's source file (md5) -
  not just "file exists," actually the same bytes.
- Suite bumped 1.20.1 -> 1.20.2 (patch: asset swap, no behavior change). Built and deployed while Revit
  was closed (confirmed via `tasklist` before touching the locked DLL).

### 2026-07-19 (New tool: Highlight Selection)
- Ajmal asked for a permanent ribbon tool that turns everything in the active view gray and keeps the
  selection red.
- New command `CmdHighlightSelection` (`src/Commands/GraphicsTools/CmdHighlightSelection.cs`, View
  panel, button "Highlight Selection"): colors the current selection red and every other element in the
  active view gray, in one undo step. Deliberately built on the *existing* Graphics command
  infrastructure instead of a one-off — `GraphicsCommandService.TryCreateContext`/
  `ExecuteSummaryTransaction`, `GraphicsSelectionService.GetPreselectedOrPromptElementIds`,
  `GraphicsElementService.ApplyOverrides`, `GraphicsOverrideBuilder.Build` — so it behaves consistently
  with Apply/Match/Reset Graphics (same preselect-or-pick flow, same silent-on-success convention, same
  solid-fill-pattern lookup already used in `FilterApplier.cs`/`ColorizeWindow.xaml.cs`).
- No new "reset" button added — the existing Reset Graphics pulldown (Graphics panel) already clears
  both category- and element-level overrides, so undoing this tool's effect doesn't need new capability.
- New guard specific to this tool: if the current Revit selection includes elements not present in the
  active view (e.g. tags that belong to a different open view — confirmed live the same day), only the
  in-view subset gets colored red; if the *entire* selection turns out
  to be outside the active view, that's reported as an error rather than silently doing nothing.
- New icon `Resources/Highlight Selection.png` — generated programmatically (PowerShell +
  System.Drawing: 3 flat gray rounded squares + 1 red one), matching the existing flat-icon style since
  no suitable icon already existed in the set.
- Found and fixed a version-attribute drift while bumping for this tool: `AssemblyInfo.cs`'s
  `[assembly: AssemblyVersion]`/`[AssemblyFileVersion]` attributes were still `1.19.0.0` even though the
  changelog comment block already documented `v1.19.1` as shipped earlier the same day — the previous
  session bumped the changelog text but missed the actual attributes. Corrected both to match, then
  bumped to 1.20.0 (minor: new tool) on top of the corrected baseline.
- Build-verified clean (zero warnings) on the Release (2020) baseline, and compiles clean on Release R25
  too (that config does show ~648 pre-existing CA1416/SYSLIB0023 platform-compat warnings from unrelated
  older WinForms/AiShell files under the net8.0-windows target — none from this new file; not touched,
  out of scope for this task). Not yet loaded/clicked in a live Revit session — Ajmal still needs to
  confirm the button appears and behaves as expected in Revit itself.

### 2026-07-19 (Second improvement round: auto-fix diff highlight, crash recovery)
- Asked again for improvement ideas; offered extending the diff-highlight to the auto-fix loop plus
  crash/close recovery for unsaved Prompt+code. Ajmal: "YES" (both again).
- `CodeGenerated` now also fires from `RunCodeAsync`'s auto-fix path (captured `codeBeforeFix` right
  before `ErrorCorrectionService.RequestFixAsync`, raised right after `CodeEditorContent = fixedCode`)
  - same event, same View-side handler, no new plumbing needed since the event was already designed
    generically ("whenever the AI rewrites the code"), not Generate-specific by name.
- New recovery snapshot: `PromptInput`/`CodeEditorContent` setters now call `ScheduleRecoverySave()`
  (2s debounce `DispatcherTimer`, same pattern as `AiShellView`'s syntax-check timer) which writes both
  to `%AppData%/AJTools/ajai-recovery.json`. Restored in the constructor via
  `LoadRecoverySnapshotIfAny()` - deliberately sets the BACKING FIELDS directly (`_promptInput`/
  `_codeEditorContent`), not the public setters, so loading doesn't immediately re-trigger a save of
  the same data it just read. Confirmed this is still safe for the View to pick up correctly even
  without a `PropertyChanged` firing during load: `AiShellView_DataContextChanged` reads
  `newVm.CodeEditorContent` directly when `DataContext` is first assigned (not relying on a change
  notification), and the `PromptInput` XAML binding does its own initial pull the same way - so a
  silent backing-field set during construction, before the View even exists yet, is fine.
- Deliberately never deletes the recovery file - it's a rolling "current state" snapshot, not a
  one-time crash flag, so it stays useful across many sessions without needing explicit cleanup logic.
- Suite bumped 1.18.0 → 1.19.0 (minor: new capability). Build-verified clean and deployed; recovery in
  particular needs a real test - close Revit without saving a script, reopen, confirm the pane comes
  back with the same Prompt/code and the "Recovered unsaved work..." status text.

### 2026-07-18 (Prompt asked for improvement ideas; built the two suggested)
- Asked "do you have any idea to improve the tool" - offered two ideas grounded in the incremental-edit
  feature just built: highlight which lines actually changed after an edit, and stop clearing the
  Prompt box after every generate. Ajmal: "Yes do it" (both, not a choice between them).
- `PromptInput = string.Empty;` removed from the end of `GenerateCodeAsync` - one-line fix.
- Diff-highlight needed more care: `CodeEditorContent` gets reassigned from FOUR different places
  (`GenerateCodeAsync`, `FormatCode`, `RunFromHistory`, the auto-fix loop in `RunCodeAsync`), but only
  the first one should trigger a "here's what changed" highlight - reusing the generic
  `PropertyChanged` notification would've lit up the editor after Format/RunFromHistory too, which is
  noisy/meaningless there. Added a dedicated `AiShellViewModel.CodeGenerated(string previousCode,
  string newCode)` event, raised only from `GenerateCodeAsync`, so the signal is unambiguous about
  which action it came from.
- Implemented a real LCS-based line diff in `AiShellView.xaml.cs` (`ComputeChangedNewLineIndices`) -
  deliberately not a naive index-by-index line comparison, which would misfire the moment a line is
  inserted/deleted and shifts everything after it. Highlights reuse `TextMarkerService` (already in
  the project for syntax-error squiggles) via its `BackgroundColor` marker property instead of
  `MarkerTypes`/`SquigglyUnderline`, translucent Neon Blue (`#3500C8FF`) so it reads as "touched", not
  "wrong".
- **Real interaction bug caught before shipping**: `SyntaxCheckTimer_Tick` already calls
  `_textMarkerService.Clear()` on every keystroke pause (~500ms) to refresh error squiggles - since
  `Clear()` removes ALL markers indiscriminately, it would have wiped the new diff-highlights almost
  immediately after they appeared. Fixed by tagging markers (`ITextMarker.Tag`, already existed on the
  interface, just unused before) as `"error"` vs `"changed"` and switching both the timer and the diff
  handler to `RemoveAll(m => Equals(m.Tag, theRightTag))` instead of a blanket `Clear()`, so each kind
  only clears its own markers. General lesson: before adding a second use of a shared clear-everything
  mechanism, check what ELSE already calls `.Clear()` on the same collection and on what cadence.
- Skips highlighting entirely on the very first generate (nothing to diff against - "100% new" isn't a
  useful signal) and when the diff says every line changed (a fresh rewrite, not an edit - same
  reasoning).
- Suite bumped 1.17.1 → 1.18.0 (minor: new capability). Build-verified clean and deployed; not yet
  seen live - Ajmal needs to run the red-then-green duct example again and confirm the changed line(s)
  actually light up.

### 2026-07-18 (New capability: incremental code generation instead of always-fresh)
- Ajmal (dictated, described via a worked example): "change all ducts to red" -> generate -> run, then
  "change [duct accessories] to green" should EDIT the existing script (small related change: a
  different color and a different category target) rather than generating an unrelated-looking fresh
  script every time. "Create a filter" instead would be unrelated, so THAT should generate fresh. He
  believed this used to work ("I think you removed [it]") - checked the code: it never did.
  `GenerateCodeAsync` has always built a throwaway `messages` list per click (Revit-context injection +
  the new prompt only) with no `History` and no `CodeEditorContent` - every generate click was fully
  stateless, unlike `ReviewCodeAsync`/`RunCodeAsync`'s auto-fix path which already do pass the
  conversation/existing code. Not a regression from anything this session touched - a genuine gap.
- Fixed in `AiShellViewModel.GenerateCodeAsync` (v1.5.0): if `CodeEditorContent` is non-empty, inject it
  as `[EXISTING CODE IN THE EDITOR]` with explicit instructions for the AI to decide small-edit-in-place
  vs fresh-generate itself. Deliberately a judgment call handed to the model via prompt instructions,
  not a deterministic diff/string-similarity heuristic in this codebase - "is this request related to
  what's already there" is exactly the kind of fuzzy call an LLM is better positioned to make than
  regex/Levenshtein distance would be. Deliberately only the CURRENT editor content, not full `History`
  - keeps the request small since editor content already reflects the latest script by construction.
- Suite bumped 1.16.2 → 1.17.0 (minor: new capability). Build-verified clean and deployed; not yet
  tested live - this one specifically needs Ajmal to try the exact red-then-green duct example to
  confirm the AI actually honors the instruction (a prompt-engineering behavior, not something a build
  can verify - the code sends the right context, but whether Gemini/OpenAI reliably follow the
  instruction is inherently a live/subjective judgment call, not a pass/fail compile check).

### 2026-07-18 (Second live-test round: button clipping, white ComboBoxes, cramped Output)
- Ajmal's second screenshot round from the fixed build surfaced three more issues, all genuinely
  runtime-only (none catchable by `msbuild`): (1) "Review Code"/"Format Code" button text visibly
  clipped ("Cod" with the "e" missing) - those buttons had a fixed `Width="100"` that didn't fit the
  label at the new `16,8` padding. Fixed by dropping fixed widths on the execution-row buttons
  entirely (auto-size to content) - more robust than guessing a wider fixed number, since it can never
  clip regardless of label length. (2) Settings' Provider/Model `ComboBox`es still showed a
  white/system-grey background despite `Background="{StaticResource SurfaceBrush}"` being set -
  **confirms a suspicion flagged in the very first restyle entry**: a "colors only" property-setter
  restyle does NOT work for `ComboBox` - the default Windows theme's internal toggle-button chrome
  ignores the outer `ComboBox.Background`. Needed a real custom `ControlTemplate`. (3) Output console
  felt too cramped - gave it more relative row height and a `MinHeight` floor.
- **ComboBox custom template gotchas worth remembering for next time**: inside a nested
  `ToggleButton.Template` (the toggle chrome nested inside the outer `ComboBox` template), use
  `{Binding X, RelativeSource={RelativeSource AncestorType=ComboBox}}` to reach the ComboBox's own
  Background/BorderBrush - a plain `{TemplateBinding X}` there binds to the ToggleButton's own
  properties, not the ComboBox's. Separately, `RelativeSource`/`TemplateBinding` lookups do NOT
  reliably cross a `Popup` boundary (Popup content renders outside the normal visual tree) - don't try
  to bind the dropdown's width to `{Binding ActualWidth, RelativeSource={RelativeSource
  AncestorType=ComboBox}}` from inside the `Popup`; use a fixed `MinWidth` instead, and set
  `Popup.PlacementTarget="{Binding ElementName=...}"` explicitly (an `ElementName` binding within the
  same template's namescope IS reliable) so the dropdown anchors under the control correctly.
- Suite bumped 1.16.1 → 1.16.2 (patch: same-day fixes, no new capability). Build-verified clean and
  deployed; still awaiting Ajmal's next live look to confirm these three are actually fixed.

### 2026-07-18 (Settings moved from the "C#" pane's inline panel into its own popup window)
- Ajmal, right after the UI restyle: "THE SETTINGS IF I CLICK ITS COMING ISIDE THIS WINDOW I NEED TO
  POPUP A SEPARATE WINDOW" - wanted the inline collapsible Settings section (provider/API
  keys/model/scripts folder) to open as its own popup instead of expanding inside the docked pane.
- Confirmed Settings never touches the Revit API (pure local file I/O via `AiShellConfig.Save()`), so
  a plain modal `Window.ShowDialog()` from `AiShellView`'s code-behind is safe with no `ExternalEvent`
  needed - the API-context boundary only applies to actual Revit API calls, not every window in a
  dockable pane.
- Built `SettingsWindow.xaml`/`.xaml.cs` (new files, `src/AiShell/Views/`) with `DataContext =
  AiShellView's DataContext` - reuses the SAME `AiShellViewModel` instance and every existing Settings
  property/command as-is, no new ViewModel needed. Owned via `WindowInteropHelper` +
  `Process.GetCurrentProcess().MainWindowHandle` (the owner pattern for a pane-hosted popup with no
  normal WPF owner Window available).
- **New pattern worth reusing**: extracted the Soft Revit UI brush/style resources (added earlier the
  same day for the pane restyle) out of `AiShellView.xaml` into `SoftUiStyles.xaml`, a standalone
  `ResourceDictionary` with no `x:Class`/code-behind, merged into both `AiShellView.xaml` and the new
  `SettingsWindow.xaml` via `<ResourceDictionary.MergedDictionaries>`. Any future AJ AI/C# popup should
  merge this same file rather than re-declaring the brushes/styles inline - one visual-style source,
  not copy-pasted XAML per window.
- Removed `AiShellViewModel`'s now-dead `IsSettingsVisible`/`ToggleSettingsCommand`/`ToggleSettings()`
  entirely rather than leaving them unused.
- Suite bumped 1.15.2 → 1.16.0 (minor: genuinely new UI surface, a popup window, not just a restyle).
  Build-verified clean and deployed; not yet clicked in Revit.

### 2026-07-18 (Restyled the "C#" dockable pane UI, via the revit-ui skill)
- Ajmal: "AFTER THAT I NEED TO CHANGE THE UI FOR THE C# MAKE IT NICE ONE NOW ITS LIKE A NORMAL ONE" -
  the panel looked flat/plain (VS-Code-style dark theme, square borders, solid-color buttons) and he
  wanted it visually upgraded. Routed through the `revit-ui` skill for house style guidance rather than
  freelancing colors - this is the first AJ AI/AiShell UI element to actually apply the documented Soft
  Revit UI spec (Neumorphism + Claymorphism, Neon Blue `#00C8FF` primary, dark theme variant from
  `references/visual-style.md`).
- Restyle only, confirmed with Ajmal's own framing ("make it nice", not "add/change features") -
  `AiShellViewModel.cs` untouched, every `Binding`/`Command` in the XAML identical to before, just the
  visual treatment changed (rounded soft cards, reusable button styles with hover/pressed states, a
  custom rounded `TextBox` template, ComboBox/Expander given a lighter-touch color-only restyle rather
  than full custom templates to avoid risking broken dropdown/expand behavior).
- Removed decorative emoji from button labels (🧹 Format Code, 💾 Save Script) per
  `references/ui-wording.md`'s "no emoji in labels" rule - kept the plain geometric glyphs (▶ ⏹) since
  those aren't colorful emoji and read as simple play/stop icons, not decoration.
- **Caught and self-corrected a real bug before it shipped**: first draft of a custom `ProgressBar`
  `ControlTemplate` (for rounded corners) didn't actually bind the fill width to `Value` - WPF's real
  determinate-progress rendering needs proper `Track`-relative width math that a quick hand-written
  template doesn't get for free. Would have silently shown wrong/no progress during script execution.
  Reverted to WPF's default ProgressBar chrome with only `Background`/`Foreground` color overrides.
  **General lesson**: a from-scratch WPF `ControlTemplate` for anything with data-driven fill/state
  (ProgressBar, Slider, CheckBox) needs the real named parts/math, not just a Border + binding guess -
  when in doubt, keep the default template and override colors/BorderThickness only, don't reinvent the
  rendering logic for a cosmetic detail.
- Also caught mid-build: setting a rounded card's `Padding="0"` so a child control (AvalonEdit, a plain
  TextBox) can span edge-to-edge risks the child's square corners visually poking past the card's
  rounded corners (WPF doesn't auto-clip children to a parent `Border`'s `CornerRadius`). Fixed by
  keeping the same 14px padding on every card instead of special-casing the code-editor/output cards to
  zero - the standard safe rule is inset >= corner radius.
- Rendered an HTML mockup preview (via the `visualize` tool) so Ajmal could see the intended look before
  opening Revit, since a WPF `UserControl` inside a Revit dockable pane can't be screenshotted from here.
- Suite bumped 1.15.1 → 1.15.2 (patch: restyle/refactor, no new capability). Build-verified clean
  (BAML/XAML compiles, which catches resource-key typos and malformed markup) and deployed; visual
  result not yet confirmed live in Revit - only Ajmal can see the real rendered pane.

### 2026-07-18 (Same-day follow-up: shortened "C# with AI" to "C#")
- Shortened the chat/code-generation tool's label from "C# with AI" to just **"C#"** everywhere
  live (ribbon button text, dockable pane title in `App.cs`, `ShowAiShellCommand`'s error message,
  every AiShell chat-file's "Tool Name" metadata, Purpose/Notes lines) - left every dated changelog
  entry that already said "C# with AI" untouched, same historical-record reasoning as always.
- Suite bumped 1.15.0 → 1.15.1 (patch: same-day fix on an unreleased/untested change, not a new
  capability). Build-verified clean, deployed, superseded deploy folder cleaned up. Not yet tested live.

### 2026-07-18 (Renamed the chat pane to "C# with AI")
- Ajmal (dictating) renamed the chat/C#-generation panel **"C# with AI"** (was "AJ AI") and supplied
  its icon `C#.png` at `Y:\Ajmal Ps\icon\`.
- Copied into `src/Resources/` as `CSharp_with_AI.png` — deliberately
  NOT kept as `C#.png`: `IconLoader.Load` builds a `Uri` from the file path, and `#` is a URI fragment
  delimiter, so a literal `#` in a resource filename would silently break icon loading (empty/garbled
  path after the `#`). Worth remembering for any future icon file Ajmal supplies with a symbol in the
  name — rename before wiring in, don't just copy verbatim.
- Renamed the dockable pane title itself (`App.cs`'s `RegisterDockablePane` call) from "AJ AI" to
  "C# with AI" to match, plus the matching Purpose/Notes lines and `ShowAiShellCommand`'s error message
  text. Swept the "Tool Name" metadata header across every AiShell chat-feature file (ViewModel,
  Constants, ActivityLogger, ErrorCorrectionService, GeneratedCodeSafetyValidator,
  RevitContextExtractionService, RevitExecutionService) to "C# with AI". Left dated changelog entries
  everywhere untouched (same historical-record
  reasoning as every other rename this session).
- Suite bumped 1.14.0 → 1.15.0. Build-verified clean and deployed; not yet clicked in Revit. Also
  proactively cleaned the immediately-superseded prior deploy folder in
  `AppData\Roaming\Autodesk\Revit\Addins\2020\` after this deploy, matching the cleanup Ajmal asked for
  earlier this session (see the "AJ Tools.<timestamp>" folder buildup — not re-documented here, ask
  found it, not this file, if it needs re-explaining later).

### 2026-07-16 (rule change: skill creation is now create-then-report, not ask-first)
- **Ajmal explicitly changed his own 2026-07-08 rule.** Old: flag a skill idea, wait for his yes, never
  build silently. New: when a recurring, bounded, uncovered pattern is spotted — in work **or plain
  conversation** — create the skill immediately via `ajtools-claude-maker` and **report it in the same
  reply** ("created X because Y — say delete if you don't want it"). He chose this over ask-first knowing
  the trade-off: occasional wrongly-scoped skill to delete, in exchange for the system growing itself
  from every successful piece of work. Knowledge facts and scripts were already auto-captured; this
  extends automation to the last gated asset type.
- **What did NOT change:** the report is mandatory (silent creation stays forbidden — he must always be
  able to veto with one word), and **deleting or replacing an existing skill/file still needs his
  explicit OK**. Because creation is ungated now, the pre-creation **overlap check is load-bearing**:
  colliding descriptions make the wrong skill fire (the exact failure that forced deleting
  `ajtools-skill-maker` earlier today). Updated in the same pass: CLAUDE.md discipline #3 + quality
  floor #10, `ajtools-claude-maker` (description + trigger 2),
  `ajtools-knowledge-sync` step 4, and the cross-session memory.

### 2026-07-16 (claude-maker replaces skill-maker)
- **`ajtools-skill-maker` → `ajtools-claude-maker`** (old one deleted, content fully absorbed). Ajmal
  wanted one skill that creates/modifies *anything* in `.claude` — skills, knowledge files, script
  fragments, and the indexes — not just skills. Decided to **replace, not add alongside**: two skills both
  triggering on "make this a skill" would collide (overlapping descriptions are the top cause of the wrong
  skill firing). `claude-maker` is the single owner of the helper layer; its Step 1 is a routing table
  (fact→knowledge, C#→scripts, task-pattern→skill, universal-habit→CLAUDE.md, big-file→split) and it bakes
  in the 2026-07-16 safe-split method (measure first, `sed` don't retype, prove lossless, signpost, run the
  checker). Refined the size rule while writing it: past ~300 lines is a *candidate* to split, not a
  mandate — review it, and if it's one coherent job (like `tagging.md`), leave it. Retargeted the 5 live
  references (CLAUDE.md ×2, `ajtools-knowledge-sync`, 2 memory files); historical log mentions left as-is.
- **pyRevit/Python confirmed absent from the C# repo** — zero `.py` files in `D:\Ajmal\Revit Addins`;
  the plugin is 100% C#. The only Python left is 10 unported tools in the
  separate old extension (`D:\Ajmal\AutoCAD APP\Pyrevit\AJ-Tools.extension`), which Ajmal is keeping there.
  `ajtools-port-pyrevit` stays useful only until those are ported; revisit retiring it once they are.

### 2026-07-16 (knowledge restructure: split big files, route through an index — Ajmal's idea)
- `ajtools-conventions.md` was split 618 → 98 lines of rules + `ajtools-conventions-log.md` for dated
  history, cut at section seams and proved lossless by sorted diff. Lessons: measure before
  restructuring (the file everyone points at is the one that costs); when splitting, *move* text — if
  you rewrite a section, diff it, because a summary quietly loses things; run
  `.claude/tools/verify-knowledge-consistency.ps1` after any file move.

### 2026-07-13 (build, Transfer View Templates memory)
- Added Copy From/Copy To memory to Transfer View Templates, scoped to exactly those two fields per
  Ajmal's request (not the checked-templates list, override checkbox, or filter text). Two static
  `string` fields directly in `TransferViewTemplatesWindow` (no new service/model file - proportionate
  to a 2-field scope), same in-memory-per-Revit-session convention as `FilterProStateTracker`, keyed by
  `Document.Title` (a live `Document` reference doesn't survive across tool re-opens the way a title
  does) and saved only after a successful Transfer, mirroring Filter Pro's "remember a confirmed action,
  not every transient combo change" rule. Confirmed clean build, suite version bumped to 1.12.0.
- Investigated but could NOT pin down Ajmal's separate report that the Filter textbox's text is "not
  properly visible" in the same window - its style (`ModernTextBox`) and resource colors
  (`TextPrimary` #FFF2F7FA on `CardBackground` #FF2B2B2B) are identical to working search boxes in
  Filter Pro/Colorize, and layout looks sound. Asked Ajmal for detail instead of guessing at a fix.

### 2026-07-13 (build)
- Added **Grids** and **Levels** as two new pinnable/unpinnable Model groups in the Pin / Unpin
  Elements tool (`PinTargetGroup` enum, `PinElementsService`'s `_modelDefinitions` +
  `IsModelGroup`/`AddModelGroupCandidates`, `OST_Grids`/`OST_Levels`). No UI/XAML change was needed -
  `PinElementsWindow` builds its checkbox list entirely from `PinElementsService.GetModelTargetDefinitions()`,
  so new groups just need a service-side definition + collection case, same shape as every existing
  group. Confirmed clean normal build (`Release`, 2020 baseline) with auto-deploy on, suite version
  bumped to 1.11.1.

### 2026-07-12
- Auto-deploy writes each build to a timestamped AppData payload folder and updates the root `AJ Tools.addin`
  manifest to the new DLL. Never overwrite a loaded add-in DLL directly; Revit loads the new payload after
  the next restart. Old payload folders are intentionally left alone while a running Revit process may use them.

### 2026-07-11
- `RoslynService` (the C# pane's runner) reuses a bounded cache of 64 compiled safe scripts. Revit model
  data is deliberately never cached, so every run still sees current document state.

Add a new dated entry each time `ajtools-build` or `ajtools-debug` finishes a piece of work. Keep entries
short — a sentence or two is enough. The goal is "what would the next session need to know," not a full
changelog.

### 2026-07-08
- Created the `ajtools-build` and `ajtools-debug` skills and this log file, seeded from the conventions
  above (previously only known to Claude's cross-session memory, now also living here in the repo).
- Ajmal confirmed both skills should work by **planning first, then splitting the task into visible
  steps, then executing step by step** (verifying each step before moving to the next) rather than doing
  the whole ask in one opaque action — e.g. "quantity of VCD" splits into: recognize VCD as a family
  within Duct Accessories → collect Duct Accessories → filter to VCD → count/group → report. Same
  discipline applies to model-changing tasks (e.g. drawing a wall), not just queries. For genuinely large
  or independent chunks of work, split across separate `Agent` calls like a small team; for normal-sized
  tasks, do it directly with visible step tracking — don't spin up agents for something small.
- Split the shared knowledge log into focused files: `debug-log.md` (bugs-only history, separate from
  coding conventions). This file stays focused on coding conventions/decisions only.
- Created **`ajtools-skill-maker`** — a skill that scaffolds new AJ Tools skills (one folder per skill,
  plan-split-execute baked in, pointing at the shared knowledge files instead of duplicating them). It
  triggers both when Ajmal asks directly and proactively when a recurring task pattern is noticed during
  other work (see the always-on memory backing this, since a skill can't retroactively notice things it
  wasn't consulted for).
- Ajmal asked for an "always check and auto-save new knowledge, this is like a loop" habit. Ran it past
  `ajtools-skill-maker`'s own decision rule first: a universal habit that must apply to *every* task,
  regardless of which skill (if any) is running, doesn't belong in a skill (skills only activate when
  triggered) — it belongs somewhere always loaded. Created root `CLAUDE.md` for that always-on rule
  (check knowledge files before starting, capture new knowledge after finishing, propose-don't-silently-
  build new skills). Also created **`ajtools-knowledge-sync`** as a lightweight on-demand companion, for
  when Ajmal wants a deliberate full-session sweep rather than relying on the per-task habit alone.
  Trigger for this: this session, right before the CLAUDE.md gap was found, Claude nearly re-derived an
  already-documented finding from scratch mid-task without first re-reading the knowledge files — a
  concrete case of the check-first half of the habit not being followed.
- Added a "modeler mindset" principle to `CLAUDE.md` — don't trust Revit's API/naming at face value
  (proof case: CRAC trace, where both the tag names AND the `IsConnected` flag were wrong), and keep
  growing a library of "tricks" in the knowledge files whenever a new one is found, rather than
  re-discovering the same workaround each time.
- Full skill audit ("chek all the skills", 2026-07-08): found `ajtools-knowledge-sync` was the one skill
  missing YAML frontmatter (`name`/`description`) — it showed up in the skill list with just its heading
  text as the description instead of a real trigger phrase, which would have hurt auto-triggering. Fixed by
  adding frontmatter built from its existing "When to use this" section. Everything else (frontmatter on
  all other skills, knowledge-file links) checked out
  consistent.

### 2026-07-09
- Rewrote root `CLAUDE.md` (Ajmal-requested audit+upgrade): added the project identity/stack section,
  the never-edit-stale-trees hard rule (promoted from this file for safety), the hollow-root-`.git`
  warning (git only works inside `AJ Tools\` — flow unresolved, open question with Ajmal), the
  auto-deploy warning (`-p:SkipAjToolsAutoDeploy=true` for compile-only), a Definition of done, and a
  "Quality floor" block of non-negotiables so smaller models hold the same standard. Also found (not
  yet fixed, awaiting Ajmal): suite-version drift — root src 1.10.0 (header 1.8.0, changelog 1.9.0) vs
  the `AJ Tools\` repo's v1.11.3; README/CHANGELOG stale at 1.8.0/2020-only.
- Full skills audit (Ajmal-requested systems pass): all 10 existing repo skills judged keep. Created
  two new skills from the most
  repeated by-hand workflows in session history: **`ajtools-panel-audit`** (the behaviour-preserving
  panel refactor/audit pass that produced v1.5.2→1.8.0, six-plus sessions) and **`ajtools-port-pyrevit`**
  (converting tools from `D:\Ajmal\AutoCAD APP\Pyrevit\AJ-Tools.extension` into AJ Tools C# — Colorize
  and a Debug-panel tool were done by hand; the pyRevit script is the specification, port faithfully).
  Cloud-side skills (revit-csharp-plugin, aj-tools-github, etc.) can't be edited from this repo — noted
  in the audit report that revit-csharp-plugin's trigger lacks a "not for the AJ Tools repo" exclusion
  and aj-tools-github doesn't know about the hollow root git.
- Created [`.claude/tools/verify-knowledge-consistency.ps1`](../tools/verify-knowledge-consistency.ps1) —
  a QA pass over this repo's docs (SKILL.md name+description frontmatter; every relative markdown link
  resolves); wired into `ajtools-knowledge-sync`. Its first version failed to parse because of em-dashes
  in comments under Windows PowerShell 5.1 — .ps1 files here must stay plain ASCII in comments/strings.
- Doc refresh pass ("improve everything", same session): brought `README.md`, `INSTALL.md`,
  `docs/USAGE.md`, and `CHANGELOG.md` in line with verified reality — multi-version 2020–2027 build
  facts (NuGet Revit API, no local Revit needed to compile, auto-deploy warning), README suite version
  1.8.0 → 1.10.0 (per root AssemblyInfo attributes, with the reconciliation note in CHANGELOG since the
  release repo is at v1.11.3), backfilled the missing CHANGELOG 1.9.0 entry (Arrange Text in Box) and a
  factual Unreleased list, rewrote USAGE.md's ribbon map from `RibbonManager.cs`/
  `AnnotationRibbonManager.cs` source truth (it still described the old single-tab layout), and added
  the missing Text panel to README's ribbon list. `package.ps1` verified as genuinely 2020-only (`Release`
  config), so installer-facing claims were left as 2020 deliberately. NOT touched, still open with
  Ajmal: reconciling suite version numbering with the `AJ Tools\` release repo, and the hollow root
  `.git`.
- 2026-07-28: v1.25.6 full-project UI audit pass (mechanical: resources, grids, owners, caps, popups, ribbon). 18 fixes / 22 files, behaviour preserved; all checks now clean. New rule applied everywhere: every modal window gets a Revit owner; borderless+maximize windows get the AboutWindow MaxWidth/MaxHeight caps.
- 2026-08-05: v1.39.2 About window motion pass + project-wide rounded-corner audit. Motion: staggered
  entrance/exit/section-swap/ambient storyboards on AboutWindow (motion-design skill; entrances
  decelerate, exit accelerates, nav cascade 45ms step, last stagger start 460ms). Corners: a WPF Border
  does NOT clip children to its CornerRadius — About's header/footer painted square corners over the 22px
  curve; fixed with concentric radii (21 = 22 - 1px border). Audit of all 38 XAML files found About was
  the ONLY window with that defect (reasons per window recorded in ajtools-conventions.md — don't
  re-audit). Real finding elsewhere: the 4 View Crop windows kept an 8px radius while maximized, showing
  desktop through the corners → new shared `WindowChromeHelper.ApplyStateChrome` (v1.1.0) + an
  OnStateChanged override per window so Win+Up/snap are covered too. Two bugs caught by reasoning before
  shipping, not live: the IsCancel+Click double-Closing trap, and frozen ResourceDictionary storyboards
  refusing .Completed/.Stop (must .Clone()). Behaviour preserved; no window made non-resizable (Ajmal's
  explicit constraint). Clean on Release + R25; R27 blocked by this machine's .NET SDK 9.0.316. Not
  loaded in Revit by the assistant.
- 2026-08-05: v1.39.3 window entrance motion rolled out suite-wide. New shared `WindowMotionHelper`
  (220ms fade + 12px rise, CubicEase EaseOut) wired into 33 windows with one call after
  InitializeComponent(); About keeps its own ~750ms showcase entrance, Game HUD excluded. TWO TIERS ON
  PURPOSE — a working dialog must feel instant, so About's staged timing is never copied onto one (see
  ajtools-conventions.md). Animates the root CONTENT element, not Window.Opacity, because Window.Opacity
  only works with AllowsTransparency=True (7 of 35 windows). Entrance only — no close path touched, so
  DialogResult/validate-on-close flows are untouched; no window made non-resizable (Ajmal's constraint).
  Applied by script + dry run, then verified: 33 call sites, zero duplicates, all correctly placed.
  Clean on Release + R25. Not loaded in Revit by the assistant.
- 2026-08-05: v1.39.4 interaction motion added to the shared theme `src/UI/ModernStyles.xaml` (v1.3.0),
  which 29 windows merge — hover wash, press dip (97%) + shade, keyboard-focus ring, enable/disable fade
  on buttons; press dip on the window min/max/close buttons; hover + focus rings on text boxes; dropdown
  arrow rotation; hover fades on list items, combo items and tab headers; plus a house `ProgressBar`
  style with a breathe when indeterminate. One shared `CubicEase EaseOut` (`MotionEaseOut`); timings per
  the motion-design skill (hover 90, press 110, settle 240). TWO SAFETY RULES established and written
  into the XAML as comments — never animate Background/Foreground in a shared style (a running animation
  outruns a code-behind local value; About's nav buttons and PipeSizing's mode toggles both set those),
  and one trigger per animated property so hover/press/focus never fight. Layout untouched, selection
  left instant on purpose. Verified beforehand that nothing in `src/` reaches into a template part
  (zero GetTemplateChild/Template.FindName). Behaviour preserved; nothing made non-resizable; no close
  path touched. Clean on Release + R25, deployed to both 2020 addin folders. Not loaded in Revit by the
  assistant.
- 2026-08-05: v1.39.5 carried the same interaction motion into `src/AiShell/Views/SoftUiStyles.xaml`
  (v1.1.0) — the AJ AI pane, AI Settings and Saved Scripts — with timings identical to ModernStyles so
  the shell stops feeling like a separate app. The Primary/Secondary/Warning buttons' instant Background
  swaps were replaced by the shared animated overlays (measured to land within a shade of the old
  colours), which also made all three behave alike; the Warning one had been dimming while the other two
  swapped colour. Dropdowns gained hover feedback they never had. AI progress bars deliberately left on
  WPF's default chrome because the busy strip is IsIndeterminate and already animates.
  TWO THINGS WORTH MORE THAN THE MOTION ITSELF: (1) built `tools\verify-wpf-styles.ps1`, which loads the
  compiled dictionaries out of the built DLL and forces all 28 styles to instantiate their templates —
  catching the XamlParseException class of bug that a clean build provably cannot and that once broke
  Revit startup (v1.16.0); all 28 pass. (2) disproved a stale comment claiming a custom ProgressBar
  template needs its own width math — reflection over the real PresentationFramework.dll shows the
  control declares PART_Track/PART_Indicator and sizes the indicator itself, and v1.39.4's house bar
  measured exact at 25/50/100%. Both facts are in ajtools-conventions.md. Clean on Release + R25,
  deployed to both 2020 addin folders. Not loaded in Revit by the assistant.
- 2026-08-05: v1.39.6 finished the interaction-motion pass on the four windows that carry their own local
  styles and merge nothing. About (showcase tier) got a sidebar slide + link lift + chrome dip rather than
  the working-dialog wash; Graphics Override (26 local styles, the project's biggest UI surface) got press
  dip/shade, focus ring, eased disable, growing colour swatches, rotating dropdown arrow, fading row and
  tab highlights, a growing slider handle, and a toggle switch whose knob SLIDES instead of jumping ends;
  Game Key Settings had no styles at all, so its raw Windows buttons were replaced by one implicit house
  style. Game HUD needed nothing — verified every element is IsHitTestVisible="False" (pure overlay, no
  buttons), which is a finding, not a skip.
  THREE JUDGEMENTS WORTH KEEPING: (1) Graphics Override and About deliberately keep INSTANT hover colours
  — About because ShowSection() sets them from code-behind, Graphics Override because its danger-hover
  step (#5B1C1C -> #8B2B2B) cannot be reproduced by a neutral wash; both get motion via transforms
  instead. (2) The category checkbox ticks stay instant (virtualized list — an animated tick replays on
  every scroll), while the standalone one animates. (3) Caught mid-pass: the colour swatch had hover and
  press sharing ONE transform, which can strand it enlarged if you press then drag off — split into two,
  restoring the one-trigger-per-property rule. All three are in ajtools-conventions.md.
  Also built tools\verify-window-styles.ps1, which lifts a window's <Window.Resources> out of the source,
  re-parses it standalone and forces all 35 styles/templates to build — it finds them via TargetType, so
  new styles need no list update. Clean on Release + R25, deployed to both 2020 addin folders. Not loaded
  in Revit by the assistant.
- 2026-08-05: v1.39.7 added tab-change transitions via a new shared `TabMotionHelper` (180ms fade + 8px
  rise), wired with one call into the five tabbed windows — Colorize, Duct Standards Manager, Filter Pro,
  Graphics Override, Location Data Assigner. Chosen as the step-4 target because a tab switch was the
  most-repeated state change left in the suite and still a hard cut. Attaches by walking the visual tree
  on Loaded, so zero XAML changed. THE REASON THE HELPER EXISTS: Selector.SelectionChanged is routed, so
  a dropdown inside a tab bubbles up and a naive handler replays the whole tab transition on every
  dropdown pick — guarded via e.OriginalSource, with tools\verify-tab-motion.ps1 kept as a regression
  check (it proves a dropdown change does NOT animate, a tab change does, and neither selection is
  disturbed). Verified on the real WPF library that PART_SelectedContentHost exists in the default
  template, under ModernStyles' implicit style, and in GraphicsOverride's custom template — one helper
  covers all five. Rejected deliberately: show/hide panel transitions (those windows are SizeToContent,
  so the window would resize mid-animation) and exit animations (still awaiting Ajmal's go-ahead). Clean
  on Release + R25, deployed to both 2020 addin folders. Not loaded in Revit by the assistant.
- 2026-08-05: v1.40.0 added exit animations (150ms fade + 6px sink, CubicEase EaseIn) to the same 33
  windows that carry the entrance, on Ajmal's explicit go-ahead. Minor bump rather than patch because it
  changes the close PATH, not just appearance.
  THE NEAR-MISS THAT JUSTIFIES THE WHOLE "measure, don't assume" RULE: an exit animation must cancel the
  window's own Closing, animate, then re-issue the close — and WPF DISCARDS DialogResult when a close is
  cancelled. Measured on real dialogs before writing any helper code: DialogResult=true + cancelled
  Closing -> ShowDialog() returns FALSE. Since every command reads `if (window.ShowDialog() == true)`,
  the naive implementation would have made EVERY Run button in the suite behave like Cancel — open,
  close, do nothing, no error. Fixed by capturing DialogResult before the cancel and restoring it after;
  verified against a no-animation control group in all four shapes (Run/Cancel/plain Close/Click+IsCancel
  double-close). Three flags needed, not the two from the About pass, because the animation's Completed
  and a backstop DispatcherTimer both race to issue the close. The backstop is armed BEFORE the animation
  so a dialog can never be left un-closable. Audited beforehand that only PipeSizingWindow has its own
  Closing handler (idempotent SaveState) and that nothing external closes these windows and depends on it.
  tools\verify-exit-motion.ps1 keeps all of it honest. Full rule set in ajtools-conventions.md. Clean on
  Release + R25, deployed to both 2020 addin folders. Not loaded in Revit by the assistant.
- 2026-08-05: v1.40.1 added real progress reporting, starting with the slowest tool. New shared
  `ProgressReporter` helper wired into Purge Unused Elements' scan (which trial-deletes every candidate
  in a rolled-back transaction — the longest silent freeze in the suite). KEY POINT FOR FUTURE SESSIONS:
  this is NOT a background thread and must never become one — the Revit API only runs on Revit's UI
  thread. The work stays put; the window repaints part-way through via an empty dispatcher Invoke at
  DispatcherPriority.Render, which redraws WITHOUT processing input, so a click cannot re-enter a delete
  loop mid-run (Input priority sits below Render — a DoEvents-style pump would allow exactly that).
  Throttled to ~33ms with first/last always painted; measured 500 reports = 86ms total. Behaviour kept
  safe by construction: the service method gained an OPTIONAL Action<int,int> defaulting to null (all
  existing callers unchanged), the callback is try/caught so reporting can never abort a scan, and the
  progress rows went INSIDE the existing button grid rather than the window's root Grid (which would
  have shifted every Grid.Row below it). Same helper fits the other Purge windows, Transfer Views and
  the tagging services — not yet wired, awaiting Ajmal's go-ahead. Clean on Release + R25, deployed to
  both 2020 addin folders. Not loaded in Revit by the assistant.
- 2026-08-05: v1.40.2 templated the last un-styled controls — ModernCheckBox, ModernRadioButton and
  ToggleSwitchCheckBox in ModernStyles.xaml (v1.4.0). They had been setter-only since v1.0, so ~90 tick
  boxes across 21 windows were drawing raw Windows chrome inside the soft Neon Blue UI. Now a rounded
  box with an accent fill and a tick that fades+scales in, a matching radio with a popping dot, and
  ToggleSwitchCheckBox finally drawn as the switch its name always claimed (knob slides 20px), matching
  Graphics Override's. All original Setters kept so no layout moved. Deliberately left KEYED rather than
  implicit so DataGridCheckBoxColumn's generated checkbox (Duct Standards) is untouched — an animated
  tick there would replay on every scroll, the same rule as list selection. Verified nothing uses
  IsThreeState before templating, and all three added to tools\verify-wpf-styles.ps1. Clean on Release +
  R25, deployed to both 2020 addin folders. Not loaded in Revit by the assistant.
- 2026-08-05: v1.40.3 closed out the UI motion pass. New ModernListCheckBox (same house box, INSTANT
  tick) applied to the two virtualized-list checkboxes that were still raw Windows chrome — instant
  because those rows are recycled on scroll and an animated tick would flicker down the list; hover/
  press/focus still animate since they fire on user action. Purge Unplaced Views' scan got the same
  progress reporting as Purge Unused Elements (optional Action<int,int>, defaults null, try/caught) —
  those two are the only genuine multi-second freezes, both trial-deleting every candidate in a
  rolled-back transaction. Graphics Override's text boxes finally got hover/focus rings; they had no
  Template at all, so the focus edge was an instant BorderBrush swap.
  THREE DELIBERATE NON-ACTIONS, recorded so nobody "finishes" them by accident: (a) show/hide panel
  transitions stay out because those windows are SizeToContent and would resize mid-animation — fixing
  that means touching resizing, Ajmal's one hard constraint; (b) NO entrance for the AJ AI docked pane,
  because AiShellPaneProvider constructs AiShellView during Revit's OnStartup and a fault there takes the
  whole add-in down (it already did once, v1.16.0) — a fade on a docked panel is not worth touching the
  highest-blast-radius file in the project; (c) the Duct Standards DataGridCheckBoxColumn keeps its own
  editing flow. Clean on Release + R25, all four verification scripts pass, deployed to both 2020 addin
  folders. Not loaded in Revit by the assistant.
- 2026-08-05: v1.40.4 — fixes from an INDEPENDENT AUDIT of the whole v1.39.2 -> v1.40.3 UI pass (six
  lenses, findings adversarially refuted before counting: 10 confirmed, 4 thrown out). Worth knowing that
  the audit caught things the author's own verification did not, including a false safety claim.
  THE REAL BUG: ProgressReporter's comment claimed a Render-priority pump means the user "cannot click a
  button half way through a delete loop". Half right, and the dangerous half wrong — Dispatcher.Invoke on
  the calling thread pushes a nested frame that runs a real Win32 message loop, so a posted WM_CLOSE was
  measured firing Closing DURING the scan loop. Both purge windows now veto their own close while busy,
  and AttachStandardExit now respects an existing e.Cancel (a latent bug: it would previously animate and
  then force a vetoed window shut). Also fixed: five controls left with NO keyboard focus marker because
  they inherited FocusVisualStyle="{x:Null}" without drawing their own ring; and Linked Search's bare
  ToggleButton, the last raw Windows chrome in the suite, whose label went unreadable when open.
  FALSE NOTES CORRECTED — the GameHud "every element IsHitTestVisible=False / non-interactive" claim was
  backwards (its RootGrid exists to CAPTURE input; PauseLayer is click-to-resume), the "zero
  GetTemplateChild in src/" claim was falsified the same day by TabMotionHelper, the "all four standalone
  windows declare MotionEaseOut" claim (Game HUD does not), inconsistent style counts, and a README stuck
  at 1.39.1. LESSON: exact counts in prose rot fast — let the scripts report them.
  Clean on Release + R25, all verification scripts pass. Not loaded in Revit by the assistant.
