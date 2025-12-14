// Tool Name: Copy View Range
// Description: Implements the Copy View Range command and supporting model snapshot logic.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows.Forms

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.CopyViewRange;
using AJTools.UI;

namespace AJTools.Commands
{
    using ViewRangeSnapshot = AJTools.Services.CopyViewRange.CopyViewRangeModel;

    /// <summary>
    /// Copies the active plan view's view range and applies it to other plan views.
    /// Uses a cached snapshot so later runs can paste to active or multiple views.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    internal class CmdCopyViewRange : IExternalCommand
    {
        private const string Title = "AJ Tools - Copy View Range";

        // Session cache (like an in-memory clipboard)
        private static ViewRangeSnapshot _cachedSnapshot;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData?.Application;
            UIDocument uiDoc = uiApp?.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show(Title, "An active project document is required.");
                return Result.Failed;
            }

            Document doc = uiDoc.Document;
            if (!(doc.ActiveView is ViewPlan activePlan) || activePlan.IsTemplate)
            {
                TaskDialog.Show(
                    Title,
                    "Activate a non-template plan view (Floor/Ceiling/Engineering) before running Copy View Range.");
                return Result.Failed;
            }

            // Show menu depending on whether we already have a cached snapshot
            TaskDialogResult choice = ShowActionDialog(_cachedSnapshot != null);
            if (choice == TaskDialogResult.Cancel)
                return Result.Cancelled;

            if (choice == TaskDialogResult.CommandLink1)
                return DoCopyFromActive(activePlan);

            if (choice == TaskDialogResult.CommandLink2)
                return DoPasteToActive(doc, activePlan);

            if (choice == TaskDialogResult.CommandLink3)
                return DoPasteToMultiple(doc, activePlan);

            return Result.Cancelled;
        }

        /// <summary>
        /// Shows the main action dialog.
        /// </summary>
        private static TaskDialogResult ShowActionDialog(bool hasCache)
        {
            var dialog = new TaskDialog(Title)
            {
                MainInstruction = hasCache
                    ? "Copy or paste view range"
                    : "Copy view range from the active view",
                MainContent = hasCache
                    ? "Use the cached view range, or copy a new one from the active plan view."
                    : "Copy the active plan view's range so you can paste it to this or other plan views.",
                CommonButtons = TaskDialogCommonButtons.Cancel,
                AllowCancellation = true
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Copy view range from active view");

            if (hasCache)
            {
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Paste view range to active view");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Paste view range to multiple views");
            }

            return dialog.Show();
        }

        private static Result DoCopyFromActive(ViewPlan activePlan)
        {
            try
            {
                _cachedSnapshot = ViewRangeSnapshot.From(activePlan);
                TaskDialog.Show(Title, $"View range copied from '{activePlan.Name}'.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(Title, "Could not copy view range:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static Result DoPasteToActive(Document doc, ViewPlan activePlan)
        {
            if (_cachedSnapshot == null)
            {
                TaskDialog.Show(Title, "Nothing copied yet. Copy a view range first.");
                return Result.Cancelled;
            }

            using (var t = new Transaction(doc, "Copy View Range - Active View"))
            {
                t.Start();
                try
                {
                    _cachedSnapshot.ApplyTo(activePlan);
                    t.Commit();
                    TaskDialog.Show(Title, $"View range applied to active view '{activePlan.Name}'.");
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    TaskDialog.Show(Title, "Could not apply view range to active view:\n" + ex.Message);
                    return Result.Failed;
                }
            }
        }

        private static Result DoPasteToMultiple(Document doc, ViewPlan activePlan)
        {
            if (_cachedSnapshot == null)
            {
                TaskDialog.Show(Title, "Nothing copied yet. Copy a view range first.");
                return Result.Cancelled;
            }

            IList<ViewPlan> eligibleViews = GetEligibleViews(doc);
            if (eligibleViews.Count == 0)
            {
                TaskDialog.Show(Title, "No plan views are available to receive the copied view range.");
                return Result.Cancelled;
            }

            List<ViewPlan> selectedViews;
            // Ensure ViewSelectionForm exists in AJTools.UI
            using (var form = new ViewSelectionForm(eligibleViews, activePlan))
            {
                DialogResult dialogResult = form.ShowDialog();
                if (dialogResult != DialogResult.OK || form.SelectedViews.Count == 0)
                    return Result.Cancelled;

                selectedViews = form.SelectedViews;
            }

            IList<ViewPlan> targets = FilterTargets(activePlan, selectedViews);
            if (targets.Count == 0)
            {
                TaskDialog.Show(Title, "No target plan views were selected.");
                return Result.Cancelled;
            }

            var failures = new List<string>();
            int appliedCount = 0;

            using (var t = new Transaction(doc, "Copy View Range - Multiple Views"))
            {
                t.Start();

                foreach (ViewPlan plan in targets)
                {
                    try
                    {
                        _cachedSnapshot.ApplyTo(plan);
                        appliedCount++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{plan.Name}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            if (appliedCount == 0)
            {
                string warning = failures.Count > 0
                    ? "No view ranges were updated:\n" + string.Join(Environment.NewLine, failures)
                    : "No eligible target views.";

                TaskDialog.Show(Title, warning);
                return Result.Cancelled;
            }

            if (failures.Count > 0)
            {
                TaskDialog.Show(
                    Title,
                    $"View range copied with warnings:\n{string.Join(Environment.NewLine, failures)}");
            }
            else
            {
                TaskDialog.Show(
                    Title,
                    $"View range copied to {appliedCount} view{(appliedCount == 1 ? string.Empty : "s")}.");
            }

            return Result.Succeeded;
        }

        private static IList<ViewPlan> GetEligibleViews(Document doc)
        {
            var allowed = new HashSet<ViewType>
            {
                ViewType.FloorPlan,
                ViewType.CeilingPlan,
                ViewType.EngineeringPlan
            };

            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate && allowed.Contains(v.ViewType))
                .OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static IList<ViewPlan> FilterTargets(ViewPlan source, IEnumerable<ViewPlan> selectedViews)
        {
            var result = new List<ViewPlan>();
            var seenIds = new HashSet<int>();

            foreach (ViewPlan view in selectedViews)
            {
                if (view == null) continue;
                if (view.Id == source.Id) continue;

                if (seenIds.Add(view.Id.IntegerValue))
                    result.Add(view);
            }

            return result;
        }
    }
}
