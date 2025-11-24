using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AJTools
{
    [Transaction(TransactionMode.Manual)]
    public class CmdCopyDimensionText : IExternalCommand
    {
        private class DimensionSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Dimension;
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

            try
            {
                Reference sourceRef = uidoc.Selection.PickObject(ObjectType.Element, new DimensionSelectionFilter(), "Select SOURCE dimension to copy text from");
                Dimension sourceDim = doc.GetElement(sourceRef) as Dimension;
                if (sourceDim == null)
                    return Result.Cancelled;

                string textAbove = sourceDim.Above;
                string textBelow = sourceDim.Below;
                string textPrefix = sourceDim.Prefix;
                string textSuffix = sourceDim.Suffix;

                if (string.IsNullOrEmpty(textAbove) && string.IsNullOrEmpty(textBelow) && string.IsNullOrEmpty(textPrefix) && string.IsNullOrEmpty(textSuffix))
                {
                    TaskDialog.Show("Copy Dim Text", "The selected dimension has no Above/Below/Prefix/Suffix text to copy.");
                    return Result.Cancelled;
                }

                int updatedCount = 0;

                while (true)
                {
                    try
                    {
                        Reference targetRef = uidoc.Selection.PickObject(ObjectType.Element, new DimensionSelectionFilter(), "Select TARGET dimension (ESC to finish)");
                        Dimension targetDim = doc.GetElement(targetRef) as Dimension;
                        if (targetDim == null)
                            continue;

                        using (Transaction t = new Transaction(doc, "Paste Dimension Text"))
                        {
                            t.Start();
                            targetDim.Above = textAbove;
                            targetDim.Below = textBelow;
                            targetDim.Prefix = textPrefix;
                            targetDim.Suffix = textSuffix;
                            t.Commit();
                        }

                        uidoc.RefreshActiveView();
                        updatedCount++;
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                }

                if (updatedCount > 0)
                {
                    TaskDialog.Show("Copy Dim Text", $"Finished. Copied text to {updatedCount} dimension(s).");
                    return Result.Succeeded;
                }

                TaskDialog.Show("Copy Dim Text", "No dimensions were updated.");
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
    }
}
