#region Metadata
/*
 * Tool Name     : AJ Tools Assembly Metadata
 * File Name     : AssemblyInfo.cs
 * Purpose       : Defines assembly-level metadata and suite version for the AJ Tools add-in.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.23.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-20
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
[assembly: AssemblyVersion("1.23.0.0")]
[assembly: AssemblyFileVersion("1.23.0.0")]
