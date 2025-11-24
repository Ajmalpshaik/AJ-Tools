using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AJTools
{
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchElevation : IExternalCommand
    {
        private class MepSelectionFilter : ISelectionFilter
        {
            private readonly HashSet<BuiltInCategory> _categories = new HashSet<BuiltInCategory>
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_FlexDuctCurves,
                BuiltInCategory.OST_FlexPipeCurves
            };

            public bool AllowElement(Element elem)
            {
                Category cat = elem.Category;
                if (cat == null)
                    return false;

                return _categories.Contains((BuiltInCategory)cat.Id.IntegerValue);
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

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
                Reference sourceRef = uidoc.Selection.PickObject(ObjectType.Element, filter, "Select SOURCE element to copy elevation from");
                Element sourceElem = doc.GetElement(sourceRef);
                double? sourceElevation = GetMiddleElevation(sourceElem);
                if (sourceElevation == null)
                {
                    TaskDialog.Show("Match Elevation", "Could not read elevation from the selected element.");
                    return Result.Cancelled;
                }

                int updatedCount = 0;

                while (true)
                {
                    try
                    {
                        Reference targetRef = uidoc.Selection.PickObject(ObjectType.Element, filter, "Click targets to match elevation (ESC to finish)");
                        Element targetElem = doc.GetElement(targetRef);
                        if (targetElem == null)
                            continue;

                        using (Transaction t = new Transaction(doc, "Match Elevation"))
                        {
                            t.Start();
                            bool applied = SetMiddleElevation(targetElem, sourceElevation.Value);
                            if (!applied)
                            {
                                t.RollBack();
                                continue;
                            }
                            t.Commit();
                        }

                        updatedCount++;
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                }

                if (updatedCount > 0)
                {
                    TaskDialog.Show("Match Elevation", $"Updated {updatedCount} element(s) to match elevation.");
                    return Result.Succeeded;
                }

                TaskDialog.Show("Match Elevation", "No elements were updated.");
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

        private static double? GetMiddleElevation(Element elem)
        {
            LocationCurve loc = elem?.Location as LocationCurve;
            if (loc == null)
                return null;

            Curve curve = loc.Curve;
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            return (p0.Z + p1.Z) / 2.0;
        }

        private static bool SetMiddleElevation(Element elem, double targetElevation)
        {
            LocationCurve loc = elem?.Location as LocationCurve;
            if (loc == null)
                return false;

            Curve curve = loc.Curve;
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            double currentMiddle = (p0.Z + p1.Z) / 2.0;
            double diff = targetElevation - currentMiddle;

            XYZ newP0 = new XYZ(p0.X, p0.Y, p0.Z + diff);
            XYZ newP1 = new XYZ(p1.X, p1.Y, p1.Z + diff);

            Line newLine = Line.CreateBound(newP0, newP1);
            loc.Curve = newLine;
            return true;
        }
    }
}
