#region Metadata
/*
 * Tool Name     : AJ Tools Assembly Metadata
 * File Name     : AssemblyInfo.cs
 * Purpose       : Defines assembly-level metadata and suite version for the AJ Tools add-in.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.13.5
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-15
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
[assembly: AssemblyVersion("1.13.5.0")]
[assembly: AssemblyFileVersion("1.13.5.0")]
