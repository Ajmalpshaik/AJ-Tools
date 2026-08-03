#region Metadata
/*
 * Tool Name     : Reassign Reference Level
 * File Name     : CmdReassignLevel.cs
 * Purpose       : Reassigns supported MEP elements (MEP curves, free-standing family instances, spaces)
 *                 from one level to another without moving them physically - either across the whole
 *                 project (FROM level -> TO level), or just the elements already selected in Revit
 *                 (TO level only; each element's own current level is used as its FROM).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.4.0
 *
 * Created Date  : 2026-04-14
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils, AJTools.Services.ReassignLevel,
 *                 AJTools.UI.ReassignLevel.ReassignLevelWindow (WPF)
 *
 * Input         : Whole Project - FROM level and TO level chosen in a dialog. OR Selected Elements -
 *                 whatever is already selected in Revit when the tool is run, plus a TO level.
 * Output        : Matching elements re-pointed to the TO level (host offset compensated so they stay put);
 *                 single undo step; final report of reassigned / failed / already-on-target / skipped counts.
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Whole Project scope: a confirmation dialog states how many elements will change before any edit.
 * - Selected Elements scope: uses whatever was already selected in Revit before the tool was run (the
 *   picker window is modal, so Revit's selection can't change while it's open); if nothing eligible is
 *   selected, that option is disabled in the window with an explanatory tooltip rather than a dead-end
 *   Run click.
 * - Hosted family instances are intentionally skipped (their level follows the host) and reported; in
 *   Selected Elements scope, unsupported selected items and elements already on the TO level are also
 *   reported separately (not counted as failures).
 * - All reassignments run inside ONE transaction, so a single Ctrl+Z reverses the whole operation.
 * - Thin command wrapper: context/selection validation, transaction handling, and result dialogs live
 *   here; the level-reassignment algorithm itself lives in Services/ReassignLevel/ReassignLevelService.cs.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-14) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: full metadata block; added Full-Project bulk-edit confirmation;
 *                       version-safe ElementId access. Reassign behaviour unchanged.
 * v1.2.0 (2026-07-17) - Extracted the level-reassignment algorithm (eligibility checks, host-offset
 *                       compensation, space copy logic) into Services/ReassignLevel/ReassignLevelService.cs
 *                       (code review cleanup pass) - no behavior change.
 * v1.3.0 (2026-07-28) - UI only, reassignment logic untouched: the WinForms level prompt was replaced by
 *                       ReassignLevelWindow (themed WPF, matching the rest of the suite). Fixes: picking
 *                       the same level twice used to close the dialog, show an error popup and cancel the
 *                       command - now caught live inline with Run disabled; the "Reassign Elements" button
 *                       overlapped Cancel by 15 px; the dialog had no owner so it could drop behind Revit;
 *                       the fixed-size intro label could clip long text (now wraps). Added a Swap button
 *                       and an up-front note that the scope is the whole project and hosted elements are
 *                       skipped - previously only discoverable from the report after the fact.
 * v1.4.0 (2026-07-28) - Added a Selected Elements scope: pick elements in Revit (or already have them
 *                       selected), choose just a TO level, and only those elements are reassigned - no
 *                       FROM level needed since it is read per element. Whole Project path is unchanged
 *                       byte-for-byte; the new path is an additional branch using the new
 *                       ReassignLevelService.CollectCandidatesFromSelection/ReassignElementToLevel.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.ReassignLevel;
using AJTools.UI.ReassignLevel;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Reassigns supported MEP elements from one level to another while keeping physical positions unchanged.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdReassignLevel : IExternalCommand
    {
        private const string ToolTitle = "Reassign Level";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            List<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (allLevels.Count < 2)
            {
                DialogHelper.ShowError(ToolTitle, "At least 2 levels are required.");
                return Result.Cancelled;
            }

            ICollection<ElementId> preSelectedIds = uidoc.Selection.GetElementIds() ?? new List<ElementId>();
            int totalSelectedCount = preSelectedIds.Count;
            List<Element> selectedCandidates = ReassignLevelService.CollectCandidatesFromSelection(
                doc, preSelectedIds, out int selSkippedHosted, out int selSkippedUnsupported);

            if (!TryPromptScope(
                    commandData.Application,
                    allLevels,
                    totalSelectedCount,
                    selectedCandidates.Count,
                    out ReassignScope scope,
                    out Level fromLevel,
                    out Level toLevel))
            {
                return Result.Cancelled;
            }

            bool isRevit2020OrAbove = ReassignLevelService.IsRevit2020OrAbove(commandData.Application);
            var offsetHelper = new ReassignLevelService.OffsetHelper(doc, isRevit2020OrAbove);

            if (scope == ReassignScope.SelectedElements)
            {
                return ExecuteSelectedElementsScope(
                    doc, toLevel, selectedCandidates, selSkippedHosted, selSkippedUnsupported, offsetHelper, ref message);
            }

            ElementId fromId = fromLevel.Id;
            ElementId toId = toLevel.Id;

            List<Element> candidates = ReassignLevelService.CollectCandidates(doc, fromId, out int skippedHosted);

            if (candidates.Count == 0)
            {
                DialogHelper.ShowInfo(
                    ToolTitle,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "No elements were found on \"{0}\" that can be reassigned.",
                        fromLevel.Name));
                return Result.Cancelled;
            }

            // Full Project scope - confirm the bulk change before touching the model.
            string confirmMessage = string.Format(
                CultureInfo.CurrentCulture,
                "This will reassign {0} element(s) from \"{1}\" to \"{2}\" across the whole project.\n\n" +
                "Elements stay in the same physical position. Continue?",
                candidates.Count,
                fromLevel.Name,
                toLevel.Name);
            if (!DialogHelper.ShowYesNo(ToolTitle, confirmMessage))
            {
                return Result.Cancelled;
            }

            int okCount = 0;
            int failCount = 0;

            try
            {
                using (var tx = new Transaction(doc, string.Format("Reassign Level: {0} to {1}", fromLevel.Name, toLevel.Name)))
                {
                    tx.Start();

                    foreach (Element element in candidates)
                    {
                        try
                        {
                            if (ReassignLevelService.ReassignElement(doc, element, fromLevel, toLevel, fromId, toId, offsetHelper))
                            {
                                okCount++;
                            }
                            else
                            {
                                failCount++;
                            }
                        }
                        catch
                        {
                            failCount++;
                        }
                    }

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            string resultMessage = string.Format(
                CultureInfo.CurrentCulture,
                "{0} element(s) reassigned\n\nFROM : {1}\nTO   : {2}",
                okCount,
                fromLevel.Name,
                toLevel.Name);

            if (failCount > 0)
            {
                resultMessage += string.Format(CultureInfo.CurrentCulture, "\n\n{0} element(s) failed.", failCount);
            }

            if (skippedHosted > 0)
            {
                resultMessage += string.Format(CultureInfo.CurrentCulture, "\n\n{0} hosted element(s) skipped.", skippedHosted);
            }

            resultMessage += "\n\nElements should stay in the same physical location.";
            DialogHelper.ShowInfo("Reassign Level - Complete", resultMessage);
            return Result.Succeeded;
        }

        /// <summary>
        /// Runs the Selected Elements scope: reassigns only the already-eligible candidates gathered
        /// from Revit's selection before the window opened, to the single TO level chosen there. Each
        /// element's own current level is used as its FROM (a selection can span several levels at
        /// once), via ReassignLevelService.ReassignElementToLevel.
        /// </summary>
        private static Result ExecuteSelectedElementsScope(
            Document doc,
            Level toLevel,
            List<Element> candidates,
            int skippedHosted,
            int skippedUnsupported,
            ReassignLevelService.OffsetHelper offsetHelper,
            ref string message)
        {
            if (candidates.Count == 0)
            {
                DialogHelper.ShowInfo(ToolTitle, "None of the currently selected elements can be reassigned.");
                return Result.Cancelled;
            }

            string confirmMessage = string.Format(
                CultureInfo.CurrentCulture,
                "This will reassign {0} of your selected element(s) to \"{1}\".\n\n" +
                "Elements stay in the same physical position. Continue?",
                candidates.Count,
                toLevel.Name);
            if (!DialogHelper.ShowYesNo(ToolTitle, confirmMessage))
            {
                return Result.Cancelled;
            }

            int okCount = 0;
            int failCount = 0;
            int alreadyOnTargetCount = 0;
            ElementId toId = toLevel.Id;

            try
            {
                using (var tx = new Transaction(doc, string.Format(CultureInfo.CurrentCulture, "Reassign Level: Selection to {0}", toLevel.Name)))
                {
                    tx.Start();

                    foreach (Element element in candidates)
                    {
                        try
                        {
                            if (ReassignLevelService.ReassignElementToLevel(doc, element, toLevel, toId, offsetHelper, out bool alreadyOnTarget))
                            {
                                okCount++;
                            }
                            else if (alreadyOnTarget)
                            {
                                alreadyOnTargetCount++;
                            }
                            else
                            {
                                failCount++;
                            }
                        }
                        catch
                        {
                            failCount++;
                        }
                    }

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            string resultMessage = string.Format(
                CultureInfo.CurrentCulture,
                "{0} element(s) reassigned\n\nTO : {1}",
                okCount,
                toLevel.Name);

            if (failCount > 0)
            {
                resultMessage += string.Format(CultureInfo.CurrentCulture, "\n\n{0} element(s) failed.", failCount);
            }

            if (alreadyOnTargetCount > 0)
            {
                resultMessage += string.Format(CultureInfo.CurrentCulture, "\n\n{0} element(s) were already on this level.", alreadyOnTargetCount);
            }

            if (skippedHosted > 0)
            {
                resultMessage += string.Format(CultureInfo.CurrentCulture, "\n\n{0} hosted element(s) in your selection were skipped.", skippedHosted);
            }

            if (skippedUnsupported > 0)
            {
                resultMessage += string.Format(
                    CultureInfo.CurrentCulture,
                    "\n\n{0} selected item(s) are not supported by this tool (only MEP curves, free-standing families, and spaces can be reassigned).",
                    skippedUnsupported);
            }

            resultMessage += "\n\nElements should stay in the same physical location.";
            DialogHelper.ShowInfo("Reassign Level - Complete", resultMessage);
            return Result.Succeeded;
        }

        private static bool TryPromptScope(
            UIApplication uiapp,
            IList<Level> levels,
            int totalSelectedCount,
            int eligibleSelectedCount,
            out ReassignScope scope,
            out Level fromLevel,
            out Level toLevel)
        {
            scope = ReassignScope.WholeProject;
            fromLevel = null;
            toLevel = null;

            var window = new ReassignLevelWindow(levels, totalSelectedCount, eligibleSelectedCount);

            if (uiapp != null)
            {
                new WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };
            }

            if (window.ShowDialog() != true)
                return false;

            scope = window.Scope;
            fromLevel = window.FromLevel;
            toLevel = window.ToLevel;

            if (toLevel == null)
                return false;

            // The window disables Run unless the combination is valid; re-check anyway.
            if (scope == ReassignScope.WholeProject && (fromLevel == null || fromLevel.Id == toLevel.Id))
                return false;

            return true;
        }
    }
}
