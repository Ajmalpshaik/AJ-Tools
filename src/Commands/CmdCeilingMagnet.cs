#region Metadata
/*
 * Tool Name     : Elements to Ceiling Grid (Ceiling Magnet)
 * File Name     : CmdCeilingMagnet.cs
 * Purpose       : Snaps point-based elements to the nearest ceiling grid tile centers. On Revit 2025.3+
 *                 reads the ceiling's real grid line geometry directly (exact, no click needed). On
 *                 older versions, falls back to the ceiling's surface pattern (or a 600x600 fallback)
 *                 plus one clicked grid intersection - unchanged from the original behaviour.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.0
 *
 * Created Date  : 2026-04-12
 * Last Updated  : 2026-07-17
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (DialogHelper), AJTools.Services.CeilingMagnet
 *
 * Input         : Active project - a ceiling (host or linked), one anchor grid intersection, then
 *                 point-based elements (pre-selected and/or picked one-by-one, Esc to finish).
 * Output        : Elements moved in plan to the nearest tile center; final report of moved / aligned / skipped.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Tile size/angle is read from the ceiling surface pattern; uses
 *   UnitUtils with DisplayUnitType (the Revit 2020 unit API) - revisit for 2021+ ForgeTypeId builds.
 * - Reads linked-model ceilings for reference only; never modifies linked elements.
 * - The whole session (pre-selected batch + each pick) is one TransactionGroup assimilated into a single undo step.
 * - Esc during a pick ends the session silently; pinned / non-point elements are skipped and counted.
 * - Real-grid path (2025.3+, see CeilingGridApiCompat): clusters the ceiling's actual grid lines into
 *   two perpendicular families, derives tile size/angle from each family's own inter-line spacing (median,
 *   for robustness against clipped boundary segments), and derives the anchor point by intersecting one
 *   line from each family - so the manual PickAnchorPoint click is skipped entirely on those versions.
 *   Falls back to the original type-pattern-or-fallback + manual-click method on any version, or any
 *   ceiling, where the real grid data is unavailable or ambiguous (never guesses on ambiguous data).
 * - Thin command wrapper: ceiling/element selection, the pick-loop, and transaction handling live
 *   here; grid detection (real-geometry and surface-pattern paths) and the per-element snap math live
 *   in Services/CeilingMagnet/CeilingMagnetService.cs.
 * - Production-ready implementation.
 *
 * Changelog     :
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
                    if (originPoint == null)
                    {
                        return Result.Cancelled;
                    }
                }

                SnapSummary summary = SnapElementsToGrid(uidoc, doc, originPoint, grid);
                ShowSummary(grid, summary);

                return Result.Succeeded;
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
            return uidoc.Selection.PickPoint("Pick one ceiling grid intersection (anchor)");
        }

        private static SnapSummary SnapElementsToGrid(UIDocument uidoc, Document doc, XYZ originPoint, CeilingGridDefinition grid)
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
                "Select ceiling from current model or linked model");
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
