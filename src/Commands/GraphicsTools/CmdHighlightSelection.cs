#region Metadata
/*
 * Tool Name     : Highlight Selection
 * File Name     : CmdHighlightSelection.cs
 * Purpose       : Colors the selected elements red and every other element in the active view gray,
 *                 for instant visual identification of a selection against the rest of the model.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-07-19
 * Last Updated  : 2026-07-26
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View - selected elements (preselected, or picked when none preselected).
 * Output        : Selected elements overridden red, every other element in the active view overridden
 *                 gray (single undo step). Use the existing Reset Graphics tools to clear it afterward.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Colors both projection and cut (line + solid surface fill) so the effect reads the same in plan,
 *   section, and 3D views - mirrors AJ AI Bridge fragment action-highlight-vs-rest.cs, proven live
 *   2026-07-19 (22 selected, 19 landed in the active 3D view, 3 were tags belonging to a different view).
 * - The current selection can include elements not present in the active view's collector (e.g. tags
 *   that belong to a different open view) - only the subset actually in the active view gets colored
 *   red; if NONE of the selection is in the active view, that's reported as an error instead of silently
 *   doing nothing. See knowledge/live-model/views.md for the live-verified root cause.
 * - ExpandInsulationAndLining() is deliberately NOT restricted to Ducts/Pipes - it calls
 *   Autodesk.Revit.DB.InsulationLiningBase.GetInsulationIds / GetLiningIds on every highlighted
 *   element regardless of category, and just catches/skips the ArgumentException Revit itself throws
 *   for a category that can't host insulation/lining ("This id does not represent a valid host for
 *   insulation"). Confirmed via Autodesk's own API docs (DuctInsulation/PipeInsulation class remarks:
 *   "insulation applied to the outside of a given duct/pipe, FITTING, or ACCESSORY/content") that Duct
 *   Accessories and Pipe Accessories are official, documented insulation hosts alongside ducts, pipes,
 *   and fittings - not just an edge case - so this generic per-element try/catch is the correct design,
 *   not a gap to hardcode a category list for. If Revit ever exposes insulation on Mechanical Equipment
 *   too, this code already handles it with zero changes - don't "simplify" this to a category filter.
 * - Covers BOTH directions since v1.2.0: host-selected -> its insulation AND lining follow
 *   (GetInsulationIds + GetLiningIds, same shape), and insulation/lining-selected -> its host follows
 *   (instance property InsulationLiningBase.HostElementId), with the pulled-in host then contributing
 *   its own remaining wraps too. API members verified against the real installed RevitAPI.dll on
 *   2020/2024 (full signatures identical) and 2027 (metadata name scan) on 2026-07-26 - not from the
 *   NuGet reference package alone. Note there is no PipeLining class in any supported version - lining
 *   is a duct-side concept only; the generic try/catch absorbs that difference with no special casing.
 * - Normal success is silent, matching the rest of the Graphics tool family.
 * - ESC during a pick cancels silently (no error dialog).
 *
 * Changelog     :
 * v1.2.0 (2026-07-26) - Completed the insulation story flagged in v1.1.0's scope note, both halves:
 *                       (1) selecting insulation/lining directly now pulls its HOST into the red set
 *                       (InsulationLiningBase.HostElementId), so the duct/pipe under a picked wrap
 *                       highlights too; (2) hosts now also pull in their LINING via GetLiningIds
 *                       alongside the existing GetInsulationIds - duct lining was previously left
 *                       gray. AddHostedInsulation renamed to ExpandInsulationAndLining; same
 *                       category-agnostic try/catch design as before, applied per lookup.
 * v1.1.0 (2026-07-19) - Fix: selecting a duct/pipe with insulation left the insulation gray (a separate
 *                       hosted ElementId, not part of the raw selection). Added AddHostedInsulation() -
 *                       calls InsulationLiningBase.GetInsulationIds per highlighted host element and
 *                       folds any hosted insulation id (that's also present in the active view) into
 *                       the red set before the gray "rest" pass runs.
 * v1.0.0 (2026-07-19) - Initial release, requested after proving the same effect live via the AJ AI
 *                       Bridge (highlight selection red / rest of view gray).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.GraphicsTools;
using AJTools.Services.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Colors the current selection red and every other element in the active view gray.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdHighlightSelection : IExternalCommand
    {
        private const string DialogTitle = "Highlight Selection";

        private static readonly GraphicsColorValue HighlightColor = GraphicsColorValue.FromRgb(255, 0, 0);
        private static readonly GraphicsColorValue RestColor = GraphicsColorValue.FromRgb(128, 128, 128);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    DialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                {
                    return contextResult;
                }

                SelectionCaptureResult selection = GraphicsSelectionService.GetPreselectedOrPromptElementIds(
                    context.UIDocument,
                    selectionFilter: null,
                    prompt: "Select elements to highlight in the active view.");

                if (selection.WasCancelled)
                {
                    return Result.Cancelled;
                }

                if (selection.ElementIds.Count == 0)
                {
                    DialogHelper.ShowError(DialogTitle, "Select at least one element.");
                    return Result.Cancelled;
                }

                ICollection<ElementId> elementsInView = new FilteredElementCollector(context.Document, context.ActiveView.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds();

                var highlightKeys = new HashSet<int>(
                    selection.ElementIds.Select(id => ElementIdHelper.GetIntegerValue(id)));

                List<ElementId> highlightIds = elementsInView
                    .Where(id => highlightKeys.Contains(ElementIdHelper.GetIntegerValue(id)))
                    .ToList();

                if (highlightIds.Count == 0)
                {
                    DialogHelper.ShowError(
                        DialogTitle,
                        "None of the currently selected elements are visible in the active view (they may belong to a different view). Select elements that are visible here and try again.");
                    return Result.Cancelled;
                }

                highlightIds = ExpandInsulationAndLining(context.Document, highlightIds, elementsInView);

                var highlightIdSet = new HashSet<int>(highlightIds.Select(id => ElementIdHelper.GetIntegerValue(id)));
                List<ElementId> restIds = elementsInView
                    .Where(id => !highlightIdSet.Contains(ElementIdHelper.GetIntegerValue(id)))
                    .ToList();

                OverrideGraphicSettings highlightSettings = BuildSolidOverride(context.Document, HighlightColor);
                OverrideGraphicSettings restSettings = BuildSolidOverride(context.Document, RestColor);

                GraphicsOperationSummary summary = GraphicsCommandService.ExecuteSummaryTransaction(
                    context.Document,
                    "AJ Tools - Highlight Selection",
                    () =>
                    {
                        GraphicsOperationSummary highlightSummary = GraphicsElementService.ApplyOverrides(
                            context.Document,
                            context.ActiveView,
                            highlightIds,
                            highlightSettings);
                        GraphicsOperationSummary restSummary = GraphicsElementService.ApplyOverrides(
                            context.Document,
                            context.ActiveView,
                            restIds,
                            restSettings);

                        return new GraphicsOperationSummary
                        {
                            Attempted = highlightSummary.Attempted + restSummary.Attempted,
                            Applied = highlightSummary.Applied + restSummary.Applied,
                            Skipped = highlightSummary.Skipped + restSummary.Skipped
                        };
                    });

                if (!summary.HasChanges)
                {
                    DialogHelper.ShowError(DialogTitle, "No graphics overrides were applied.");
                    return Result.Cancelled;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(DialogTitle, ex.Message);
                return Result.Failed;
            }
        }

        private static List<ElementId> ExpandInsulationAndLining(
            Document doc,
            List<ElementId> selectedIds,
            ICollection<ElementId> elementsInView)
        {
            var viewKeys = new HashSet<int>(elementsInView.Select(id => ElementIdHelper.GetIntegerValue(id)));
            var expandedKeys = new HashSet<int>(selectedIds.Select(id => ElementIdHelper.GetIntegerValue(id)));
            var expanded = new List<ElementId>(selectedIds);

            // Direction 1: a selected insulation/lining element pulls in its host, so picking the wrap
            // highlights the duct/pipe underneath it too (DuctInsulation, DuctLining, PipeInsulation all
            // derive from InsulationLiningBase - verified on installed RevitAPI.dll 2020/2024, names
            // confirmed present in 2027; there is no PipeLining class in any of them).
            foreach (ElementId selectedId in selectedIds)
            {
                var wrap = doc.GetElement(selectedId) as InsulationLiningBase;
                if (wrap == null)
                {
                    continue;
                }

                ElementId hostId = wrap.HostElementId;
                if (hostId == null || hostId == ElementId.InvalidElementId)
                {
                    continue;
                }

                int key = ElementIdHelper.GetIntegerValue(hostId);
                if (viewKeys.Contains(key) && expandedKeys.Add(key))
                {
                    expanded.Add(hostId);
                }
            }

            // Direction 2: every element in the red set (including hosts just added above) pulls in its
            // hosted insulation AND lining. Index-capped loop: wraps appended inside the loop can't host
            // anything themselves, so they never need a pass of their own.
            int hostCandidateCount = expanded.Count;
            for (int i = 0; i < hostCandidateCount; i++)
            {
                AddHostedWrapIds(InsulationLiningBase.GetInsulationIds, doc, expanded[i], viewKeys, expandedKeys, expanded);
                AddHostedWrapIds(InsulationLiningBase.GetLiningIds, doc, expanded[i], viewKeys, expandedKeys, expanded);
            }

            return expanded;
        }

        private static void AddHostedWrapIds(
            Func<Document, ElementId, ICollection<ElementId>> getWrapIds,
            Document doc,
            ElementId hostId,
            HashSet<int> viewKeys,
            HashSet<int> expandedKeys,
            List<ElementId> expanded)
        {
            ICollection<ElementId> wrapIds;
            try
            {
                // Deliberately category-agnostic (see Notes): Revit itself throws ArgumentException for
                // an id that can't host insulation/lining - catching per element IS the category filter.
                wrapIds = getWrapIds(doc, hostId);
            }
            catch
            {
                return;
            }

            if (wrapIds == null)
            {
                return;
            }

            foreach (ElementId wrapId in wrapIds)
            {
                int key = ElementIdHelper.GetIntegerValue(wrapId);
                if (viewKeys.Contains(key) && expandedKeys.Add(key))
                {
                    expanded.Add(wrapId);
                }
            }
        }

        private static OverrideGraphicSettings BuildSolidOverride(Document doc, GraphicsColorValue color)
        {
            ElementId solidFillPatternId = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(pattern => pattern.GetFillPattern().IsSolidFill)
                ?.Id ?? ElementId.InvalidElementId;

            var input = new GraphicsOverrideInput
            {
                ProjectionLineColor = color,
                SurfaceForegroundPatternId = solidFillPatternId,
                SurfaceForegroundPatternColor = color,
                UseProjectionSurfaceSettingsForCut = true
            };

            return GraphicsOverrideBuilder.Build(input);
        }
    }
}
