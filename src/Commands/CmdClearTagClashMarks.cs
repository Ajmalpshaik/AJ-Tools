#region Metadata
/*
 * Tool Name     : Clear Tag Clash Marks
 * File Name     : CmdClearTagClashMarks.cs
 * Purpose       : Removes the colour Fix Tag Clash puts on tags it could not separate.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-16
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.TagClash (TagClashHighlighter),
 *                 AJTools.Utils (DialogHelper, ValidationHelper)
 *
 * Input         : Active View.
 * Output        : Graphic overrides reset on every tag in that view.
 *
 * Notes         :
 * - This resets the override on EVERY tag in the view, not only the ones Fix Tag Clash coloured.
 *   Nothing is written to the model to record which tags were marked, so a blanket reset is the only
 *   way to be certain no mark is left behind. A deliberate manual override on a tag in this view is
 *   cleared too - the confirmation prompt says so before anything happens.
 * - View-specific: no other view is touched.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.TagClash;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Clears the clash colour from the tags in the active view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdClearTagClashMarks : IExternalCommand
    {
        private const string ToolTitle = "Clear Tag Clash Marks";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                View activeView = doc.ActiveView;
                if (activeView == null || activeView.IsTemplate || activeView is ViewSheet)
                {
                    DialogHelper.ShowError(
                        ToolTitle,
                        "Please run this tool in a normal project view, not a sheet or a view template.");
                    return Result.Cancelled;
                }

                // This tool already asked before doing anything, but it never said how BIG the job was.
                // On a heavy view that is the difference between a click and a long freeze, so the count
                // goes into the question that is already being asked rather than a second dialog on top.
                int tagCount = TagClashHighlighter.CountTagsInView(doc, activeView);

                string sizeLine = tagCount > DialogHelper.LongRunConfirmThreshold
                    ? string.Format(
                        "This resets the graphic override on all {0} tags in \"{1}\".\n\n"
                        + "Revit will be busy while it works and can't be stopped part way.\n\n",
                        tagCount, activeView.Name)
                    : string.Format(
                        "This resets the graphic override on every tag in \"{0}\".\n\n",
                        activeView.Name);

                bool proceed = DialogHelper.ShowYesNo(
                    ToolTitle,
                    sizeLine
                        + "If you have coloured a tag in this view on purpose, that colour is removed too.\n\n"
                        + "Continue?");

                if (!proceed)
                    return Result.Cancelled;

                int reset;
                using (Transaction transaction = new Transaction(doc, "Clear Tag Clash Marks"))
                {
                    transaction.Start();
                    reset = TagClashHighlighter.ClearAll(doc, activeView);

                    if (reset > 0)
                        transaction.Commit();
                    else
                        transaction.RollBack();
                }

                if (reset == 0)
                {
                    DialogHelper.ShowInfo(ToolTitle, "No tags were found in this view, so nothing was changed.");
                    return Result.Cancelled;
                }

                DialogHelper.ShowInfo(
                    ToolTitle,
                    string.Format("Cleared the override on {0} tag(s) in \"{1}\".", reset, activeView.Name));

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
