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

        private struct ExpectedPlane
        {
            public ElementId LevelId { get; set; }
            public double Offset { get; set; }
            public bool ApplyOffset { get; set; }
        }

        private const double OffsetTolerance = 1e-6;

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
            PlanViewRange viewRange = sourceView.GetViewRange();

            ElementId sourceLevelId = GetBaseLevelId(sourceView, viewRange);
            if (sourceLevelId == ElementId.InvalidElementId)
                throw new InvalidOperationException("Source view does not have an associated level.");

            var result = new CopyViewRangeModel { SourceName = sourceView.Name };

            // Sort levels by Elevation
            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.Id.IntegerValue)
                .ToList();

            bool hasSourceLevel = allLevels.Any(l => l.Id == sourceLevelId);
            if (!hasSourceLevel)
                throw new InvalidOperationException("Could not find the current view's level in the project.");

            ElementId levelAboveId = GetLevelAboveId(allLevels, sourceLevelId);
            ElementId levelBelowId = GetLevelBelowId(allLevels, sourceLevelId);

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
                else if (planeLevelId == levelAboveId &&
                    (plane == PlanViewPlane.TopClipPlane || plane == PlanViewPlane.CutPlane))
                {
                    data.Relationship = LevelRelationship.Above;
                }
                else if (planeLevelId == levelBelowId &&
                    (plane == PlanViewPlane.BottomClipPlane ||
                     plane == PlanViewPlane.ViewDepthPlane ||
                     plane == PlanViewPlane.CutPlane))
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
            PlanViewRange original = targetView.GetViewRange();
            PlanViewRange vr = targetView.GetViewRange();
            ElementId destLevelId = GetBaseLevelId(targetView, vr);
            if (destLevelId == ElementId.InvalidElementId)
            {
                skipReason = "Target view has no associated level.";
                return false;
            }

            IList<Level> allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.Id.IntegerValue)
                .ToList();

            bool hasDestLevel = allLevels.Any(l => l.Id == destLevelId);
            if (!hasDestLevel)
            {
                skipReason = "Target level not found in project list.";
                return false;
            }

            Level destLevel = allLevels.First(l => l.Id == destLevelId);
            ElementId destAbove = GetLevelAboveId(allLevels, destLevelId);
            ElementId destBelow = GetLevelBelowId(allLevels, destLevelId);

            // 3. SAFE ORDER: Depth -> Bottom -> Top -> Cut
            // This prevents "Top is below Bottom" errors during the transition.
            var safeOrder = new[]
            {
                PlanViewPlane.ViewDepthPlane,
                PlanViewPlane.BottomClipPlane,
                PlanViewPlane.TopClipPlane,
                PlanViewPlane.CutPlane
            };

            var expected = new Dictionary<PlanViewPlane, ExpectedPlane>();

            foreach (PlanViewPlane plane in safeOrder)
            {
                if (!_planes.TryGetValue(plane, out PlaneData data)) continue;

                if (!TryMapPlane(plane, data, destLevelId, destLevel, destAbove, destBelow, doc,
                        out ElementId newLevelId, out bool applyOffset))
                    continue;

                vr.SetLevelId(plane, newLevelId);
                if (applyOffset)
                    vr.SetOffset(plane, data.Offset);

                expected[plane] = new ExpectedPlane
                {
                    LevelId = newLevelId,
                    Offset = data.Offset,
                    ApplyOffset = applyOffset
                };
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

            if (!ValidateAppliedRange(targetView, expected, out string mismatch))
            {
                try
                {
                    targetView.SetViewRange(original);
                }
                catch
                {
                    // Best-effort restore; we still report the mismatch.
                }

                skipReason = mismatch;
                return false;
            }

            skipReason = null;
            return true;
        }

        private static ElementId GetBaseLevelId(ViewPlan view, PlanViewRange viewRange)
        {
            ElementId genLevelId = view?.GenLevel?.Id ?? ElementId.InvalidElementId;
            if (genLevelId != ElementId.InvalidElementId)
                return genLevelId;

            if (viewRange != null)
            {
                ElementId cutLevelId = viewRange.GetLevelId(PlanViewPlane.CutPlane);
                if (cutLevelId != ElementId.InvalidElementId && cutLevelId != PlanViewRange.Unlimited)
                    return cutLevelId;
            }

            return ElementId.InvalidElementId;
        }

        private static ElementId GetLevelAboveId(IList<Level> levels, ElementId baseLevelId)
        {
            if (levels == null || levels.Count == 0)
                return ElementId.InvalidElementId;

            Level baseLevel = levels.FirstOrDefault(l => l.Id == baseLevelId);
            if (baseLevel == null)
                return ElementId.InvalidElementId;

            Level above = levels
                .Where(l => l.Elevation > baseLevel.Elevation)
                .OrderBy(l => l.Elevation)
                .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.Id.IntegerValue)
                .FirstOrDefault();

            return above?.Id ?? ElementId.InvalidElementId;
        }

        private static ElementId GetLevelBelowId(IList<Level> levels, ElementId baseLevelId)
        {
            if (levels == null || levels.Count == 0)
                return ElementId.InvalidElementId;

            Level baseLevel = levels.FirstOrDefault(l => l.Id == baseLevelId);
            if (baseLevel == null)
                return ElementId.InvalidElementId;

            Level below = levels
                .Where(l => l.Elevation < baseLevel.Elevation)
                .OrderByDescending(l => l.Elevation)
                .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.Id.IntegerValue)
                .FirstOrDefault();

            return below?.Id ?? ElementId.InvalidElementId;
        }

        private static bool TryMapPlane(
            PlanViewPlane plane,
            PlaneData data,
            ElementId destLevelId,
            Level destLevel,
            ElementId destAbove,
            ElementId destBelow,
            Document doc,
            out ElementId levelId,
            out bool applyOffset)
        {
            levelId = destLevelId;
            applyOffset = true;

            switch (data.Relationship)
            {
                case LevelRelationship.None:
                    if (plane == PlanViewPlane.CutPlane)
                    {
                        levelId = destLevelId;
                        applyOffset = true;
                    }
                    else
                    {
                        levelId = PlanViewRange.Unlimited;
                        applyOffset = false;
                    }
                    return true;

                case LevelRelationship.Associated:
                    levelId = destLevelId;
                    applyOffset = true;
                    return true;

                case LevelRelationship.Above:
                    if (plane == PlanViewPlane.BottomClipPlane || plane == PlanViewPlane.ViewDepthPlane)
                    {
                        levelId = PlanViewRange.Unlimited;
                        applyOffset = false;
                        return true;
                    }

                    if (destAbove != ElementId.InvalidElementId)
                    {
                        levelId = destAbove;
                        applyOffset = true;
                    }
                    else if (plane == PlanViewPlane.CutPlane)
                    {
                        levelId = destLevelId;
                        applyOffset = true;
                    }
                    else
                    {
                        levelId = PlanViewRange.Unlimited;
                        applyOffset = false;
                    }
                    return true;

                case LevelRelationship.Below:
                    if (plane == PlanViewPlane.TopClipPlane)
                    {
                        levelId = PlanViewRange.Unlimited;
                        applyOffset = false;
                        return true;
                    }

                    if (destBelow != ElementId.InvalidElementId)
                    {
                        levelId = destBelow;
                        applyOffset = true;
                    }
                    else if (plane == PlanViewPlane.CutPlane)
                    {
                        levelId = destLevelId;
                        applyOffset = true;
                    }
                    else
                    {
                        levelId = PlanViewRange.Unlimited;
                        applyOffset = false;
                    }
                    return true;

                case LevelRelationship.Absolute:
                    if (doc.GetElement(data.AbsoluteLevelId) is Level absLevel)
                    {
                        if (!IsValidAbsolute(plane, destLevel, absLevel))
                        {
                            if (plane == PlanViewPlane.CutPlane)
                            {
                                levelId = destLevelId;
                                applyOffset = true;
                            }
                            else
                            {
                                levelId = PlanViewRange.Unlimited;
                                applyOffset = false;
                            }
                            return true;
                        }

                        levelId = absLevel.Id;
                        applyOffset = true;
                    }
                    else if (plane == PlanViewPlane.CutPlane)
                    {
                        levelId = destLevelId;
                        applyOffset = true;
                    }
                    else
                    {
                        levelId = PlanViewRange.Unlimited;
                        applyOffset = false;
                    }
                    return true;

                default:
                    levelId = destLevelId;
                    applyOffset = true;
                    return true;
            }
        }

        private static bool ValidateAppliedRange(
            ViewPlan view,
            IDictionary<PlanViewPlane, ExpectedPlane> expected,
            out string reason)
        {
            reason = null;
            if (expected == null || expected.Count == 0)
                return true;

            PlanViewRange applied;
            try
            {
                applied = view.GetViewRange();
            }
            catch (Exception ex)
            {
                reason = "Failed to read view range after paste: " + ex.Message;
                return false;
            }

            foreach (KeyValuePair<PlanViewPlane, ExpectedPlane> kvp in expected)
            {
                PlanViewPlane plane = kvp.Key;
                ExpectedPlane exp = kvp.Value;

                ElementId actualLevelId = applied.GetLevelId(plane);
                if (exp.LevelId == PlanViewRange.Unlimited)
                {
                    if (actualLevelId != PlanViewRange.Unlimited)
                    {
                        reason = $"{GetPlaneLabel(plane)} plane did not apply as Unlimited.";
                        return false;
                    }
                    continue;
                }

                if (actualLevelId != exp.LevelId)
                {
                    reason = $"{GetPlaneLabel(plane)} plane level mismatch after paste.";
                    return false;
                }

                if (exp.ApplyOffset)
                {
                    double actualOffset = applied.GetOffset(plane);
                    if (Math.Abs(actualOffset - exp.Offset) > OffsetTolerance)
                    {
                        reason = $"{GetPlaneLabel(plane)} plane offset mismatch after paste.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsValidAbsolute(PlanViewPlane plane, Level baseLevel, Level absoluteLevel)
        {
            if (baseLevel == null || absoluteLevel == null)
                return false;

            if (plane == PlanViewPlane.TopClipPlane)
                return absoluteLevel.Elevation >= baseLevel.Elevation;

            if (plane == PlanViewPlane.BottomClipPlane || plane == PlanViewPlane.ViewDepthPlane)
                return absoluteLevel.Elevation <= baseLevel.Elevation;

            return true;
        }

        private static string GetPlaneLabel(PlanViewPlane plane)
        {
            switch (plane)
            {
                case PlanViewPlane.TopClipPlane:
                    return "Top";
                case PlanViewPlane.CutPlane:
                    return "Cut";
                case PlanViewPlane.BottomClipPlane:
                    return "Bottom";
                case PlanViewPlane.ViewDepthPlane:
                    return "View Depth";
                default:
                    return plane.ToString();
            }
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
