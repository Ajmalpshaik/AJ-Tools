#region Metadata
/*
 * Tool Name     : AJ Tools Assembly Metadata
 * File Name     : AssemblyInfo.cs
 * Purpose       : Defines assembly-level metadata and suite version for the AJ Tools add-in.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.54.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-08-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System.Reflection, System.Runtime.InteropServices
 *
 * Input         : Build metadata.
 * Output        : Versioned AJ Tools assembly attributes.
 *
 * Notes         :
 * - Suite version is independent of each tool's own version (tracked in its source file metadata).
 * - Bump rules: patch on internal refactor with no new tool; minor when a tool is added; major on suite restructure.
 *
 * Changelog     :
 * v1.54.0 (2026-08-20) - QUICK MENU NOW BEHAVES LIKE THE RIBBON PANEL IT MIRRORS, AND OPENS FASTER.
 *                       A slot whose tool the ribbon would grey out is now drawn greyed out on the
 *                       wheel and cannot be picked, with the hub saying so - it used to look normal,
 *                       accept the click, and then do nothing at all, because Revit silently ignores
 *                       a posted command whose availability rule says no. Five of the eight default
 *                       slots carry such a rule - Unhide All, Toggle Revit Links, Highlight Selection,
 *                       Colorize and Filter Pro - which is why "sometimes it does not run the tool"
 *                       was so easy to hit. The wheel does
 *                       not copy those rules: it asks the button's own availability class, so a rule
 *                       added to a ribbon button is followed here by itself. Behind that, Revit is
 *                       now asked CanPostCommand before a tool is posted, so a refusal is explained
 *                       instead of being silent. For speed, the blurred hover glow and the opacity
 *                       fade were removed - this window is see-through, which means Windows draws it
 *                       entirely on the CPU, and re-blurring a wedge on every pointer move was what
 *                       made aiming feel sluggish; the lit wedge now uses a brighter fill and a
 *                       thicker outline instead. Close-on-lose-focus arms only once the wheel is
 *                       fully up, so a stray focus change while it opens can no longer swallow it,
 *                       and Enter, Space or an out-of-range number key no longer close the wheel
 *                       without running anything. Reported by Ajmal - "sometimes very slow and
 *                       sometimes its not running the tool correctly".
 *                       Minor bump: new behaviour in an existing tool, no new ribbon button.
 * v1.53.0 (2026-08-19) - QUICK MENU CUSTOMISE IS NOW THE WHEEL ITSELF. The plain numbered slot list
 *                       was replaced by a live drawing of the wheel, using the same geometry and
 *                       colours as QuickMenuWindow, so the window shows what will actually open
 *                       instead of a list standing in for it. Tools are arranged by dragging: from
 *                       the list onto a slot fills it, slot onto slot swaps the two, and a slot
 *                       dragged back onto the list empties it. The Set / Clear / Move buttons stay
 *                       and now act on whichever slot is picked on the wheel, so nothing is lost for
 *                       working by button. Asked for by Ajmal after using 1.52.0 - "drag and drop
 *                       the tools in there and easy for the working".
 *                       Minor bump: new capability in an existing tool, no new ribbon button.
 * v1.52.0 (2026-08-19) - NEW TOOL: AJ Quick Menu - the game-style quick tool wheel. A ring of your
 *                       own favourite tools opens around the mouse pointer; point at one and click
 *                       (or press its number 1-9) and Revit runs it exactly as if its ribbon button
 *                       had been clicked. Slots, slot count (4-12) and wheel size are all
 *                       customisable and saved to %APPDATA%\AJTools\quickmenu-slots.txt.
 *                       Minor bump: a tool was added.
 *                       A SLOT CAN HOLD EITHER KIND OF TOOL (added 2026-08-19): one of Ajmal's own
 *                       AJ Tools / AJ Annotation buttons, OR one of Revit's own built-in commands
 *                       (Undo, Thin Lines, Visibility/Graphics, Purge Unused, place a wall...). The
 *                       customise window has a Show filter - All / AJ Tools only / Revit commands
 *                       only - so his own tools are never lost in Revit's long list.
 *                       WHY REVIT'S LIST IS NOT WRITTEN INTO THE CODE: Revit publishes its commands
 *                       as the PostableCommand enum and that enum changes every release, so naming a
 *                       member would break the build on versions that never had it. The names are
 *                       walked with Enum.GetNames(typeof(PostableCommand)) at run time - only the
 *                       TYPE is named in code - so one file builds for 2020 to 2027 and each version
 *                       offers exactly the commands it actually has. Saved keys are prefixed
 *                       "revit:" so they can never collide with a command class name.
 *                       New "Quick" panel, first on the AJ Tools tab, holding one split button
 *                       (Quick Menu / Customise). Nothing else moved except one panel to the right.
 *                       HOW IT LAUNCHES ANOTHER TOOL: an add-in cannot call another IExternalCommand
 *                       directly (ExternalCommandData cannot be constructed), so the wheel is shown
 *                       modally and the chosen tool is handed to Revit with
 *                       UIApplication.PostCommand while the Quick Menu command is still running.
 *                       The command id that needs is READ off the live ribbon rather than guessed -
 *                       see QuickMenuCatalog for the mechanism and QuickMenuLauncher for the
 *                       fallbacks.
 *                       NOT YET BUILT OR RUN IN REVIT - written in a Linux container that has no
 *                       msbuild and no Revit API assemblies. Needs a Windows build and a live test.
 * v1.51.0 (2026-08-18) - No-leader accessory tagging FINALISED against Ajmal's live model. Minor bump
 *                       because the placement rule is now a settled feature rather than a trial.
 *                       TUNED LIVE, not guessed: the rule was iterated on 32 real duct accessories in
 *                       his "1 - Mech" view at 1:50, placing tags through the AJ AI Bridge, looking,
 *                       and correcting. Three rounds - 300mm sideways-only (tags landed on a junction
 *                       fitting), 300mm with the four-direction escape (good, but "very far"), then
 *                       150mm, which he approved.
 *                       THE NUMBER: NoLeaderOffsetInternal = 150mm, deliberately SEPARATE from
 *                       SmartTagSettingsTracker's general 300mm offset. Halving the shared one would
 *                       have moved every duct tag in the suite; he scoped this to accessories. Only
 *                       the deliberate leader-off case uses it - the no-leader FALLBACK pass, used
 *                       when no leader position could be found, keeps the general offset, so duct and
 *                       pipe tagging is untouched.
 *                       MEASURED RESULT of the approved run, verified by a fresh read-back rather
 *                       than by eye: 32 of 32 tagged, 0 with a leader, 0 sitting on a duct or
 *                       fitting, 0 overlapping another tag.
 *                       WHY THE SPLIT LOOKS THE WAY IT DOES, worth keeping: an accessory sits INSIDE
 *                       a run, so for one in a HORIZONTAL duct "left" and "right" point along the
 *                       duct and land on it - both sides are blocked and the tag correctly escapes
 *                       above or below. For one in a VERTICAL duct the sides are across the run and
 *                       clear, so it goes right. The rule therefore reads as "beside a vertical run,
 *                       above/below a horizontal one" - which is the same convention the duct tags
 *                       already follow, and it falls out of the geometry rather than being coded.
 * v1.50.5 (2026-08-18) - No-leader tags can now SEE the ducts, and go above or below when both sides
 *                       are blocked. Found by Ajmal looking at 32 live accessory tags: most read
 *                       perfectly beside their accessory, but the ones at a junction had the tag sat
 *                       squarely on the fitting.
 *                       ROOT CAUSE, and it is the interesting part: ScoreCandidatePosition has never
 *                       seen model geometry. It weighs a position against other TAGS, text and
 *                       dimensions only, plus a disqualify on the tag's OWN host. So a tag landing on
 *                       a duct junction scored perfectly - as far as the engine knew that spot was
 *                       empty. No amount of reordering directions could fix that; the engine was
 *                       blind, not badly ordered.
 *                       BuildModelObstacleIndex now indexes duct/pipe/tray curves, fittings,
 *                       accessories and equipment as they appear in the view. Built ONCE per run and
 *                       only when some category actually has its leader switched off, so a normal run
 *                       pays nothing for it.
 *                       ORDER: Right, Left, Bottom, Top - beside first, below before above (Ajmal's
 *                       call, matching the rest of the suite where a horizontal run tags below).
 *                       FIRST-CLEAR-WINS, not best-scoring, and that distinction is the whole fix.
 *                       The scorer rates free space, so on an open run it happily rates "below" above
 *                       "beside" - which is exactly how these tags ended up under their elements in
 *                       v1.49.7. Here the ORDER decides and the checks only answer yes or no. If
 *                       nothing is completely clear it falls through to the scorer, which still picks
 *                       the least-bad rather than refusing to tag.
 *                       Supersedes v1.49.9's SidewaysOnly, which was right about the open case and
 *                       wrong about the crowded one - restricting to two directions left the tag
 *                       nowhere to escape to on a junction.
 *                       Accessories only in practice, since Duct Accessory is the only category with
 *                       its leader turned off - ducts and pipes are untouched.
 * v1.50.4 (2026-08-18) - Create Tags Settings tidied, and a DataGrid fault fixed across SIX windows.
 *                       Ajmal sent a screenshot: the category list squeezed down to two visible rows,
 *                       a stray narrow column past "In Model", and a three-line explanation under
 *                       "Distance from the element" he did not want.
 *                       ROOT CAUSE of the stray column: AutoGenerateColumns was never set, and it
 *                       DEFAULTS TO TRUE - so WPF adds a column for every public property on the row
 *                       object ON TOP of the ones declared in XAML. Nothing in a build catches it; it
 *                       only shows up as junk columns at runtime.
 *                       ROOT CAUSE of the squeeze: the grid sits in a star row, which is not the same
 *                       as having a floor. As the content under it grew (the new distance field and
 *                       its hint), the star row gave up its space and the list shrank to two rows.
 *                       A MinHeight is what actually protects it.
 *                       SWEPT EVERY DataGrid IN THE SUITE per Ajmal's standing rule, and found four
 *                       more windows with the same missing setting: Duct Standards Manager and the
 *                       three Purge windows. Checked each defines its own columns FIRST - setting
 *                       AutoGenerateColumns="False" on a grid that relies on auto-generation would
 *                       have blanked it completely - all four do, so all four are fixed. Only the two
 *                       tagging grids needed the MinHeight; the others were not being squeezed.
 *                       The long hint under "Distance from the element" is gone, heading kept, and the
 *                       spacing under it corrected so "Minimum length to tag" is not touching the box
 *                       above it. The explanation now lives only in the field's tooltip.
 * v1.50.3 (2026-08-18) - The whole Tags panel is stacked small buttons, in the order Ajmal laid out:
 *                           Smart MEP Tags | Fix Tag Clash
 *                           Rearrange Tags | Stack Tags
 *                           Create Tags    | L-Shape Leader
 *                       plus Center Room Tags / Section Mark Visibility as a third column of two.
 *                       Done in two steps in one sitting - first every button switched from large to
 *                       stacked, then the six were reordered into these two columns.
 *                       WHAT ACTUALLY CONTROLS BUTTON SIZE, since it is not obvious: AddStackedItems
 *                       vs AddItem, nothing else. AddStackedItems packs 2 or 3 rows into the width of
 *                       one normal button, and Revit then draws the SMALL (16x16) icon; AddItem makes
 *                       a large button drawing the 32x32. RibbonPanelHelper.ApplyIcons already loads
 *                       both sizes for every button, so switching a button between the two styles
 *                       needs no icon work at all - only the Add call changes.
 *                       A stack of TWO is legal, which is what the last column uses rather than
 *                       leaving one large button sitting beside two columns of small ones.
 *                       Children and tooltips are untouched: the pulldowns keep exactly the same
 *                       contents, and Section Mark Visibility keeps its AvailabilityClassName, now
 *                       read off the stacked result instead of the AddItem result.
 * v1.50.1 (2026-08-17) - Create Tags no longer asks where to put each tag, and Stack Tags moves out to
 *                       its own ribbon button. Minor bump, not patch: the interaction changes.
 *                       NEW BEHAVIOUR: pick the elements, press Finish, and every tag is placed at
 *                       once - at the distance set in the new Create Tags Settings field, on the side
 *                       the project's own rule already specifies (a run lying horizontally in the view
 *                       is tagged BELOW it, a vertical run to its RIGHT), with the same L-shaped
 *                       leader routine as before. The old flow demanded a pre-selection and then one
 *                       PickPoint per tag - up to 47 clicks for 47 ducts.
 *                       SELECTION MODEL - TWO modes, and the first attempt at this got it wrong.
 *                       Elements already selected when the button is pressed are all tagged at once.
 *                       Nothing selected means ONE ELEMENT PER CLICK, each tagged the instant it is
 *                       clicked, Esc to finish - PickObject (single) in a loop.
 *                       The first build used PickObjectS with a Finish button for the second mode,
 *                       reading his "I will finish the selection then tag" as applying to it. It does
 *                       not: that sentence was about the pre-selected case. Corrected same day on his
 *                       "if I run the tool without selecting, that time one by one I can select the
 *                       element - not multiple selection, I don't want". The batch path is for a
 *                       selection made BEFORE the tool runs; the picking path is one at a time.
 *                       Both go through one TagElements routine so the two modes cannot drift into
 *                       different skip rules - the exact drift undone across the tag tools in 1.49.1.
 *                       alreadyTagged is updated as tags go in, so clicking the same element twice in
 *                       one-by-one mode reports "already tagged" rather than stacking a second tag.
 *                       REPORT now follows the house rule properly: silent when everything went in,
 *                       and only shown when something was skipped or warned about. It used to pop up
 *                       unconditionally, which in one-by-one mode meant "0 tag(s) created" every time
 *                       a run was ended with Esc.
 *                       NEW SETTING: "Distance from the element", default 300mm, the same offset
 *                       Smart MEP Tags already uses so a view tagged by either tool reads the same.
 *                       0 is refused (a tag cannot sit on its own element), unlike the minimum length
 *                       beside it where 0 legitimately means "no minimum".
 *                       TWO BUGS IN THE FIRST BUILD OF THIS SETTING, both found by Ajmal in Revit and
 *                       both fixed here:
 *                       1) IT WAS MULTIPLIED BY THE VIEW SCALE. The 300mm default was copied from
 *                       Smart MEP Tags, whose offset is a MODEL distance
 *                       (SmartTagSettingsTracker.ResolveOffsetInternal is mm * MM_TO_FEET, no scale
 *                       term) - but this treated it as a paper size and scaled it. At 1:100 a 300mm
 *                       setting threw the tag 30 METRES off its duct. Copying a number while changing
 *                       its meaning is the whole bug. It is a real exception to the "check view.Scale
 *                       first" rule: that rule is about clearances measured on the SHEET (tag gaps,
 *                       elbow pushes, clash margins), whereas a tag sitting 300mm off a duct is a
 *                       distance in the building and must not change with the sheet scale.
 *                       2) IT MEASURED FROM THE ELEMENT'S CENTRE, so half the duct ate the distance
 *                       and a wide duct had the tag starting inside it. Now measured from the EDGE
 *                       (hostHalfW + offset), the same shape as the placement engine's own
 *                       "hostHalfW + offsetFromHostToTextEdge".
 *                       FIXED, a regression I introduced in v1.49.7 and Ajmal hit while testing:
 *                       FixTagClashSettingsWindow built a fresh TagClashSettingsState carrying only
 *                       the fields it shows. Save REWRITES THE WHOLE FILE, so the five Smart MEP Tag
 *                       values added in 1.49.7 were wiped every time that window was saved - size
 *                       rules and per-category leader choices back to nothing. SkipVerticalRuns was
 *                       already carried through for precisely this reason (noted in the 1.49.2 entry);
 *                       the new fields were not. Now re-read via Load() at save time rather than from
 *                       a snapshot taken when the window opened, so using the other settings window in
 *                       between cannot lose anything either. The lesson: any window writing a SHARED
 *                       settings file must carry every value it does not own, and adding a field to
 *                       that file means auditing every writer of it.
 *                       FIXED: Fix Tag Clash reported "0 of 50 separated" with the real cause - a 1mm
 *                       drift limit refusing all 424 attempted moves - as one line in a list of
 *                       counts. A tag is taller than 1mm on the sheet, so nothing can move anywhere.
 *                       It now says so in plain words when drift blocked EVERY move, and names the
 *                       setting and the normal value to put back.
 *                       RIBBON: Stack Tags is now its own pulldown button on the Tags panel, carrying
 *                       Stack Tags + the arrange gap settings, instead of being a child of Create
 *                       Tags. The two do different jobs - Create Tags puts each tag beside its own
 *                       element, Stack Tags gathers a batch into one column at a clicked point - and
 *                       burying the second inside the first made it hard to find.
 *                       KNOCK-ON worth knowing: the earlier note that "Create Tags deliberately has no
 *                       long-run confirm because it is click-per-element and self-paced" no longer
 *                       holds - it now tags a whole selection in one go. Left without a confirm for
 *                       now because the user has just finished hand-picking the elements and knows
 *                       exactly how many there are, which is not the blind case the threshold exists
 *                       for. Revisit if a huge crossing-window selection turns out to freeze.
 * v1.49.10 (2026-08-17) - Tag spacing now means the GAP BETWEEN tags, not the centre-to-centre step.
 *                       Ajmal set 1-2mm, watched the tool override him to 17mm, and asked why his
 *                       setting was being ignored. He was right and the setting was wrong.
 *                       THE REAL BUG: the number was the distance from one tag's CENTRE to the next
 *                       one's. His tags are 12mm tall on the sheet, so 8mm centre-to-centre overlapped
 *                       by 4mm and 1mm put them almost on top of each other. Nobody can judge that
 *                       number by eye without first knowing the tag height - a number typed as
 *                       "spacing" means the space you can SEE between two tags.
 *                       FIX: the setting is now the clear GAP, and the tallest tag's own height is
 *                       added to get the step. 1mm gives a tight stack, 10mm an open one, and every
 *                       value works. TagStackService.ResolveVerticalStepFeet replaces
 *                       ResolveSafeVerticalOffsetFeet - the old name is gone rather than deprecated,
 *                       because the two read identically at a call site and mean different things.
 *                       DELETED with it: the whole v1.49.8 "your spacing was too small, I raised it to
 *                       X" guard, in both tools. Any positive gap is un-overlappable by construction,
 *                       so there is nothing left to guard against and nothing to interrupt the user
 *                       about. That guard was the right answer to the wrong problem - it defended a
 *                       setting that should not have existed in that form.
 *                       CONSEQUENCE, worth knowing: existing saved values now produce WIDER stacks,
 *                       because the tag height is added on top. A stored 12mm was a 12mm step; it is
 *                       now a 12mm visible gap.
 *                       Window wording follows: "Gap between tags", and the live preview says how much
 *                       clear space is left rather than how far apart the tags stack. Validation was
 *                       already 0.1mm minimum, so 1-2mm never needed unlocking - it was only ever the
 *                       stacking maths refusing them.
 *                       ALSO: Center Room Tags now uses Center Annotation's icon (Reset Position.png)
 *                       instead of the stacking icon, so the two centring tools read as a pair.
 * v1.49.9 (2026-08-17) - FIXES v1.49.7's no-leader placement, found by Ajmal testing it in Revit.
 *                       Accessory tags with the leader turned off were still landing BELOW the
 *                       accessory instead of beside it.
 *                       ROOT CAUSE, and it is a lesson worth keeping: v1.49.7 put Right and Left FIRST
 *                       in the direction priority list and assumed that decided the outcome. It does
 *                       not. The scoring loop keeps the HIGHEST-scoring direction, not the first one
 *                       it evaluates - so the order only ever mattered for an exact tie or the
 *                       score >= 60 early exit. Below a duct is usually the emptiest space in the
 *                       view, so it kept scoring best and kept winning. The change read as if it
 *                       worked and did nothing.
 *                       FIX: hand the loop ONLY Right and Left (SidewaysOnly) instead of reordering
 *                       all four. Restricting the choice is what forces it; the scorer then picks
 *                       whichever side is clearer, which is also what makes "if one side clashes, use
 *                       the other" work. No change to the scorer itself.
 *                       CHECKED, so no fallback was added: ScoreCandidatePosition only disqualifies a
 *                       position (-1) when the tag would overlap its OWN host element, and Right/Left
 *                       are offset clear of the host by construction (hostHalfW + offset + halfW).
 *                       Cutting four candidates to two therefore cannot leave an accessory untagged.
 *                       ALIGNMENT: already correct and needed no work - Right/Left are a pure viewRight
 *                       offset from the element midpoint, with no vertical component, so the tag comes
 *                       out exactly level with the accessory centre.
 *                       AJMAL'S EXPLICIT CHOICE: left/right in VIEW space whatever way the duct runs.
 *                       He was shown that on a duct running left-right this puts the tag at the
 *                       accessory's connector ends, and chose it anyway - one straight column of tags
 *                       down the sheet matters more to him than clearing the connectors. Do not
 *                       "correct" this to perpendicular-to-run without asking him again.
 * v1.49.8 (2026-08-17) - Tag spacing can no longer be set too small to work. Ajmal asked whether the
 *                       Rearrange Tags spacing setting is still needed now that clash detection
 *                       exists, and whether it could be automatic. Checked the code before answering:
 *                       he was HALF right, and the half he was right about was real.
 *                       RIGHT: the spacing is a blind number. It steps from one tag's POSITION to the
 *                       next and never measures how tall a tag is, so a two-line tag or a taller tag
 *                       family silently overlaps at a spacing that looked fine before - and NEITHER
 *                       stacking tool does any clash checking, so nothing catches it.
 *                       NOT RIGHT: replacing it with clash detection would make the stack worse.
 *                       Clash detection only guarantees "not touching" and moves each tag the shortest
 *                       distance that clears, which on a column of mixed-length tags gives UNEVEN
 *                       gaps. An even column is the entire point of Rearrange Tags. Fix Tag Clash is
 *                       also a separate button - Rearrange does not call it - so removing the spacing
 *                       would not have automated anything, just produced a mess until he ran the other
 *                       tool. Told him so rather than agreeing.
 *                       DONE INSTEAD: TagStackService.ResolveSafeVerticalOffsetFeet measures the
 *                       TALLEST tag it can see, adds the clash engine's own minimum gap, and raises
 *                       the step if the setting is below that. The setting stays and still means "how
 *                       far apart I want them" - this only stops it being too small. Rearrange Tags
 *                       measures the actual selected tags, so it is exact; Stack Tags has not created
 *                       its tags yet, so it measures the tags already in the view as a stand-in, and
 *                       an empty view leaves the setting untouched rather than guessing.
 *                       Reported ONCE after the click loop, never inside it - every click restacks the
 *                       whole batch, so a message in the loop would fire on every click. Rearrange
 *                       shows a single note; Stack folds it into the summary it already shows.
 *                       SWEPT ALL TAG TOOLS as instructed. The spacing is used by exactly two -
 *                       Rearrange Tags and Stack Tags - both now covered by the one shared helper.
 *                       Create Tags asks for a click per tag and never stacks; Smart MEP Tags already
 *                       measures via its placement engine's tag size hints; Center Room Tags has no
 *                       spacing at all; L-Shape Leader already measures through TagLeaderService.
 *                       UNIT BUG CAUGHT IN THIS CHANGE before it shipped: two early-return paths in
 *                       the new helper converted mm to feet WITHOUT the view scale, which the callers
 *                       had always applied. At 1:100 that is a step 100x too small - every tag on one
 *                       spot - and it reads as a perfectly sensible unit conversion.
 * v1.49.7 (2026-08-17) - Smart MEP Tag Settings gains an Advanced tab and a per-category leader tick.
 *                       Ajmal asked for a settable minimum length and size, on their own panel, plus a
 *                       per-category "no leader" option for accessories; he then delegated every open
 *                       decision. Confirmed with him first: leader is per CATEGORY, a round section
 *                       counts its diameter as BOTH width and height, and a run below EITHER minimum
 *                       is skipped (400x50 fails a 100x100 minimum).
 *                       WINDOW: now two tabs. "What to tag" keeps the category grid, which gains a
 *                       Leader? column, plus the shared vertical-run tick. "Advanced" holds the
 *                       shortest run worth tagging, an "Also filter by size" tick, and the width and
 *                       height minimums, which grey out when the tick is off. Validation message and
 *                       the buttons stay OUTSIDE the TabControl, so an error raised on Advanced is
 *                       still readable from the other tab. 0 and blank both mean "no minimum" and are
 *                       valid - only a non-number or a negative is refused. Reset covers all of it.
 *                       FILTER: MinCurveLength (1000mm), MinDuctWidth (100mm) and MinPipeDiameter
 *                       (0mm) are no longer hardcoded - they are the DEFAULTS and the user sets the
 *                       rest. Still applied to duct/pipe/cable tray only; an accessory or a piece of
 *                       equipment has no meaningful run length and is never size-filtered.
 *                       LEADER OFF: the placement engine already ran a leader pass then a no-leader
 *                       pass, returning early whenever the leader pass scored - so no-leader was only
 *                       ever a fallback. With the tick off, the leader pass is skipped entirely and
 *                       the tag lands on the close-in offset, beside the element. Direction order
 *                       becomes Right, Left, then the usual two, so a clash on one side is answered by
 *                       the other side and only then falls through to the normal clash handling -
 *                       exactly the order asked for. The scoring loop itself is untouched, and the
 *                       existing no-leader FALLBACK keeps its old order, because the new order is
 *                       gated on the SETTING rather than on the pass.
 *                       PERSISTENCE: these live in %APPDATA%\AJTools\TagClash.config, not in
 *                       SmartTagSettingsTracker, which is a static in-memory field that empties on
 *                       every Revit restart - a drafting standard that forgets itself would read as a
 *                       bug. Leader choices are stored as the EXCEPTIONS ("categories with no
 *                       leader"), so a category the tool gains later defaults correctly with no
 *                       migration. Read-modify-write-and-verify, and a failed write is reported.
 *                       BUG FOUND WHILE WRITING IT: a round PIPE answers to RBS_PIPE_DIAMETER_PARAM,
 *                       not RBS_CURVE_DIAMETER_PARAM, which is the duct one. Reading only the curve
 *                       parameter would have measured every duct and no pipe at all, leaving the new
 *                       pipe size filter silently doing nothing - the same shape of dead filter that
 *                       MinPipeDiameter = 0 already was. TryGetCrossSectionMm reads both.
 *                       DEFAULT CHOSEN UNDER DELEGATION: size filter ON, width 100, height 0. Ducts
 *                       then behave exactly as before (100mm width, no height test). The one change is
 *                       that pipes under 100mm are now skipped, where no pipe was ever size-filtered
 *                       before; it shows in the skip tally and is switched off by setting width to 0.
 *                       Rejected 100x100 (silently drops shallow ducts too) and filter OFF (silently
 *                       starts tagging small ducts). Design note in
 *                       docs/superpowers/specs/2026-08-17-smart-mep-tag-size-filters-and-leader-toggle-design.md
 * v1.49.6 (2026-08-17) - Fix Tag Clash moves tags the SHORTEST way out instead of a fixed step, an
 *                       idea taken from the AJ AI Brain after Ajmal asked whether the Brain's clash
 *                       work was better than this one. Compared both: it is NOT, and it was not
 *                       adopted. The Brain's pass (knowledge/live-model/tagging.md) sees tag-vs-tag
 *                       only, splits every move 50/50 across BOTH tags, has no drift limit, no pinned
 *                       handling and no frozen-winner guard - and its own notes say it is "NOT full
 *                       clash-free placement" and defer to the compiled tool. It exists because the
 *                       real engine is unreachable from a script, so it is a workaround, not an
 *                       upgrade. Taking it wholesale would have been a downgrade.
 *                       ONE idea in it genuinely beat this engine, and that is what was taken: measure
 *                       how far two boxes ACTUALLY overlap and move by exactly that much plus the gap,
 *                       rather than by a fixed quantum. Every existing candidate here is a whole tag
 *                       height up/down or a whole tag width sideways, so two tags overlapping by a
 *                       hair were still shoved a full tag apart. Measured on the maths: a 0.2-wide
 *                       overlap moved 2.50 before and 0.70 now.
 *                       Why it matters beyond looks: a move that overshoots burns the drift allowance,
 *                       and a tag that runs out of drift is left clashing and MARKED rather than
 *                       fixed. Shorter moves mean more tags actually get fixed within the same 50mm.
 *                       Implemented as BuildOverlapEscapeOffsets, feeding the SAME candidate pool the
 *                       fixed steps already feed - the existing "smallest move that ends up genuinely
 *                       clear" selector is unchanged, so a measured escape only wins when it really
 *                       works. Deliberately NOT copied from the Brain: the 50/50 split across both
 *                       tags, which would break this engine's who-moves rule (decided by leader
 *                       length) and its frozen-winner guard. Clearance is minGap + tolerance, not
 *                       minGap, or a pair landing exactly on the boundary still counts as clashing on
 *                       the next pass and the tag oscillates instead of settling.
 * v1.49.5 (2026-08-17) - The long-run warning rolled out across the WHOLE Tags panel, on Ajmal's
 *                       standing rule: a change asked for on one tool must be checked against every
 *                       other tool it could apply to, not just the one named. v1.49.4 added the
 *                       count-and-confirm to Smart MEP Tags and Fix Tag Clash only; five more tools on
 *                       the same panel could freeze Revit exactly the same way and said nothing.
 *                       NOW ASK FIRST (over 500 elements): Stack Tags (one click creates and stacks a
 *                       tag for EVERY selected element), Rearrange Tags (every click re-arranges the
 *                       whole selection), L-Shape Leader (preselected path only - it reworks every tag
 *                       in one go), Center Room Tags (one press moves every room tag in the view, each
 *                       needing its room boundary solved), and Clear Tag Clash Marks.
 *                       Clear Tag Clash Marks is the odd one: it ALREADY asked before running, but
 *                       never said how big the job was. The count went into the question it already
 *                       asks rather than adding a second dialog on top - two prompts back to back for
 *                       one click would be worse than the problem. Needed a new
 *                       TagClashHighlighter.CountTagsInView, deliberately using the same collector
 *                       shape as ClearAll so the number quoted is the number acted on.
 *                       DELIBERATELY NOT ADDED - Create Tags. It asks for one click per tag and the
 *                       prompt already reads "Click a location for the next tag (3 of 47 remaining) -
 *                       Esc to finish", so it is self-paced with a live count and cannot freeze. A
 *                       warning there would be a click in the way, not a safety net. Section Mark
 *                       Visibility also skipped: it owns real windows, so a progress bar is the right
 *                       answer there (see ProgressReporter), not a prompt.
 *                       DE-DUPLICATED while doing it, per the same single-source rule as 1.49.1: the
 *                       "Revit will be busy... single undo step... Continue?" paragraph existed twice
 *                       and was about to exist seven times. It now lives once as
 *                       DialogHelper.ConfirmLongRun(title, count, whatWillHappen), which also owns the
 *                       500 threshold - moved off TagClashSettings, since tools with nothing to do with
 *                       tag clash now share it. Smart MEP Tags and Fix Tag Clash were rewired onto the
 *                       helper; their wording and threshold are byte-for-byte what they already were.
 *                       Checked and found already done: all four Tags panel settings windows already
 *                       have "Reset to defaults" (added in 1.49.4). The three windows still missing one
 *                       - MEP Opening, Revision Cloud, Flow Direction - are on OTHER panels and were
 *                       left alone, since this pass was scoped to the Tags panel.
 * v1.49.4 (2026-08-17) - The last three findings from the Tags panel UI audit, all UI-only.
 *                       1) NO MORE SILENT FREEZE. Smart MEP Tags and Fix Tag Clash now say what they
 *                       are about to do when a run is big (over 500 elements) and let you back out
 *                       first: "About to place tags on 3,204 elements... Revit will be busy and can't
 *                       be stopped part way. It is a single undo step." Neither tool can show a progress
 *                       bar or a Cancel - they run without a window, so there is nowhere to put one
 *                       short of rebuilding them as modeless windows driven by an ExternalEvent.
 *                       Asking up front is the honest alternative to Revit going white with no warning.
 *                       The threshold is one shared constant so both tools agree, and is deliberately
 *                       not in any settings window - it is a nag threshold, not a modelling choice.
 *                       2) Fix Tag Clash Settings' clash tolerance and minimum gap are now behind a
 *                       "Show advanced settings" tick box, since they are almost never touched. It
 *                       opens itself automatically when either value is NOT the default, so a
 *                       non-default setting can never sit hidden and then get blamed on the tool.
 *                       3) All four tagging settings windows now have a "Reset to defaults" button -
 *                       Smart MEP Tag and Create Tags had none at all, and Arrange Tags said "Reset to
 *                       default" while Fix Tag Clash said "defaults". Reset puts categories back on,
 *                       priorities and minimum length back to their own defaults, and the shared
 *                       vertical-run rule back on; Smart MEP Tag's per-category default priorities come
 *                       from SmartTagSettingsTracker rather than being copied into the window, and
 *                       Create Tags' default length from CreateTagsSettingsTracker.
 *                       Widths evened up as two matching pairs rather than one size for all: the two
 *                       category-grid windows stay at 640, and the two simple windows go to 520 (from
 *                       470 and 500). Forcing a one-number spacing window to 640 to match a four-column
 *                       grid would have made it worse, not more consistent.
 * v1.49.3 (2026-08-17) - Same "Skip vertical ducts, pipes and cable trays" tick box added to Smart MEP
 *                       Tag Settings as well, so it can be set from either tagging tool's own settings
 *                       instead of only from Create Tags. One stored value behind both tick boxes -
 *                       change it in either place and Smart MEP Tags, Create Tags and Stack Tags all
 *                       follow. Each window says so under the tick box.
 *                       Three windows now write that one settings file (both tick boxes, plus Fix Tag
 *                       Clash carrying the value through untouched), so the read-modify-write-and-verify
 *                       save moved into TagClashSettings.TrySetSkipVerticalRuns rather than being
 *                       copied into a second command - the same single-source rule the 1.49.1 clean-up
 *                       was about. Smart MEP Tag Settings tooltip reworded off "category-wise
 *                       enable/disable". No behaviour change to how any tool treats a vertical run.
 * v1.49.2 (2026-08-17) - "Skip vertical ducts, pipes and cable trays" tick box moved from the Fix Tag
 *                       Clash settings window to the Create Tags settings window, where a tagging rule
 *                       belongs (Ajmal's call). It only landed in the clash window because that is the
 *                       one tagging store that survives closing Revit - Smart MEP Tag's and Create
 *                       Tags' own trackers are in-memory only. The VALUE still lives in that same file
 *                       so it keeps persisting; only the tick box moved, and Smart MEP Tags, Create
 *                       Tags and Stack Tags all still read the one value.
 *                       Two consequences worth knowing: the Fix Tag Clash window now carries the value
 *                       through untouched (saving there rewrites the whole file, so dropping it would
 *                       reset a choice made in Create Tags), and its "Reset to defaults" deliberately
 *                       leaves that one value alone. Create Tags Settings writes it read-modify-write
 *                       and confirms it by reading back, so a failed write reports instead of being
 *                       silently lost. Both settings tooltips reworded to match. No behaviour change to
 *                       how any tool treats a vertical run.
 * v1.49.1 (2026-08-16) - Tag tools de-duplicated onto shared blocks, and four honest bugs fixed.
 *                       Behaviour preserved everywhere except the two reporting fixes noted below.
 *                       SHARED BLOCKS: the L-shaped leader routine existed FOUR times over (Smart MEP
 *                       Tags, Stack Tags, Rearrange Tags, L-Shape Leader) and the copies had drifted -
 *                       two nudged the elbow clear of the tag text and retried when Revit refused, two
 *                       did neither. Those differences are now OPTIONS on one routine
 *                       (Services/LeaderLogic/TagLeaderService.cs), so each tool keeps exactly the
 *                       behaviour it had while a leader fix lands everywhere at once. Rearrange Tags'
 *                       deliberate refusal to touch the leader end is preserved as
 *                       PreserveLeaderEnd - it is a real requirement, not drift. Likewise the
 *                       nearest-first stacking loop existed twice, with a character-for-character
 *                       identical AlignToBaseX in both; it is now
 *                       Services/TagArrange/TagStackService.cs, with the two genuine differences
 *                       (what is carried, what happens at each slot) passed in as callbacks. The
 *                       bounding-box/tag-text-box measurement existed three times and now lives once
 *                       in TagViewGeometry. Net: about 730 lines removed from the four tools.
 *                       FIXED: L-Shape Leader's header and ribbon tooltip both promised "run again on
 *                       the same tag flips the elbow side". The code never did that - same head plus
 *                       same leader end always gives the same elbow. Wording corrected rather than
 *                       inventing a side-flip nobody asked for on a tool that works; the unused
 *                       LeaderToggleState enum that was the only trace of the idea is removed.
 *                       FIXED: Stack Tags and Rearrange Tags rolled the WHOLE click back in silence
 *                       when one element could not be placed, which looks exactly like the click doing
 *                       nothing. Both now say how many attempts were undone and why. Arranging stays
 *                       all-or-nothing - only the silence is fixed.
 *                       FIXED: the pipe diameter filter read as a working filter but could never fire
 *                       (MinPipeDiameter is 0, so "diameter >= 0 && diameter < 0"). Now skipped
 *                       explicitly at 0 and documented as "no minimum".
 *                       DOCUMENTED: scoring criterion 4 adds a flat 20 to every candidate position and
 *                       is not a real criterion - the score is out of 80 with 20 free points, and the
 *                       "score >= 60" early exit is calibrated against that. Left exactly as-is on
 *                       purpose; removing it would silently retune every Smart MEP Tag placement.
 * v1.49.0 (2026-08-16) - NEW TOOL: Fix Tag Clash, on the AJ Annotation "Tags" panel, with Clear Tag
 *                       Clash Marks and its own settings. It works the opposite way round to the old
 *                       Smart MEP Tag approach: instead of asking "does this clash?" before every
 *                       placement (up to 24 scored positions per element - roughly 240,000 clash
 *                       questions before 10,000 tags exist), the tags are placed first and only the
 *                       few that actually collide are worked on afterwards. Point it at any view and
 *                       it separates the clashing tags, however they were placed - Smart MEP Tags,
 *                       Create Tags, Stack Tags, Revit's own Tag All, or by hand.
 *                       The rule for who moves: the tag sitting closest to its own element keeps its
 *                       place, the stretched one moves; ties break on element id so a re-run gives the
 *                       same answer. A tag clashing with a text note or dimension always gives way,
 *                       because the annotation cannot move for it. Two guards stop A-pushes-B-pushes-A
 *                       looping forever: a tag that wins a contest is frozen for the rest of the run,
 *                       and no tag may travel further than the drift limit from where it started.
 *                       Anything still clashing when the rounds run out is coloured and left selected.
 *                       New shared pieces this introduces, both built to be reused by the other tag
 *                       tools rather than copied again: TagClashEngine (the one clash engine, with the
 *                       old engine's AnnotationBox, AnnotationSpatialIndex and tuned 1.5mm/5mm
 *                       tolerances kept deliberately) and TagViewGeometry (view projection, rotated
 *                       bounding boxes, and the tag-text-box measurement that currently exists in three
 *                       private copies). The clash check also now sees detail components, detail lines,
 *                       generic annotations, keynote tags, spot elevations/coordinates and revision
 *                       clouds, which the old engine was blind to.
 *                       CHANGED: skipping vertical runs is now a setting rather than hard-coded, and
 *                       one rule for every tagging tool. Smart MEP Tags previously skipped vertical
 *                       DUCTS only while Create Tags and Stack Tags skipped duct, pipe and cable tray,
 *                       so the same vertical pipe behaved differently depending on the button pressed.
 *                       Both now read the same setting and use the same check. Default is on, matching
 *                       Create Tags' existing behaviour - Smart MEP Tags is the tool whose behaviour
 *                       changes. Settings are file-backed (%APPDATA%\AJTools\TagClash.config) so they
 *                       survive closing Revit, unlike the in-memory Smart MEP Tag / Create Tags trackers.
 * v1.48.3 (2026-08-16) - Auto MEP Dimension Settings window split into two tabs, "What to dimension"
 *                       (services and reference targets) and "How it's drawn" (chain style and the
 *                       skip filters). All four sections previously sat in one scroll behind a nine-row
 *                       reference table, so the two sections changed most often were always below the
 *                       fold. Each tab keeps its own scrollbar, and the validation message stays outside
 *                       the tabs so it is readable from either one. Layout only - no control, setting,
 *                       label or validation rule changed.
 * v1.48.2 (2026-08-16) - Auto MEP Dimension: two ducts measured to the same wall no longer leave two
 *                       overlapping dimensions. Once the outer duct's chain (wall - inner - outer) is
 *                       drawn, the shorter wall - inner dimension the tool made earlier is removed, and
 *                       a run already carried inside an existing chain is skipped instead of being
 *                       dimensioned again. The duplicate test used to require both dimension strings to
 *                       sit within 187.5 mm of each other ALONG the run before it would compare what
 *                       they measured - two parallel ducts of different lengths never do, so the check
 *                       could not fire. It now compares what each dimension documents, and recognises a
 *                       chain left by an earlier run of the tool as well as one from the current run.
 *                       Hand-drawn dimensions are still never deleted.
 * v1.48.1 (2026-08-16) - Connect MEP Elements Settings window: the Main tab was cut off at the bottom
 *                       (the "Warn me if the new run hits something" card) and the window could not be
 *                       dragged bigger to reveal it - v1.48.0 made it a fixed 560 x 700 with no
 *                       scrollbar, on an estimate of the content height that turned out ~60px short.
 *                       Now 560 x 780 by default, ResizeMode CanResize with MinWidth/MinHeight, and
 *                       both tabs sit in a ScrollViewer - the same pattern every other settings window
 *                       in the suite already used. Settings, wording and behaviour are unchanged.
 * v1.48.0 (2026-08-16) - Connect MEP Elements v3: the routing-mode choice is GONE and the tool now
 *                       always stretches what was picked. A piece is created only where nothing can
 *                       be stretched - the bridging run across a crank, and the run up to flex or
 *                       equipment. An end that COULD stretch but is held back by "which element may
 *                       move" now refuses with a clear message instead of quietly getting a piece
 *                       bolted on (that was the v2 behaviour Ajmal asked to remove). Settings window
 *                       rebuilt as Main/Advanced tabs with hover tooltips instead of a permanent hint
 *                       line under every control. The two grouped picking flags were split one per
 *                       category (Conduit, Flex Duct, Flex Pipe, Air terminals, Equipment, Fittings,
 *                       Accessories) with Equipment as the deliberate catch-all so nothing that used
 *                       to be pickable was silently dropped. Comments/Mark copying reduced to workset
 *                       only. "Show failed report" replaced the carry-into-next-prompt behaviour with
 *                       a popup, and moved to the footer.
 *                       A 5-dimension multi-agent housekeeping audit raised 24 findings, 18 survived
 *                       adversarial verification, all fixed. The one that mattered: CopyWorkset had
 *                       NEVER worked, in v2 either - ELEM_PARTITION_PARAM has Integer storage, but it
 *                       was being copied through an ElementId helper whose storage-type guard
 *                       returned early every single time. Now copied via a new CopyIntegerParameter.
 *                       Also removed: SmartConnectRoutePlan.FirstDirection/SecondDirection and
 *                       ConnectionOutcome.Label (all write-only), and an
 *                       "&& !result.Warnings.Any()" guard that suppressed the "built at X instead of
 *                       your Y" notice whenever any unrelated warning happened to be present. Added
 *                       the shared TabMotionHelper call every other tabbed AJ Tools window makes.
 * v1.47.8 (2026-08-16) - Repo housekeeping pass: synced README.md/AssemblyInfo.cs's stale 1.46.0
 *                       version claims to the real 1.47.4, removed the "Auto Dimension" ribbon panel
 *                       from README.md/docs/USAGE.md (merged into "Dimensions" in v1.46.0),
 *                       rewrote CONTRIBUTING.md's Development Setup to match the actual build
 *                       requirements, fixed a stale example tag in RELEASE_PROCESS.md, added the
 *                       missing Release R21-R27 configurations to AJ Tools.sln so Visual Studio's
 *                       Configuration Manager can reach them, and corrected the vestigial
 *                       RootNamespace (AJ_Tools -> AJTools) in the csproj. No tool behavior changed.
 * v1.47.7 (2026-08-16) - Fixed the "Connect MEP Elements" split button getting stuck on Settings.
 *                       Revit's SplitButton defaults to showing whichever child was clicked last as
 *                       the permanent top face (IsSynchronizedWithCurrentItem = true) - so opening
 *                       the dropdown and clicking "Connect MEP Elements Settings" once made Settings
 *                       the new default action, and the next plain click ran Settings again instead
 *                       of connecting. AddSmartConnectTool() was missing the
 *                       IsSynchronizedWithCurrentItem = false configuration that the Opening panel's
 *                       "Create Openings" split button already has for exactly this reason. The main
 *                       face is now permanently pinned to "Connect MEP Elements" regardless of which
 *                       child was run last, matching the Opening tool's pattern precisely.
 * v1.47.6 (2026-08-16) - Connect MEP Elements: cleanup pass Ajmal asked for after the two 1.47.5
 *                       removals - "check entirely we remove something, is there anything related
 *                       with that removed feature settings... if any settings only work if that
 *                       removed feature exists, remove it too." Found and removed: ElementPair.Distance
 *                       (SmartConnectCommand.cs) - write-only, always passed 0, only ever read by the
 *                       nearest-pairing sort that 1.47.5 deleted. ShowSummary's extraNotes parameter /
 *                       2-arg overload (SmartConnectCommand.cs) - existed only for the "N elements
 *                       left unpaired" batch message, always null now that both callers pass a single
 *                       ConnectionOutcome. TryGetBestOpenConnectorPair, AreDomainsCompatible and
 *                       ComputeOrientationPenalty (SmartConnectConnectorUtils.cs) - zero callers
 *                       anywhere, leftover from before the 1.47.0 route-builder rewrite, not directly
 *                       caused by 1.47.5 but the same class of problem so removed in the same pass.
 *                       Also corrected: a code comment illustrating text-wrapping still quoted the
 *                       deleted "Neither - leave both alone" option string; two file-header
 *                       descriptions still said "batch" for a setting that no longer batches anything.
 * v1.47.5 (2026-08-16) - Connect MEP Elements simplified twice at Ajmal's request, both times
 *                       removing a redundant/confusing mechanism rather than just hiding it.
 *                       (1) Removed the nearest-open-end auto-pairing algorithm for a big selection
 *                       (BuildNearestPairs, ClosestDistance, MaxPairDistanceMm, SingleUndoForBatch).
 *                       Selecting exactly two elements now connects them directly - no matching
 *                       involved; selecting more than two asks the user to narrow it down instead of
 *                       guessing pairs. (2) Removed SmartConnectMoveMode.None ("Neither - leave both
 *                       alone") from "Which pipe is allowed to move." It duplicated the "Never touch
 *                       the picked pipes" routing mode via a second, unrelated setting - the two
 *                       could disagree, and in fact did during live testing (RoutingMode said
 *                       Automatic, MoveMode said Neither, the tool followed MoveMode and looked
 *                       broken). An older settings file with the removed value 3 falls back to
 *                       "Both" automatically via the existing Enum.IsDefined guard in Sanitize() -
 *                       no migration code needed.
 * v1.47.4 (2026-08-15) - Fixed the real bug Ajmal caught by testing live: two straight ducts (or
 *                       pipes) dead in line with a gap between them were always bridged with a
 *                       brand new third piece, even when both picked elements were free to move.
 *                       TryPlanParallelPair's Inline branch hard-coded FirstShift/SecondShift = 0 and
 *                       NeedsMiddleSegment = true regardless of the move-mode setting, so the tool
 *                       never used its own permission to stretch. It now shares the gap via
 *                       TryDistributeShift exactly like the offset crank already did, stretching the
 *                       real duct(s) the user picked and joining directly with no extra element -
 *                       one changed duct instead of one new one, or none changed and both extended
 *                       to meet in the middle. A brand new bridging piece is now created only when
 *                       neither picked element is allowed to move (equipment, flex, or "Never touch
 *                       the picked pipes"), where a real gap genuinely has to be filled.
 * v1.47.3 (2026-08-15) - Connect MEP Elements settings window: the two Bend angle presets (90/45)
 *                       now sit on one line, with a single shared line underneath explaining the
 *                       trade-off instead of one long sentence per option. Also fixed a latent UI
 *                       bug found while checking it: a plain-string RadioButton/CheckBox Content
 *                       does not wrap in WPF - it silently runs past the edge of the window instead
 *                       - which mattered here because the window is resizable down to 500px wide.
 *                       Every option in the window now wraps gracefully if it is ever long enough
 *                       to need it. No behaviour change.
 * v1.47.2 (2026-08-15) - Connect MEP Elements audit pass: 27 findings raised by a six-dimension
 *                       multi-agent review, 13 confirmed after adversarial verification, all fixed.
 *                       Worst was a sign error in the offset planner: the shift option was chosen by
 *                       smallest travel, but the bend angle depends on the SIGN of
 *                       (axisOffset - totalShift), so every crank whose open ends had already passed
 *                       each other was built at the supplement (180 - angle) and folded back over
 *                       the run it left, while still reporting the requested angle. Also: skew plans
 *                       now screen deflection and travel (the closest-approach solve is
 *                       ill-conditioned within 2.6 degrees of parallel and dragged runs enormous
 *                       distances); flex could never join rigid because AreCompatible compared raw
 *                       categories, making the whole flex path unreachable; every in-line route
 *                       falsely reported "built at 180 degrees instead of your angle"; FallbackAngles
 *                       was sorted (destroying the try-order) and dropped on every save; interactive
 *                       failures were reported twice; batch pairing re-read connectors O(n^2) times
 *                       and silently ignored unpaired elements.
 * v1.47.1 (2026-08-15) - Connect MEP Elements settings window reorganised. Ajmal found 17 controls on
 *                       one page unreadable ("very difficult to understand what is what"). Only the
 *                       four real day-to-day choices stay on the main page - bend angle, which pipe
 *                       may move, batch pairing distance, clash warning. The rest moved into a
 *                       collapsed "advanced" block, nothing removed, each control gaining a
 *                       plain-English hint line. Settings behaviour and persistence unchanged.
 * v1.47.0 (2026-08-15) - Connect MEP Elements rebuilt, and its settings split onto their own ribbon
 *                       button. The tool no longer opens a dialog on every click: it loads the saved
 *                       settings and goes straight to work. Two defects fixed - the "Offset + 2
 *                       Elbows" routing mode was dead (SmartConnectSettingsService.Sanitize forced
 *                       SingleElbow on every load and save), and custom angles above ~92 degrees
 *                       could be saved and selected but were rejected by the route builder on every
 *                       pick (angle range now honestly capped at 5-90, older files clamped on load).
 *                       New: one geometry planner covering in-line, corner, offset and skewed ends -
 *                       previously only exactly parallel, facing ends were accepted; Conduit, Flex
 *                       Duct/Pipe and connector-bearing family instances (equipment, air terminals,
 *                       fittings, accessories) are now pickable; batch connect from a pre-selection
 *                       with greedy nearest open-end pairing; control over which element may be
 *                       trimmed; angle fallback instead of outright failure; optional single undo for
 *                       a whole batch; one end-of-run summary in place of a popup per failure;
 *                       insulation, lining, Comments, Mark and Workset copied onto new pieces;
 *                       automatic transition on size mismatch; optional clash warning. Adds
 *                       CmdSmartConnectSettings and SmartConnectRoutePlan.
 * v1.46.0 (2026-08-15) - NEW: "A separate row for each run, stacked" - a third chain style, from
 *                       Ajmal's own sketch. Two ducts going back to one wall can now read as two
 *                       independent dimensions, one under the other (924/300, then 3090/300, each
 *                       measured from the wall), instead of a single chain where the second duct is
 *                       measured from the first. Adds DimensionChainStyle.RowPerRun, a "Gap between
 *                       stacked rows" setting (paper mm, default 8, scaled by the view), and
 *                       MepDimensionCollector.AddRowPerRun. Rows stack outward from the reference, so
 *                       the nearest run takes the first row.
 *                       RIBBON: the "Auto Dimension" panel is merged into "Dimensions" at Ajmal's
 *                       request - one panel, Auto MEP Dimension as the large button beside Automatic
 *                       Dimension / Quick Dimension / Copy Dimension Text. No tool moved or changed.
 *                       UI: the six service tick boxes now sit on ONE line (UniformGrid, 6 columns)
 *                       instead of a WrapPanel breaking them 4 + 2.
 * v1.45.2 (2026-08-15) - "How the dimensions are drawn" fixed. Ajmal reported that of that whole
 *                       section only "Include each run's own width" behaved; the rest did nothing
 *                       useful. Settings were saving correctly (verified in the JSON), so all four were
 *                       runtime faults:
 *                       (1) OVERHANG could never work and is now REMOVED from both settings windows.
 *                       NewDimension reads the supplied line for position and direction ONLY, then draws
 *                       the dimension between the references itself - extra line length is discarded.
 *                       The extension past the last witness line is a Dimension Type property in Revit.
 *                       The control was a dead input; the window now says where the real setting lives.
 *                       MepDimensionSettings.PaddingMm is kept, unused, so existing files still load.
 *                       (2) SEPARATE-SEGMENTS produced overlapping dimensions: every segment's line was
 *                       padded at BOTH ends, so each piece ran into its neighbours by twice the padding.
 *                       Segments now span exactly their two references and sit end to end.
 *                       (3) BOTH SIDES slid each chain along the run by ~6 mm x scale to keep them apart
 *                       - solving a collision that cannot happen, since the two chains extend in
 *                       OPPOSITE directions from the run. Removed, so both now sit on one continuous
 *                       line. It also drew the run's width twice, once per side, when Include width was
 *                       on; the width now belongs to one side only.
 *                       (4) DIMENSION TYPE could offer an empty list: types were filtered on
 *                       DimensionStyleType.Linear/LinearFixed with no fallback, so anything StyleType did
 *                       not report as linear left the dropdown with only "(project default)". It now
 *                       falls back to every dimension type rather than showing nothing.
 *                       ResolveSideOffsetFeet and the padding arguments are gone with them.
 * v1.45.1 (2026-08-15) - No success popup. Ajmal ran 1.45.0 live (6 dimensions across 6 linked models,
 *                       first confirmed live run of the rebuilt tool) and asked for the report popup to
 *                       go. Both dimension toolsets now finish SILENTLY when they dimensioned everything
 *                       asked of them - the dimensions on screen are the result. The report still appears
 *                       when nothing was created (there is nothing to look at otherwise) or when
 *                       something was skipped or refused, which is the case the report existed for.
 *                       Also dropped the per-link "Read linked model: X" lines from a successful report -
 *                       six lines of noise - keeping them only when nothing was created, where they
 *                       explain where the tool looked. The linked-dimension caveat now lives only in the
 *                       settings windows, where links are switched on, rather than after every run.
 *                       New DimensionRunReport.HasUnfinishedWork gates the popup; the ShowReport setting
 *                       and both checkbox labels were reworded to match what it now does.
 *                       This restores the house no-success-popup rule that v1.45.0 broke while fixing the
 *                       opposite defect (a report built and never shown at all).
 * v1.45.0 (2026-08-15) - Both dimension toolsets rebuilt on a shared engine, with settings windows.
 *                       Auto Duct Dimension is now AUTO MEP DIMENSION: ducts, flex ducts, pipes, flex
 *                       pipes, cable tray and conduit; measured to walls, structural columns, beams,
 *                       architectural columns, floors, grids, levels and other runs; each reference
 *                       target independently set to read from this model, from loaded Revit links, or
 *                       both. Three modes now (pick runs / selected runs / whole view), sections and
 *                       elevations as well as plans, one continuous string or separate segments,
 *                       both-sides mode, and the run report is finally SHOWN - it was built and
 *                       discarded before, so batch runs finished silently.
 *                       Automatic Dimension (grids/levels) rewritten: both-sides placement, per-row
 *                       dimension types, settings-driven offsets, name filters, story-levels-only,
 *                       linked grids and levels, duplicate protection, and it no longer refuses to run
 *                       on an uncropped view - it measures from the datums instead.
 *                       Defects fixed: dimensions to LINKED elements needed the stable-representation
 *                       rewrite (CreateLinkReference alone throws "not geometric references");
 *                       Coarse views found no solids because the model-geometry fallback only ran on a
 *                       null result; face positions came from PlanarFace.Origin, which can sit outside
 *                       the face; one failing edge discarded a whole face's tessellation; the stable-key
 *                       fallback collapsed every face on an element to one key; the search reach was
 *                       read from a stale CropBox when the crop was off; round pipes and conduit have
 *                       no flat faces and produced nothing (they now use their centreline); a two-grid
 *                       view produced two identical stacked dimensions; and one reference Revit refused
 *                       aborted an entire run. Element identity is now (link instance, element) rather
 *                       than a bare id, which repeats across documents.
 *                       Each run is now ONE undo step (TransactionGroup + Assimilate) instead of one
 *                       per dimension.
 *                       New: src/Models/Dimensioning/, src/Services/Dimensioning/,
 *                       src/Services/MepReferenceDimension/, src/UI/Dimensioning/ (2 settings windows),
 *                       CmdDimensionSettings.cs, MepReferenceDimensionCommand.cs.
 *                       Removed: src/Services/DuctReferenceDimension/ (6 files) and
 *                       DuctReferenceDimensionCommand.cs, replaced not disabled.
 *                       Touched: AutoDimensionService.cs v2.0.0, CmdAutoDimensions.cs v2.0.0,
 *                       AnnotationRibbonManager.cs v1.5.0, SelectionFilters.cs (comment only).
 *                       A multi-agent adversarial review of the new code raised 27 candidate defects;
 *                       22 survived refutation and all 22 were fixed before release. The ones that
 *                       would have been visible to a modeller: a grid or level was accepted as the
 *                       "nearest reference" without checking it runs square to the measurement, so half
 *                       the grids in a normal grid system beat the real wall and Revit then rejected the
 *                       whole chain; round runs still could not be dimensioned because a centreline is
 *                       NON-VISIBLE geometry and needs IncludeNonVisibleObjects (new Reference(element)
 *                       is valid only for datums, never for a pipe); a longer chain was discarded as a
 *                       "duplicate" of a shorter one and its runs then retired, so which ducts got
 *                       dimensioned depended on element id order; skipping an already-dimensioned grid
 *                       removed it from the middle of the chain instead of skipping the row, printing
 *                       6000/12000/6000 across an undimensioned grid; linked grids were classified from
 *                       their flat plan curve so no linked grid ever dimensioned correctly in a section;
 *                       linked datums were pulled from the whole link document with no crop culling,
 *                       giving a 400 m dimension on a 30 m view; two runs stacked at the same position
 *                       produced a zero-length segment that killed the row; and opening either settings
 *                       window in a project lacking the saved dimension type silently erased that
 *                       setting for every other project, since the store is one shared user-level file.
 *                       Built clean on Release (2020) and Release R25, zero warnings. Both settings
 *                       windows were parsed, laid out and resource-checked outside Revit.
 *                       NOT loaded in Revit - no tool here has been run against a real model.
 * v1.44.0 (2026-08-13) - REMOVED: Web Panel, at Ajmal's instruction. The ribbon button, the local
 *                       HTTP server, the served page and the tool registry are all gone -
 *                       src/WebPanel/ deleted outright rather than switched off, so there is no
 *                       setting to find and nothing left listening. Same treatment as the spoken
 *                       voice in v1.42.0.
 *                       Kept deliberately: UnhideAllService. It was split out of CmdUnhideAll to
 *                       give the panel a UI-free entry point, but UI-free model work stands on its
 *                       own merit and the ribbon button runs through it unchanged.
 *                       Kept as findings, not as design: the measured notes in
 *                       .claude/knowledge/ajtools-conventions.md about localhost HttpListener
 *                       needing no admin rights, names-not-code, and token+Origin defences. The
 *                       admin-rights one corrected a claim this repo had written down wrongly, so it
 *                       outlives the feature that produced it.
 *                       The AJ AI bridge (named pipe, McpBridgeService) is a DIFFERENT feature and
 *                       is untouched.
 * v1.43.1 (2026-08-13) - FIX: the installer could not install Revit 2025/2026/2027 at all. It
 *                       refused them by a hardcoded list and installed the root 2020 net472 build to
 *                       2020-2024, never reading Payload\<year>\ - a guard left from before the
 *                       multi-version backbone (2026-07-06). Every zip since had shipped correct
 *                       per-version builds the installer ignored, so installing the documented way
 *                       left 2025-2027 with nothing while INSTALL.md advertised 2020-2027.
 *                       Installable versions now come from what Payload\ actually contains, so the
 *                       list cannot go stale again; a legacy package with no Payload still installs
 *                       from the root files.
 *                       Also: the installer no longer deletes a working install before the
 *                       replacement is in place (a locked folder is renamed aside), and uninstall
 *                       clears the set-aside and timestamped payload folders it used to leave behind.
 *                       No change to any tool's behaviour - packaging and install only.
 * v1.43.0 (2026-08-12) - NEW: Web Panel. A ribbon button starts a local HTTP server on
 *                       http://localhost:<port>/ and opens a browser page carrying AJ Tools buttons;
 *                       clicking one runs the tool on the live model and shows the result on the page
 *                       instead of in a Revit popup. First tool wired: Unhide All.
 *                       Three decisions worth keeping:
 *                       (1) NAMES, NOT CODE. The browser sends a tool id from a fixed registry in
 *                           WebPanelToolRunner. It cannot send C#, so a hostile page can at worst
 *                           trigger something already on the ribbon. Deliberately narrower than the AJ
 *                           AI bridge, which does accept code. Widening this to downloaded tool code
 *                           needs a signature check designed first.
 *                       (2) MEASURED, NOT ASSUMED. McpBridgeService's header claimed an HTTP loopback
 *                           listener needs admin rights or a URL ACL reservation, which is why the
 *                           named pipe was chosen. Tested on this machine as a non-admin user:
 *                           "http://localhost:5599/" starts fine; only the "+" wildcard is denied. The
 *                           note has been corrected in place. No admin, no firewall prompt.
 *                       (3) ONE LOGIC, TWO FRONT DOORS. CmdUnhideAll's model work moved to
 *                           Services/UnhideAll/UnhideAllService.cs, which returns its report instead of
 *                           showing it. The ribbon turns that into a TaskDialog; the panel returns it
 *                           to the browser. Ribbon behaviour is unchanged.
 *                       Nothing listens until the button is clicked, the port is localhost-only, and
 *                       every request needs a per-session token plus an Origin check.
 *                       LIVE-TESTED 2026-08-12 against Revit 2020 (model "Project1", view "1 - Mech"):
 *                       the button starts the server on 48210 and opens the browser; /api/context
 *                       returned real session data through the ExternalEvent path; a wrong token got
 *                       401 and a request carrying a foreign Origin got 403; and Ajmal confirmed
 *                       Unhide All run from the browser works on the live model.
 * v1.42.1 (2026-08-11) - Transfer Legends / Transfer Drafting Views produced EMPTY views in the target
 *                       project. Ajmal's report: "its creating the view in anothonr model but inside
 *                       that drafting view its not creating". Exactly right, and the cause is a Revit
 *                       API behaviour rather than anything wrong with the tool's own logic.
 *                       ROOT CAUSE: the document-to-document overload of
 *                       ElementTransformUtils.CopyElements copies the view SHELL ONLY. It does not carry
 *                       the detail lines, text notes, filled regions or legend components drawn inside
 *                       the view. Nothing about the call reports this - it succeeds, returns an id, and
 *                       the view appears in the target Project Browser looking correct until it is
 *                       opened. So the tool showed "1 drafting view transferred" and was, on its own
 *                       terms, telling the truth.
 *                       MEASURED LIVE, not reasoned about, on Ajmal's own two open models (Revit 2020):
 *                       a 131-element drafting view copied with the exact call the tool makes returned
 *                       ONE element id and read back in the target holding a single element - its
 *                       internal ExtentElem. All 130 real items were left behind.
 *                       FIX (TransferViewsCommandRunner v1.1.0): legends and drafting views are now
 *                       copied in two passes. Pass 1 copies the view on its own - one view per call, so
 *                       the returned id can be paired back to the source view it came from, which a bulk
 *                       call does not guarantee. Pass 2 collects everything drawn in the source view and
 *                       copies it into the newly created view with the VIEW-TO-VIEW CopyElements
 *                       overload, which is the one that actually carries view-specific elements.
 *                       ExtentElem is excluded (it has no category and is recreated with the view).
 *                       Both passes verified live in a SINGLE transaction - the second pass does not
 *                       need a Regenerate() to see the view the first pass just created - and the copy
 *                       read back at 131/131 against the source. Every live test was reversed with
 *                       Revit's native Undo afterwards; both of Ajmal's models were confirmed back at
 *                       their exact starting contents.
 *                       SCHEDULES DELIBERATELY UNCHANGED and still use the single bulk copy: a
 *                       ViewSchedule's rows are generated from the target model's own elements, so there
 *                       is nothing drawn inside one to leave behind. Transfer Schedules never had this
 *                       bug.
 *                       Also added: the report now states how many items were copied inside the views,
 *                       and a content-warning section, so an empty result can never again look like a
 *                       success. A view whose contents fail does not fail the whole transfer, matching
 *                       how sheet-placement failures are already handled.
 *                       LEGENDS ARE FIXED BY THE SAME CODE PATH but were NOT live-verified: neither
 *                       open model contains a single legend view, and the Revit API cannot create one to
 *                       test with. The defect and the fix are shared - both kinds ran through the same
 *                       one bulk call, and both now run through the two-pass copy - so this is a
 *                       code-level conclusion, not a measured one. Ajmal re-tests legends on a model
 *                       that has them.
 * v1.42.0 (2026-08-11) - AJ AI Voice (Revit side) added and REMOVED again on the same day. Recorded
 *                       here because it briefly existed in a build and because it never should have
 *                       reached one silently: AiVoiceService shipped on 2026-08-11 with no suite bump
 *                       and no entry in this file at all - grepping it for "voice" returned nothing.
 *                       A capability reached the ribbon with no record that it existed, which is the
 *                       exact failure this changelog exists to prevent.
 *                       What it did: spoke a one-line confirmation of what each AJ AI Bridge request
 *                       returned, in a second voice, so a running job could be followed by ear.
 *                       Why it is gone: the AJ AI Brain's own voice already announces every job
 *                       before it runs and reads the answer at the end, so this was a second speaker
 *                       confirming news Ajmal had just been given - "totally remove that female voice
 *                       feature, only men voice ... remove everything, even the code also related to
 *                       this." An off-by-default toggle was built first and he asked for removal
 *                       instead, which was the better call: a feature nobody wants is not improved by
 *                       making it optional, it just leaves dead code and a switch to explain.
 *                       AiVoiceService.cs is deleted and McpBridgeService (v1.10.0) no longer calls
 *                       it. Nothing else changed - the bridge, the activity banner, the audit log,
 *                       the safety validator and every model operation are untouched.
 * v1.41.0 (2026-08-05) - NVIDIA NIM added as a FOURTH AI provider in the "C#" shell, on Ajmal's
 *                       request: he wants the free build.nvidia.com catalog to cut API cost and to
 *                       try specific open models. New NvidiaApiService v1.0.0; default model
 *                       z-ai/glm-5.2. Minor bump - new capability, nothing existing changed.
 *                       PURELY ADDITIVE: SelectedProvider still defaults to "Gemini" and Gemini/
 *                       OpenAI/Claude are untouched, so the pane behaves exactly as before until the
 *                       provider dropdown is changed. ErrorCorrectionService needed no change either
 *                       - it uses whichever IAiProviderService it is handed, so it follows
 *                       GetActiveService() automatically.
 *                       NIM is OpenAI-wire-compatible, so the service is the OpenAiApiService shape
 *                       with a different address. FOUR SETTINGS DIFFER, all because the default model
 *                       is a REASONING model (glm-5.2, 753B) rather than a straight chat model, and
 *                       all four were verified against NVIDIA's own published sample rather than
 *                       assumed: (1) its OWN HttpClient at 180s, not the 60s the other three share -
 *                       a reasoning model can genuinely exceed a minute and the shared timeout
 *                       surfaced as an error that reads like a bad key; (2) max_tokens 16384, because
 *                       reasoning tokens come out of the SAME budget as the answer, so a low cap
 *                       truncates the generated script after the model has spent the allowance
 *                       thinking; (3) temperature/top_p 1, NOT OpenAiApiService's 0.2 - clamping a
 *                       reasoning model degrades its chain of thought, and 1/1 is what NVIDIA ships;
 *                       (4) NO seed, deliberately dropping the seed=42 in NVIDIA's sample, because a
 *                       fixed seed makes a retry return the IDENTICAL broken script and the auto-fix
 *                       retry loop depends on a retry being a fresh attempt.
 *                       Also handled: HttpClient reports its own timeout as TaskCanceledException,
 *                       indistinguishable from the user pressing Stop unless the token is checked -
 *                       so a timeout would have been reported as "you cancelled this". And a reply
 *                       that finishes with finish_reason "length" and no content now raises a plain
 *                       "used its whole budget on reasoning" message instead of returning empty and
 *                       failing later as a confusing compile error.
 *                       CAUGHT BEFORE IT SHIPPED, by checking the style rather than trusting it: the
 *                       model picker was designed as one editable ComboBox, but SoftComboBoxStyle
 *                       replaces the ComboBox ControlTemplate and that template has NO
 *                       PART_EditableTextBox - IsEditable="True" would have rendered a control with
 *                       nothing to type into, killing the one feature (paste any model id) it existed
 *                       for. Patching the shared dictionary was rejected: the docked pane merges it
 *                       during Revit's OnStartup, where a fault takes the whole add-in down (v1.16.0).
 *                       Shipped as a shortlist ComboBox plus a plain TextBox, both bound TwoWay to
 *                       NvidiaModel - same result, no shared-style risk.
 *                       Model ids in the shortlist were confirmed against live sources, not recalled:
 *                       z-ai/glm-5.2, qwen/qwen3-coder-480b-a35b-instruct, deepseek-ai/deepseek-v3.1,
 *                       nvidia/llama-3.3-nemotron-super-49b-v1.5. Others that get named around (Kimi
 *                       K2, Mistral Large 3) were left out precisely because their exact id strings
 *                       were NOT verified - a wrong id is a 404 with no useful message.
 *                       STREAMING DEFERRED, not forgotten: NVIDIA's sample uses stream=True, but that
 *                       only changes delivery, not content, and IAiProviderService is a single-reply
 *                       contract shared by all four providers. Ajmal chose to try it without
 *                       streaming first and see whether the wait is actually a problem.
 *                       See NvidiaApiService v1.0.0, AiShellViewModel v1.13.0, SettingsWindow
 *                       v1.2.0 / .xaml.cs v1.2.0.
 * v1.40.6 (2026-08-05) - Game Mode SELECTOR camera-whip fix (GameHudWindow v1.9.5). Reported symptom:
 *                       shooting anything with the selection gun sends the camera flying/jumping.
 *                       Root cause was input, not physics. The selector is the only weapon that
 *                       changes the Revit SELECTION, and that makes Revit re-lay-out its own chrome
 *                       (Options Bar appears/disappears, contextual "Modify | ..." tab swaps in),
 *                       which moves the game view's window rectangle. The HUD follows that rectangle
 *                       in OnTick -> ApplyPixelRect(remember: true), which updated _full* - and the
 *                       mouse-look centre is derived from _full*. The pointer was still parked on
 *                       the OLD centre from the previous SetCursorPos, so the next mouse move read
 *                       (old centre - new centre) as genuine aiming and applied it as yaw/pitch at
 *                       0.15 deg/px. Nothing anywhere re-centred the pointer when the rectangle
 *                       moved. FIX: ApplyPixelRect now re-centres the pointer whenever a remembered
 *                       rectangle moves the centre while the look is active; StartLook centres
 *                       before arming _mouseLookActive; OnGameMouseMove drops (and re-centres on)
 *                       any single step beyond 400 px as a backstop for DPI/remote-session cursor
 *                       warps. Physics ruled out first: the engine's dt is already clamped to
 *                       0.25 s, so a slow frame cannot rocket the player. The ZoomToFit re-fit on
 *                       rectangle change (v1.38.2's aim/display sync) was left alone deliberately -
 *                       removing it would bring back the shots-land-beside-the-crosshair bug.
 *                       Builds clean on Release (2020/net472) and Release R25. NOT loaded in Revit
 *                       by the assistant - Ajmal verifies on screen.
 * v1.40.5 (2026-08-05) - The last uncovered UI surface, found by enumerating them rather than trusting
 *                       the running list. Every AJ Tools UI surface is now accounted for:
 *                       35 XAML windows (33 with entrance+exit motion; AboutWindow has its own staged
 *                       pair; GameHudWindow excluded as a real-time overlay), 1 dockable UserControl
 *                       (AiShellView - styled, no entrance by design), and 2 windows built purely in C#
 *                       with no XAML. Those last two are the ones a .xaml sweep silently misses:
 *                       AiTaskWarningBarService's banner already animated, but BridgeStatusToast had
 *                       NO motion at all - it popped onto the screen and vanished.
 *                       It now fades in over 180ms and out over 220ms (EaseOut in, EaseIn out, matching
 *                       the suite). This one animates Window.Opacity DIRECTLY, which is correct here and
 *                       wrong almost everywhere else: it sets AllowsTransparency = true, so the layered
 *                       window makes Window.Opacity real. Most AJ Tools windows do not, which is why
 *                       WindowMotionHelper animates the root content element instead.
 *                       A backstop DispatcherTimer is armed BEFORE the fade, so the toast definitely
 *                       goes away even if the animation never completes - same rule as the window exit.
 *                       Simpler than the window case on purpose: nothing else owns this toast's lifetime
 *                       and the user cannot click it, so there is no DialogResult and no veto to
 *                       preserve, only the guarantee that it disappears.
 *                       Also confirmed while enumerating: zero WinForms UI remains anywhere in src/.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.40.4 (2026-08-05) - Fixes found by an independent audit of the v1.39.2 -> v1.40.3 UI pass. Ten
 *                       defects were confirmed (four further claims were refuted on inspection); three
 *                       were code, the rest were false statements in this project's own notes.
 *                       1. REAL BUG, introduced by v1.40.1's progress reporting. ProgressReporter's own
 *                       comment claimed the Render-priority pump means "the user cannot click a button
 *                       half way through a delete loop". THAT WAS WRONG. Measured both halves:
 *                       (a) a DispatcherOperation queued at Input priority does NOT run during the loop
 *                       - that half was right; (b) Dispatcher.Invoke on the calling thread waits by
 *                       pushing a nested dispatcher frame, which runs a REAL Win32 message loop - so a
 *                       posted WM_CLOSE was observed firing the window's Closing DURING the scan loop.
 *                       So the title-bar X and Esc could close a purge window mid-scan while the scan
 *                       carried on running underneath it. Disabling the buttons never covered those two,
 *                       because neither goes through a button.
 *                       FIX: both purge windows now refuse to close while busy (Closing guarded on an
 *                       _isBusy flag, subscribed BEFORE the motion helpers so its veto is already set).
 *                       2. LATENT BUG THAT EXPOSED, in WindowMotionHelper.AttachStandardExit: it did not
 *                       check e.Cancel, so if ANY other handler vetoed a close - a busy guard, an
 *                       unsaved-changes prompt, a validation refusal - the exit animation would play and
 *                       then force the window shut anyway, overriding the veto. Now returns early when
 *                       the close is already cancelled. This was latent before the purge guard existed.
 *                       3. ACCESSIBILITY REGRESSION from v1.40.2/1.40.3: ModernListCheckBox and
 *                       ToggleSwitchCheckBox inherit FocusVisualStyle="{x:Null}" from ModernCheckBox but
 *                       had no focus ring of their own, so five controls had NO keyboard-focus marker at
 *                       all. Nulling the OS focus rectangle obliges the template to draw its own; both
 *                       now do.
 *                       4. MISSED CONTROL: Linked Search's model picker was a bare ToggleButton with no
 *                       template - the last raw Windows chrome in the suite, and its label went barely
 *                       readable while the dropdown was open because the default checked state repaints
 *                       it. New shared ModernDropdownToggle never touches Foreground/Background, so the
 *                       label stays put in both states and "open" shows as an accent ring.
 *                       5. FALSE NOTES CORRECTED (they would have misled the next session):
 *                       - The claim that GameHudWindow "has every element IsHitTestVisible=False, a pure
 *                         non-interactive overlay" was backwards. RootGrid's Background="#01000000" is
 *                         1/255 alpha precisely SO IT CAPTURES every click and key, and PauseLayer is a
 *                         click-to-resume surface. Only "no <Button" was true. The exclusion still
 *                         stands, but on the frame-budget reason. Corrected here and in conventions.md.
 *                       - "Zero GetTemplateChild/Template.FindName in src/" was made false the same day
 *                         by TabMotionHelper looking up PART_SelectedContentHost. Corrected.
 *                       - "All four standalone windows declare their own MotionEaseOut" - Game HUD does
 *                         not; it was never touched. Corrected.
 *                       - Style counts disagreed between files (24 vs 33, 28 vs 23). The brittle exact
 *                         counts are gone; the scripts report the real number when run.
 *                       - README.md still advertised 1.39.1, ten versions behind. Updated.
 *                       tools/verify-exit-motion.ps1 gains a permanent case proving a busy window can
 *                       veto its own close and that the exit helper respects it.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.40.3 (2026-08-05) - Closes out the UI motion and polish pass. NO tool behaviour changed anywhere.
 *                       LIST TICK BOXES: new ModernListCheckBox (ModernStyles v1.5.0) - the same house
 *                       box, but with an INSTANT tick - applied to the two list checkboxes that were
 *                       still raw Windows chrome (View Crop target views, Section Mark views). The tick
 *                       is instant there on purpose: those rows are created and recycled by a
 *                       virtualized list, so an animated tick would replay every time a row scrolled
 *                       into view. Hover/press/focus still animate - those fire on user action, not on
 *                       materialization.
 *                       PROGRESS: Purge Unplaced Views' scan now reports progress too, same shape as
 *                       Purge Unused Elements in v1.40.1 - an OPTIONAL Action<int,int> on Scan() that
 *                       defaults to null, so existing callers are untouched, wrapped in try/catch so a
 *                       reporting fault can never abort a scan. These are the two services that trial
 *                       -delete every candidate inside a rolled-back transaction, i.e. the only genuine
 *                       multi-second freezes in the suite.
 *                       GRAPHICS OVERRIDE TEXT BOXES: the last field in the project that did not light
 *                       up. It had no Template at all, so its focus edge was an instant BorderBrush
 *                       swap; now a fading hover ring and focus ring like every other field. Padding
 *                       still drives the content inset, so anything sitting over those boxes is
 *                       unmoved.
 *                       DELIBERATELY NOT DONE, and these are decisions rather than omissions:
 *                       (a) Show/hide panel transitions (Reassign Level's scope toggle, View Crop
 *                       options) - those windows are SizeToContent, so animating a panel in or out
 *                       makes the whole window resize mid-animation. Fixing that means giving them
 *                       fixed sizes, which touches resizing - Ajmal's one hard constraint.
 *                       (b) An entrance for the AJ AI docked pane. AiShellPaneProvider constructs
 *                       AiShellView unconditionally during Revit's OnStartup, so a fault there takes
 *                       the WHOLE add-in down, not just that pane - it already did once, in v1.16.0.
 *                       A fade on a docked panel is not worth touching the highest-blast-radius file
 *                       in the project for.
 *                       (c) The Duct Standards grid tick column - DataGridCheckBoxColumn generates its
 *                       own checkbox with its own editing flow, and it is one column in one window.
 *                       Verified: every shared style builds (tools/verify-wpf-styles.ps1 now covers the
 *                       new list variant), plus the window-local, exit-motion and tab-motion checks.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.40.2 (2026-08-05) - Tick boxes, radio buttons and the toggle switch finally templated
 *                       (UI/ModernStyles.xaml v1.4.0). These three were setter-only since v1.0, so every
 *                       tick box in the suite drew RAW WINDOWS CHROME inside an otherwise soft, rounded,
 *                       Neon Blue UI - the last big visual inconsistency left, and roughly 90 controls
 *                       across 21 windows.
 *                       NOW: an 18px rounded box whose accent fill and tick fade+scale in; a matching
 *                       radio with a dot that pops; and ToggleSwitchCheckBox drawn as the switch its
 *                       name has always claimed - knob sliding 20px across a 40px track - matching the
 *                       switch in Graphics Settings Manager. All three gain hover ring, press dip,
 *                       keyboard-focus ring and disabled fade, on the suite timings.
 *                       LAYOUT UNCHANGED: every original Setter (Foreground/VerticalAlignment/FontSize/
 *                       Margin) is kept, so nothing moves on any window.
 *                       LEFT KEYED, NOT IMPLICIT, ON PURPOSE: an implicit TargetType="CheckBox" style
 *                       would also capture the CheckBox that DataGridCheckBoxColumn generates (Duct
 *                       Standards has one), where an animated tick would replay every time a row
 *                       scrolled into view. Same rule as list selection and the Graphics Override
 *                       category list - animate what the user changed, not what merely appeared.
 *                       CHECKED FIRST: nothing in the project uses IsThreeState or sets IsChecked to
 *                       null, so there is no indeterminate state the new templates could fail to draw.
 *                       Verified: all three build their templates and resolve every resource
 *                       (tools/verify-wpf-styles.ps1, now covering them).
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.40.1 (2026-08-05) - Real progress reporting, starting with the slowest tool. New shared
 *                       Helpers/ProgressReporter.cs v1.0.0, wired into Purge Unused Elements' scan.
 *                       WHAT IT IS NOT: the Revit work has NOT been moved to a background thread. The
 *                       Revit API must be called on Revit's own UI thread, so a worker thread is not an
 *                       option - it throws or corrupts the document. The scan runs exactly where and how
 *                       it did; this only makes the window repaint part-way through instead of sitting
 *                       frozen behind an hourglass. Same elements checked, same conclusions, same result.
 *                       HOW: setting ProgressBar.Value inside a UI-thread loop changes the number but
 *                       paints nothing, because the loop starves WPF's render pass. So after updating
 *                       the values the reporter pumps the dispatcher with an empty Invoke at
 *                       DispatcherPriority.Render - this project's already-established technique.
 *                       Render priority is chosen deliberately: Input priority sits BELOW Render, so the
 *                       pump repaints WITHOUT processing clicks or keystrokes, and the user cannot
 *                       re-enter the loop by clicking a button half way through a delete. A DoEvents
 *                       -style pump at Input priority would allow exactly that.
 *                       Throttled to one repaint per 33ms (~30/sec), with the first and last always
 *                       painted so the bar visibly starts empty and finishes full. Measured: 500 reports
 *                       cost 86ms in total, against one trial-delete transaction PER ITEM - noise.
 *                       WHY THIS TOOL FIRST: UnusedElementPurgeService.Scan() trial-deletes every
 *                       candidate inside a rolled-back transaction, so on a busy model it is the longest
 *                       silent freeze in the suite.
 *                       BEHAVIOUR-SAFE BY CONSTRUCTION: Scan() gained an OPTIONAL Action<int,int>
 *                       callback defaulting to null, so every existing caller compiles and behaves
 *                       unchanged; the callback is wrapped in try/catch so a reporting fault can never
 *                       abort a scan; and the progress row is Collapsed when idle, so it takes no space.
 *                       The two new rows were added INSIDE the existing button grid, not to the window's
 *                       root Grid, which would have shifted every Grid.Row index below it.
 *                       Verified: reporter reaches the exact final value despite throttling, hides
 *                       afterwards, and survives a zero-item scan without dividing by zero.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.40.0 (2026-08-05) - Exit animations, on Ajmal's explicit go-ahead. Content fades out over 150ms
 *                       while sinking 6px, CubicEase EaseIn - exits accelerate away where entrances
 *                       decelerate in, and the exit is deliberately shorter than the 220/280ms entrance.
 *                       WindowMotionHelper v1.1.0 (AttachStandardExit), wired into the same 33 windows
 *                       that carry the entrance. AboutWindow keeps its own 260ms exit; GameHudWindow
 *                       stays excluded. Minor bump, not patch: this one changes the close PATH, which is
 *                       a bigger structural change than the motion passes before it.
 *                       THE BUG THIS NEARLY SHIPPED, found by measuring instead of assuming: an exit
 *                       animation must CANCEL the window's own Closing, animate, then re-issue the close
 *                       - and WPF THROWS DialogResult AWAY when a close is cancelled. Measured on real
 *                       dialogs: set DialogResult = true, cancel the Closing, close again -> ShowDialog()
 *                       returns FALSE. Every AJ Tools command is written as
 *                       `if (window.ShowDialog() == true) { ...do the work... }`, so the naive version
 *                       would have made EVERY Run button behave like Cancel: window opens, window closes,
 *                       tool silently does nothing, no error anywhere. Fixed by capturing
 *                       window.DialogResult BEFORE cancelling and restoring it after the animation
 *                       (restoring it re-issues the close by itself).
 *                       Also carried over from the About window lesson: a Button that is BOTH Click= and
 *                       IsCancel="True" raises Closing TWICE per click, so the guard needs three pieces
 *                       of state (IsExitPlaying / IsReadyToClose / IsFinished), not one flag.
 *                       GameKeySettingsWindow's Cancel button is exactly that shape and is covered.
 *                       SAFETY NET: a DispatcherTimer is armed BEFORE the animation starts and forces
 *                       the close regardless of what happens to the animation. A dialog that cannot be
 *                       dismissed would be far worse than a missing flourish, so the close never depends
 *                       on an animation completing. Whichever of the two fires first wins, once.
 *                       AUDITED FIRST: cancelling makes Closing fire 2-3 times, so any window with its
 *                       own Closing logic runs it that many times. Only PipeSizingWindow has any, and it
 *                       is a full-overwrite SaveState() - idempotent, so repeats are harmless. Re-check
 *                       this before attaching the exit to a NEW window whose Closing has a real side
 *                       effect. Also confirmed nothing outside a window calls Close() on one of these 33
 *                       and then depends on it being gone (only the AI toast/banner do that, and neither
 *                       is in this set).
 *                       VERIFIED: new tools/verify-exit-motion.ps1 runs the REAL helper against real
 *                       dialogs and asserts the returned result matches a no-animation control group for
 *                       Run(true), Cancel(false), plain Close() and the Click+IsCancel double-close.
 *                       All four match. Kept as a regression guard - this is a silent, data-shaped
 *                       failure with no error message, so it must never be checked by eye again.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.39.7 (2026-08-05) - Tab-change transitions. NO behaviour changed: no tool result, no validation, no
 *                       DialogResult, no selection logic, and no window made non-resizable.
 *                       New shared Helpers/TabMotionHelper.cs v1.0.0 - the selected panel fades in over
 *                       180ms while rising 8px over 220ms, same CubicEase EaseOut as the rest of the
 *                       suite. Wired with ONE call after InitializeComponent() into the five tabbed
 *                       windows: Colorize, Duct Standards Manager, Filter Pro, Graphics Settings Manager,
 *                       Location Data Assigner. Chosen as the step-4 target because a tab switch is the
 *                       most-repeated state change left in the suite and it was still a hard cut.
 *                       DELIBERATELY SHORTER than the 220/280ms window entrance: a window opens once, a
 *                       tab is clicked over and over in one sitting.
 *                       THE TRAP THIS HELPER EXISTS FOR: Selector.SelectionChanged is a ROUTED event, so
 *                       a ComboBox or ListBox INSIDE a tab bubbles its own selection change up to the
 *                       TabControl. Hooking it naively replays the entire tab transition every time the
 *                       user picks a value from a dropdown inside the tab - and four of these five
 *                       windows are full of dropdowns. Guarded by requiring e.OriginalSource to be the
 *                       TabControl itself. The handler never sets e.Handled, so existing SelectionChanged
 *                       logic in these windows keeps running untouched.
 *                       Attaches by walking the visual tree on Loaded rather than by x:Name, so NO XAML
 *                       changed in any of the five windows and a window with two TabControls gets both.
 *                       VERIFIED, not assumed, on the real WPF library: PART_SelectedContentHost is a
 *                       ContentPresenter in the default TabControl template AND under ModernStyles'
 *                       implicit style AND in GraphicsOverrideWindow's own custom template - which is
 *                       why one helper covers every tabbed window here. Then functionally tested via new
 *                       tools/verify-tab-motion.ps1: changing a dropdown inside a tab does NOT animate,
 *                       switching a tab DOES, and neither selection is disturbed. Kept as a regression
 *                       guard because that trap is easy to reintroduce.
 *                       NOT DONE, on purpose: (a) show/hide panel transitions (Reassign Level's scope
 *                       toggle, View Crop options, Purge lists) - most of those windows are
 *                       SizeToContent, so animating a panel in or out would make the whole window
 *                       resize mid-animation; (b) exit animations - still needs Ajmal's explicit
 *                       go-ahead, since an exit must cancel and re-issue the window's own close;
 *                       (c) wiring real progress reporting into long-running tools - that needs
 *                       dispatcher/threading work, which is a behaviour change, not motion.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.39.6 (2026-08-05) - Interaction motion finished off in the four windows that carry their OWN local
 *                       styles and merge nothing. NO behaviour changed in any of them.
 *                       ABOUT (showcase tier - deliberately NOT the working-dialog wash): sidebar items
 *                       slide 4px right on hover and dip on press; footer links lift 2px and dip; the
 *                       min/max/close glyphs dip to 88%. Hover COLOURS stay plain Setters because
 *                       ShowSection() sets Background/Foreground directly on the active nav button - the
 *                       one place in the project where animating colour would provably break state.
 *                       A side effect worth knowing: the active item never showed hover feedback before
 *                       (its local colours outrank a trigger Setter); it now does, via motion.
 *                       GRAPHICS OVERRIDE (26 local styles, the biggest surface in the project): press
 *                       dip + shade, keyboard-focus ring and an eased enable/disable on the base button
 *                       style (which 5 button variants inherit); caption-button glyph dip; colour
 *                       swatches grow 20% under the pointer and dip on click - never washed or tinted,
 *                       because a swatch IS the colour being picked; dropdown arrow rotates; dropdown
 *                       and category rows fade their highlight; TAB headers fade a hover wash; the
 *                       slider handle grows under the pointer; and the toggle switch's knob now SLIDES
 *                       its 20px instead of jumping ends by swapping alignment.
 *                       This window KEEPS its own instant hover colours on purpose - unlike the shared
 *                       theme, its hover steps carry meaning (accent -> brighter accent, danger ->
 *                       brighter red) and a neutral wash cannot reproduce the danger step (measured:
 *                       12% white over #5B1C1C gives #692C2C, nowhere near the intended #8B2B2B).
 *                       So hover stays a colour change here and the ADDED motion is what was missing.
 *                       GAME KEY SETTINGS: had no styles at all, so every button used raw Windows chrome
 *                       - square, and ignoring its own colour on hover in favour of Aero blue. One
 *                       implicit style now gives Save, Cancel and the code-behind-built key buttons the
 *                       house look and the standard motion. The code-behind still recolours a key button
 *                       to amber while it waits for a key press, untouched.
 *                       GAME HUD: NOTHING TO DO - it is a real-time overlay with its own frame-budget
 *                       animation, and it contains no <Button at all (that part was verified).
 *                       CORRECTED 2026-08-05 by audit: this entry originally also claimed "every element
 *                       in it is IsHitTestVisible=False ... a pure non-interactive overlay". That was
 *                       WRONG and backwards - RootGrid's Background="#01000000" exists precisely so the
 *                       HUD DOES capture every click and key, and PauseLayer is a click-to-resume
 *                       surface. The exclusion still stands on the frame-budget reason; do not reason
 *                       about the HUD's input handling from the original wording.
 *                       CAUGHT WHILE WRITING IT: the colour swatch first had hover and press driving ONE
 *                       shared transform. Pressing then dragging off fires both exit animations and the
 *                       last one to land wins, which could strand a swatch enlarged. Split into two
 *                       transforms - the "one trigger per animated property" rule, which this pass now
 *                       violates nowhere.
 *                       ALSO DELIBERATE: the category checkboxes' ticks stay instant while the standalone
 *                       one animates. They sit in a virtualized list, so an animated tick would replay
 *                       every time a row scrolled into view. Same reason selection stays instant in the
 *                       shared theme - animate what the user changed, not what merely appeared.
 *                       VERIFIED: new tools/verify-window-styles.ps1 lifts each window's <Window.Resources>
 *                       out of the XAML source, re-parses it standalone and forces every style and
 *                       template to build - 35 of them across the three windows, all pass. It discovers
 *                       the styles from their TargetType, so new ones are covered with no list to update.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.39.5 (2026-08-05) - Same interaction motion carried into the AJ AI shell's own theme
 *                       (src/AiShell/Views/SoftUiStyles.xaml v1.1.0), shared by the docked AI pane,
 *                       AI Settings and Saved Scripts. NO behaviour changed anywhere.
 *                       Identical timings and easing to ModernStyles v1.3.0 on purpose, so the AI shell
 *                       and the tool windows read as one product: hover wash, press dip to 97% + shade,
 *                       keyboard-focus ring, enable/disable fade on buttons; hover + focus rings on the
 *                       text and password fields; dropdown arrow rotates while the list is open, with
 *                       hover/open rings on the field (the dropdown previously had no hover feedback at
 *                       all). The instant Background swaps on Primary/Secondary/Warning buttons are gone,
 *                       replaced by the shared animated overlays - measured to land within a shade of
 *                       the old colours (Primary pressed: overlay gives ~#00A0CC vs the old #009FCC), and
 *                       all three buttons now behave identically instead of the Warning one dimming while
 *                       the other two swapped colour.
 *                       LEFT ALONE DELIBERATELY: the AI progress bars keep WPF's default chrome. The busy
 *                       strip is IsIndeterminate="True" and relies on WPF's own sliding-glow animation;
 *                       replacing that would swap something that works for something unproven, on the
 *                       indicator Ajmal watches during an AI run.
 *                       CORRECTED A STALE NOTE while here: SoftUiStyles claimed a custom ProgressBar
 *                       template "needs real Track-width math to avoid silently showing wrong progress".
 *                       Not true - verified by reflection over the real PresentationFramework.dll that
 *                       ProgressBar declares [TemplatePart] PART_Track/PART_Indicator/PART_GlowRect and
 *                       sizes the indicator itself in SetProgressBarIndicatorLength(). Then measured
 *                       v1.39.4's house bar directly: 25/50/100% give exactly 50/100/200px of a 200px
 *                       track. The comment now records the real reason (the indeterminate strip).
 *                       VERIFIED WITHOUT LAUNCHING REVIT: new tools/verify-wpf-styles.ps1 loads both
 *                       compiled dictionaries out of the built DLL and forces every style to build
 *                       their templates - all pass, every StaticResource resolves. This catches the
 *                       XamlParseException class of bug that a clean msbuild provably cannot, and which
 *                       took the whole add-in down at startup once before (v1.16.0). It matters most for
 *                       this file: AiShellPaneProvider builds the AI pane during Revit's OnStartup.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.39.4 (2026-08-05) - Interaction motion in the shared theme (src/UI/ModernStyles.xaml v1.3.0), which
 *                       29 windows merge - so this lands everywhere at once instead of being repeated
 *                       per window. NO tool behaviour changed; no window made non-resizable; no close
 *                       path touched; no control added, removed, renamed or re-ordered.
 *                       WHAT MOVES NOW: buttons take a soft wash on hover (90ms), dip to 97% with a
 *                       shade on press (110ms) and settle back over 240ms; keyboard focus draws a ring
 *                       (140ms); enabling/disabling fades over 120ms instead of snapping (this is the
 *                       validation state Ajmal sees most - the Run button greying out). Window
 *                       min/max/close buttons dip to 88% on press. Text boxes fade a blue hover ring
 *                       and a blue focus ring in. The dropdown arrow rotates 180 degrees while the list
 *                       is open. Dropdown items, list items and tab headers fade their hover in.
 *                       Everything decelerates (one shared CubicEase EaseOut, key MotionEaseOut).
 *                       TWO RULES THIS FILE NOW ENFORCES, both written into the XAML as comments:
 *                       (1) nothing animates Background or Foreground - only an overlay element's
 *                       Opacity or a RenderTransform inside a ControlTemplate - because a running
 *                       animation outruns a locally-set value and would break windows that colour
 *                       controls from code-behind (About's nav buttons, PipeSizing's mode toggles);
 *                       (2) one trigger per animated property, each state owning its own overlay, so
 *                       hover/press/focus cannot fight when several are true at once.
 *                       Layout is untouched: the button template still keeps the padding and the
 *                       content inside the same fill Border, so no button changed size.
 *                       ALSO: a house ProgressBar style (the default WPF bar is the old Aero one) with
 *                       a slow breathe when indeterminate. Only the Location Data Assigner uses a
 *                       progress bar today, and it is determinate-only, so the pulse is future cover.
 *                       Selection stays instant on purpose - a list loading hundreds of already-selected
 *                       rows would otherwise animate as a wave.
 *                       Checked before writing any of it: no code anywhere reaches into a template part
 *                       (zero GetTemplateChild/Template.FindName in src/), and PipeSizing's colour-set
 *                       buttons are ToggleButtons, which these styles do not target.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.39.3 (2026-08-05) - Window entrance motion rolled out across the suite. NO tool behaviour changed;
 *                       no window made non-resizable; no close path touched.
 *                       New WindowMotionHelper v1.0.0 gives a shared entrance - content fades in over
 *                       220ms while rising 12px over 280ms, CubicEase EaseOut - wired into 33 windows
 *                       with one call after InitializeComponent(). AboutWindow keeps its own staged
 *                       ~750ms showcase entrance and GameHudWindow is excluded (real-time overlay with
 *                       its own animation).
 *                       DELIBERATELY NOT a copy of About's timing: About is opened occasionally and can
 *                       carry a staged reveal, while a settings dialog opened many times a day must feel
 *                       instant - a 750ms cascade there reads as waiting, not polish. Two tiers, one
 *                       shared constant block each, so either can be retuned in one place.
 *                       Animates the ROOT CONTENT element, not Window.Opacity: Window.Opacity only has a
 *                       visual effect when AllowsTransparency="True", which only 7 of the 35 windows set.
 *                       Entrance only. An exit animation must cancel and re-issue the window's own close,
 *                       which is real risk on dialogs that validate or set DialogResult on close (see the
 *                       IsCancel double-Closing trap in v1.39.2) - not worth it for a 200ms flourish.
 *                       Skips any window whose root already carries a RenderTransform, and every failure
 *                       path restores the window to fully visible, so motion can never stop one opening.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.39.2 (2026-08-05) - About window motion + a project-wide rounded-corner audit. NO tool behaviour
 *                       changed; nothing was made non-resizable.
 *                       ABOUT WINDOW - motion: staggered entrance on Loaded (shell fade/scale/rise,
 *                       then logo, wordmark, content, a 45ms nav cascade, footer links; last stagger
 *                       starts at 460ms), an exit storyboard on close, a section-swap rise, and an
 *                       ambient pulse on the status dot. Entrances decelerate (Cubic/Quintic EaseOut),
 *                       the exit accelerates (CubicEase EaseIn, 260ms). Found and fixed while writing
 *                       it: the Close button is BOTH Click= and IsCancel="True", so one click raises
 *                       Closing twice and a single-flag guard let the second pass kill the window
 *                       mid-animation - now two flags (see ajtools-conventions.md).
 *                       ABOUT WINDOW - corners: a Border does not clip children to its own
 *                       CornerRadius, so the header and footer bars painted square corners over the
 *                       22px curve (top-right and bottom-right looked square; Ajmal reported it).
 *                       Header/footer now round themselves, children use 21 not 22 (concentric =
 *                       outer minus the 1px border), the resize grip glyph that sat outside the curve
 *                       is gone (CanResize - still fully resizable), and corners flatten while
 *                       maximized.
 *                       PROJECT AUDIT - all 38 XAML files checked; 10 matched AllowsTransparency but
 *                       3 were false hits (2 style dictionaries + a Popup inside LinkedSearchWindow's
 *                       dropdown, which uses standard OS chrome). Of the 7 real custom-chrome windows,
 *                       About was the ONLY one with the clipping defect: GameHudWindow and
 *                       LinkedSearchWindow insulate children with root Padding, GraphicsOverrideWindow
 *                       is CornerRadius=0 by design, and the 4 View Crop windows already round every
 *                       filled Border (their title bar is a transparent hit-test Grid).
 *                       WindowChromeHelper v1.1.0 - the 4 View Crop windows DO maximize, and their 8px
 *                       shell kept its radius while maximized, showing the desktop through all four
 *                       corners. New ApplyStateChrome squares the radius and drops the shadow margin
 *                       while maximized, remembering each border's design radius in an attached
 *                       property. Each window calls it from an OnStateChanged override so Win+Up and
 *                       top-edge snap are covered, not just the maximize button.
 *                       Builds clean (zero warnings) on Release (2020/net472) and Release R25
 *                       (net8.0-windows). Release R27 could not be built: this machine's .NET SDK is
 *                       9.0.316 and cannot target net10.0 - environment gap, not a code fault.
 *                       NOT loaded in Revit by the assistant - Ajmal verifies on screen.
 * v1.39.1 (2026-08-04) - Build hygiene, ZERO behaviour change - no tool touched. The Revit 2025+
 *                       configurations (net8.0-windows / net10.0-windows) were emitting 11 CA1416
 *                       platform warnings on Windows-only WinForms calls in AiShell and
 *                       GraphicsTools (ColorDialog, FolderBrowserDialog, Screen), the last remnant
 *                       of the count this changelog has tracked down from 682 (v1.25.3) through
 *                       490 (v1.25.4) and 274 (v1.25.5). Root cause was never the call sites: the
 *                       .csproj sets GenerateAssemblyInfo=false because this file is hand-maintained,
 *                       which suppresses the SDK's automatic [SupportedOSPlatform("windows")] stamp
 *                       that a net8.0-windows target would otherwise carry - so the analyzer treated
 *                       an add-in that only ever runs inside Revit on Windows as if it might run on
 *                       Linux. Declared the attribute explicitly at the foot of this file, guarded
 *                       with #if NET5_0_OR_GREATER so the net472/net48 builds (Revit 2020-2024),
 *                       which have no such attribute type, are untouched. Release (2020) and
 *                       Release R25 both rebuilt 0 errors / 0 warnings. Also records that v1.39.0's
 *                       pending build verification is now done - both configurations build clean.
 *                       Not click-tested in Revit.
 * v1.39.0 (2026-07-30) - Game Mode: the laser identity line now also shows the element's SYSTEM and
 *                       LEVEL (Ajmal picked this from the audit's idea list: "Laser also shows
 *                       System and Level i need it"). System = the System Name parameter (e.g.
 *                       "SAD 5"), falling back to the system classification ("Supply Air"); Level =
 *                       the same LevelId/reference-level/family-level ladder ReassignLevelService
 *                       uses. Linked elements resolve both against their own linked document.
 *                       Elements with neither (walls, floors) show the same line as before. Because
 *                       every weapon's element text funnels through the one Describe() method, the
 *                       cleaner/snag/selector toasts and the snag punch-list report gain System +
 *                       Level automatically too. Both new BuiltInParameters byte-scan-verified in
 *                       the real installed 2020 AND 2027 RevitAPI.dll before use (bridge was down).
 *                       See GameCollisionService.cs v1.2.0. Build verification pending (same
 *                       permission-system outage as 1.38.4); not click-tested in Revit.
 * v1.38.4 (2026-07-29) - Game Mode full audit pass ("check that tool entirely"), ZERO behaviour
 *                       change - no logic bug found; every fix is documentation/cleanup: (1) ribbon
 *                       tooltip now covers the SELECTOR weapon and professional mode (N), both
 *                       shipped in 1.38.0 but never added to the tooltip (RibbonManager v1.13.6);
 *                       (2) dead PxToDip helper removed from the HUD Render partial - orphaned when
 *                       measuring was deleted in 1.37.0 (GameHudWindow v1.9.4); (3) missing file
 *                       changelog entries stamped: GameSession v1.8.1 (ResyncViewQueued) and
 *                       GameMotionEngine v1.7.1 (ZoomToFit aim sync) - both referenced by the
 *                       1.38.2 suite entry but never written into the files; (4) eight stale header/
 *                       comment blocks corrected (engine header still listed the deleted Measure
 *                       partial and section box; Extras/Weapons/Render purposes named deleted or
 *                       outdated features; CmdGameMode + RibbonManager still described the old
 *                       Commands/Services/UI folder split); (5) CHANGELOG.md backfilled - it had
 *                       stopped at 1.25.8, missing every version from 1.26.0 to 1.38.3. Also
 *                       verified clean: transactions all named "AJ Tools - ...", no success popups,
 *                       ElementIdHelper used at every id compare, credit line on both HUD layers +
 *                       Key Settings, all 4 game images exist, help card and key list match the
 *                       real bindings. Build verification pending (permission-system outage during
 *                       the audit session); not click-tested in Revit this pass.
 * v1.38.3 (2026-07-29) - Game Mode: Ajmal's final weapon color scheme, applied everywhere -
 *                       GUN amber, LASER green (beam, dot and readouts now green), CLEANER black &
 *                       white (white crosshair bars with black edges), SNAG red, SELECTOR blue.
 *                       See GameHudWindow v1.9.3.
 * v1.38.2 (2026-07-29) - Game Mode: (1) AIM/DISPLAY SYNC FIX for Ajmal's live report (shots landing
 *                       beside the crosshair on a fresh start, self-fixing when the Properties
 *                       palette resized the view): Revit can 2D-zoom/pan perspective views like a
 *                       photo, moving the picture centre off the camera axis - the engine now
 *                       forces UIView.ZoomToFit on the game view at start, on every resume from
 *                       pause and on every view resize, keeping crosshair and true aim identical.
 *                       (2) The crosshair is now colored by the active tool (blue/red/amber/
 *                       magenta/green) - visible weapon indicator even in professional mode.
 *                       See GameSession v1.8.1, GameMotionEngine v1.7.1, GameHudWindow v1.9.2.
 * v1.38.1 (2026-07-29) - Game Mode: teleport visual finalized to Ajmal's VR reference (Workshop-
 *                       XR-like) - thick solid green ballistic arc from the muzzle dropping onto
 *                       a double landing disc drawn flat on the floor, matching the approved
 *                       sample render. See GameHudWindow v1.9.1.
 * v1.38.0 (2026-07-29) - Game Mode round 17, two features from Ajmal: (1) PROFESSIONAL MODE -
 *                       press N (remappable) and NO gun ever shows, permanently (remembered in
 *                       AppData\AJTools\ajgame-prefs.txt across sessions): presentable in front of
 *                       a manager or in a meeting, while EVERY tool keeps working - laser, cleaner
 *                       hide, snag marking, selecting - with beams starting from the bottom of the
 *                       view like a laser pointer; muzzle flash, recoil and the scroll-holster are
 *                       disabled while on; N again brings the guns back. (2) SELECTOR - a 5th
 *                       weapon (right-click cycle, green): the shot SELECTS the element in Revit's
 *                       live selection (shot again unselects; toast shows the running count), and
 *                       the selection STAYS after exiting the game, ready for editing. Linked
 *                       elements are refused with an explanation, like the other tool weapons.
 *                       See GamePrefs.cs v1.0.0, GameKeyBindings (new Professional action),
 *                       GameSession v1.8.0, GameMotionEngine v1.7.0, GameHudWindow v1.9.0. Built
 *                       clean; not yet click-tested.
 * v1.37.0 (2026-07-29) - Game Mode round 16, three changes from Ajmal: (1) MEASURING REMOVED
 *                       entirely ("not working properly") - rubber-band line, dimension badge,
 *                       measure card, engine measure/projection code and the Measure partial file
 *                       all deleted; the laser keeps its live distance (mm) + element identity.
 *                       (2) J is now a full "Reset Element Graphics in View" inside the game -
 *                       every element override in the game view resets (the existing Reset tool's
 *                       proven approach), so red snag marks from EARLIER sessions clear too; U
 *                       stays the temporary-hide reset. (3) REMAPPABLE KEYS: new Key Settings
 *                       window (pause with Esc, then press S) lists every game action - click its
 *                       key button, press the new key, Save; stored in AppData\AJTools\
 *                       ajgame-keys.txt and loaded every game start. Esc/mouse/wheel/1-9/arrows
 *                       stay fixed; duplicates and reserved keys are rejected inline. See
 *                       GameKeyBindings.cs + GameKeySettingsWindow.xaml(.cs) v1.0.0,
 *                       GameSession.cs v1.7.0, GameMotionEngine.cs v1.6.0, GameHudWindow v1.8.0,
 *                       RibbonManager v1.13.5. Built clean; not yet click-tested.
 * v1.36.4 (2026-07-29) - Game Mode: CLEANER rifle rotated 16 degrees and repositioned so its
 *                       barrel and the shot/aim line form ONE straight line to the crosshair
 *                       (Ajmal spotted the angle mismatch in the preview; snag blaster confirmed
 *                       perfect, untouched). See GameHudWindow v1.7.3.
 * v1.36.3 (2026-07-29) - Game Mode: the SNAG MARKER now shows Ajmal's blue/orange BLASTER picture
 *                       (his "SNAG gun .png"; shipped untouched as Resources\GameSnagGun.png -
 *                       real transparency confirmed, residual green keyed). Every weapon has its
 *                       own gun now: pistol (gun + laser), rifle (cleaner), blaster (snag);
 *                       glow/flash/tracer/laser follow whichever muzzle is active. See
 *                       GameHudWindow v1.7.2.
 * v1.36.2 (2026-07-29) - Game Mode: the CLEANER weapon now shows Ajmal's RIFLE picture instead of
 *                       the pistol (rifle.png supplied at Y:\Ajmal Ps\icon; shipped untouched as
 *                       Resources\GameRifle.png - it already had real transparency, only residual
 *                       green keyed out and RGB blanked under the alpha). Muzzle glow, flash,
 *                       tracer and laser all start from the rifle's flash-hider tip while the
 *                       cleaner is selected; pistol returns on the other weapons. See
 *                       GameHudWindow v1.7.1.
 * v1.36.1 (2026-07-29) - Saved positions are now UNLIMITED (Ajmal: tour must run "until how much we
 *                       have"): B keeps counting 10, 11, 12... instead of rotating back over 1-9,
 *                       the tour (O) visits EVERY saved slot in order, and the left-side list shows
 *                       them all (slots above 9 marked "tour only" since number keys stop at 9).
 * v1.36.0 (2026-07-29) - Game Mode round 11, per Ajmal ("add all" + measure correction). (1) The
 *                       measure is now a TRUE rubber band: hold on the first face and a GREEN laser
 *                       line stays anchored to that face while you aim the second one, with the mm
 *                       dimension riding the middle of the line; after release the locked line
 *                       stays glued to both faces as you walk (engine projects the 3D points to
 *                       screen every frame - GetZoomCorners calibration, assumed-FOV fallback).
 *                       (2) SNAG MARKER 4th weapon (right-click cycles GUN/LASER/CLEANER/SNAG):
 *                       shot paints the element red in the game view and adds it to a punch list
 *                       with position; J clears the marks; on exiting the game a snag report
 *                       (.txt) is saved to Documents\AJ Game Snags and the end screen says so.
 *                       (3) Tour mode (O): flies smoothly through the saved positions in order,
 *                       adopting each saved look; any move key stops it. (4) Compass + level line
 *                       ("Facing N | Level 03") in the status card. (5) Crouch: hold C while
 *                       walking = 1000 mm eye height (collision rays adapt). (6) Speed dial: + / -
 *                       adjust walking/flying speed x0.4..x3.0 live. (7) Flashlight night mode (V)
 *                       - dark vignette except where you look. (8) Synthesized gunshot sound per
 *                       shot (no sound files; M mutes). See GameSession.cs v1.6.0,
 *                       GameMotionEngine.cs v1.5.0 (+Movement/Measure/Extras partials),
 *                       GameHudWindow v1.7.0. Built clean; not yet click-tested - the projection
 *                       calibration (rubber-band anchor accuracy) is the main thing to check live.
 * v1.35.1 (2026-07-29) - Removed the follow-me section box entirely (Ajmal: "X no need, remove
 *                       that") - X key, engine logic, help line and tooltip mention all gone; the
 *                       game is back to zero undo entries in every mode. See GameSession.cs v1.5.1,
 *                       GameMotionEngine.cs v1.4.1, RibbonManager.cs v1.13.4.
 * v1.35.0 (2026-07-29) - Game Mode teleport rework, modelled on Autodesk Workshop XR at Ajmal's
 *                       request (he tested teleport live and asked for the XR feel): HOLD T shows
 *                       a glowing dashed JUMP ARC to the crosshair with a pulsing landing ring and
 *                       the jump distance in mm; RELEASE T = confirm and go (gray arc + "aim at a
 *                       surface" when nothing valid is aimed). And the saved positions are now
 *                       VISIBLE: a left-side "SAVED POSITIONS" panel lists each B-saved spot with
 *                       its number and X/Y/Z coordinates in mm - press that number to go there.
 *                       Engine feeds the aim target every frame while T is held (any weapon). See
 *                       GameSession.cs v1.5.0, GameMotionEngine.cs v1.4.0, GameHudWindow v1.6.0.
 *                       Built clean; awaiting Ajmal's live test.
 * v1.34.1 (2026-07-29) - Game Mode restructure, ZERO behaviour change, per Ajmal's own idea ("keep
 *                       it entirely in one folder... each feature separate .cs file... editing
 *                       also it will be easy"): everything now lives in ONE folder, src/GameMode/
 *                       (was spread over Commands/Services/UI GameMode subfolders), and the two
 *                       big classes are split into small per-feature partial files - engine: core
 *                       + Movement + Measure + Extras; HUD: core + Controls + Weapons + Render +
 *                       Photo. 13 files total, each one focused. Namespaces and all code kept
 *                       byte-identical (only 'partial' added), so nothing else in the project was
 *                       touched. Removing the game is now: delete src/GameMode/, Resources/
 *                       GameMode.png + GameGun.png, and the one "Game" block in RibbonManager.cs.
 * v1.34.0 (2026-07-29) - Game Mode "add all" round - all six offered extras accepted by Ajmal:
 *                       (1) TELEPORT: T jumps you to the point under the crosshair. (2) SAVED
 *                       POSITIONS: B stores the current spot + look direction into rotating slots
 *                       1-9; the number keys jump back. In-session only (not saved with the model).
 *                       (3) PHOTO MODE: K hides the HUD for a frame and saves a clean PNG of the
 *                       view area to Pictures\AJ Game Photos. (4) CLEANER weapon (right-click now
 *                       cycles GUN / LASER / CLEANER): one shot temporarily hides the element hit
 *                       (Revit's own temporary hide - host elements only, linked ones refused with
 *                       an explanation; U restores everything). (5) CLEAR HEIGHT: live floor-to-
 *                       obstruction height in mm in the status card while walking. (6) FOLLOW-ME
 *                       SECTION BOX: X toggles a 10x10x7 m section box centred on you, re-centred
 *                       every 2.5 m walked - honest note: each re-centre commits one transaction,
 *                       so this mode adds undo entries (everything else in the game still adds
 *                       none). Plus toast messages for all of the above. See GameSession.cs v1.4.0,
 *                       GameMotionEngine.cs v1.3.0, GameHudWindow.xaml(.cs) v1.5.0,
 *                       RibbonManager.cs v1.13.3. Built clean; not yet click-tested in Revit -
 *                       HideElementsTemporary/SetSectionBox transaction behaviour uses
 *                       try-direct-then-transaction fallbacks since Revit was closed during
 *                       development.
 * v1.33.0 (2026-07-29) - Game Mode, two features from Ajmal: (1) scroll-wheel HOLSTER - scroll down
 *                       and the gun slides away off-screen (no shooting; laser and measuring keep
 *                       working, the beam rising from the bottom of the view like a handheld
 *                       pointer); scroll up - or a click - draws it back. Scrolling is ignored
 *                       mid-measurement so a wheel touch cannot spoil a held measure. (2) The
 *                       measure card now shows the BIM-360-style axis breakdown: Total on top,
 *                       then X / Y / Z deltas and the plan distance, all in mm. See GameSession.cs
 *                       v1.3.0, GameMotionEngine.cs v1.2.1, GameHudWindow.xaml(.cs) v1.4.0. Built
 *                       clean; not yet click-tested in Revit.
 * v1.32.2 (2026-07-28) - Game Mode: laser/bullet/flash start point moved to the gun's REAL barrel
 *                       tip (top-left nose, by the single-dot front sight) - v1.32.1 had them
 *                       starting from the striker back plate at the rear of the slide, which Ajmal
 *                       spotted immediately ("laser is coming from the back side"). Muzzle
 *                       fractions (0.650, 0.343) -> (0.041, 0.081). See GameHudWindow.xaml.cs
 *                       v1.3.2. No other change.
 * v1.32.1 (2026-07-28) - Game Mode gun art correction, per Ajmal's direct reference image: the gun
 *                       now displays EXACTLY as he generated it - no flip, no tilt (v1.31.1 had
 *                       mirrored + rotated it toward the crosshair; he rejected that). Processing
 *                       is background removal + trim only; muzzle re-tracked to fractions
 *                       (0.650, 0.343); display height 300, corner bleed 40/60. See
 *                       GameHudWindow.xaml.cs v1.3.1.
 * v1.32.0 (2026-07-28) - Game Mode: laser MEASURING, per Ajmal ("like the BIM 360 distance
 *                       feature"). With the laser weapon selected: HOLD left-click while the laser
 *                       dot is on the first face, keep holding and aim at the second face, release
 *                       to lock. A green card shows Total, Horizontal (plan) and Vertical (level
 *                       difference) distances in mm - live while holding, frozen on screen after
 *                       release until the next measurement. Uses the exact 3D laser hit points on
 *                       the real faces (linked models included); read-only, no model changes. Also
 *                       retuned the gun picture after a rendered preview: smaller (height 340->260)
 *                       and tucked deeper into the corner so the arm's cut end stays off-screen.
 *                       See GameSession.cs / GameMotionEngine.cs v1.2.0, GameHudWindow.xaml(.cs)
 *                       v1.3.0, RibbonManager.cs v1.13.2. Built clean; not yet click-tested.
 * v1.31.1 (2026-07-28) - Game Mode polish, both from Ajmal: (1) the HUD gun is now his own
 *                       AI-generated pistol picture (Y:\Ajmal Ps\icon\gun.png) - green background
 *                       removed by chroma-key with fringe cleanup, flipped + tilted 60 degrees so
 *                       the barrel aims at the crosshair, muzzle position tracked through every
 *                       transform so the flash/tracer/laser start exactly at the barrel; ships as
 *                       Resources\GameGun.png, and the old vector pistol stays as automatic
 *                       fallback if that file is ever missing. (2) Freshly created "AJ Game View"
 *                       views now come with Crop View OFF and the crop region boundary OFF - he
 *                       was switching both off by hand on every new model. See CmdGameMode.cs
 *                       v1.1.0, GameHudWindow.xaml(.cs) v1.2.0.
 * v1.31.0 (2026-07-28) - AJ Game Mode weapon + speed rework, from Ajmal's feedback after first
 *                       playing v1.30.0 ("this is great game"): (1) hold left-click = AUTOMATIC fire
 *                       (~7.7 shots/s) instead of one bullet per click; (2) every bullet impact now
 *                       bursts a spark splash at the crosshair (8 flying sparks + expanding ring,
 *                       timed to land with the bullet); (3) right-click switches the weapon between
 *                       GUN and LASER - the L key is gone, and in laser mode the gun's accent stripe
 *                       and muzzle glow turn red; (4) shooting no longer pops the element-info card -
 *                       instead the LASER continuously shows BOTH the distance in mm AND the identity
 *                       of whatever it touches (category, family/type, Size, Element ID, linked
 *                       marker), live under the crosshair; (5) the pistol was redrawn realistically
 *                       (slide with serrations, ejection port, front/rear sights, hammer, trigger
 *                       guard, raked textured grip, magazine base) and recoil now kicks along the
 *                       barrel axis; (6) sprint speed raised - Shift now runs at 3.0x walking speed
 *                       (was 2.2x), in fly mode too. See GameSession.cs, GameMotionEngine.cs,
 *                       GameCollisionService.cs v1.1.0, GameHudWindow.xaml(.cs) v1.1.0,
 *                       RibbonManager.cs v1.13.1 (tooltip). Built clean; not yet click-tested in
 *                       Revit.
 * v1.30.0 (2026-07-28) - New tool: AJ Game Mode ("Game" panel, AJ Tools tab) - a first-person,
 *                       video-game style walkthrough inside a REAL Revit perspective view
 *                       ("AJ Game View", created once and reused), so every Revit view control
 *                       (VG, filters, hide/isolate, section box, display style) shapes the game
 *                       world - including collision, which raycasts only what is visible.
 *                       WASD+mouse-look walking with gravity, stairs step-up, Shift sprint, Space
 *                       jump; walls/slabs/everything visible blocks movement; doors are passed
 *                       with E when near; windows are climbed by jumping; F toggles free flight,
 *                       G toggles ghost mode (through everything), R respawns. HUD overlay
 *                       (transparent WPF window glued pixel-exact over the view) draws a
 *                       crosshair, a vector-drawn gun with muzzle flash/recoil, a visible bullet
 *                       tracer per shot, and a red laser with live distance readout in mm;
 *                       shooting identifies the element hit (category, family/type, Size, distance,
 *                       Element ID, linked-model marker). Esc pauses to a small "click to continue"
 *                       pill so Revit stays fully usable mid-game; Esc again (or the ribbon button,
 *                       which toggles) exits. KEY TECHNIQUE (verified live on Revit 2020,
 *                       2026-07-28): View3D.SetOrientation needs NO transaction - camera moves are
 *                       navigation, create zero undo entries and zero model changes; and
 *                       ReferenceIntersector works fine on a perspective view (~0.1 ms/ray) with
 *                       FindReferencesInRevitLinks resolving linked architecture. The only model
 *                       change this tool ever makes is creating the "AJ Game View" itself. Fully
 *                       self-contained for easy removal: Commands/GameMode, Services/GameMode,
 *                       UI/GameMode, Resources/GameMode.png + one ribbon block (RibbonManager
 *                       v1.13.0). See CmdGameMode.cs, GameSession.cs, GameCollisionService.cs,
 *                       GameMotionEngine.cs, GameHudWindow.xaml(.cs) v1.0.0. Built clean; not yet
 *                       click-tested in Revit.
 * v1.29.1 (2026-07-28) - Stack Tags fix + ribbon move (Ajmal tested v1.29.0 live). Fix: Stack Tags'
 *                       first-click tag creation was borrowing Smart MEP Tag's leader routine
 *                       (SmartTagPlacementEngine.ApplyLeaderBehavior), which nudges the elbow outside
 *                       the tag's own text box and falls back to toggling the leader end condition -
 *                       neither of which Rearrange Tags does (its own TryApplyLShapeLeader comment:
 *                       "do not toggle leader end condition as fallback"). Replaced with a new local
 *                       ApplyFreshLeader in StackTagsService.cs: plain ComputeElbow + TrySetLeaderElbow,
 *                       matching Rearrange Tags exactly - only kept the L1 rollback-probe fallback
 *                       (a Revit API read quirk, not a style choice). Create Tags' own leader technique
 *                       is untouched - it's deliberately modeled on Smart MEP Tag, not Rearrange Tags.
 *                       Ribbon: moved Stack Tags from a standalone button into the Create Tags pulldown
 *                       as a third child (Create Tags / Stack Tags / Create Tags Settings), per Ajmal's
 *                       request.
 * v1.29.0 (2026-07-28) - New tool: Stack Tags, alongside Create Tags on AJ Annotation - Tags panel.
 *                       Select MEP elements, click ONE location, and a tag is created for every
 *                       eligible element, arranged into a vertical stack starting there - exactly
 *                       Rearrange Tags' own single-click, whole-batch stacking behaviour, but starting
 *                       from raw elements instead of pre-existing tags. Click again to relocate the
 *                       whole stack (moves the tags this run already created rather than creating
 *                       duplicates - the first click creates, every later click moves). Same
 *                       eligibility rules and Settings as Create Tags (already tagged / too short /
 *                       vertical; category + minimum length); stack spacing comes from Arrange Tags
 *                       Settings, unchanged - no new settings window for this tool. Extracted the
 *                       shared "which selected elements are eligible" logic out of CreateTagsService
 *                       into CreateTagsEligibilityFilter.cs so Create Tags and Stack Tags can't
 *                       quietly drift apart on the rules. See CmdStackTags.cs, StackTagsService.cs,
 *                       CreateTagsEligibilityFilter.cs v1.0.0.
 * v1.28.0 (2026-07-28) - 9 new tools across the Transfer and Purge pulldowns (Manage panel), all built
 *                       on two new shared engines so future variants stay cheap to add:
 *                       Transfer: added Transfer Schedules, Transfer Legends, Transfer Drafting Views
 *                       alongside the existing Transfer View Templates (untouched). Same copy-between-
 *                       open-projects UX; override mode now also restores the copy's sheet placement(s)
 *                       (Viewport for Legends/Drafting Views, ScheduleSheetInstance for Schedules - a
 *                       Legend can be placed on several sheets at once, all are restored). New shared
 *                       engine: TransferViewsCommandRunner + TransferViewsWindow + TransferElementCollector
 *                       (Models/Transfer, Services/Transfer, UI/Transfer). See CmdTransferSchedules.cs,
 *                       CmdTransferLegends.cs, CmdTransferDraftingViews.cs v1.0.0.
 *                       Purge: added Purge Unused View Templates, Purge Unused Filters, and Purge Unused
 *                       Groups (Model + Detail Group types with zero placed instances, shown together via
 *                       a kind filter) - a different shape of "unused" to the existing Purge Unplaced
 *                       family (not-referenced-anywhere vs not-on-a-sheet), same probe-before-delete
 *                       safety net (a rolled-back Document.Delete decides what Revit really allows, not
 *                       just this tool's static usage scan - catches cases like a template silently set
 *                       as Revit's own default for a view type). New shared engine:
 *                       UnusedElementPurgeCommandRunner + PurgeUnusedElementsWindow + UnusedElementCollector
 *                       + UnusedElementPurgeService (Models/Purge, Services/Purge, UI/Purge). See
 *                       CmdPurgeUnusedViewTemplates.cs, CmdPurgeUnusedFilters.cs, CmdPurgeUnusedGroups.cs
 *                       v1.0.0. Also extended the existing Purge Unplaced family (UnplacedViewPurgeMode)
 *                       with 3 more kinds - Schedules, Legends, Drafting Views - reusing the same
 *                       collector/service/window unchanged (ThreeDViews/SectionViews behaviour untouched).
 *                       See CmdPurgeUnplacedSchedules.cs, CmdPurgeUnplacedLegends.cs,
 *                       CmdPurgeUnplacedDraftingViews.cs v1.0.0.
 * v1.27.0 (2026-07-28) - New tool: Create Tags, on AJ Annotation - Tags panel. Select one or more
 *                       MEP elements (duct, pipe, mechanical equipment, duct/pipe accessory, cable
 *                       tray), then click a location for each in turn (nearest untagged-in-this-run
 *                       element wins each click, Esc stops early) - same click-loop rhythm as
 *                       Rearrange Tags, but creates a fresh tag with an L-shaped leader instead of
 *                       moving an existing one. Auto-skips an element that's already tagged in the
 *                       view, shorter than the configured minimum length, or a vertical run (duct,
 *                       pipe, OR cable tray - broader than Smart MEP Tag's own duct-only vertical
 *                       check, per Ajmal's confirmed answer). New Create Tags Settings window
 *                       (category grid + a minimum-length mm field - unlike Smart MEP Tag Settings,
 *                       which hardcodes its size thresholds today). Reuses SmartMepTagService's
 *                       pre-flight checks, tag-family resolution, and its already-tagged/curve-length/
 *                       midpoint helpers, plus SmartTagPlacementEngine's leader-attachment routine (4
 *                       methods widened private->internal for reuse, zero behaviour change to Smart
 *                       MEP Tag itself). See CmdCreateTags.cs, CmdCreateTagsSettings.cs,
 *                       CreateTagsService.cs, CreateTagsSettingsTracker.cs,
 *                       CreateTagsSettingsWindow.xaml(.cs) v1.0.0.
 * v1.26.0 (2026-07-28) - Reassign Reference Level: added a Selected Elements scope alongside the
 *                       existing Whole Project scope. Pre-select elements in Revit, open the tool,
 *                       and only those elements are reassigned to a single TO level - each element's
 *                       own current level is read as its FROM, so a mixed-level selection is fine.
 *                       The option is disabled with an explanatory tooltip until something eligible
 *                       is selected, so there is no dead-end Run click. Whole Project path (FROM
 *                       level -> TO level, across the whole model) is unchanged. See
 *                       CmdReassignLevel.cs v1.4.0, ReassignLevelService.cs v1.1.0,
 *                       ReassignLevelWindow.xaml(.cs) v1.1.0.
 * v1.25.8 (2026-07-28) - HVAC Schematic error dialogs now show the exception type and the failing
 *                       AJ Tools method/line (trimmed stack trace) instead of a bare message, so a
 *                       live crash pinpoints its own source. Written while the v1.25.7 "key not
 *                       present" crash was still unreproduced locally; kept as a standing diagnostic
 *                       even after the root cause was found, since a bare message alone had proven
 *                       too vague to act on. See HvacSchematicCommand.cs v1.1.0. No other tool touched.
 * v1.25.7 (2026-07-27) - Fixed a crash in HVAC Schematic: "An unexpected error occurred. The given
 *                       key was not present in the dictionary." fired on almost every run (any
 *                       selection producing at least one leaf node in the schematic tree - i.e.
 *                       nearly always, including a single isolated element). Root cause in
 *                       SchematicLayoutEngine.AssignTreePositions: a variable was pre-set to "no
 *                       continuation child" (-1) then passed as the out-parameter of a
 *                       Dictionary.TryGetValue call - TryGetValue always overwrites its out-parameter,
 *                       even when the key is missing, so the "-1" default was silently replaced by 0
 *                       and then used as if it were a real element id, which isn't in the node lookup.
 *                       See SchematicLayoutEngine.cs v1.1.1. No other tool touched.
 * v1.25.6 (2026-07-28) - Full-force UI audit pass (mechanical checks over all 33 XAML + every window
 *                       call site), behaviour-preserving. VERIFIED CLEAN: every StaticResource/
 *                       DynamicResource reference resolves in its window's actual merged dictionaries,
 *                       no duplicate resource keys, no root-attribute StaticResource (the startup-crash
 *                       class), no Grid.Row/Column overflow, credit footer present everywhere, no
 *                       duplicate ribbon button IDs, all 34 ribbon icons exist on disk, no empty
 *                       tooltips, no live Application.Current. FIXED: (1) 12 modal windows were shown
 *                       without a Revit owner and could drop behind the Revit window - Duct Standards,
 *                       Filter Pro, Linked ID Viewer, Linked Search, Pipe Sizing, both Purge tools,
 *                       Revision Cloud Settings, Transfer View Templates, Apply Graphics, Shared Param
 *                       to Family Param, Saved Scripts - all now owned via WindowInteropHelper;
 *                       (2) 5 borderless windows (Graphics Override + 4 View Crop) could maximize OVER
 *                       the Windows taskbar - given the same MaxWidth/MaxHeight caps AboutWindow got in
 *                       v1.19.1; (3) Esc now closes MEP Opening Settings, Pipe Sizing and About
 *                       (IsCancel on their Close buttons); (4) removed Pipe Sizing CSV export's
 *                       "Report saved successfully." popup per the no-success-popup rule (failure
 *                       popup kept). Left alone, noted as accepted: AiShell's two MessageBox confirm
 *                       dialogs (functioning confirmations, AiShell styling debt). Release (2020)
 *                       rebuild 0 errors / 0 warnings; R25 0 errors. Not click-tested in Revit.
 * v1.25.5 (2026-07-28) - Smart MEP Tagging Settings v2.0.0, UI only - the LAST WinForms dialog in the
 *                       suite is gone; every AJ Tools window is now themed WPF. Same treatment as
 *                       v1.25.2/v1.25.4: live inline validation (unticking every category now disables
 *                       Save with a message instead of closing the dialog, erroring and cancelling the
 *                       command), priority is a fixed-choice dropdown so an invalid value is impossible
 *                       by construction, window owned by the Revit main window, credit footer standard.
 *                       Added Tag all / Tag none buttons. Dropped the "Settings saved." success popup
 *                       per the house rule. Saved-state shape and the offset carry-over logic unchanged.
 *                       R25 CA1416 count fell 490 -> 274 (all 216 of this file's WinForms warnings
 *                       gone); every remaining warning now sits in AiShell/AvalonEdit-era files.
 *                       Release (2020) rebuild 0 errors / 0 warnings. Not click-tested in Revit.
 * v1.25.4 (2026-07-28) - Reassign Reference Level v1.3.0, UI only - the reassignment algorithm in
 *                       ReassignLevelService is untouched. The WinForms level prompt was replaced by
 *                       ReassignLevelWindow (themed WPF). Fixed: (1) picking the same level in both
 *                       boxes closed the dialog, showed an error popup and cancelled the command, so
 *                       the tool had to be restarted from the ribbon - now caught live inline with Run
 *                       disabled; (2) the "Reassign Elements" button overlapped Cancel by 15 px
 *                       (carried over from v1.25.3, now moot - the WinForms form is gone); (3) no owner
 *                       window, so the dialog could drop behind Revit; (4) the intro text sat in a fixed
 *                       460x225 form with a fixed 430x32 label and could clip at larger Windows text
 *                       scaling - now wraps in a resizable window. Added: a Swap button, and an up-front
 *                       note that the scope is the WHOLE project and that hosted elements are skipped
 *                       (previously only discoverable from the report after the run). The bulk-change
 *                       confirmation with the element count is unchanged and still fires before any
 *                       edit. Side effect: R25's CA1416 count fell 682 -> 490, since all 192 of
 *                       CmdReassignLevel's WinForms warnings went with the form. Release (2020) rebuild
 *                       0 errors / 0 warnings. Not click-tested in Revit.
 * v1.25.3 (2026-07-27) - Suite-wide credit line, no new tool: "Created & All Rights Reserved @ Ajmal
 *                       P.S." now appears in EVERY window. It already existed in 9 windows and those
 *                       were left exactly where they were; 18 windows that had no credit got it added
 *                       as a bottom-centred footer (root layout wrapped in a DockPanel so no existing
 *                       Grid.Row index moved, and fixed-height windows gained +24 px so the footer
 *                       cannot squeeze the button row). Three windows that carried a DIFFERENT wording
 *                       were normalised to the standard text without moving them: Graphics Override
 *                       ("Copyright (c) 2026 Ajmal P.S. All Rights Reserved."), Pipe Sizing ("Created
 *                       and all rights reserved (c) Ajmal P.S. (AJ Tools)") and the About window
 *                       ("(c) 2026 AJ Tools", which was built from DateTime.Now.Year in code-behind).
 *                       The two remaining WinForms dialogs (Smart MEP Tag Settings, Reassign Level)
 *                       got a matching grey label. Fixed in passing: Reassign Level's "Reassign
 *                       Elements" button overlapped the Cancel button by 15 px (x=235 + width 130 = 365
 *                       against Cancel at x=350) - now 220 + 125 = 345 with a 5 px gap. Release (2020)
 *                       rebuild 0 errors / 0 warnings; R25 builds, its CA1416 count rose 654 -> 682
 *                       purely from the two new WinForms labels. No window click-tested in Revit yet.
 * v1.25.2 (2026-07-27) - Arrange Tags Settings v2.0.0, no new tool: the last WinForms prompt in the
 *                       Annotation tab was rebuilt as a themed WPF window (ArrangeTagsSettingsWindow,
 *                       matching ModernStyles like every other settings window). Fixes carried in the
 *                       same pass: (1) a typo no longer closes the window and throws the entry away -
 *                       validation is live and inline, Save stays disabled until the value is valid;
 *                       (2) comma-decimal Windows locales could read "12.5" as 125 and silently save a
 *                       10x spacing - both formats now parse correctly; (3) added a 0.1-250 mm range
 *                       check (any positive number was accepted before); (4) the window is owned by the
 *                       Revit main window so it can no longer hide behind Revit; (5) the tool no longer
 *                       demands an open project - the setting lives in AppData, and the active view
 *                       scale is now only used for the live "on sheet vs in model" explanation;
 *                       (6) the save is verified by read-back instead of assumed, so a failed write is
 *                       reported instead of silently swallowed. The routine "saved" popup was dropped
 *                       per the house no-success-popup rule. Not yet click-tested in Revit.
 * v1.25.1 (2026-07-26) - Maintenance pass, no new tool: (1) Highlight Selection v1.2.0 - selecting
 *                       insulation/lining directly now highlights its host too, and hosts now pull in
 *                       duct lining alongside insulation (both flagged as open items in v1.20.1's
 *                       scope note; API verified on real RevitAPI.dll 2020/2024/2027). (2) Fixed the
 *                       ProgramData all-users deploy writing a broken manifest path
 *                       ("AJ ToolsAJ Tools.dll", found 2026-07-21) - files now land in the AJ Tools\
 *                       subfolder and the manifest derives from the real copy path. (3) Removed the
 *                       orphaned CmdQuickParallelDimension class (superseded by the CenterLine/
 *                       FaceEdge pair; confirmed unreferenced). All 8 configs (2020-2027) built with
 *                       0 errors and 0 warnings from changed files, then deployed to all 8 years after
 *                       Revit closed - read-back verified both ProgramData subfolder DLLs (v1.25.1.0,
 *                       manifests matching) and AppData payloads. Highlight Selection's new insulation
 *                       behaviour still needs Ajmal's live click-test.
 * v1.25.0 (2026-07-25) - Merge of the GitHub line (v1.24.0/v1.24.1: Claude as third AJ AI provider,
 *                       Gemini key-switch fix, misreported-failure fixes, HVAC Schematic drawing
 *                       fixes, full-suite Revit 2020-2027 compatibility audit, suite-wide UI
 *                       readability fixes) with the local line (v1.23.2-v1.23.5 below: ribbon
 *                       restructure, Run Pinned / Saved Scripts split button, standalone Saved
 *                       Scripts window, Smart Selection fixes, masked API-key Settings fields).
 *                       Provider key unified to "Claude", default model claude-sonnet-5. Chosen
 *                       1.25.0 because both 1.23.x (local) and 1.24.x (GitHub) already exist.
 * v1.23.5 (2026-07-22) - Smart Selection: fixed a workflow gap Ajmal reported - selecting an element in
 *                       Revit BEFORE running the tool was ignored, always forcing a fresh interactive
 *                       pick for the reference element. Now a pre-existing single-element selection (if
 *                       categorized) is used as the reference directly, skipping straight to the
 *                       follow-up box-select stage. See CmdSmartSelection.cs v1.1.1.
 * v1.23.4 (2026-07-21) - Correction on top of v1.23.3/v1.23.2: both split buttons (Opening, Run Pinned)
 *                       now keep their default face permanently fixed (Create Openings / Run Pinned)
 *                       instead of tracking whichever child ran last - Ajmal watched the tracking
 *                       behavior live and wanted the simpler fixed version instead. See RibbonManager.cs
 *                       v1.12.0, App.cs v1.16.0.
 * v1.23.3 (2026-07-21) - AI Assistant panel: "Run Pinned" and "Saved Scripts" combined into one split
 *                       button (Run Pinned default, Saved Scripts in the dropdown, top face tracks
 *                       whichever ran last) - same pattern as the Opening split button just below. See
 *                       RibbonManager.cs v1.11.0, App.cs v1.15.0.
 * v1.23.2 (2026-07-21) - Ribbon restructure, no new tools: View panel (AJ Tools tab) - Filter Pro,
 *                       Colorize, and Highlight Selection compacted into one small stacked group;
 *                       Section Mark Visibility relocated to the AJ Annotation tab's Tags panel. Opening
 *                       panel's split button now defaults to "Create Openings" and its top face tracks
 *                       whichever of Create Openings / Opening Settings was actually run last. See
 *                       RibbonManager.cs v1.10.0, AnnotationRibbonManager.cs v1.4.0, App.cs v1.14.0.
 * v1.23.1 (2026-07-20) - Smart Selection: swapped the multi-pick window/crossing/click loop (needed an
 *                       explicit Finish/Enter to end) for a single one-shot window/crossing box-select
 *                       that completes the instant the drag ends - per Ajmal's feedback after live
 *                       testing. See CmdSmartSelection.cs v1.1.0.
 * v1.23.0 (2026-07-20) - Smart Selection (Modify panel, AJ Tools tab): new tool - pick one reference
 *                       element, then window-select, crossing-select, or click-select more elements in
 *                       the view; only elements sharing the reference element's category are added,
 *                       everything else caught in the box is skipped automatically. Read-only, no
 *                       model changes. Ported in from a separate cloud-session PR (#16) and adapted to
 *                       the current live source tree. See CmdSmartSelection.cs / SmartSelectionFilter.cs.
 * v1.22.0 (2026-07-20) - Elements to Ceiling Grid (Ceiling Magnet): Ajmal asked to keep BOTH the
 *                       original one-at-a-time workflow and the new v1.21.0 window-select-then-loop
 *                       workflow in the same tool, rather than replace one with the other. The tool now
 *                       opens with a TaskDialog command-link choice ("Pick one at a time" vs
 *                       "Window-select multiple at once") and runs whichever flow was picked -
 *                       CmdCeilingMagnet.cs's original v1.3.0 logic is preserved byte-for-byte as one
 *                       branch. See CmdCeilingMagnet.cs v1.5.0 for full detail.
 * v1.21.0 (2026-07-20) - Elements to Ceiling Grid (Ceiling Magnet): reworked the selection workflow.
 *                       Elements to snap are now window/click multi-selected ONCE up front
 *                       (src/Commands/CmdCeilingMagnet.cs, reuses the current selection if one already
 *                       exists) instead of picked one at a time after the ceiling. The command then
 *                       repeats a ceiling+anchor-point round (Esc to finish the whole loop) - each
 *                       round snaps only the elements from that batch sitting over the picked ceiling
 *                       (new CeilingMagnetService.FilterElementsOverCeiling, reading the ceiling's real
 *                       solid geometry rather than a bounding-box guess), so one selection can be
 *                       walked room-by-room without re-running the command or re-snapping elements an
 *                       earlier round already placed. See CmdCeilingMagnet.cs v1.4.0 for full detail.
 * v1.20.2 (2026-07-19) - New About icon: replaced Resources/About.png with Ajmal's own artwork
 *                       (Y:\Ajmal Ps\icon\about.png, a purple question-mark badge) - same filename, so
 *                       both the ribbon button and the About window's own taskbar icon (AboutWindow.xaml.cs,
 *                       IconLoader.LoadLarge("About.png")) pick it up automatically, no other file touched.
 * v1.20.1 (2026-07-19) - Fix on top of v1.20.0's Highlight Selection tool: a selected duct/pipe with
 *                       insulation left the insulation gray instead of red (it's a separate hosted
 *                       ElementId, not part of the raw selection). CmdHighlightSelection now pulls each
 *                       highlighted element's insulation ids via InsulationLiningBase.GetInsulationIds
 *                       (verified against the real installed RevitAPI.dll on 2020/2024/2027 - identical
 *                       signature on all three) and colors them red too.
 * v1.20.0 (2026-07-19) - New tool: Highlight Selection (View panel, src/Commands/GraphicsTools/
 *                       CmdHighlightSelection.cs) - colors the current selection red and every other
 *                       element in the active view gray, for instant visual identification. Reuses the
 *                       existing Graphics command infrastructure (GraphicsCommandService,
 *                       GraphicsElementService, GraphicsOverrideBuilder) rather than a one-off override
 *                       path. Also corrected a version-attribute drift found while bumping this: the
 *                       [assembly: AssemblyVersion]/[AssemblyFileVersion] attributes were still
 *                       "1.19.0.0" even though the changelog below already documented v1.19.1 as
 *                       shipped - the attribute bump was missed in that prior session. Now both match.
 * v1.19.1 (2026-07-19) - About window overhaul (src/UI/AboutWindow.xaml/.xaml.cs): added a real
 *                       taskbar/window icon (loaded from Resources/About.png via the existing
 *                       IconLoader), a Minimize button next to Close (there was no way to minimize
 *                       this custom-chrome window before), and a MaxWidth/MaxHeight fix so the
 *                       existing double-click-to-maximize no longer draws over the taskbar. Retuned
 *                       the accent color from a generic cyan to the house Neon Blue dark value
 *                       (#00C8FF) per the UI style guide. Content accuracy pass: Core Tools tab now
 *                       lists the real current ribbon (previously named several tools - "Auto
 *                       Dimensions", "Reset Datums", "Reset Text Position" - that don't match any
 *                       actual button, and omitted real ones like Colorize, Smart MEP Tags, Pipe
 *                       Sizing, MEP Openings, the AJ AI shell/bridge); Updates tab replaced its
 *                       "replace this with your real notes" placeholder with actual recent
 *                       highlights pulled from this changelog; License tab replaced a vague
 *                       "restricted based on your release policy" line with the repository's real
 *                       All Rights Reserved terms; fixed GetDeploymentLabel() only ever recognizing
 *                       a Revit 2020 install path (regex now matches any Addins\<year> folder, so
 *                       the label is correct on every supported Revit version, not just 2020).
 *                       Read-only info window; no model-facing behaviour changed.
 * v1.19.0 (2026-07-19) - Two more improvements Ajmal asked for after a second round of "any idea to
 *                       improve the tool": (1) the diff-highlight from v1.18.0 now also covers Run
 *                       Code's auto-fix loop, not just Generate - same gap, different code path.
 *                       (2) new crash/close recovery: the Prompt and code editor content auto-save
 *                       (2s debounce) to %AppData%/AJTools/ajai-recovery.json and restore on next
 *                       open, so a Revit crash no longer loses work that was never explicitly saved
 *                       as a script file.
 * v1.18.0 (2026-07-18) - Two improvements Ajmal asked for after "any idea to improve the tool":
 *                       (1) the Prompt box no longer clears after a successful generate, so a quick
 *                       follow-up tweak doesn't mean retyping the whole request. (2) After a generate
 *                       that edits existing code (the v1.17.0 incremental-edit feature), the changed
 *                       lines are now highlighted in the code editor (translucent Neon Blue
 *                       background, via AiShellViewModel's new CodeGenerated event + a line-level LCS
 *                       diff in AiShellView.xaml.cs) - makes it obvious at a glance which part the AI
 *                       actually touched instead of having to re-read the whole script. Skipped
 *                       entirely on the first-ever generate (nothing to diff against) and when
 *                       everything changed (a fresh rewrite, not an edit).
 * v1.17.1 (2026-07-18) - "Generate C# Code" shrunk from a big full-width bar to a normal-sized button
 *                       (same style/padding as Run Code etc.), left-aligned in its row. Added a Stop
 *                       button beside it (same StopCommand the Run Code row already uses), visible
 *                       while IsBusy, so there's a way to cancel while the AI is generating - not just
 *                       while the code is running below, where the only Stop control used to live.
 * v1.17.0 (2026-07-18) - New capability Ajmal asked for: "Generate C# Code" now sends the code
 *                       already in the editor as context, so a small follow-up prompt ("change the
 *                       color to green" right after "change all ducts to red") edits the existing
 *                       script instead of always generating an unrelated-looking fresh one. The AI
 *                       itself decides small-edit vs fresh-generate based on the injected instructions
 *                       (AiShellViewModel.GenerateCodeAsync) - not a deterministic diff/heuristic in
 *                       this codebase, since "is this request related" is a judgment call the model is
 *                       better placed to make than a string comparison would be.
 * v1.16.2 (2026-07-18) - Three visual fixes Ajmal reported from the first successful live launch:
 *                       (1) "Review Code"/"Format Code" button labels were clipped ("Cod" with the
 *                       "e" cut off) - those buttons had a fixed Width="100" too narrow for the
 *                       label at the new padding; removed the fixed widths so all execution-row
 *                       buttons auto-size to their content instead. (2) Provider/Model ComboBoxes in
 *                       Settings still showed a white/system-grey background despite Background being
 *                       set - a "colors only" ComboBox restyle doesn't work, the default Windows
 *                       theme's internal toggle-button chrome ignores the outer ComboBox.Background
 *                       property; replaced with a real custom ControlTemplate (SoftUiStyles.xaml).
 *                       (3) Output console felt cramped - gave it more relative row height (1.5* ->
 *                       2*, Code Editor 3* -> 2.5*, Prompt 2* -> 1.5*) plus a 90px MinHeight floor.
 * v1.16.1 (2026-07-18) - Fixed a real startup crash in the v1.16.0/v1.15.2 work below: AiShellView.xaml
 *                       and SettingsWindow.xaml both set Background/Foreground as StaticResource
 *                       attributes directly on their own root element (UserControl/Window). WPF
 *                       processes a root element's own attributes before its Resources dictionary is
 *                       populated, so that StaticResource lookup always fails - "Cannot find resource
 *                       named 'SurfaceBrush'" - which crashed Revit's OnStartup entirely (AiShellView
 *                       is constructed unconditionally by AiShellPaneProvider). Fixed by moving
 *                       Background/Foreground one level down onto the first child Grid instead, which
 *                       DOES correctly resolve the parent's Resources. Confirmed live by Ajmal - this
 *                       is the first bug this session that only a real Revit launch could catch (a
 *                       clean msbuild only compiles BAML, it doesn't evaluate StaticResource lookups
 *                       against the runtime resource tree).
 * v1.16.0 (2026-07-18) - Settings for the "C#" pane moved out of its inline collapsible panel into a
 *                       new standalone popup window (src/AiShell/Views/SettingsWindow.xaml, modal,
 *                       opened from AiShellView's code-behind), per Ajmal's request. Binds to the
 *                       SAME AiShellViewModel instance the pane already uses - no new ViewModel, no
 *                       Revit API access (pure local config), so a plain ShowDialog() needed no
 *                       ExternalEvent. Extracted the shared Soft Revit UI brush/style resources into
 *                       src/AiShell/Views/SoftUiStyles.xaml (a merged ResourceDictionary) so the pane
 *                       and the new popup draw from one visual-style source instead of duplicated
 *                       XAML. Removed AiShellViewModel's now-unused IsSettingsVisible/
 *                       ToggleSettingsCommand.
 * v1.15.2 (2026-07-18) - Restyled the "C#" dockable pane (src/AiShell/Views/AiShellView.xaml) to the
 *                       house Soft Revit UI look (Neumorphism + Claymorphism, Neon Blue #00C8FF
 *                       primary, dark theme) - was a flat VS-Code-style layout with plain solid-color
 *                       buttons and square borders. Rounded soft cards (CornerRadius 14) for each
 *                       section (Settings, Prompt, C# Code, Output, saved-script rows), reusable
 *                       button styles (Primary/Secondary/Warning with hover+pressed states), a custom
 *                       rounded TextBox template, removed decorative emoji from button labels per
 *                       house UI-wording rules (kept plain glyphs like the run/stop triangle-square).
 *                       Restyle only - AiShellViewModel.cs untouched, every binding/command identical.
 *                       Caught and fixed one real bug while building this: a first-draft custom
 *                       ProgressBar ControlTemplate didn't correctly bind fill width to Value, which
 *                       would have silently shown wrong/no progress - reverted to WPF's default
 *                       ProgressBar chrome with just color overrides instead of shipping that.
 * v1.15.1 (2026-07-18) - Two small fixes on the v1.15.0 rebrand below, same day: (1) AJ AI ON/OFF
 *                       icons re-supplied by Ajmal as proper transparent PNGs (AJ_AI_ON.png /
 *                       AJ_AI_OFF.png) - the original JPGs had a solid background box. (2) Chat
 *                       button/pane label shortened from "C# with AI" to just "C#".
 * v1.15.0 (2026-07-18) - Swapped branding between the AI Assistant panel's two buttons, per
 *                       Ajmal-supplied art (Y:\Ajmal Ps\icon, 3 files): the chat/C#-generation panel
 *                       (ShowAiShellCommand, the dockable pane, AiShellViewModel and every AiShell
 *                       service file's "Tool Name" metadata) is now branded "C# with AI" instead of
 *                       "AJ AI" - new icon Resources/CSharp_with_AI.png. The MCP bridge toggle
 *                       (ToggleAiBridgeCommand, added in the v1.14.0 entry below the same day) is now
 *                       branded just "AJ AI" instead of "AJ AI Bridge", and its ribbon button icon now
 *                       dynamically swaps between Resources/AJ_AI_ON.jpg (connected) and
 *                       AJ_AI_OFF.jpg (disconnected) after every click - via a new static
 *                       App.AiBridgeButton PushButton reference captured when the ribbon is built,
 *                       updated directly in ToggleAiBridgeCommand.Execute() using a fresh IconLoader.
 *                       Old placeholder icons (AJ_AI.png sparkle, AJ_AI_Bridge.png chain-link,
 *                       generated in-house earlier the same day) removed as orphaned once superseded.
 * v1.14.0 (2026-07-18) - New ribbon tool: "AJ AI Bridge" button on the AI Assistant panel
 *                       (ToggleAiBridgeCommand), connecting/disconnecting the live-Revit MCP bridge
 *                       directly from the ribbon. Removed the equivalent Connect/Disconnect control
 *                       from inside the AJ AI chat panel (AiShellViewModel/AiShellView) - it now
 *                       lives only as this standalone button, per Ajmal's request. Both reach the
 *                       same running McpBridgeService instance via a new static AJTools.App.App.
 *                       AiBridge reference set at startup (AiShellPaneProvider now exposes it via a
 *                       public Bridge property) - no second bridge/pipe is created. A new
 *                       BridgeStatusToast helper shows a brief non-blocking confirmation on click,
 *                       since a plain ribbon PushButton has no persistent on/off visual state the way
 *                       the old WPF-bound panel button did. New dedicated icon
 *                       (Resources/AJ_AI_Bridge.png, a chain-link glyph in the same purple/blue/pink
 *                       gradient as AJ_AI.png) instead of reusing the AJ AI sparkle icon.
 * v1.13.11 (2026-07-18) - Renamed the AJ AI pane's live-Revit MCP bridge from "AutoDebugger" to
 *                       "AJ AI Bridge" everywhere: on-screen status/button text, named-pipe
 *                       protocol name (AJTools.AutoDebugger -> AJTools.AjAi), discovery/audit file
 *                       names (autodebugger-bridge.json/autodebugger-audit.jsonl -> ajai-bridge.json/
 *                       ajai-audit.jsonl), the companion Node.js MCP server (mcp-server/index.js,
 *                       package.json), its registration in .mcp.json (server key
 *                       aj-tools-autodebugger -> aj-tools-aj-ai, so the tool names Claude calls are
 *                       now mcp__aj-tools-aj-ai__ping/run_csharp/model_summary), the PowerShell
 *                       fallback caller (.claude/tools/invoke-autodebugger.ps1 ->
 *                       invoke-aj-ai-bridge.ps1), and ~25 of this project's own .claude/knowledge
 *                       and .claude/skills files that referenced the old tool name. Both ends of the
 *                       named pipe and the MCP registration were updated together since they must
 *                       agree - requires reconnecting the AJ AI Bridge toggle in Revit and restarting/
 *                       reconnecting Claude Code's MCP connection before the new tool names resolve;
 *                       until then the old mcp__aj-tools-autodebugger__* tools simply won't exist for
 *                       an already-running agent session. Historical changelog entries above and in
 *                       CHANGELOG.md/debug-log.md/ProjectCleanupTracker.md left as-is (dated record of
 *                       what things were called at the time). Behaviour unchanged otherwise.
 * v1.13.10 (2026-07-18) - Renamed the AI shell's internal branding away from "Gemini"/"Gemini
 *                       Shell" everywhere it isn't the actual provider choice: folder+namespace
 *                       `src/GeminiShell` -> `src/AiShell`, classes GeminiShellConfig/
 *                       GeminiShellViewModel/GeminiShellView/GeminiShellPaneProvider/
 *                       ShowGeminiShellCommand -> AiShellConfig/AiShellViewModel/AiShellView/
 *                       AiShellPaneProvider/ShowAiShellCommand, and the generic chat-message model
 *                       GeminiMessage -> ChatMessage. User-visible strings updated to plain "AJ AI"
 *                       (dockable pane title, ribbon tooltip, TransactionGroup/undo entry name,
 *                       error messages, README/USAGE/testing-checklist docs). Left untouched, on
 *                       purpose: GeminiApiService, its ProviderName/model-name/API-key members, and
 *                       the Gemini/OpenAI provider picker in Settings - those name the actual Google
 *                       Gemini provider, paired with OpenAiApiService, and are supposed to say
 *                       "Gemini". Behaviour unchanged; historical changelog entries above and in
 *                       CHANGELOG.md/debug-log.md left as-is since they're a dated record of what
 *                       things were called at the time, not current-state docs.
 * v1.13.9 (2026-07-18) - Full code review + security hardening pass over the AJ AI (Gemini Shell)
 *                       subsystem and its companion AutoDebugger MCP server (mcp-server/), covering
 *                       all 24 GeminiShell C# files and index.js. Found and fixed: (1)
 *                       GeneratedCodeSafetyValidator only blocked Process.GetCurrentProcess().Kill()
 *                       - a script could still kill ANY other running process on the machine via
 *                       Process.GetProcessesByName(...)/.Kill() without tripping any check; widened
 *                       to block any .Kill( call. (2) McpBridgeService.Start() leaked a named-pipe
 *                       handle on every failed start attempt (e.g. an AppData permission error) -
 *                       the pipe was created and stored before the failure point but nothing ever
 *                       disposed it; the catch block now does. (3) mcp-server/index.js's own
 *                       response timeout (65s) was stale against RevitExecutionService's hard
 *                       backstop, raised to 80s in the previous pass (v1.13.7/v1.13.8's timeframe) -
 *                       a script still legitimately unwinding between 65-80s would get reported to
 *                       the AI agent as "timed out" even though Revit would have finished it
 *                       normally moments later; raised to 90s with an explanatory comment. (4)
 *                       AiTaskWarningBarService's activity banner window set AllowsTransparency to
 *                       False while using Background=Transparent and WindowStyle=None - WPF cannot
 *                       render true alpha transparency without AllowsTransparency=True, so the
 *                       banner likely rendered as a solid black rectangle instead of the intended
 *                       soft floating card with a drop shadow; every other custom-chrome window in
 *                       this project already had this set correctly, this file was the one
 *                       inconsistent case. (5) GeminiApiService's model-list lookup call didn't pass
 *                       through the cancellation token used everywhere else, so pressing Stop
 *                       couldn't interrupt that one specific step. Reviewed and found already solid:
 *                       RevitExecutionService's cancellation/backstop chain, LoopProtectionRewriter,
 *                       GeminiShellConfig's DPAPI-based API key encryption at rest, the
 *                       IsBusy/re-entrancy guards across GeminiShellViewModel, and
 *                       TextMarkerService (standard AvalonEdit sample code).
 * v1.13.8 (2026-07-18) - Third cleanup pass, acting on items the second pass had deliberately
 *                       deferred: (1) SmartMepTagService.MarkDenseZones and
 *                       SmartTagPlacementEngine's parallel-group check both moved off O(n^2) full
 *                       pairwise scans onto the existing AnnotationSpatialIndex as an X/Y coarse
 *                       pre-filter - the exact original 3D DistanceTo <= Radius check is still
 *                       applied to every candidate the index returns, so results are identical, just
 *                       faster on models with many tags/annotations. (2) Consolidated the ~150-line
 *                       duplicated leader-probing reflection block that SmartTagPlacementEngine and
 *                       IntelligentTagArrangerService had each independently reimplemented into a
 *                       single shared LeaderLogicService (GetL1 and friends) - confirmed zero other
 *                       callers before converting it to static. IntelligentTagArrangerService's one
 *                       deliberate behavioral difference (TryApplyLShapeLeader does not toggle the
 *                       leader end condition as a fallback, per its own existing comment) was kept
 *                       intact; only the identical leaf helpers were merged. (3) SharedParamUtils.cs
 *                       trimmed to the handful of methods actually shared across multiple unrelated
 *                       tools (Purge, Duct Standards, a Model class); the feature-specific snapshot/
 *                       restore logic used only by the Shared Param to Family Param conversion moved
 *                       into SharedParamToFamilyParamService.cs, its only real consumer.
 *                       (4) AJ AI's GeneratedCodeSafetyValidator now also blocks `using static` and
 *                       `using X = Y;` type-alias directives, closing the specific bypass documented
 *                       in v1.13.6/v1.13.7 (a script could otherwise rename a blocked call or type to
 *                       dodge the name-based checks) - see that file's own changelog for detail; it
 *                       remains text/regex matching, not an AST/semantic scan. Evaluated and
 *                       deliberately left alone this pass, each for a specific reason (not simply
 *                       skipped): DuctShapeService's reflection-based Shape read (no way to confirm a
 *                       direct DuctType.Shape property exists on every supported Revit version
 *                       2020-2027 without a compiler); LocationDataAssignerWindow.xaml.cs's embedded
 *                       business logic (its loop updates live UI progress controls directly - a safe
 *                       extraction needs a new callback abstraction, a bigger design call than a
 *                       mechanical move); Colorize/FilterPro's near-identical LoadParameters/
 *                       LoadValues (found real behavioral drift between them - different status
 *                       messages, and only Colorize's LoadValues calls ApplyValueFilters()
 *                       immediately - forcing one shared method risks changing live behavior in one
 *                       tool); FilterProState/FilterSelection's ~20-property overlap (several
 *                       properties differ in type on purpose - persisted IDs vs richer runtime
 *                       objects - and verifying a shared base class is safe would mean also auditing
 *                       FilterProStateTracker's conversion logic, not done this pass); FilterCategoryItem/
 *                       PatternItem/GraphicsIdOption's identical wrapper shape (property name differs,
 *                       Name vs DisplayName - unifying risks silently breaking an XAML binding that
 *                       can't be checked visually here). The AJ AI API-key PasswordBox swap flagged in
 *                       the first pass remains skipped for the same reason given then (WPF's
 *                       PasswordBox.Password isn't bindable the same way as a normal TextBox).
 * v1.13.7 (2026-07-18) - Second cleanup pass, acting on items the first pass had deliberately
 *                       deferred: (1) AJ AI's blocking task.Wait() now has a hard backstop
 *                       (MaxLoopRuntime + 20s) instead of no timeout at all - narrows but does not
 *                       fully close the freeze risk for a script that never yields (see
 *                       RevitExecutionService.cs's own notes for why a full fix needs a real Revit/
 *                       Visual Studio environment to verify). (2) Gemini API key now sent via the
 *                       x-goog-api-key header instead of a URL query param, matching
 *                       OpenAiApiService's existing approach - moderate confidence, not verified
 *                       against a live key. (3) Renamed the AJTools.Utils.DuctSelectionFilter /
 *                       AJTools.Services.DuctReferenceDimension.DuctSelectionFilter name collision
 *                       (not a live bug, a future trap). (4) Deduped the four config-store classes'
 *                       identical GetConfigPath() into a shared AppDataConfigStore. (5) Extracted
 *                       the four outlier Commands that had their full tool logic inline instead of a
 *                       Service - CmdReassignLevel, CmdArrangeTextInBox, CmdForceTagLeaderLShape,
 *                       CmdCeilingMagnet - into ReassignLevelService, ArrangeTextInBoxService,
 *                       ForceTagLeaderLShapeService, and CeilingMagnetService respectively; each
 *                       Command is now a thin wrapper. (6) Deduped AnnotationRibbonManager's 28
 *                       repeated icon-loading blocks into the shared RibbonPanelHelper.ApplyIcons.
 *                       No behavior change in any of the above except (1) and (2), documented
 *                       individually. Not done this pass either (still deferred): the two O(n^2) hot
 *                       loops in SmartMepTagService/SmartTagPlacementEngine, the duplicated
 *                       leader-probing block between SmartTagPlacementEngine and
 *                       IntelligentTagArrangerService, Colorize/FilterPro's duplicated Load*
 *                       methods, FilterProState/FilterSelection's ~20-property duplication,
 *                       LocationDataAssignerWindow.xaml.cs's embedded business logic, and the AI
 *                       safety validator's remaining text-matching limitation (still not an AST/
 *                       semantic scan).
 * v1.13.6 (2026-07-17) - Full repo structure/cleanliness + code review pass. AJ Annotation ribbon
 *                        typo fixed ("Auto Dimention" -> "Auto Dimension", visible on the tab/panel/
 *                        button). Removed ~15 confirmed-unused classes/methods (cross-checked
 *                        repo-wide before deletion): RuleTypeItem, DuctDimensionBuildResult,
 *                        DuctPipeSelectionFilter, ValidationHelper.ValidateViewType/
 *                        ValidateCropBoxActive, two unused TransactionHelper.ExecuteSafe overloads,
 *                        CmdForceTagLeaderLShape.AdjustElbowSide, CmdCreateMepOpenings.
 *                        ShouldRunDirectOpenings, AutoDimensionService.GetCurveDirection,
 *                        LeaderLogicService.ComputeSideElbow/DetermineToggleState,
 *                        GraphicsSelectionService.GetValidPreselectedElementIds,
 *                        QuickParallelDimensionService's dead single-arg Execute overload,
 *                        MepOpeningSourceElement.SourceLabel, LinkedSearchWindow's dead
 *                        Identify/Reset override handlers, FilterProWindow.GetPatternItem.
 *                        AJ AI safety hardening: GeneratedCodeSafetyValidator now blocks #r/#load
 *                        script directives (previously a full, undetected bypass of every other
 *                        check - RoslynService never disabled Roslyn's default directive resolver),
 *                        blocks reflection-based indirect member access (GetMethod/GetProperty/
 *                        GetField + Invoke/SetValue/GetValue), and adds SmtpClient/Dns/Ping/
 *                        Process.Kill/Environment.FailFast to the blocklist. RevitExecutionService
 *                        now guarantees its Task always completes even if TransactionGroup.RollBack()
 *                        itself throws after a failed Commit() (previously could hang the AJ AI pane
 *                        on IsBusy forever). Fixed a real null-deref risk in
 *                        CmdRevisionCloudByElements (Document.ActiveView can be null). Consolidated
 *                        the RibbonManager/AnnotationRibbonManager duplicate GetOrCreatePanel into a
 *                        shared RibbonPanelHelper, and ViewCropExtentsService's duplicate IsFinite
 *                        into the existing ViewCropGeometryProjectionHelper.IsFinite. Replaced two
 *                        duplicated "10mm in feet" literals with Constants.MM_TO_FEET. Documented
 *                        (rather than silently swallowed) 6 previously-empty catch blocks across
 *                        App.cs, CmdSectionMarkVisibility, and CmdForceTagLeaderLShape's reflection
 *                        helpers - behaviour unchanged, but a future failure there is no longer
 *                        invisible. NOT done this pass (flagged for a follow-up, not attempted
 *                        blind without a Revit/Visual Studio environment to verify against):
 *                        larger structural refactors (CmdCeilingMagnet/CmdForceTagLeaderLShape/
 *                        CmdReassignLevel/CmdArrangeTextInBox still have full algorithms inline
 *                        instead of a Service; SmartTag/TagArrange's O(n^2) hot loops; the
 *                        AnnotationRibbonManager icon-loading duplication; config-store base-class
 *                        dedup), and the AI safety validator's deeper limitation (it is still text/
 *                        regex matching, not an AST/semantic scan - ordinary idioms like `using
 *                        static` or type aliasing can still bypass it).
 * v1.13.1 (2026-07-15) - Fixed Transfer View Templates: the Filter textbox had a hard-coded Height="30",
 *                        shorter than what the shared ModernTextBox style's Padding="8,6" needs at
 *                        MinHeight="34" - typed characters were getting clipped at the bottom. Changed to
 *                        MinHeight="34" to match every other filter box in the app. No other window affected
 *                        (only this one had an explicit Height override on a ModernTextBox).
 * v1.12.0 (2026-07-13) - Transfer View Templates now remembers the last-used Copy From / Copy To
 *                        projects (in-memory for the current Revit session, matched by document
 *                        title) and pre-selects them next time the tool opens, saved only after a
 *                        successful Transfer - same convention as Filter Pro's own state memory.
 * v1.11.2 (2026-07-13) - Fixed Pin / Unpin Elements: mouse-wheel scrolling did nothing over the category
 *                        lists (only dragging the scrollbar thumb worked) - the window's outer ScrollViewer
 *                        (added so both list groups can scroll once they exceed MaxHeight) was having its
 *                        mouse wheel input silently swallowed by each ListBox's own internal ScrollViewer.
 * v1.11.1 (2026-07-13) - Pin / Unpin Elements: added Grids and Levels as two more pinnable/unpinnable
 *                        Model groups, same pattern as the existing category groups.
 * v1.11.0 (2026-07-13) - Added the Colorize tool (View panel, next to Filter Pro) to this live project.
 *                        It previously existed only in the stale pre-multiversion "AJ Tools\" tree
 *                        (hand-ported there on 2026-07-02 and never carried into root src/), so it
 *                        could never appear on the ribbon no matter how many times the add-in was
 *                        rebuilt - this fixes that by porting it here properly, wired into the ribbon.
 * v1.10.5 (2026-07-12) - Restyled the AI activity banner to match the AJ Tools dark theme.
 * v1.10.4 (2026-07-12) - Fixed the AI activity banner to use Revit's UI dispatcher.
 * v1.10.3 (2026-07-12) - Ensured the AI activity banner remains visible long enough for fast tasks.
 * v1.10.2 (2026-07-12) - Added a temporary, non-blocking AI activity banner for AutoDebugger tasks.
 * v1.10.1 (2026-07-11) - AutoDebugger performance pass: persistent authenticated named-pipe requests
 *                         and a bounded cache for compiled safe Roslyn scripts. Live Revit model data
 *                         is intentionally never cached.
 * v1.9.0 (2026-07-05) - Added the Arrange Text in Box tool on a new "Text" panel (AJ Annotation tab);
 *                       ported from the pyRevit "Text Box Arrange Loop" script. No other tool changed.
 * v1.8.0 (2026-07-01) - Full project audit pass: added Pipe Sizing tool (MEP panel) with its own metadata,
 *                       report, and CSV export; hardened the AJ AI shell with GeneratedCodeSafetyValidator
 *                       (blocks process/registry/network/reflection/file-delete calls, flags destructive
 *                       Revit ops for confirmation), AiShellActivityLogger, and AiShellConstants; wired the
 *                       previously-unused CmdPurgeUnusedFamilyParametersAvailability into its ribbon button
 *                       so Purge Family Parameters is only enabled in the Family Editor; fixed the About
 *                       panel's inconsistent "Aj tool" label; removed 8 orphaned icon resources and a
 *                       stray local dev script/screenshot from src. All existing tool behaviour unchanged.
 * v1.7.0 (2026-07-01) - AJ Annotation tab refactor/audit: full metadata blocks across every Dimensions,
 *                       Auto Duct Dimension, Tags, Duct Flow, Revision Cloud, and Text tool; single-undo
 *                       grouping for Copy Dimension Text, Copy Text, and continuous Revision Clouds; About
 *                       and both ribbon-builder files standardized. All tool behaviour unchanged.
 * v1.6.0 (2026-07-01) - Modify / MEP / Coordination / Data / Manage / Family panels refactor/audit: full
 *                       metadata blocks across every tool in these panels; Match Elevation now a single
 *                       undo step; Reassign Level gains a Full-Project bulk-edit confirmation; version-safe
 *                       ElementId access (Linked ID Viewer, Reassign Level); Duct Standards no-document
 *                       path cancels cleanly with a project guard; removed loose scratch scripts from src.
 *                       All tool behaviour unchanged.
 * v1.5.4 (2026-06-30) - Datums panel refactor/audit: full metadata blocks across all datum tools, removed success popups (silent success), single-undo batch for window-select Flip Bubbles, Family-Editor guards, and de-duplicated reset logic. Datum behaviour unchanged.
 * v1.5.3 (2026-06-30) - Graphics panel refactor/audit: single-undo TransactionGroup for both Match tools, view-scoped Reset Element Graphics in View, full metadata blocks, and 2024+ ElementId readiness. Graphics behaviour unchanged.
 * v1.5.2 (2026-06-27) - View Crop tool refactor/audit pass: shared helpers, bulk-edit confirmation, ElementId helper for 2024+ readiness. Behaviour of View Crop unchanged.
 * v1.5.0 (2026-05-30) - Added Filter Pro Search and Sort capabilities.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AJ Tools")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("AJ Tools")]
[assembly: AssemblyCopyright("Copyright (c) 2025-2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
//[assembly: Guid("fe1f581f-9ea0-4752-b870-7192ae828b82")]
[assembly: Guid("fe1f581f-9ea0-4752-b870-7192ae828b82")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("1.54.0.0")]
[assembly: AssemblyFileVersion("1.54.0.0")]

// AJ Tools is a Revit add-in: Windows-only by definition, on every supported Revit version.
// On the .NET 5+ targets (Revit 2025+) the SDK would normally stamp this assembly with
// [SupportedOSPlatform("windows")] automatically because the TargetFramework is
// net8.0-windows / net10.0-windows - but GenerateAssemblyInfo is false in the .csproj (this
// file is maintained by hand), which suppresses that stamp. Without it the CA1416 analyzer
// treats every Windows-only WinForms/WPF call (ColorDialog, FolderBrowserDialog, Screen) as
// if it might run on Linux and warns. Declaring it here states the truth and keeps the
// newer-Revit configurations as warning-free as the 2020 baseline.
// Guarded: net472 / net48 (Revit 2020-2024) have no such attribute type.
#if NET5_0_OR_GREATER
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
