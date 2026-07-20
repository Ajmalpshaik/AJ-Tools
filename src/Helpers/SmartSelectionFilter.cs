#region Metadata
/*
 * Tool Name     : Smart Selection - Selection Filter
 * File Name     : SmartSelectionFilter.cs
 * Purpose       : Restricts a pick session to elements sharing one reference element's category -
 *                 used by Smart Selection for its window/crossing/click selection stage.
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
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Optionally, a reference element's Category ElementId.
 * Output        : true/false per element, evaluated by Revit during PickObject/PickObjects.
 *
 * Notes         :
 * - No-arg constructor allows any element with a valid Category - used for the first (reference
 *   element) pick, before the target category is known.
 * - Category-id constructor allows only elements whose Category matches - used for the follow-up
 *   window/crossing/click stage, once the reference element's category is known.
 * - Category ids are compared via ElementIdExtensions.IntValue(), not ElementId ==, matching the
 *   rest of AJ-Tools (safe across the Revit 2024+ ElementId 32-bit -> 64-bit widening).
 * - AllowReference always false - this tool selects whole elements only, not faces/edges/points.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-20) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace AJTools.Utils
{
    /// <summary>
    /// Selection filter for Smart Selection: allows any categorized element (reference pick), or
    /// only elements matching one reference category (follow-up pick), depending on the constructor.
    /// </summary>
    internal sealed class SmartSelectionFilter : ISelectionFilter
    {
        private readonly int? _categoryId;

        /// <summary>
        /// No filtering beyond "has a category" - used for the reference element pick.
        /// </summary>
        public SmartSelectionFilter()
        {
            _categoryId = null;
        }

        /// <summary>
        /// Filters to elements whose Category matches <paramref name="categoryId"/> - used for the
        /// follow-up window/crossing/click selection stage.
        /// </summary>
        public SmartSelectionFilter(ElementId categoryId)
        {
            _categoryId = categoryId?.IntValue();
        }

        public bool AllowElement(Element elem)
        {
            Category category = elem?.Category;
            if (category == null)
            {
                return false;
            }

            return _categoryId == null || category.Id.IntValue() == _categoryId;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
