#region Metadata
/*
 * Tool Name     : Get Element ID from Selection (Linked ID Viewer)
 * File Name     : CmdLinkedElementIdViewer.cs
 * Purpose       : Lets the user pick one element - in the host model or a linked model - and shows its
 *                 Element ID together with which model it came from.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.2
 *
 * Created Date  : 2025-12-07
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (LinkedIdViewerWindow), AJTools.Utils (ElementIdHelper)
 *
 * Input         : Selection - one picked element (host or linked). Read-only tool, no transaction.
 * Output        : A window showing the Element ID and its model source.
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Read-only: reads linked-model data via the link instance, never modifies any element.
 * - One pick (ObjectType.PointOnElement) covers both models: a linked pick comes back with
 *   LinkedElementId set, a host pick without it, and Execute branches on that. A click that lands on
 *   a link without hitting one element inside it is asked again as a linked-element pick, the same
 *   pick this tool always used for links.
 * - Esc during the pick cancels silently.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.1 (2025-12-10) - Host/linked pick fallback and clean link naming.
 * v1.1.0 (2026-07-01) - Refactor/audit: full metadata block; version-safe ElementId display. Behaviour unchanged.
 * v1.1.1 (2026-07-28) - Audit: window is now owned by the Revit main window (WindowInteropHelper), so it cannot drop behind Revit.
 * v1.1.2 (2026-10-12) - Host-model elements can now be picked: the pick used ObjectType.LinkedElement only, and
 *                       the host fallback waited for an InvalidOperationException PickObject never throws, so
 *                       "model or linked" (the ribbon tooltip) only ever worked for linked elements. Now one
 *                       PointOnElement pick, branched on LinkedElementId; linked results are unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.UI;
using AJTools.Utils;

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
                    ElementIdHelper.ToReportString(elementIdToShow),
                    modelSource);

                new WindowInteropHelper(window)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

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
                // PointOnElement reaches the current model AND loaded links: a linked pick returns the link
                // instance as ElementId with LinkedElementId set, a host pick leaves LinkedElementId invalid.
                // (LinkedElement alone can never pick a host element.)
                Reference picked = uiDoc.Selection.PickObject(
                    ObjectType.PointOnElement,
                    "Select an element in the current model or a linked model, or press ESC to cancel");

                // A click on a loaded link that did not land on one element inside it comes back as the link
                // instance itself - ask once more with the linked-element pick this tool always used for links.
                if (picked != null &&
                    picked.LinkedElementId == ElementId.InvalidElementId &&
                    uiDoc.Document.GetElement(picked.ElementId) is RevitLinkInstance linkInstance &&
                    linkInstance.GetLinkDocument() != null)
                {
                    return uiDoc.Selection.PickObject(
                        ObjectType.LinkedElement,
                        "Select the element inside the linked model, or press ESC to cancel");
                }

                return picked;
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
