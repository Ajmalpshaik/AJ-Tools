// Tool Name: Linked ID Viewer
// Description: Displays the Element ID and model source for a picked element (host or linked).
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
using AJTools.UI;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdLinkedElementIdViewer : IExternalCommand
    {
        private const string Title = "Linked ID Viewer";

        /// <summary>
        /// Shows a dialog with the element ID and model source for a picked host or linked element.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;

                if (uiDoc == null)
                {
                    TaskDialog.Show(Title, "Please open a project before running this tool.");
                    return Result.Failed;
                }

                Reference pickedReference = PickObject(uiDoc);
                if (pickedReference == null)
                    return Result.Cancelled;

                if (pickedReference.ElementId == ElementId.InvalidElementId)
                {
                    TaskDialog.Show(Title, "Please select a valid element.");
                    return Result.Failed;
                }

                ElementId elementIdToShow;
                string modelSource;

                if (pickedReference.LinkedElementId != ElementId.InvalidElementId)
                {
                    if (!GetLinkedElementInfo(
                            uiDoc.Document,
                            pickedReference,
                            out elementIdToShow,
                            out modelSource,
                            out message))
                    {
                        return Result.Failed;
                    }
                }
                else
                {
                    if (!GetHostElementInfo(
                            uiDoc.Document,
                            pickedReference,
                            out elementIdToShow,
                            out modelSource,
                            out message))
                    {
                        return Result.Failed;
                    }
                }

                if (elementIdToShow == ElementId.InvalidElementId)
                {
                    TaskDialog.Show(Title, "Could not determine the Element ID for the selection.");
                    return Result.Failed;
                }

                var window = new LinkedIdViewerWindow(
                    elementIdToShow.IntegerValue.ToString(),
                    modelSource);

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(Title, "An error occurred:\n\n" + ex.Message);
                return Result.Failed;
            }
        }

        private Reference PickObject(UIDocument uiDoc)
        {
            try
            {
                // First try picking from linked models.
                return uiDoc.Selection.PickObject(
                    ObjectType.LinkedElement,
                    "Select an element from a linked model or press ESC to cancel");
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                // Fall back to current model if linked selection is not available in this context.
                try
                {
                    return uiDoc.Selection.PickObject(
                        ObjectType.Element,
                        "Select an element from the current model or press ESC to cancel");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return null;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        private bool GetLinkedElementInfo(
            Document doc,
            Reference reference,
            out ElementId elementId,
            out string modelSource,
            out string errorMessage)
        {
            elementId = ElementId.InvalidElementId;
            modelSource = string.Empty;
            errorMessage = string.Empty;

            RevitLinkInstance linkInstance = doc.GetElement(reference.ElementId) as RevitLinkInstance;
            if (linkInstance == null)
            {
                errorMessage = "The selected reference is not a valid Revit link instance.";
                return false;
            }

            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null)
            {
                errorMessage = "The linked model is not loaded. Please load it and try again.";
                return false;
            }

            if (reference.LinkedElementId == ElementId.InvalidElementId)
            {
                errorMessage = "The selected reference does not point to a linked element.";
                return false;
            }

            elementId = reference.LinkedElementId;
            modelSource = "Linked Model: " + GetCleanLinkName(linkInstance, linkDoc);

            return true;
        }

        private bool GetHostElementInfo(
            Document doc,
            Reference reference,
            out ElementId elementId,
            out string modelSource,
            out string errorMessage)
        {
            elementId = ElementId.InvalidElementId;
            modelSource = "Current Model";
            errorMessage = string.Empty;

            Element element = doc.GetElement(reference.ElementId);
            if (element == null)
            {
                errorMessage = "Unable to read the selected element.";
                return false;
            }

            elementId = element.Id;
            return true;
        }

        private static string GetCleanLinkName(RevitLinkInstance linkInstance, Document linkDoc)
        {
            string name = (linkDoc != null && !string.IsNullOrWhiteSpace(linkDoc.Title))
                ? linkDoc.Title
                : (linkInstance?.Name ?? "Linked Model");

            int colonIndex = name.IndexOf(':');
            if (colonIndex > -1)
            {
                name = name.Substring(0, colonIndex).Trim();
            }

            return name;
        }
    }
}
