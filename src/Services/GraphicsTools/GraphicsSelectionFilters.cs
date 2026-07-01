#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsSelectionFilters.cs
 * Purpose       : Provides model-category selection filtering for the Graphics tools.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Revit selection candidates.
 * Output        : Accepted model-category elements only.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Rejects references (face/edge picks) so only whole model elements are selectable.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed model-category selection filtering for release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
