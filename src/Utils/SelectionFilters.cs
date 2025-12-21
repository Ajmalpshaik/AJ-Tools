// Tool Name: Selection Filters
// Description: Reusable selection filters for common Revit element types.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace AJTools.Utils
{
    /// <summary>
    /// Selection filter for Grid and Level datum elements.
    /// </summary>
    internal class DatumSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Grid || elem is Level;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Selection filter for Dimension elements.
    /// </summary>
    internal class DimensionSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Dimension;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Selection filter for text notes.
    /// </summary>
    internal class TextNoteSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is TextNote;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Selection filter for MEP curve elements (pipes, ducts, cable trays, conduits, flex curves).
    /// </summary>
    internal class MepSelectionFilter : ISelectionFilter
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
            Category cat = elem?.Category;
            if (cat == null)
                return false;

            return _categories.Contains((BuiltInCategory)cat.Id.IntegerValue);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Selection filter for duct and pipe curves only.
    /// </summary>
    internal class DuctPipeSelectionFilter : ISelectionFilter
    {
        private readonly HashSet<BuiltInCategory> _categories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_DuctCurves
        };

        public bool AllowElement(Element elem)
        {
            Category cat = elem?.Category;
            if (cat == null)
                return false;

            return _categories.Contains((BuiltInCategory)cat.Id.IntegerValue);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
