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
<<<<<<< HEAD

namespace AJTools.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autodesk.Revit.DB;

    /// <summary>
    /// Stores a source view's view range as relative relationships to its level.
    /// </summary>
    internal class CopyViewRangeModel
    {
        private enum LevelRelationship
        {
            None,
            Associated,
            Above,
            Below,
            Absolute
        }

        private class PlaneData
        {
            public LevelRelationship Relationship { get; set; }
            public ElementId AbsoluteLevelId { get; set; } = ElementId.InvalidElementId;
            public double Offset { get; set; }
        }

        private readonly Dictionary<PlanViewPlane, PlaneData> _planes
            = new Dictionary<PlanViewPlane, PlaneData>();

        public string SourceName { get; private set; }

        private CopyViewRangeModel() { }

        public static CopyViewRangeModel From(ViewPlan sourceView)
        {
            if (sourceView == null) throw new ArgumentNullException(nameof(sourceView));
            Document doc = sourceView.Document;

            var result = new CopyViewRangeModel
            {
                SourceName = sourceView.Name
            };

            // Get levels sorted by elevation
            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            List<ElementId> levelIds = allLevels.Select(l => l.Id).ToList();
            ElementId sourceLevelId = sourceView.GenLevel?.Id ?? ElementId.InvalidElementId;
            int sourceLevelIndex = levelIds.IndexOf(sourceLevelId);

            ElementId levelAboveId = ElementId.InvalidElementId;
            ElementId levelBelowId = ElementId.InvalidElementId;

            if (sourceLevelIndex >= 0)
            {
                if (sourceLevelIndex + 1 < levelIds.Count)
                    levelAboveId = levelIds[sourceLevelIndex + 1];

                if (sourceLevelIndex - 1 >= 0)
                    levelBelowId = levelIds[sourceLevelIndex - 1];
            }

            PlanViewRange viewRange = sourceView.GetViewRange();
            var planes = new[]
            {
                PlanViewPlane.TopClipPlane,
                PlanViewPlane.CutPlane,
                PlanViewPlane.BottomClipPlane,
                PlanViewPlane.ViewDepthPlane
            };

            foreach (PlanViewPlane plane in planes)
            {
                ElementId planeLevelId = viewRange.GetLevelId(plane);
                double offset = viewRange.GetOffset(plane);

                var data = new PlaneData { Offset = offset };

                bool forceAbsolute = (plane == PlanViewPlane.BottomClipPlane ||
                                      plane == PlanViewPlane.ViewDepthPlane);

                if (planeLevelId == ElementId.InvalidElementId)
                {
                    data.Relationship = LevelRelationship.None;
                }
                else if (!forceAbsolute && planeLevelId == sourceLevelId)
                {
                    data.Relationship = LevelRelationship.Associated;
                }
                else if (!forceAbsolute && planeLevelId == levelAboveId)
                {
                    data.Relationship = LevelRelationship.Above;
                }
                else if (!forceAbsolute && planeLevelId == levelBelowId)
                {
                    data.Relationship = LevelRelationship.Below;
                }
                else
                {
                    data.Relationship = LevelRelationship.Absolute;
                    data.AbsoluteLevelId = planeLevelId;
                }

                result._planes[plane] = data;
            }

            return result;
        }

        public void ApplyTo(ViewPlan targetView)
        {
            if (targetView == null) throw new ArgumentNullException(nameof(targetView));
            Document doc = targetView.Document;

            // Check if view range is read-only
            Parameter vrParam = targetView.get_Parameter(BuiltInParameter.PLAN_VIEW_RANGE);
            if (vrParam != null && vrParam.IsReadOnly)
            {
                throw new InvalidOperationException($"View range is read-only (possibly controlled by a View Template).");
            }

            // Get levels sorted by elevation
            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            List<ElementId> levelIds = allLevels.Select(l => l.Id).ToList();
            ElementId destLevelId = targetView.GenLevel?.Id ?? ElementId.InvalidElementId;

            int destLevelIndex = levelIds.IndexOf(destLevelId);
            if (destLevelIndex < 0)
                throw new InvalidOperationException("Target view's level not found in project.");

            ElementId destLevelAboveId = ElementId.InvalidElementId;
            ElementId destLevelBelowId = ElementId.InvalidElementId;

            if (destLevelIndex + 1 < levelIds.Count)
                destLevelAboveId = levelIds[destLevelIndex + 1];

            if (destLevelIndex - 1 >= 0)
                destLevelBelowId = levelIds[destLevelIndex - 1];

            PlanViewRange vr = targetView.GetViewRange();

            foreach (KeyValuePair<PlanViewPlane, PlaneData> kvp in _planes)
            {
                PlanViewPlane plane = kvp.Key;
                PlaneData data = kvp.Value;
                ElementId newLevelId;

                switch (data.Relationship)
                {
                    case LevelRelationship.None:
                        newLevelId = ElementId.InvalidElementId;
                        break;

                    case LevelRelationship.Associated:
                        newLevelId = destLevelId;
                        break;

                    case LevelRelationship.Above:
                        // FIX: If there is no level above (e.g., Roof), fall back to Associated level
                        // to prevent crash, rather than sending InvalidElementId.
                        newLevelId = (destLevelAboveId != ElementId.InvalidElementId) 
                            ? destLevelAboveId 
                            : destLevelId; 
                        break;

                    case LevelRelationship.Below:
                        // FIX: If there is no level below (e.g., Ground), fall back to Associated level.
                        newLevelId = (destLevelBelowId != ElementId.InvalidElementId) 
                            ? destLevelBelowId 
                            : destLevelId;
                        break;

                    case LevelRelationship.Absolute:
                        newLevelId = data.AbsoluteLevelId;
                        break;

                    default:
                        newLevelId = ElementId.InvalidElementId;
                        break;
                }

                // Safety check: CutPlane and TopClipPlane cannot be set to InvalidElementId (Unlimited).
                // Only ViewDepthPlane supports InvalidElementId.
                if (newLevelId == ElementId.InvalidElementId && plane != PlanViewPlane.ViewDepthPlane)
                {
                     // Fallback to current level if calculation failed for critical planes
                     newLevelId = destLevelId;
                }

                vr.SetLevelId(plane, newLevelId);
                vr.SetOffset(plane, data.Offset);
            }

            targetView.SetViewRange(vr);
        }
    }
}
=======
>>>>>>> c89754d (Restructure project into professional folder hierarchy)
