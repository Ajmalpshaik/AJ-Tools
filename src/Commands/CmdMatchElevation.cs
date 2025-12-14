// Tool Name: Match Elevation
// Description: Matches the middle elevation from a source MEP element to target elements.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Matches the middle elevation from a source MEP element to selected targets.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchElevation : IExternalCommand
    {
        /// <summary>
        /// Executes the match elevation workflow.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            var filter = new MepSelectionFilter();

            try
            {
                // 1) Pick SOURCE
                Reference sourceRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    filter,
                    "Select SOURCE element to copy elevation from");

                Element sourceElem = doc.GetElement(sourceRef);
                double? sourceElevation = GetMiddleElevation(sourceElem);
                if (sourceElevation == null)
                {
                    DialogHelper.ShowError("Match Elevation", "Could not read elevation from the selected element.");
                    return Result.Cancelled;
                }

                // 2) Pick TARGETS in a loop until user cancels
                int updatedCount = 0;
                while (true)
                {
                    Reference targetRef;
                    try
                    {
                        targetRef = uidoc.Selection.PickObject(
                            ObjectType.Element,
                            filter,
                            "Select TARGET element to match elevation (ESC to finish)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break; // user pressed ESC to finish applying
                    }

                    Element targetElem = doc.GetElement(targetRef);
                    if (targetElem == null)
                        continue;

                    using (Transaction t = new Transaction(doc, "Match Elevation"))
                    {
                        try
                        {
                            t.Start();

                            bool changed = SetMiddleElevation(targetElem, sourceElevation.Value);
                            if (changed)
                            {
                                t.Commit();
                                updatedCount++;
                            }
                            else
                            {
                                t.RollBack();
                            }
                        }
                        catch (Exception)
                        {
                            if (t.HasStarted() && !t.HasEnded())
                                t.RollBack();
                            // Continue to next element on error
                        }
                    }
                }

                if (updatedCount > 0)
                {
                    DialogHelper.ShowInfo("Match Elevation", $"Updated {updatedCount} element(s) to match elevation.");
                    return Result.Succeeded;
                }

                DialogHelper.ShowInfo("Match Elevation", "No elements were updated.");
                return Result.Cancelled;
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
        /// Returns the middle elevation (Z at mid-point) of the element's location curve.
        /// </summary>
        private static double? GetMiddleElevation(Element elem)
        {
            LocationCurve loc = elem?.Location as LocationCurve;
            if (loc == null)
                return null;

            Curve curve = loc.Curve;
            if (curve == null)
                return null;

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            return (p0.Z + p1.Z) / 2.0;
        }

        /// <summary>
        /// Moves the element vertically so that its middle elevation equals targetElevation.
        /// </summary>
        private static bool SetMiddleElevation(Element elem, double targetElevation)
        {
            LocationCurve loc = elem?.Location as LocationCurve;
            if (loc == null)
                return false;

            Curve curve = loc.Curve;
            if (curve == null)
                return false;

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            double currentMiddle = (p0.Z + p1.Z) / 2.0;
            double diff = targetElevation - currentMiddle;

            if (Math.Abs(diff) < 1e-9)
                return false; // already matched

            // Translate the whole curve in Z direction, preserving curve type (Line, Arc, NURBS, etc.)
            Transform translation = Transform.CreateTranslation(new XYZ(0, 0, diff));
            Curve movedCurve = curve.CreateTransformed(translation);

            loc.Curve = movedCurve;
            return true;
        }
    }
}
