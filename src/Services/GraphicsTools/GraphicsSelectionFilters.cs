// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Provides model-category selection filtering for graphics tools.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit selection candidates.
// Output       : Accepted model-category elements only.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.1.0 - Cleaned Graphics Tools command flow, shared validation/transaction handling, and metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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
