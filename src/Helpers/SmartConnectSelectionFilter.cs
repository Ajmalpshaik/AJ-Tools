// Tool Name: Smart Connect - Selection Filter
// Description: Restricts selection to supported and category-matching MEP elements.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-25
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI.Selection

using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace AJTools.Utils
{
    /// <summary>
    /// Selection filter for Smart Connect supported categories.
    /// </summary>
    internal sealed class SmartConnectSelectionFilter : ISelectionFilter
    {
        private static readonly HashSet<BuiltInCategory> SupportedCategories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_CableTray
        };

        private readonly HashSet<BuiltInCategory> _allowedCategories;

        public SmartConnectSelectionFilter()
        {
            _allowedCategories = new HashSet<BuiltInCategory>(SupportedCategories);
        }

        public SmartConnectSelectionFilter(BuiltInCategory category)
        {
            _allowedCategories = new HashSet<BuiltInCategory>();
            if (SupportedCategories.Contains(category))
            {
                _allowedCategories.Add(category);
            }
        }

        public bool AllowElement(Element elem)
        {
            if (!TryGetSupportedCategory(elem, out BuiltInCategory category))
            {
                return false;
            }

            return _allowedCategories.Contains(category);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }

        public static bool TryGetSupportedCategory(Element element, out BuiltInCategory category)
        {
            category = BuiltInCategory.INVALID;

            Category elementCategory = element?.Category;
            if (elementCategory == null)
            {
                return false;
            }

            int categoryId = AJTools.Utils.ElementIdHelper.GetIntegerValue(elementCategory.Id);
            if (!System.Enum.IsDefined(typeof(BuiltInCategory), categoryId))
            {
                return false;
            }

            BuiltInCategory builtInCategory = (BuiltInCategory)categoryId;
            if (!SupportedCategories.Contains(builtInCategory))
            {
                return false;
            }

            category = builtInCategory;
            return true;
        }

        public static string GetCategoryDisplayName(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_PipeCurves:
                    return "Pipe";
                case BuiltInCategory.OST_DuctCurves:
                    return "Duct";
                case BuiltInCategory.OST_CableTray:
                    return "Cable Tray";
                default:
                    return "Unsupported";
            }
        }
    }
}
