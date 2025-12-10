// Tool Name: Copy Dimension Text
// Description: Copies dimension text properties between selected dimensions.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AJTools.Commands
{
    /// <summary>
    /// Copies dimension text fields from a source dimension to selected targets.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCopyDimensionText : IExternalCommand
    {
        private class DimensionSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Dimension;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        /// <summary>
        /// Copies Above/Below/Prefix/Suffix text from a source dimension to target dimensions.
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

                IList<Reference> targetRefs = uidoc.Selection.PickObjects(ObjectType.Element, new DimensionSelectionFilter(), "Select TARGET dimension(s)");
                if (targetRefs == null || !targetRefs.Any())
                    return Result.Cancelled;

                using (Transaction t = new Transaction(doc, "Paste Dimension Text"))
                {
                    t.Start();
                    foreach (Reference targetRef in targetRefs)
                    {
                        Dimension targetDim = doc.GetElement(targetRef) as Dimension;
                        if (targetDim == null)
                            continue;
                        
                        targetDim.Above = textAbove;
                        targetDim.Below = textBelow;
                        targetDim.Prefix = textPrefix;
                        targetDim.Suffix = textSuffix;
                    }
                    t.Commit();
                }

                TaskDialog.Show("Copy Dim Text", $"Finished. Copied text to {targetRefs.Count} dimension(s).");
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
    }
}
