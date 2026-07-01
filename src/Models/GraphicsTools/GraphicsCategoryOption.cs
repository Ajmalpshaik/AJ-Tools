#region Metadata
/*
 * Tool Name     : Apply Graphics
 * File Name     : GraphicsCategoryOption.cs
 * Purpose       : Represents a selectable category option for the Apply Graphics category list.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-05-07
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Revit category identity and display name.
 * Output        : A selectable category item for graphics override workflows.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Used only for active-view categories that support Revit graphics overrides.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed category selection model for the updated Apply Graphics UI.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    internal sealed class GraphicsCategoryOption
    {
        public GraphicsCategoryOption(ElementId categoryId, string displayName, bool isSelected)
        {
            CategoryId = categoryId ?? ElementId.InvalidElementId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
            IsSelected = isSelected;
        }

        public ElementId CategoryId { get; }

        public string DisplayName { get; }

        public bool IsSelected { get; set; }
    }
}
