#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, Expected Tag Size
 * File Name     : SmartTagSizeHints.cs
 * Purpose       : Step 4: how big a new tag is expected to be, measured from tags of the same category already
 *                 in the view, with a fallback size when there are none.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-15
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Utils, the other Smart MEP Tags files
 *
 * Input         : Tags already in the view.
 * Output        : Expected tag width and height per category.
 *
 * Notes         :
 * - Part of the Smart MEP Tags restructure (suite 1.58.0): the code that used to sit in
 *   SmartMepTagService.cs and SmartTagPlacementEngine.cs is split one job per file, in step
 *   folders. Every routine was moved as-is - tagging results are unchanged.
 * - The numbers that tune the algorithm live in SmartTagTuning.cs.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Split out of the old Smart MEP Tag service / placement engine.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using AJTools.Models.SmartTag;
using AJTools.Services.CreateTags;
using AJTools.Services.LeaderLogic;
using AJTools.Services.TagClash;
using AJTools.Services.ViewCrop;
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagCandidatePositions;
using static AJTools.Services.SmartTag.SmartTagCreator;
using static AJTools.Services.SmartTag.SmartTagElementCollector;
using static AJTools.Services.SmartTag.SmartTagElementFilters;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;
using static AJTools.Services.SmartTag.SmartTagPlacementEngine;
using static AJTools.Services.SmartTag.SmartTagPositionFinder;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagTypeSelector;
using static AJTools.Services.SmartTag.SmartTagViewCheck;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// How big a new tag is expected to be.
    /// </summary>
    internal static class SmartTagSizeHints
    {
        internal sealed class TagSizeHint
        {
            public TagSizeHint(double width, double height)
            {
                Width = width;
                Height = height;
            }

            public double Width { get; private set; }
            public double Height { get; private set; }
        }

        /// <summary>
        /// Fallback estimate when no sampled tag box is available.
        /// </summary>
        internal static void EstimateFallbackTagSize(int viewScale, out double width, out double height)
        {
            // Typical MEP tag is roughly 15mm wide x 5mm tall at print scale.
            width = SmartTagTuning.FallbackTagWidthPaperMm * Constants.MM_TO_FEET * viewScale;
            height = SmartTagTuning.FallbackTagHeightPaperMm * Constants.MM_TO_FEET * viewScale;
        }

        internal static Dictionary<BuiltInCategory, TagSizeHint> BuildTagSizeHints(
            Document doc,
            View view,
            IList<TagCandidate> candidates,
            XYZ viewRight,
            XYZ viewUp,
            int viewScale)
        {
            EstimateFallbackTagSize(viewScale, out double fallbackWidth, out double fallbackHeight);

            var neededCategories = new HashSet<BuiltInCategory>();
            if (candidates != null)
            {
                foreach (TagCandidate candidate in candidates)
                {
                    if (candidate != null)
                        neededCategories.Add(candidate.Category);
                }
            }

            var hints = new Dictionary<BuiltInCategory, TagSizeHint>();
            foreach (BuiltInCategory category in neededCategories)
                hints[category] = new TagSizeHint(fallbackWidth, fallbackHeight);

            if (doc == null || view == null || neededCategories.Count == 0)
                return hints;

            var samples = new Dictionary<BuiltInCategory, List<TagSizeHint>>();
            foreach (BuiltInCategory category in neededCategories)
                samples[category] = new List<TagSizeHint>();

            try
            {
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element element in tags)
                {
                    IndependentTag tag = element as IndependentTag;
                    if (tag == null)
                        continue;

                    if (!TryGetTaggedModelCategory(doc, tag, out BuiltInCategory modelCategory))
                        continue;

                    List<TagSizeHint> categorySamples;
                    if (!samples.TryGetValue(modelCategory, out categorySamples))
                        continue;

                    if (!TryGetTagTextBoxSizeInViewPlane(tag, view, viewRight, viewUp, out double width, out double height))
                        continue;

                    categorySamples.Add(new TagSizeHint(width, height));
                }
            }
            catch (Exception)
            {
                // Best effort; fallback hints remain in place.
            }

            foreach (KeyValuePair<BuiltInCategory, List<TagSizeHint>> kvp in samples)
            {
                List<TagSizeHint> list = kvp.Value;
                if (list == null || list.Count == 0)
                    continue;

                double avgWidth = list.Average(x => x.Width);
                double avgHeight = list.Average(x => x.Height);
                if (avgWidth > Constants.ZERO_LENGTH_TOLERANCE && avgHeight > Constants.ZERO_LENGTH_TOLERANCE)
                    hints[kvp.Key] = new TagSizeHint(avgWidth, avgHeight);
            }

            return hints;
        }

        internal static bool TryGetTaggedModelCategory(
            Document doc,
            IndependentTag tag,
            out BuiltInCategory category)
        {
            category = BuiltInCategory.INVALID;
            if (doc == null || tag == null)
                return false;

            try
            {
                ElementId taggedId = TagCompat.GetTaggedLocalElementId(tag);
                if (taggedId == null || taggedId == ElementId.InvalidElementId)
                    return false;

                Element tagged = doc.GetElement(taggedId);
                if (tagged?.Category == null)
                    return false;

                category = (BuiltInCategory)tagged.Category.Id.IntValue();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void ResolveTagSizeForCandidate(
            TagCandidate candidate,
            Dictionary<BuiltInCategory, TagSizeHint> sizeHints,
            int viewScale,
            out double width,
            out double height)
        {
            if (candidate != null
                && sizeHints != null
                && sizeHints.TryGetValue(candidate.Category, out TagSizeHint hint)
                && hint != null
                && hint.Width > Constants.ZERO_LENGTH_TOLERANCE
                && hint.Height > Constants.ZERO_LENGTH_TOLERANCE)
            {
                width = hint.Width;
                height = hint.Height;
                return;
            }

            EstimateFallbackTagSize(viewScale, out width, out height);
        }

        internal static bool TryGetTagTextBoxSizeInViewPlane(
            IndependentTag tag,
            View view,
            XYZ viewRight,
            XYZ viewUp,
            out double width,
            out double height)
        {
            width = 0;
            height = 0;
            if (tag == null || view == null)
                return false;

            // The bounding-box read, the eight-corner projection and the re-centre-on-the-head trick
            // that used to be written out here are all in TagViewGeometry.GetTagTextBox now - this was
            // the third copy of that trick in the project.
            AnnotationBox box = TagViewGeometry.GetTagTextBox(tag, view, viewRight, viewUp);
            if (box == null)
                return false;

            width = box.MaxX - box.MinX;
            height = box.MaxY - box.MinY;
            return width > Constants.ZERO_LENGTH_TOLERANCE
                && height > Constants.ZERO_LENGTH_TOLERANCE;
        }
    }
}
