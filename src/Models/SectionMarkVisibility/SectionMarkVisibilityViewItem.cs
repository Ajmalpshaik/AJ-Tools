// ==================================================
// Tool Name    : Section Mark Visibility
// Purpose      : Holds data for view items displayed in the multiple view selection UI.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// ==================================================

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
