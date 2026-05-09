// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Represents a selectable category option for the Apply Graphics UI.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-05-07
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit category identity and display name.
// Output       : A selectable category item for graphics override workflows.
// Notes        : Used only for active-view categories that support Revit graphics overrides.
// Changelog    : v1.4.4 - Reviewed category selection model for the updated Apply Graphics UI.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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
