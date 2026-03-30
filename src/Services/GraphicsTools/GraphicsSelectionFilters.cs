using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace AJTools.Services.GraphicsTools
{
    /// <summary>
    /// Allows only elements with model categories.
    /// </summary>
    internal sealed class ModelCategorySelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            Category category = elem?.Category;
            return category != null && category.CategoryType == CategoryType.Model;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
