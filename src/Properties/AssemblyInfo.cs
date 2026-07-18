#region Metadata
/*
 * Tool Name     : AJ Tools Assembly Metadata
 * File Name     : AssemblyInfo.cs
 * Purpose       : Defines assembly-level metadata and suite version for the AJ Tools add-in.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.13.8
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-18
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
[assembly: AssemblyVersion("1.13.8.0")]
[assembly: AssemblyFileVersion("1.13.8.0")]
