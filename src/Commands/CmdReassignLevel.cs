#region Metadata
/*
 * Tool Name     : Reassign Reference Level
 * File Name     : CmdReassignLevel.cs
 * Purpose       : Reassigns supported MEP elements (MEP curves, free-standing family instances, spaces)
 *                 from one level to another across the whole project without moving them physically.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.0
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
 * Input         : Full Project - FROM level and TO level chosen in a dialog.
 * Output        : Matching elements re-pointed to the TO level (host offset compensated so they stay put);
 *                 single undo step; final report of reassigned / failed / skipped counts.
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Scope is Full Project, so a confirmation dialog states how many elements will change before any edit.
 * - Hosted family instances are intentionally skipped (their level follows the host) and reported.
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

            if (!TryPromptLevels(commandData.Application, allLevels, out Level fromLevel, out Level toLevel))
            {
                return Result.Cancelled;
            }

            ElementId fromId = fromLevel.Id;
            ElementId toId = toLevel.Id;
            bool isRevit2020OrAbove = ReassignLevelService.IsRevit2020OrAbove(commandData.Application);
            var offsetHelper = new ReassignLevelService.OffsetHelper(doc, isRevit2020OrAbove);

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

        private static bool TryPromptLevels(
            UIApplication uiapp,
            IList<Level> levels,
            out Level fromLevel,
            out Level toLevel)
        {
            fromLevel = null;
            toLevel = null;

            var window = new ReassignLevelWindow(levels);

            if (uiapp != null)
            {
                new WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };
            }

            if (window.ShowDialog() != true)
                return false;

            fromLevel = window.FromLevel;
            toLevel = window.ToLevel;

            // The window disables Run unless both are set and different; re-check anyway.
            if (fromLevel == null || toLevel == null || fromLevel.Id == toLevel.Id)
                return false;

            return true;
        }
    }
}
