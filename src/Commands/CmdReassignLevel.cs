#region Metadata
/*
 * Tool Name     : Reassign Reference Level
 * File Name     : CmdReassignLevel.cs
 * Purpose       : Reassigns supported MEP elements (MEP curves, free-standing family instances, spaces)
 *                 from one level to another across the whole project without moving them physically.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-14
 * Last Updated  : 2026-07-17
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils, AJTools.Services.ReassignLevel
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
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.ReassignLevel;
using AJTools.Utils;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace AJTools.Commands
{
    /// <summary>
    /// Reassigns supported MEP elements from one level to another while keeping physical positions unchanged.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdReassignLevel : IExternalCommand
    {
        private const string ToolTitle = "Reassign Level";
        private const double MetersPerFoot = 0.3048;

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

            if (!TryPromptLevels(allLevels, out Level fromLevel, out Level toLevel))
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

        private static bool TryPromptLevels(IList<Level> levels, out Level fromLevel, out Level toLevel)
        {
            fromLevel = null;
            toLevel = null;

            var levelItems = levels.Select(level => new LevelChoice(level)).ToList();

            using (var form = new WinForms.Form())
            using (var intro = new WinForms.Label())
            using (var fromLabel = new WinForms.Label())
            using (var fromCombo = new WinForms.ComboBox())
            using (var toLabel = new WinForms.Label())
            using (var toCombo = new WinForms.ComboBox())
            using (var okButton = new WinForms.Button())
            using (var cancelButton = new WinForms.Button())
            {
                form.Text = ToolTitle;
                form.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                form.StartPosition = WinForms.FormStartPosition.CenterScreen;
                form.ClientSize = new Drawing.Size(460, 225);
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;

                intro.Text = "Switch the level reference of supported MEP elements from one level to another without moving them.";
                intro.AutoSize = false;
                intro.Size = new Drawing.Size(430, 32);
                intro.Location = new Drawing.Point(15, 12);

                fromLabel.Text = "FROM Level:";
                fromLabel.AutoSize = true;
                fromLabel.Font = new Drawing.Font(form.Font, Drawing.FontStyle.Bold);
                fromLabel.ForeColor = Drawing.Color.FromArgb(192, 0, 0);
                fromLabel.Location = new Drawing.Point(15, 56);

                fromCombo.DropDownStyle = WinForms.ComboBoxStyle.DropDownList;
                fromCombo.FormattingEnabled = true;
                fromCombo.Width = 430;
                fromCombo.Location = new Drawing.Point(15, 76);

                toLabel.Text = "TO Level:";
                toLabel.AutoSize = true;
                toLabel.Font = new Drawing.Font(form.Font, Drawing.FontStyle.Bold);
                toLabel.ForeColor = Drawing.Color.FromArgb(0, 102, 0);
                toLabel.Location = new Drawing.Point(15, 112);

                toCombo.DropDownStyle = WinForms.ComboBoxStyle.DropDownList;
                toCombo.FormattingEnabled = true;
                toCombo.Width = 430;
                toCombo.Location = new Drawing.Point(15, 132);

                foreach (LevelChoice item in levelItems)
                {
                    fromCombo.Items.Add(item);
                    toCombo.Items.Add(item);
                }

                if (fromCombo.Items.Count > 0)
                {
                    fromCombo.SelectedIndex = 0;
                }

                if (toCombo.Items.Count > 1)
                {
                    toCombo.SelectedIndex = 1;
                }
                else if (toCombo.Items.Count > 0)
                {
                    toCombo.SelectedIndex = 0;
                }

                okButton.Text = "Reassign Elements";
                okButton.DialogResult = WinForms.DialogResult.OK;
                okButton.Width = 130;
                okButton.Location = new Drawing.Point(235, 178);

                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = WinForms.DialogResult.Cancel;
                cancelButton.Width = 95;
                cancelButton.Location = new Drawing.Point(350, 178);

                form.Controls.Add(intro);
                form.Controls.Add(fromLabel);
                form.Controls.Add(fromCombo);
                form.Controls.Add(toLabel);
                form.Controls.Add(toCombo);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() != WinForms.DialogResult.OK)
                {
                    return false;
                }

                LevelChoice fromChoice = fromCombo.SelectedItem as LevelChoice;
                LevelChoice toChoice = toCombo.SelectedItem as LevelChoice;

                if (fromChoice == null || toChoice == null)
                {
                    DialogHelper.ShowError(ToolTitle, "Please select both levels.");
                    return false;
                }

                if (fromChoice.Level.Id == toChoice.Level.Id)
                {
                    DialogHelper.ShowError(ToolTitle, "FROM and TO levels must be different.");
                    return false;
                }

                fromLevel = fromChoice.Level;
                toLevel = toChoice.Level;
                return true;
            }
        }

        private static double FeetToMeters(double feet)
        {
            return feet * MetersPerFoot;
        }

        private sealed class LevelChoice
        {
            public LevelChoice(Level level)
            {
                Level = level;
                Label = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1:0.000} m)",
                    level?.Name ?? "<Unnamed>",
                    level == null ? 0 : FeetToMeters(level.Elevation));
            }

            public Level Level { get; }
            public string Label { get; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
