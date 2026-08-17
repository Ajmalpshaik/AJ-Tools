#region Metadata
/*
 * Tool Name     : Tag Clash - Mark Highlighter
 * File Name     : TagClashHighlighter.cs
 * Purpose       : Colours the tags the fix loop could not separate, so they can be found and sorted
 *                 out by hand, and clears those marks again afterwards.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-16
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (TagClashSettings)
 *
 * Input         : The active view and the element ids of the tags still clashing.
 * Output        : A view-specific graphic override on each of those tags, and a way to remove it.
 *
 * Notes         :
 * - The colour is a VIEW-SPECIFIC override, so it changes nothing about the tag itself and nothing in
 *   any other view. It does, however, PERSIST until it is cleared - that is why Clear Tag Clash Marks
 *   exists as its own button.
 * - Clearing resets the override on EVERY tag in the view, not only the ones this tool coloured.
 *   Nothing records which tags were marked (that would mean writing to the model), so a blanket reset
 *   is the only reliable way to guarantee no mark is left behind. If a tag in the view carries a
 *   deliberate manual override, clearing removes that too - documented here so it is never a surprise.
 * - Whether Revit accepts a projection-line override on a given tag category has NOT been verified
 *   against a live model. Every call is guarded and the caller is told how many actually took, so a
 *   refusal shows up as an honest count rather than a silent no-op.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.TagClash
{
    /// <summary>
    /// Applies and removes the "still clashing" colour on tags in a view.
    /// </summary>
    internal static class TagClashHighlighter
    {
        /// <summary>
        /// Colours the given tags in the view. Returns how many overrides Revit actually accepted,
        /// so the caller can report honestly rather than assume it worked.
        /// </summary>
        internal static int Mark(Document doc, View view, IList<ElementId> tagIds, TagClashMarkColour colour)
        {
            if (doc == null || view == null || tagIds == null || tagIds.Count == 0)
                return 0;

            OverrideGraphicSettings overrides;
            try
            {
                overrides = new OverrideGraphicSettings();
                overrides.SetProjectionLineColor(TagClashSettings.ToRevitColour(colour));
            }
            catch (Exception)
            {
                return 0;
            }

            // A thicker line makes the mark easier to spot, but it is a nice-to-have: if this
            // particular setter is unavailable the colour alone still does the job.
            try
            {
                overrides.SetProjectionLineWeight(5);
            }
            catch (Exception)
            {
                // Colour without the extra weight is fine.
            }

            int applied = 0;
            foreach (ElementId id in tagIds)
            {
                if (id == null || id == ElementId.InvalidElementId)
                    continue;

                try
                {
                    view.SetElementOverrides(id, overrides);
                    applied++;
                }
                catch (Exception)
                {
                    // Some categories refuse an override - count it as not applied and carry on.
                }
            }

            return applied;
        }

        /// <summary>
        /// Removes the override from every tag in the view. Returns how many were reset.
        /// </summary>
        internal static int ClearAll(Document doc, View view)
        {
            if (doc == null || view == null)
                return 0;

            OverrideGraphicSettings cleared;
            try
            {
                cleared = new OverrideGraphicSettings();
            }
            catch (Exception)
            {
                return 0;
            }

            var ids = new List<ElementId>();
            try
            {
                var collector = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element element in collector)
                {
                    if (element != null && element.Id != ElementId.InvalidElementId)
                        ids.Add(element.Id);
                }
            }
            catch (Exception)
            {
                return 0;
            }

            int reset = 0;
            foreach (ElementId id in ids)
            {
                try
                {
                    view.SetElementOverrides(id, cleared);
                    reset++;
                }
                catch (Exception)
                {
                    // Nothing to do - the tag simply keeps whatever it had.
                }
            }

            return reset;
        }
    }
}
