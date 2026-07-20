#region Metadata
/*
 * Tool Name     : Smart Selection
 * File Name     : CmdSmartSelection.cs
 * Purpose       : Pick one reference element, then window-select, crossing-select, or click-select more
 *                 elements in the view - only elements sharing the reference element's category are
 *                 added to the selection; everything else caught in the box is skipped automatically.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-20
 * Last Updated  : 2026-07-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (SmartSelectionFilter, ValidationHelper, DialogHelper,
 *                 ElementIdIntegerComparer)
 *
 * Input         : Active document, Selection scope - one reference element, then any number of
 *                 window/crossing/click picks in one pick session (Finish/Enter to end, Esc to cancel).
 * Output        : The matched elements (reference + picked) are left as the active Revit selection.
 *                 No model changes.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Read-only tool (TransactionMode.ReadOnly) - only the active selection changes, never the model.
 * - Category-level match only: any element sharing the reference element's category is allowed (e.g.
 *   pick one duct, then window-select adds every duct in the box, skipping pipes/walls/tags/etc.).
 * - Esc on the reference pick cancels silently (Result.Cancelled, no error shown).
 * - Esc during the follow-up pick stage (after a reference element is already picked) falls back to
 *   leaving just the reference element selected, instead of losing the pick entirely.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-20) - Initial release.
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
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Entry command for Smart Selection: pick a reference element, then window/crossing/click-select
    /// more elements of the same category only.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class CmdSmartSelection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uiDocument, out message))
            {
                DialogHelper.ShowError("Smart Selection", message);
                return Result.Cancelled;
            }

            Document document = uiDocument.Document;

            try
            {
                Element referenceElement = PickReferenceElement(uiDocument);
                if (referenceElement == null)
                {
                    return Result.Cancelled;
                }

                Category referenceCategory = referenceElement.Category;
                if (referenceCategory == null)
                {
                    DialogHelper.ShowError(
                        "Smart Selection",
                        "That element has no category to match against. Please pick a normal model element.");
                    return Result.Cancelled;
                }

                HashSet<ElementId> matchedIds = new HashSet<ElementId>(new ElementIdIntegerComparer())
                {
                    referenceElement.Id
                };

                try
                {
                    IList<Reference> pickedReferences = uiDocument.Selection.PickObjects(
                        ObjectType.Element,
                        new SmartSelectionFilter(referenceCategory.Id),
                        $"Smart Selection: window, crossing, or click-select more {referenceCategory.Name} " +
                        "elements. Click Finish (or press Enter) when done.");

                    foreach (Reference pickedReference in pickedReferences)
                    {
                        Element pickedElement = document.GetElement(pickedReference);
                        if (pickedElement != null)
                        {
                            matchedIds.Add(pickedElement.Id);
                        }
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // Esc during the follow-up stage - keep just the reference element selected.
                }

                uiDocument.Selection.SetElementIds(matchedIds);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError("Smart Selection", "An unexpected error occurred:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static Element PickReferenceElement(UIDocument uiDocument)
        {
            try
            {
                Reference reference = uiDocument.Selection.PickObject(
                    ObjectType.Element,
                    new SmartSelectionFilter(),
                    "Smart Selection: pick the reference element. Press Esc to cancel.");

                return uiDocument.Document.GetElement(reference);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }
    }
}
