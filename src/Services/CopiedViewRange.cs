// Tool Name: Filter Pro - Copied View Range
// Description: Captures and reapplies plan view range settings between views.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Collections.Generic
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services
{
    internal sealed class CopiedViewRange
    {
        private readonly Dictionary<PlanViewPlane, LevelSnapshot> _snapshots;

        private CopiedViewRange(string sourceName, Dictionary<PlanViewPlane, LevelSnapshot> snapshots)
        {
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? "View" : sourceName;
            _snapshots = snapshots ?? new Dictionary<PlanViewPlane, LevelSnapshot>();
        }

        internal string SourceName { get; }

        internal static CopiedViewRange From(ViewPlan view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            PlanViewRange range = view.GetViewRange();
            if (range == null)
                throw new InvalidOperationException("View range is not available for the provided view.");

            var map = new Dictionary<PlanViewPlane, LevelSnapshot>();
            foreach (PlanViewPlane plane in Enum.GetValues(typeof(PlanViewPlane)))
            {
                TryCapture(range, map, plane);
            }

            if (map.Count == 0)
                throw new InvalidOperationException("No view range planes could be captured from the source view.");

            return new CopiedViewRange(view.Name, map);
        }

        internal void ApplyTo(ViewPlan target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (_snapshots.Count == 0)
                return;

            PlanViewRange range = target.GetViewRange();
            foreach (KeyValuePair<PlanViewPlane, LevelSnapshot> snapshot in _snapshots)
            {
                ApplyLevel(range, snapshot.Key, snapshot.Value);
            }

            target.SetViewRange(range);
        }

        private static void TryCapture(
            PlanViewRange range,
            IDictionary<PlanViewPlane, LevelSnapshot> map,
            PlanViewPlane plane)
        {
            try
            {
                ElementId levelId = range.GetLevelId(plane);
                double offset = range.GetOffset(plane);
                map[plane] = new LevelSnapshot(levelId, offset);
            }
            catch
            {
                // Ignore planes not supported by this Revit version or view type.
            }
        }

        private static void ApplyLevel(
            PlanViewRange range,
            PlanViewPlane plane,
            LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            try
            {
                if (snapshot.LevelId != ElementId.InvalidElementId)
                {
                    range.SetLevelId(plane, snapshot.LevelId);
                }

                range.SetOffset(plane, snapshot.Offset);
            }
            catch
            {
                // Ignore incompatible planes when applying to the target view.
            }
        }

        private sealed class LevelSnapshot
        {
            internal LevelSnapshot(ElementId levelId, double offset)
            {
                // ElementId is a struct (non-nullable); treat InvalidElementId as "no level".
                LevelId = levelId;
                Offset = offset;
            }

            internal ElementId LevelId { get; }
            internal double Offset { get; }
        }
    }
}
