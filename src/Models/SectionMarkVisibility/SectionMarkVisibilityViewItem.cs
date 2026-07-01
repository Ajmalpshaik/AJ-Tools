#region Metadata
/*
 * Tool Name     : Section Mark Visibility
 * File Name     : SectionMarkVisibilityViewItem.cs
 * Purpose       : Data item for a plan view shown in the multiple-view selection list.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-05-24
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : —
 * Output        : —
 *
 * Changelog     :
 * v1.0.0 (2026-05-24) - Initial release.
 * v1.2.0 (2026-06-30) - Cleanup pass: metadata block.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;

namespace AJTools.Models.SectionMarkVisibility
{
    /// <summary>
    /// Represents a plan view item in the view selection grid/list.
    /// </summary>
    internal sealed class SectionMarkVisibilityViewItem
    {
        public ElementId ViewId { get; set; }
        public string ViewName { get; set; }
        public string ViewTypeName { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string GroupName { get; set; }
        public bool CanSelect { get; set; } = true;
        public string StatusText { get; set; } = "Supported";
        public bool IsSelected { get; set; } = false;
    }
}
