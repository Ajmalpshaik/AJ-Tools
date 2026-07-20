#region Metadata
/*
 * Tool Name     : Elements to Ceiling Grid (Ceiling Magnet)
 * File Name     : CmdCeilingMagnet.cs
 * Purpose       : Snaps point-based elements to the nearest ceiling grid tile centers. On Revit 2025.3+
 *                 reads the ceiling's real grid line geometry directly (exact, no click needed). On
 *                 older versions, falls back to the ceiling's surface pattern (or a 600x600 fallback)
 *                 plus one clicked grid intersection - unchanged from the original behaviour. As of
 *                 v1.5.0 Ajmal is asked up front which element-picking workflow to run: the original
 *                 one-ceiling / pick-elements-one-at-a-time flow, or the v1.4.0 flow (window-select all
 *                 elements once, then a repeatable ceiling+point round, Esc to finish) - both kept side
 *                 by side, neither replaces the other.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-04-12
 * Last Updated  : 2026-07-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (DialogHelper), AJTools.Services.CeilingMagnet
 *
 * Input         : Active project - first a TaskDialog command-link choice of workflow, then either (a)
 *                 one ceiling (host or linked) + one anchor point, then point-based elements pre-selected
 *                 and/or picked one-by-one (Esc to finish), or (b) a batch of point-based elements
 *                 (current selection if any, otherwise one window/click multi-select), then a repeatable
 *                 round of one ceiling + one anchor grid intersection per room (Esc at any point in a
 *                 round to finish).
 * Output        : Elements moved in plan to the nearest tile center of whichever ceiling they snapped to;
 *                 mode (a) reports the single grid's detail, mode (b) reports an aggregate (ceilings
 *                 processed / moved / aligned / skipped).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Tile size/angle is read from the ceiling surface pattern; uses
 *   UnitUtils with DisplayUnitType (the Revit 2020 unit API) - revisit for 2021+ ForgeTypeId builds.
 * - Reads linked-model ceilings for reference only; never modifies linked elements.
 * - Both modes wrap their whole session in one TransactionGroup assimilated into a single undo step;
 *   mode (b) rolls back instead if Esc is pressed before any round completes (nothing to undo).
 * - Mode (a) - RunOneAtATimeMode/SnapElementsOneAtATime - is the original v1.3.0 logic, unchanged: one
 *   ceiling only, pre-selected elements snap immediately, then a PickObject loop (Esc to finish) snaps
 *   more one at a time, all to that same grid.
 * - Mode (b) - RunWindowMultiSelectMode/PickElementBatch/RunCeilingRounds - is the v1.4.0 logic: Esc
 *   during the up-front element pick cancels the whole command; Esc during a ceiling pick or an
 *   anchor-point pick ends the round loop (keeps whatever rounds already completed) rather than
 *   cancelling outright - a wrong (non-ceiling) pick just shows an error and lets the round be retried.
 *   Each round only snaps the elements from the original batch that geometrically sit over that round's
 *   ceiling (CeilingMagnetService.FilterElementsOverCeiling - real ceiling solid geometry, not a
 *   bounding-box guess, per the Modeler mindset rule) - lets one up-front multi-room selection be
 *   snapped to each room's own ceiling grid in turn, without re-snapping already-placed elements to a
 *   later, unrelated grid. Confirmed with Ajmal this filtering behavior, not a global re-snap.
 * - Pinned / non-point elements are skipped and counted in both modes.
 * - Real-grid path (2025.3+, see CeilingGridApiCompat): clusters the ceiling's actual grid lines into
 *   two perpendicular families, derives tile size/angle from each family's own inter-line spacing (median,
 *   for robustness against clipped boundary segments), and derives the anchor point by intersecting one
 *   line from each family - so the manual PickAnchorPoint click is skipped entirely on those versions.
 *   Falls back to the original type-pattern-or-fallback + manual-click method on any version, or any
 *   ceiling, where the real grid data is unavailable or ambiguous (never guesses on ambiguous data).
 *   Applies identically in both modes.
 * - Thin command wrapper: mode choice, element-batch/ceiling selection, the round loop, and transaction
 *   handling live here; grid detection, the geometric over-ceiling filter, and the per-element snap math
 *   live in Services/CeilingMagnet/CeilingMagnetService.cs.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.5.0 (2026-07-20) - Ajmal tried the v1.4.0 rework and asked to keep BOTH workflows in the same
 *                       tool rather than replace one with the other - the original one-at-a-time flow
 *                       ("before was good") plus the new window-select-then-loop flow ("this also I
 *                       need"). Added an up-front TaskDialog command-link choice (AskElementPickMode)
 *                       and split the old single Execute() body into RunOneAtATimeMode (restores the
 *                       exact original v1.3.0 logic, byte-for-byte behavior) and
 *                       RunWindowMultiSelectMode (the v1.4.0 logic, unchanged). ShowSummary is now
 *                       overloaded - the grid-detail report for mode (a), the aggregate report for
 *                       mode (b) - since the two modes report meaningfully different things.
 * v1.4.0 (2026-07-20) - Reworked the selection workflow per Ajmal's request: elements are now
 *                       window/click multi-selected ONCE up front (uidoc.Selection.PickObjects,
 *                       reusing the current selection if one already exists) instead of one-at-a-time
 *                       picking after the ceiling. The command then loops a ceiling+anchor-point round
 *                       repeatedly (Esc to finish the whole loop) - each round snaps only the elements
 *                       from that batch sitting over the picked ceiling (new
 *                       CeilingMagnetService.FilterElementsOverCeiling), so one selection can be
 *                       walked room-by-room without re-running the command. Confirmed with Ajmal:
 *                       later rounds filter to that ceiling's own elements, they do not re-snap
 *                       everything already placed by an earlier round. Aggregate summary (ceilings
 *                       processed + totals) replaces the old single-ceiling grid-detail report.
 *                       [Superseded same day by v1.5.0 - this became one of two selectable modes.]
 * v1.3.0 (2026-07-17) - Extracted the ceiling-grid-detection algorithm (real-grid clustering, family
 *                       spacing, 2D intersection, surface-pattern reading) and the per-element snap
 *                       math into Services/CeilingMagnet/CeilingMagnetService.cs (code review cleanup
 *                       pass) - no behavior change. CeilingGridDefinition, CeilingSelection, and
 *                       SnapSummary moved with it since both Command and Service read/build them.
 * v1.0.0 (2026-04-12) - Initial C# port of the pyRevit ceiling-snap logic.
 * v1.1.0 (2026-07-01) - Refactor/audit: standardized metadata block; ceiling selection, grid resolution
 *                       and transaction flow reviewed. Snap behaviour unchanged.
 * v1.2.0 (2026-07-07) - Added a Revit 2025.3+ real-grid detection path (Ceiling.GetCeilingGridLines):
 *                       exact tile size/angle/anchor from the ceiling's actual grid, no manual click
 *                       needed. 2020-2024 (and any ceiling without usable grid data) behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Services.CeilingMagnet;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Direct C# port of working pyRevit logic.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCeilingMagnet : IExternalCommand
    {
        private const string TransactionGroupName = "Ceiling Magnet";
        private const string SnapPreselectedTransactionName = "Snap Preselected";
        private const string SnapElementTransactionName = "Snap Element";

        /// <summary>Which of the two element-picking workflows Ajmal chose for this run.</summary>
        private enum ElementPickMode
        {
            /// <summary>Original v1.3.0 flow: pick one ceiling first, then elements one at a time.</summary>
            OneAtATime,

            /// <summary>v1.4.0 flow: window/click multi-select all elements first, then repeat
            /// ceiling+point rounds.</summary>
            WindowMultiSelect
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            try
            {
                ElementPickMode? mode = AskElementPickMode();
                if (mode == null)
                {
                    return Result.Cancelled;
                }

                return mode == ElementPickMode.OneAtATime
                    ? RunOneAtATimeMode(uidoc, doc)
                    : RunWindowMultiSelectMode(uidoc, doc);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Asks Ajmal up front which of the two element-picking workflows to run this time. Both are
        /// kept side by side per his request - the original one-ceiling / pick-elements-one-at-a-time
        /// flow, and the newer window-select-everything-then-repeat-rooms flow.
        /// </summary>
        private static ElementPickMode? AskElementPickMode()
        {
            TaskDialog dialog = new TaskDialog(CeilingMagnetService.ToolTitle)
            {
                MainInstruction = "How do you want to pick the elements to snap?",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };
            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink1,
                "Pick one at a time",
                "Pick one ceiling first, then click each element to snap - Esc when done. Good for a single room.");
            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink2,
                "Window-select multiple at once",
                "Select all the elements first (drag a box, or click multiple, then Finish), then repeat "
                    + "ceiling + point for each room - Esc to finish everything.");

            TaskDialogResult result = dialog.Show();
            switch (result)
            {
                case TaskDialogResult.CommandLink1:
                    return ElementPickMode.OneAtATime;
                case TaskDialogResult.CommandLink2:
                    return ElementPickMode.WindowMultiSelect;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Original v1.3.0 workflow: pick one ceiling, resolve its grid, then snap any pre-selected
        /// elements immediately followed by a one-at-a-time pick loop (Esc to finish) - every element
        /// in this run snaps to that same single ceiling.
        /// </summary>
        private static Result RunOneAtATimeMode(UIDocument uidoc, Document doc)
        {
            CeilingSelection ceilingSelection;
            if (!TryPickCeilingSelection(uidoc, doc, out ceilingSelection))
            {
                return Result.Cancelled;
            }

            CeilingGridDefinition grid;
            XYZ originPoint;
            if (!CeilingMagnetService.TryGetGridFromRealGeometry(ceilingSelection, out grid, out originPoint))
            {
                if (!CeilingMagnetService.TryCreateGridDefinition(ceilingSelection, out grid))
                {
                    return Result.Cancelled;
                }

                originPoint = PickAnchorPoint(uidoc);
            }

            SnapSummary summary = SnapElementsOneAtATime(uidoc, doc, originPoint, grid);
            ShowSummary(grid, summary);
            return Result.Succeeded;
        }

        private static SnapSummary SnapElementsOneAtATime(UIDocument uidoc, Document doc, XYZ originPoint, CeilingGridDefinition grid)
        {
            SnapSummary summary = new SnapSummary();

            using (TransactionGroup group = new TransactionGroup(doc, TransactionGroupName))
            {
                group.Start();

                IList<Element> preselected = GetPreselectedPointElements(uidoc, doc);
                if (preselected.Count > 0)
                {
                    using (Transaction tx = new Transaction(doc, SnapPreselectedTransactionName))
                    {
                        tx.Start();
                        foreach (Element element in preselected)
                        {
                            CeilingMagnetService.SnapElement(doc, element, originPoint, grid, summary);
                        }

                        tx.Commit();
                    }
                }

                while (true)
                {
                    Reference pickedReference;
                    try
                    {
                        pickedReference = uidoc.Selection.PickObject(
                            ObjectType.Element,
                            new PointElementSelectionFilter(),
                            "Pick element to snap (Esc to finish)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    Element element = doc.GetElement(pickedReference.ElementId);
                    using (Transaction tx = new Transaction(doc, SnapElementTransactionName))
                    {
                        tx.Start();
                        CeilingMagnetService.SnapElement(doc, element, originPoint, grid, summary);
                        tx.Commit();
                    }
                }

                group.Assimilate();
            }

            return summary;
        }

        /// <summary>
        /// v1.4.0 workflow: window/click multi-select all elements once up front, then repeat a
        /// ceiling+anchor-point round (RunCeilingRounds) until Esc.
        /// </summary>
        private static Result RunWindowMultiSelectMode(UIDocument uidoc, Document doc)
        {
            IList<Element> batch = PickElementBatch(uidoc, doc);
            if (batch == null)
            {
                return Result.Cancelled;
            }

            if (batch.Count == 0)
            {
                DialogHelper.ShowError(CeilingMagnetService.ToolTitle, "No elements were selected.");
                return Result.Cancelled;
            }

            int roundCount;
            SnapSummary summary = RunCeilingRounds(uidoc, doc, batch, out roundCount);
            if (roundCount == 0)
            {
                return Result.Cancelled;
            }

            ShowSummary(roundCount, summary);
            return Result.Succeeded;
        }

        /// <summary>
        /// Gets the batch of elements every round will snap from: reuses the current selection if
        /// Ajmal already had one before running the command, otherwise prompts one window/click
        /// multi-select (Esc cancels the whole command at this stage only).
        /// </summary>
        private static IList<Element> PickElementBatch(UIDocument uidoc, Document doc)
        {
            IList<Element> preselected = GetPreselectedPointElements(uidoc, doc);
            if (preselected.Count > 0)
            {
                return preselected;
            }

            IList<Reference> pickedReferences;
            try
            {
                pickedReferences = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new PointElementSelectionFilter(),
                    "Window-select the elements to snap, then press Finish (Esc to cancel)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }

            List<Element> result = new List<Element>();
            foreach (Reference reference in pickedReferences)
            {
                Element element = doc.GetElement(reference.ElementId);
                if (element != null)
                {
                    result.Add(element);
                }
            }

            return result;
        }

        /// <summary>
        /// Repeats a ceiling+anchor-point round until Esc: each round resolves one ceiling's grid,
        /// then snaps only the elements from <paramref name="batch"/> that sit over that ceiling. Esc
        /// on the ceiling pick or the anchor-point pick ends the loop (keeping whatever rounds already
        /// ran); an invalid ceiling pick just shows an error and lets the round be retried.
        /// </summary>
        private static SnapSummary RunCeilingRounds(UIDocument uidoc, Document doc, IList<Element> batch, out int roundCount)
        {
            SnapSummary summary = new SnapSummary();
            roundCount = 0;

            using (TransactionGroup group = new TransactionGroup(doc, TransactionGroupName))
            {
                group.Start();

                while (true)
                {
                    CeilingSelection ceilingSelection;
                    try
                    {
                        if (!TryPickCeilingSelection(uidoc, doc, out ceilingSelection))
                        {
                            continue;
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    CeilingGridDefinition grid;
                    XYZ originPoint;
                    if (!CeilingMagnetService.TryGetGridFromRealGeometry(ceilingSelection, out grid, out originPoint))
                    {
                        if (!CeilingMagnetService.TryCreateGridDefinition(ceilingSelection, out grid))
                        {
                            continue;
                        }

                        try
                        {
                            originPoint = PickAnchorPoint(uidoc);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            break;
                        }
                    }

                    IList<Element> roundElements = CeilingMagnetService.FilterElementsOverCeiling(batch, ceilingSelection);
                    if (roundElements.Count == 0)
                    {
                        DialogHelper.ShowError(
                            CeilingMagnetService.ToolTitle,
                            "None of the selected elements sit over that ceiling. Pick another ceiling, or Esc to finish.");
                        continue;
                    }

                    using (Transaction tx = new Transaction(doc, SnapElementTransactionName))
                    {
                        tx.Start();
                        foreach (Element element in roundElements)
                        {
                            CeilingMagnetService.SnapElement(doc, element, originPoint, grid, summary);
                        }

                        tx.Commit();
                    }

                    roundCount++;
                }

                if (roundCount > 0)
                {
                    group.Assimilate();
                }
                else
                {
                    group.RollBack();
                }
            }

            return summary;
        }

        private static bool TryPickCeilingSelection(UIDocument uidoc, Document hostDoc, out CeilingSelection selection)
        {
            selection = null;

            Reference ceilingReference = PickCeilingReference(uidoc);
            string skippedReason;
            if (TryResolveCeilingSelection(hostDoc, ceilingReference, out selection, out skippedReason))
            {
                return true;
            }

            if (CanPickLinkedElementFromReference(hostDoc, ceilingReference))
            {
                Reference linkedReference = PickLinkedCeilingReference(uidoc);
                if (TryResolveCeilingSelection(hostDoc, linkedReference, out selection, out skippedReason))
                {
                    return true;
                }
            }

            DialogHelper.ShowError(CeilingMagnetService.ToolTitle, skippedReason);
            return false;
        }

        private static XYZ PickAnchorPoint(UIDocument uidoc)
        {
            // Revit 2020 does not expose the rendered model pattern origin, so the anchor
            // still has to come from one manual click on a visible grid intersection.
            return uidoc.Selection.PickPoint("Pick one ceiling grid intersection (anchor) - Esc to finish");
        }

        private static void ShowSummary(int roundCount, SnapSummary summary)
        {
            DialogHelper.ShowInfo(
                CeilingMagnetService.ToolTitle,
                string.Format(
                    "Ceilings processed: {0}\nMoved: {1}\nAlready aligned: {2}\nSkipped: {3}",
                    roundCount,
                    summary.Moved,
                    summary.Aligned,
                    summary.Skipped));
        }

        private static void ShowSummary(CeilingGridDefinition grid, SnapSummary summary)
        {
            double tileUmm = RevitCompat.InternalToMm(grid.TileU);
            double tileVmm = RevitCompat.InternalToMm(grid.TileV);
            double angleDeg = Math.Atan2(grid.AxisV.Y, grid.AxisV.X) * 180.0 / Math.PI;
            string source;
            switch (grid.Source)
            {
                case CeilingGridSource.ExactApi:
                    source = string.Format("exact ceiling grid {0:0.#} x {1:0.#} mm @ {2:0.##} deg (from model, no click needed)", tileUmm, tileVmm, angleDeg);
                    break;
                case CeilingGridSource.Fallback600:
                    source = "fallback 600x600 (no model pattern on ceiling)";
                    break;
                default:
                    source = string.Format("ceiling pattern {0:0.#} x {1:0.#} mm @ {2:0.##} deg", tileUmm, tileVmm, angleDeg);
                    break;
            }

            DialogHelper.ShowInfo(
                CeilingMagnetService.ToolTitle,
                string.Format(
                    "Grid: {0}\nMoved: {1}\nAlready aligned: {2}\nSkipped: {3}",
                    source,
                    summary.Moved,
                    summary.Aligned,
                    summary.Skipped));
        }

        private static Reference PickCeilingReference(UIDocument uidoc)
        {
            return uidoc.Selection.PickObject(
                ObjectType.PointOnElement,
                "Select ceiling from current model or linked model - Esc to finish");
        }

        private static Reference PickLinkedCeilingReference(UIDocument uidoc)
        {
            return uidoc.Selection.PickObject(
                ObjectType.LinkedElement,
                "Select ceiling inside the linked model");
        }

        private static bool TryResolveCeilingSelection(
            Document hostDoc,
            Reference reference,
            out CeilingSelection selection,
            out string skippedReason)
        {
            selection = null;
            skippedReason = string.Empty;

            if (hostDoc == null || reference == null)
            {
                skippedReason = "No ceiling was selected.";
                return false;
            }

            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                RevitLinkInstance linkInstance = hostDoc.GetElement(reference.ElementId) as RevitLinkInstance;
                if (linkInstance == null)
                {
                    skippedReason = "The selected linked ceiling reference is not associated with a valid Revit link instance.";
                    return false;
                }

                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null)
                {
                    skippedReason = "The selected linked model is unloaded. Load the link and try again.";
                    return false;
                }

                Ceiling linkedCeiling = linkDoc.GetElement(reference.LinkedElementId) as Ceiling;
                if (linkedCeiling == null)
                {
                    skippedReason = "Skipped: the selected linked element is not a ceiling.";
                    return false;
                }

                selection = new CeilingSelection(linkDoc, linkedCeiling, linkInstance.GetTotalTransform());
                return true;
            }

            Element hostElement = hostDoc.GetElement(reference.ElementId);
            Ceiling hostCeiling = hostElement as Ceiling;
            if (hostCeiling == null)
            {
                RevitLinkInstance linkInstance = hostElement as RevitLinkInstance;
                if (linkInstance != null && linkInstance.GetLinkDocument() == null)
                {
                    skippedReason = "The selected linked model is unloaded. Load the link and try again.";
                }
                else
                {
                    skippedReason = linkInstance != null
                        ? "Select the ceiling inside the linked model, not only the link instance."
                        : "Skipped: the selected host element is not a ceiling.";
                }

                return false;
            }

            selection = new CeilingSelection(hostDoc, hostCeiling, Transform.Identity);
            return true;
        }

        private static bool CanPickLinkedElementFromReference(Document doc, Reference reference)
        {
            if (doc == null || reference == null || reference.ElementId == ElementId.InvalidElementId)
            {
                return false;
            }

            RevitLinkInstance linkInstance = doc.GetElement(reference.ElementId) as RevitLinkInstance;
            return linkInstance != null && linkInstance.GetLinkDocument() != null;
        }

        private static IList<Element> GetPreselectedPointElements(UIDocument uidoc, Document doc)
        {
            List<Element> result = new List<Element>();
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return result;
            }

            foreach (ElementId id in selectedIds)
            {
                Element element = doc.GetElement(id);
                if (IsPointBasedElement(element))
                {
                    result.Add(element);
                }
            }

            return result;
        }

        private static bool IsPointBasedElement(Element element)
        {
            return element?.Location is LocationPoint;
        }

        private sealed class PointElementSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return IsPointBasedElement(elem);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
