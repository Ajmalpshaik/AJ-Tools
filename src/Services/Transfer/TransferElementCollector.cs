#region Metadata
/*
 * Tool Name     : Transfer Views (shared collector)
 * File Name     : TransferElementCollector.cs
 * Purpose       : Collects the candidate elements for a given TransferViewKind (Schedule, Legend, or
 *                 Drafting View) from a document. Shared by the picker window (listing) and the command
 *                 runner (finding same-named existing elements in the target for override).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Document, TransferViewKind.
 * Output         : Ordered list of candidate Element (ViewSchedule for Schedule, View for Legend/DraftingView).
 *
 * Notes         :
 * - Schedules exclude Revit's own auto-managed IsTitleblockRevisionSchedule / IsInternalKeynoteSchedule -
 *   those are per-document singletons Revit maintains itself; copying them is not a normal workflow.
 * - Ordinary/key/material-takeoff/note-block/sheet-list schedules are all included - Ajmal can simply
 *   leave one unchecked in the picker if he doesn't want it, same as the existing Transfer View Templates
 *   tool already does for templates.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.Transfer;

namespace AJTools.Services.Transfer
{
    internal static class TransferElementCollector
    {
        public static List<Element> Collect(Document doc, TransferViewKind kind)
        {
            if (doc == null)
            {
                return new List<Element>();
            }

            switch (kind)
            {
                case TransferViewKind.Schedule:
                    return new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSchedule))
                        .Cast<ViewSchedule>()
                        .Where(vs => vs != null &&
                                     !vs.IsTemplate &&
                                     !vs.IsTitleblockRevisionSchedule &&
                                     !vs.IsInternalKeynoteSchedule)
                        .OrderBy(vs => vs.Name, System.StringComparer.OrdinalIgnoreCase)
                        .Cast<Element>()
                        .ToList();

                case TransferViewKind.Legend:
                    return CollectViewsOfType(doc, ViewType.Legend);

                default:
                    return CollectViewsOfType(doc, ViewType.DraftingView);
            }
        }

        private static List<Element> CollectViewsOfType(Document doc, ViewType viewType)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && !v.IsTemplate && v.ViewType == viewType)
                .OrderBy(v => v.Name, System.StringComparer.OrdinalIgnoreCase)
                .Cast<Element>()
                .ToList();
        }
    }
}
