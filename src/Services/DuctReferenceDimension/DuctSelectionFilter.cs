// Tool Name: Duct Selection Filter
// Description: Selection helper for the duct reference dimension workflow.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

using AJTools.Utils;
namespace AJTools.Services.DuctReferenceDimension
{
    /// <summary>
    /// Allows active-model picks while keeping duct validation explicit in the command workflow.
    /// This lets the tool warn and continue when the user clicks a non-duct element.
    /// </summary>
    internal sealed class DuctSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem != null;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }

        public static bool IsDuct(Element element)
        {
            Category category = element?.Category;
            if (category == null)
                return false;

            return category.Id.IntValue() == (int)BuiltInCategory.OST_DuctCurves;
        }
    }
}
