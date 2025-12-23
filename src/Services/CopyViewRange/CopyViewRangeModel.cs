// Tool Name: Copy View Range Model
// Description: Implements the supporting model snapshot logic for the Copy View Range command.
// Author: Ajmal P.S.
// Version: 1.0.2
// Last Updated: 2025-12-23
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace AJTools.Services.CopyViewRange
{
    /// <summary>
    /// Stores a source view's view range as relative relationships to its level
    /// and can apply that range to other plan views safely.
    /// </summary>
    internal class CopyViewRangeModel
    {
        private const string CacheVersion = "AJTools.CopyViewRange.v1";

        // Defined in specific order for serialization
        private static readonly PlanViewPlane[] CachePlanes =
        {
            PlanViewPlane.TopClipPlane,
            PlanViewPlane.CutPlane,
            PlanViewPlane.BottomClipPlane,
            PlanViewPlane.ViewDepthPlane
        };

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

        /// <summary>
        /// Create a CopyViewRangeModel snapshot from the given source plan view.
        /// </summary>
        public static CopyViewRangeModel From(ViewPlan sourceView)
        {
            if (sourceView == null) throw new ArgumentNullException(nameof(sourceView));
            Document doc = sourceView.Document;

            ElementId sourceLevelId = sourceView.GenLevel?.Id ?? ElementId.InvalidElementId;
            if (sourceLevelId == ElementId.InvalidElementId)
                throw new InvalidOperationException("Source view does not have an associated level.");

            var result = new CopyViewRangeModel { SourceName = sourceView.Name };

            // Sort levels by Elevation
            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ThenBy(l => l.Id.IntegerValue)
                .ToList();

            List<ElementId> levelIds = allLevels.Select(l => l.Id).ToList();
            int sourceIndex = levelIds.IndexOf(sourceLevelId);

            if (sourceIndex < 0)
                throw new InvalidOperationException("Could not find the current view's level in the project.");

            ElementId levelAboveId = (sourceIndex + 1 < levelIds.Count) ? levelIds[sourceIndex + 1] : ElementId.InvalidElementId;
            ElementId levelBelowId = (sourceIndex - 1 >= 0) ? levelIds[sourceIndex - 1] : ElementId.InvalidElementId;

            PlanViewRange viewRange = sourceView.GetViewRange();

            foreach (PlanViewPlane plane in CachePlanes)
            {
                ElementId planeLevelId = viewRange.GetLevelId(plane);
                double offset = viewRange.GetOffset(plane);
                var data = new PlaneData { Offset = offset };

                if (planeLevelId == ElementId.InvalidElementId || planeLevelId == PlanViewRange.Unlimited)
                {
                    data.Relationship = LevelRelationship.None;
                }
                else if (planeLevelId == sourceLevelId)
                {
                    data.Relationship = LevelRelationship.Associated;
                }
                else if (planeLevelId == levelAboveId)
                {
                    data.Relationship = LevelRelationship.Above;
                }
                else if (planeLevelId == levelBelowId)
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
        /// Apply the stored view range to the target view using Safe Order logic.
        /// </summary>
        public bool TryApplyTo(ViewPlan targetView, out string skipReason)
        {
            if (targetView == null) throw new ArgumentNullException(nameof(targetView));
            Document doc = targetView.Document;

            // 1. Check Read-Only / Template
            Parameter vrParam = targetView.get_Parameter(BuiltInParameter.PLAN_VIEW_RANGE);
            if (vrParam != null && vrParam.IsReadOnly)
            {
                if (targetView.ViewTemplateId != ElementId.InvalidElementId)
                {
                    var t = doc.GetElement(targetView.ViewTemplateId);
                    skipReason = $"Controlled by View Template '{t?.Name}'.";
                    return false;
                }
                skipReason = "View range is read-only (possibly dependent view).";
                return false;
            }

            // 2. Identify Target Levels
            ElementId destLevelId = targetView.GenLevel?.Id ?? ElementId.InvalidElementId;
            if (destLevelId == ElementId.InvalidElementId)
            {
                skipReason = "Target view has no associated level.";
                return false;
            }

            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ThenBy(l => l.Id.IntegerValue)
                .ToList();

            List<ElementId> levelIds = allLevels.Select(l => l.Id).ToList();
            int destIndex = levelIds.IndexOf(destLevelId);

            if (destIndex < 0)
            {
                skipReason = "Target level not found in project list.";
                return false;
            }

            ElementId destAbove = (destIndex + 1 < levelIds.Count) ? levelIds[destIndex + 1] : ElementId.InvalidElementId;
            ElementId destBelow = (destIndex - 1 >= 0) ? levelIds[destIndex - 1] : ElementId.InvalidElementId;

            PlanViewRange vr = targetView.GetViewRange();

            // 3. SAFE ORDER: Depth -> Bottom -> Top -> Cut
            // This prevents "Top is below Bottom" errors during the transition.
            var safeOrder = new[]
            {
                PlanViewPlane.ViewDepthPlane,
                PlanViewPlane.BottomClipPlane,
                PlanViewPlane.TopClipPlane,
                PlanViewPlane.CutPlane
            };

            foreach (PlanViewPlane plane in safeOrder)
            {
                if (!_planes.TryGetValue(plane, out PlaneData data)) continue;

                ElementId newLevelId;

                switch (data.Relationship)
                {
                    case LevelRelationship.None:
                        newLevelId = PlanViewRange.Unlimited;
                        break;
                    case LevelRelationship.Associated:
                        newLevelId = destLevelId;
                        break;
                    case LevelRelationship.Above:
                        // Fallback to Associated if no level above exists (e.g. Roof)
                        newLevelId = (destAbove != ElementId.InvalidElementId) ? destAbove : destLevelId;
                        break;
                    case LevelRelationship.Below:
                        // Fallback to Associated if no level below exists (e.g. Basement)
                        newLevelId = (destBelow != ElementId.InvalidElementId) ? destBelow : destLevelId;
                        break;
                    case LevelRelationship.Absolute:
                        // If absolute level is deleted, fallback to associated
                        newLevelId = (doc.GetElement(data.AbsoluteLevelId) is Level) ? data.AbsoluteLevelId : destLevelId;
                        break;
                    default:
                        newLevelId = destLevelId;
                        break;
                }

                vr.SetLevelId(plane, newLevelId);
                vr.SetOffset(plane, data.Offset);
            }

            try
            {
                targetView.SetViewRange(vr);
            }
            catch (Exception ex)
            {
                skipReason = "Revit rejected values: " + ex.Message;
                return false;
            }

            skipReason = null;
            return true;
        }

        // Serialization helpers
        internal string SerializeCache()
        {
            var sb = new StringBuilder();
            sb.AppendLine(CacheVersion);
            sb.AppendLine(SourceName ?? "");

            foreach (PlanViewPlane plane in CachePlanes)
            {
                if (!_planes.TryGetValue(plane, out PlaneData d))
                    d = new PlaneData { Relationship = LevelRelationship.None };

                sb.Append((int)plane).Append('|')
                  .Append((int)d.Relationship).Append('|')
                  .Append(d.Offset.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                  .Append(d.AbsoluteLevelId.IntegerValue).AppendLine();
            }
            return sb.ToString();
        }

        internal static bool TryDeserializeCache(string content, out CopyViewRangeModel model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(content)) return false;
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length < 2 || lines[0] != CacheVersion) return false;

            var res = new CopyViewRangeModel { SourceName = lines[1] };
            for (int i = 2; i < lines.Length; i++)
            {
                var p = lines[i].Split('|');
                if (p.Length < 4) continue;
                if (int.TryParse(p[0], out int pl) && int.TryParse(p[1], out int rel) &&
                    double.TryParse(p[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double off) &&
                    int.TryParse(p[3], out int id))
                {
                    res._planes[(PlanViewPlane)pl] = new PlaneData
                    {
                        Relationship = (LevelRelationship)rel,
                        Offset = off,
                        AbsoluteLevelId = new ElementId(id)
                    };
                }
            }
            model = res;
            return true;
        }
    }
}