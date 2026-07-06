#region Metadata
/*
 * Tool Name     : AJ Tools Assembly Metadata
 * File Name     : AssemblyInfo.cs
 * Purpose       : Defines assembly-level metadata and suite version for the AJ Tools add-in.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.11.1
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-06
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / .NET Fx 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
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
 * v1.11.1 (2026-07-06) - Split Revit 2020-2024 packaging into separate
 *                        API-specific legacy builds using matching Revit SDK
 *                        reference packages.
 * v1.11.0 (2026-07-06) - Added modern Revit builds: Revit 2025-2026 target
 *                        .NET 8 and Revit 2027 targets .NET 10, with
 *                        versioned installer payloads for 2020-2027.
 * v1.10.1 (2026-07-06) - Installer now stages AJ Tools add-in folders and manifests
 *                        for Revit 2020-2027. Revit 2025-2027 remain NEEDS_REVIEW
 *                        until the separate modern .NET/Revit API build is completed.
 * v1.10.0 (2026-07-03) - Added Opening split button in the MEP panel: Opening Settings
 *                        saves element-specific shape and buffer rules, insulation handling,
 *                        and merge distance; Create Openings generates direct openings for
 *                        selected pipes, ducts, cable trays, and conduits in current-model
 *                        walls, floors/slabs, and beams.
 * v1.9.1 (2026-07-02) - Colorize follow-up fixes: Shuffle Colors no longer closes the window (it
 *                       applies immediately, its own undo step, so it can be clicked repeatedly to
 *                       keep re-shuffling until Close is pressed — matches Filter Pro's own action
 *                       buttons); removed the Rule Type step entirely (Colorize now always matches
 *                       selected values with Equals); fixed BuildOverrideSettings never turning on
 *                       SetSurfaceForegroundPatternVisible/SetCutForegroundPatternVisible, which made
 *                       the Projection/Cut Fill Pattern checkboxes silently do nothing in both Colorize
 *                       and Filter Pro's own Shuffle Colors.
 * v1.9.0 (2026-07-02) - Added Colorize tool (View panel): colorizes elements by category or by
 *                       Filter-Pro-style parameter/rule/value matching directly in the active view (or
 *                       any selected views) via per-element OverrideGraphicSettings — no
 *                       ParameterFilterElement is ever created, unlike Filter Pro. UI mirrors Filter
 *                       Pro's own Selection and Apply tabs (search/sort, rule types, multi-view apply
 *                       scope) minus the Naming Convention tab and Create/Apply-To-View buttons, since
 *                       Colorize has nothing persistent to name or save; a single Shuffle Colors action
 *                       colorizes and applies in one step. Reuses Filter Pro's category/parameter/value
 *                       data provider and colour palette; extracted FilterCreator's rule-building logic
 *                       into shared FilterRuleBuilder and FilterApplier's override-construction logic
 *                       into shared BuildOverrideSettings so both tools use the same engine. Ports and
 *                       fixes the retired pyRevit Colorize tool (no view-type guard, no transaction
 *                       rollback handling, and an O(categories x elements) counting loop in the old
 *                       version).
 *                       Filter Pro's own behaviour is unchanged.
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
[assembly: AssemblyVersion("1.11.1.0")]
[assembly: AssemblyFileVersion("1.11.1.0")]
