#region Metadata
/*
 * Tool Name     : AJ Tools Assembly Metadata
 * File Name     : AssemblyInfo.cs
 * Purpose       : Defines assembly-level metadata and suite version for the AJ Tools add-in.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.40.4
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-08-05
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
[assembly: AssemblyVersion("1.40.4.0")]
[assembly: AssemblyFileVersion("1.40.4.0")]

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
