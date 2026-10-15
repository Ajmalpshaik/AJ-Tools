// Tool Name: Create Tags - Eligibility Filter
// Description: Shared "which of my selected elements can actually be tagged" logic, used by both
//              Create Tags (each element tagged automatically at the set distance) and Stack Tags
//              (one click, whole batch stacked).
//              Kept in one place so the two tools can never quietly drift apart on what counts as
//              already tagged / too short / vertical.
// Author: Ajmal P.S.
// Version: 1.0.1
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.DB.Mechanical, AJTools.Models.SmartTag,
//               AJTools.Models.CreateTags, AJTools.Services.SmartTag, AJTools.Utils
// Changelog: 1.0.1 (2026-10-12) - Each candidate now gets its orientation (horizontal/vertical run) and
//            duct width, using Smart MEP Tags' own rules. Both were left unset, so every run read as
//            horizontal and the distance was measured from the centreline - tags landed inside wide
//            ducts. Removed FindNearestCandidate/DistanceInView, unused since placement went automatic.

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using AJTools.Models.CreateTags;
using AJTools.Models.SmartTag;
using AJTools.Services.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.CreateTags
{
    /// <summary>
    /// Simple reason-&gt;count tally used to build a plain-language end-of-run summary.
    /// </summary>
    internal sealed class SkipTally
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public void Add(string reason)
        {
            _counts.TryGetValue(reason, out int count);
            _counts[reason] = count + 1;
        }

        public string BuildSummary()
        {
            if (_counts.Count == 0)
                return null;

            return string.Join("\n", _counts
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => string.Format("- {0}: {1}", kvp.Key, kvp.Value)));
        }
    }

    internal static class CreateTagsEligibilityFilter
    {
        /// <summary>
        /// Filters a selection down to elements that can actually be tagged: a supported category,
        /// enabled in Create Tags Settings, not already tagged in this view, not shorter than the
        /// configured minimum (curve-based categories only), and not a vertical run (duct, pipe, or
        /// cable tray) when the shared skip-vertical-runs setting is on.
        /// </summary>
        public static List<TagCandidate> BuildEligibleCandidates(
            Document doc,
            View activeView,
            ICollection<ElementId> selectedIds,
            CreateTagsSettingsState settings,
            double minLengthInternal,
            HashSet<ElementId> alreadyTagged,
            SkipTally tally)
        {
            var candidates = new List<TagCandidate>();

            // Read once per run, not per element - this is a file-backed setting, and it is the same
            // one Smart MEP Tags reads, so no two tagging tools can disagree about a vertical run.
            bool skipVerticalRuns = TagClashSettings.ShouldSkipVerticalRuns();

            foreach (ElementId id in selectedIds)
            {
                Element elem = doc.GetElement(id);
                if (elem?.Category == null)
                {
                    tally.Add("Not a taggable category");
                    continue;
                }

                BuiltInCategory category;
                try
                {
                    category = (BuiltInCategory)elem.Category.Id.IntValue();
                }
                catch (Exception)
                {
                    tally.Add("Not a taggable category");
                    continue;
                }

                if (!SmartTagSettingsTracker.SupportedCategories.Contains(category))
                {
                    tally.Add("Not a taggable category");
                    continue;
                }

                if (!CreateTagsSettingsTracker.IsCategoryEnabled(settings, category))
                {
                    tally.Add("Category turned off in Create Tags Settings");
                    continue;
                }

                if (alreadyTagged.Contains(id))
                {
                    tally.Add("Already tagged in this view");
                    continue;
                }

                MEPCurve mepCurve = elem as MEPCurve;
                if (mepCurve != null)
                {
                    double length = SmartTagElementGeometry.GetCurveLength(mepCurve);
                    if (length >= 0 && length < minLengthInternal)
                    {
                        tally.Add(string.Format("Shorter than {0:F0}mm", minLengthInternal / Constants.MM_TO_FEET));
                        continue;
                    }

                    if (skipVerticalRuns && IsVerticalMepCurve(mepCurve))
                    {
                        tally.Add("Vertical run");
                        continue;
                    }
                }

                XYZ midpoint = SmartTagElementGeometry.GetElementMidpoint(elem, activeView);
                if (midpoint == null)
                {
                    tally.Add("Position could not be determined");
                    continue;
                }

                // Orientation and host width decide where Create Tags' automatic placement puts the
                // tag - below a horizontal run, right of a vertical one, measured from the duct's
                // edge. Left unset, every run read as horizontal and the distance was measured from
                // the centreline, so tags landed inside wide ducts. Set exactly the way Smart MEP
                // Tags sets them, so both tools read a run the same way.
                double hostWidth = 0;
                if (category == BuiltInCategory.OST_DuctCurves && elem is Duct duct)
                {
                    hostWidth = SmartTagElementGeometry.GetDuctWidth(duct);
                }

                candidates.Add(new TagCandidate
                {
                    ElementId = id,
                    Category = category,
                    Midpoint = midpoint,
                    Orientation = SmartTagElementGeometry.GetElementOrientation(elem, activeView),
                    HostWidth = hostWidth
                });
            }

            return candidates;
        }

        /// <summary>
        /// A linear MEP element counts as vertical when its location curve runs mostly along world Z -
        /// the 0.95 threshold Smart MEP Tag's old duct-only check used, generalized to duct, pipe, and
        /// cable tray per Ajmal's confirmed answer. Smart MEP Tags now calls this same check.
        /// </summary>
        public static bool IsVerticalMepCurve(MEPCurve mepCurve)
        {
            if (mepCurve == null)
                return false;

            if (mepCurve.Location is LocationCurve locCurve && locCurve.Curve is Line line)
            {
                XYZ dir = line.Direction;
                if (Math.Abs(dir.Z) > 0.95)
                    return true;
            }

            return false;
        }

        // Removed 2026-10-12: FindNearestCandidate and DistanceInView, left over from when Create
        // Tags asked for a click per tag and matched it to the nearest element. Placement has been
        // automatic since 2026-08-17 and nothing called either one. Stack Tags' nearest-first search
        // lives in TagStackService.
    }
}
