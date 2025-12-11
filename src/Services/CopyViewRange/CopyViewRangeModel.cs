// Tool Name: Copy View Range Model
// Description: Implements the supporting model snapshot logic for the Copy View Range command.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autodesk.Revit.DB;

namespace AJTools.Services.CopyViewRange
{

    /// <summary>
    /// Stores a source view's view range as relative relationships to its level
    /// and can apply that range to other plan views.
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

        private CopyViewRangeModel()
        {
        }

        /// <summary>
        /// Create a CopyViewRangeModel snapshot from the given source plan view.
        /// </summary>
        public static CopyViewRangeModel From(ViewPlan sourceView)
        {
            if (sourceView == null)
                throw new ArgumentNullException(nameof(sourceView));

            Document doc = sourceView.Document;
            if (doc == null)
                throw new InvalidOperationException("Source view has no owning document.");

            var result = new CopyViewRangeModel
            {
                SourceName = sourceView.Name
            };

            // Collect all Levels sorted by elevation (like the Python script).
            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            List<ElementId> levelIds = allLevels.Select(l => l.Id).ToList();

            ElementId sourceLevelId = sourceView.GenLevel != null
                ? sourceView.GenLevel.Id
                : ElementId.InvalidElementId;

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

                var data = new PlaneData
                {
                    Offset = offset
                };

                // For Bottom & ViewDepth we ALWAYS treat as Absolute, so they copy exactly.
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

        /// <summary>
        /// Apply the stored view range to the given target plan view,
        /// with detailed reasons if the view range is read-only.
        /// </summary>
        public void ApplyTo(ViewPlan targetView)
        {
            if (targetView == null)
                throw new ArgumentNullException(nameof(targetView));

            Document doc = targetView.Document;
            if (doc == null)
                throw new InvalidOperationException("Target view has no owning document.");

            // Check if the view range parameter is read-only, but only if it exists.
            Parameter vrParam = targetView.get_Parameter(BuiltInParameter.PLAN_VIEW_RANGE);
            if (vrParam != null && vrParam.IsReadOnly)
            {
                string reason;

                if (targetView.ViewTemplateId != ElementId.InvalidElementId)
                {
                    var template = doc.GetElement(targetView.ViewTemplateId) as View;
                    string templateName = template?.Name ?? targetView.ViewTemplateId.IntegerValue.ToString();
                    reason = $"View range is controlled by view template '{templateName}'.";
                }
                else
                {
                    ElementId primaryId = targetView.GetPrimaryViewId();
                    if (primaryId != ElementId.InvalidElementId)
                    {
                        var primary = doc.GetElement(primaryId) as View;
                        string primaryName = primary?.Name ?? primaryId.IntegerValue.ToString();
                        reason =
                            $"This is a dependent view of '{primaryName}', so its view range is controlled by the parent view.";
                    }
                    else
                    {
                        reason = "View range parameter is read-only on this view.";
                    }
                }

                throw new InvalidOperationException(reason);
            }

            // Collect all Levels sorted by elevation.
            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            List<ElementId> levelIds = allLevels.Select(l => l.Id).ToList();

            ElementId destLevelId = targetView.GenLevel != null
                ? targetView.GenLevel.Id
                : ElementId.InvalidElementId;

            int destLevelIndex = levelIds.IndexOf(destLevelId);
            if (destLevelIndex < 0)
                throw new InvalidOperationException("Could not find target view's level in project levels.");

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
                        newLevelId = destLevelAboveId;
                        break;

                    case LevelRelationship.Below:
                        newLevelId = destLevelBelowId;
                        break;

                    case LevelRelationship.Absolute:
                        newLevelId = data.AbsoluteLevelId;
                        break;

                    default:
                        newLevelId = ElementId.InvalidElementId;
                        break;
                }

                vr.SetLevelId(plane, newLevelId);
                vr.SetOffset(plane, data.Offset);
            }

            targetView.SetViewRange(vr);
        }
    }
}
