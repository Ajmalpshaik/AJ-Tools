// Tool Name: Copy View Range
// Description: Copies the active view's range settings to selected plan views.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services;

namespace AJTools.Commands
{
    /// <summary>
    /// Copies view range from the active plan view or pastes a cached range to other plan views.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdCopyViewRange : IExternalCommand
    {
        private static CopiedViewRange _cachedRange;

        /// <summary>
        /// Executes the copy/paste workflow for view ranges.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Copy View Range", "Open a project view before running this command.");
                return Result.Failed;
            }

            Document doc = uiDoc.Document;
            ViewPlan activePlan = doc.ActiveView as ViewPlan;
            if (activePlan == null || activePlan.IsTemplate)
            {
                TaskDialog.Show("Copy View Range", "Active view must be a non-template plan view.");
                return Result.Cancelled;
            }

            TaskDialogResult action = PromptForAction();
            if (action == TaskDialogResult.CommandLink1)
            {
                return CopyFromActiveView(activePlan);
            }

            if (action == TaskDialogResult.CommandLink2)
            {
                return PasteToSelectedViews(doc, activePlan);
            }

            return Result.Cancelled;
        }

        private static TaskDialogResult PromptForAction()
        {
            TaskDialog dialog = new TaskDialog("Copy View Range")
            {
                MainInstruction = _cachedRange == null ? "Copy view range from the active view" : "Choose what to do",
                MainContent = _cachedRange == null
                    ? "Copy the active plan view's range so you can paste it to other plan views."
                    : "Use the cached view range or copy a fresh one.",
                CommonButtons = TaskDialogCommonButtons.Cancel,
                AllowCancellation = true
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Copy from active view");
            if (_cachedRange != null)
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Paste to other plan views");

            return dialog.Show();
        }

        private static Result CopyFromActiveView(ViewPlan sourceView)
        {
            try
            {
                _cachedRange = CopiedViewRange.From(sourceView);

                TaskDialog.Show("Copy View Range", $"Copied view range from '{sourceView.Name}'.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Copy View Range", "Could not copy view range:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static Result PasteToSelectedViews(Document doc, ViewPlan activePlan)
        {
            if (_cachedRange == null)
            {
                TaskDialog.Show("Copy View Range", "Nothing stored yet. Copy a view range first.");
                return Result.Cancelled;
            }

            List<ViewPlan> targets = GetTargetViews(doc, activePlan);
            if (targets.Count == 0)
            {
                TaskDialog.Show("Copy View Range", "No plan views selected.");
                return Result.Cancelled;
            }

            int updated = 0;
            int skipped = 0;

            using (Transaction t = new Transaction(doc, "Paste View Range"))
            {
                t.Start();
                foreach (ViewPlan target in targets)
                {
                    if (target.IsTemplate || !CanEditViewRange(target))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        _cachedRange.ApplyTo(target);
                        updated++;
                    }
                    catch
                    {
                        skipped++;
                    }
                }
                t.Commit();
            }

            string msg = $"Applied view range to {updated} view(s) from '{_cachedRange.SourceName}'.";
            if (skipped > 0)
                msg += $"\nSkipped {skipped} view(s) (template, read-only, or incompatible range).";
            TaskDialog.Show("Copy View Range", msg);
            return updated > 0 ? Result.Succeeded : Result.Cancelled;
        }

        private static List<ViewPlan> GetTargetViews(Document doc, ViewPlan activePlan)
        {
            List<ViewPlan> plans = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.AreaPlan)
                .OrderBy(v => v.Name)
                .ToList();

            using (ViewSelectionForm form = new ViewSelectionForm(plans, activePlan))
            {
                DialogResult result = form.ShowDialog();
                if (result != DialogResult.OK)
                    return new List<ViewPlan>();
                return form.SelectedViews;
            }
        }

        private static bool CanEditViewRange(ViewPlan view)
        {
            if (view == null) return false;
            if (view.IsTemplate) return false;
            try
            {
                // Try to access the view range; if this throws, it's not editable.
                PlanViewRange vr = view.GetViewRange();
                return vr != null;
            }
            catch
            {
                // If getting the view range throws an exception, it is not editable.
                return false;
            }
        }
    }
}
