// Tool Name: Create Tags - Eligibility Filter
// Description: Shared "which of my selected elements can actually be tagged" logic, used by both
//              Create Tags (one click per element) and Stack Tags (one click, whole batch stacked).
//              Kept in one place so the two tools can never quietly drift apart on what counts as
//              already tagged / too short / vertical.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models.SmartTag, AJTools.Models.CreateTags,
//               AJTools.Services.SmartTag, AJTools.Services.LeaderLogic, AJTools.Utils

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.CreateTags;
using AJTools.Models.SmartTag;
using AJTools.Services.LeaderLogic;
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

        public int Total => _counts.Values.Sum();

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
        /// cable tray) - deliberately wider than Smart MEP Tag's own duct-only vertical check.
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
                    double length = SmartMepTagService.GetCurveLength(mepCurve);
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

                XYZ midpoint = SmartMepTagService.GetElementMidpoint(elem, activeView);
                if (midpoint == null)
                {
                    tally.Add("Position could not be determined");
                    continue;
                }

                candidates.Add(new TagCandidate
                {
                    ElementId = id,
                    Category = category,
                    Midpoint = midpoint
                });
            }

            return candidates;
        }

        /// <summary>
        /// A linear MEP element counts as vertical when its location curve runs mostly along world Z -
        /// same 0.95 threshold and technique as Smart MEP Tag's own duct-only check, generalized here to
        /// duct, pipe, and cable tray per Ajmal's confirmed answer (broader than Smart MEP Tag today).
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

        /// <summary>
        /// Finds the candidate whose element midpoint is nearest (in view-plane distance) to the
        /// given target point. Same nearest-first idea Rearrange Tags uses for its own tags, applied
        /// here to raw element positions instead of existing leader anchors.
        /// </summary>
        public static TagCandidate FindNearestCandidate(
            IList<TagCandidate> candidates,
            LeaderLogicService leaderLogic,
            XYZ targetModel)
        {
            if (candidates == null || candidates.Count == 0 || leaderLogic == null || targetModel == null)
                return null;

            TagCandidate nearest = null;
            double minDistance = double.MaxValue;

            foreach (TagCandidate candidate in candidates)
            {
                if (candidate?.Midpoint == null)
                    continue;

                double distance = DistanceInView(targetModel, candidate.Midpoint, leaderLogic);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest ?? candidates[0];
        }

        public static double DistanceInView(XYZ p1, XYZ p2, LeaderLogicService leaderLogic)
        {
            UV uv1 = leaderLogic.ProjectToView(p1);
            UV uv2 = leaderLogic.ProjectToView(p2);
            double dx = uv1.U - uv2.U;
            double dy = uv1.V - uv2.V;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }
    }
}
