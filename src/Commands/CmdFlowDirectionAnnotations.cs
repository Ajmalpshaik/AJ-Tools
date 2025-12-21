// Tool Name: Flow Direction Annotations
// Description: Places flow direction annotation families along ducts and pipes with user-controlled spacing.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.FlowDirection

using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Models;
using AJTools.Services.FlowDirection;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Places flow direction annotations along ducts and pipes.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdFlowDirectionAnnotations : IExternalCommand
    {
        /// <summary>
        /// Executes the flow direction annotation workflow.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocumentAndView(uidoc, out message))
            {
                DialogHelper.ShowError("Flow Direction", message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError("Flow Direction", message);
                return Result.Cancelled;
            }

            View view = doc.ActiveView;
            if (!IsSupportedView(view, out message))
            {
                DialogHelper.ShowError("Flow Direction", message);
                return Result.Cancelled;
            }

            var settingsTracker = new FlowDirectionSettingsTracker(doc);
            FlowDirectionSettingsState state = settingsTracker.LastState;
            if (state == null)
            {
                DialogHelper.ShowInfo("Flow Direction", "Run 'Flow Direction Settings' to choose the annotation family and spacing.");
                return Result.Cancelled;
            }

            FamilySymbol symbol = doc.GetElement(state.SymbolId) as FamilySymbol;
            if (!IsValidAnnotationSymbol(symbol))
            {
                DialogHelper.ShowError("Flow Direction", "The saved annotation family is missing or invalid. Open Flow Direction Settings and choose a valid family.");
                return Result.Cancelled;
            }

            double spacingInternal = state.SpacingInternal;
            if (spacingInternal <= 1e-6)
            {
                DialogHelper.ShowError("Flow Direction", "The saved spacing is invalid. Open Flow Direction Settings and enter a valid spacing.");
                return Result.Cancelled;
            }

            if (!symbol.IsActive)
            {
                using (Transaction t = new Transaction(doc, "Activate Flow Annotation Family"))
                {
                    t.Start();
                    symbol.Activate();
                    t.Commit();
                }
            }

            var filter = new DuctPipeSelectionFilter();
            int processedCount = 0;
            int placedTotal = 0;
            int skippedCount = 0;
            HashSet<string> skipReasons = new HashSet<string>();

            while (true)
            {
                Reference pickedRef;
                try
                {
                    pickedRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        filter,
                        "Select duct or pipe to place flow annotations (ESC to finish)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                Element element = doc.GetElement(pickedRef);
                if (element == null)
                    continue;

                processedCount++;

                using (Transaction t = new Transaction(doc, "Place Flow Direction Annotations"))
                {
                    t.Start();
                    bool placed = FlowDirectionAnnotationService.TryPlaceFlowAnnotations(
                        doc,
                        view,
                        symbol,
                        spacingInternal,
                        element,
                        out int placedCount,
                        out string skipReason);

                    if (placed)
                    {
                        t.Commit();
                        placedTotal += placedCount;
                    }
                    else
                    {
                        t.RollBack();
                        skippedCount++;
                        if (!string.IsNullOrWhiteSpace(skipReason))
                        {
                            skipReasons.Add(skipReason);
                        }
                    }
                }
            }

            if (processedCount == 0)
            {
                DialogHelper.ShowInfo("Flow Direction", "No elements were selected.");
                return Result.Cancelled;
            }

            int succeededCount = processedCount - skippedCount;
            string summary = $"Placed {placedTotal} annotation(s) on {succeededCount} element(s).";
            if (skippedCount > 0)
            {
                summary += $"\nSkipped {skippedCount} element(s).";
                if (skipReasons.Count > 0)
                {
                    summary += "\n\nReasons:";
                    foreach (string reason in skipReasons)
                    {
                        summary += $"\n- {reason}";
                    }
                }
            }

            DialogHelper.ShowInfo("Flow Direction", summary);
            return placedTotal > 0 ? Result.Succeeded : Result.Cancelled;
        }

        private static bool IsSupportedView(View view, out string message)
        {
            if (view == null)
            {
                message = "No active view.";
                return false;
            }

            if (view.IsTemplate)
            {
                message = "Please run this tool in a non-template view.";
                return false;
            }

            ViewType viewType = view.ViewType;
            if (viewType == ViewType.ThreeD ||
                viewType == ViewType.DrawingSheet ||
                viewType == ViewType.Schedule ||
                viewType == ViewType.Report ||
                viewType == ViewType.ProjectBrowser ||
                viewType == ViewType.SystemBrowser ||
                viewType == ViewType.Legend)
            {
                message = "Flow direction annotations can only be placed in 2D model views.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool IsValidAnnotationSymbol(FamilySymbol symbol)
        {
            if (symbol == null)
                return false;

            Category category = symbol.Category;
            if (category == null || category.CategoryType != CategoryType.Annotation)
                return false;

            if (category.IsTagCategory)
                return false;

            Family family = symbol.Family;
            if (family == null)
                return false;

            return family.FamilyPlacementType == FamilyPlacementType.ViewBased;
        }
    }
}
