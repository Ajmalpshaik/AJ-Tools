// ==================================================
// Tool Name    : Purge Unplaced Views
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-11
// Last Updated : 2026-05-11
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

namespace AJTools.Models.Purge
{
    public enum UnplacedViewPurgeMode
    {
        ThreeDViews = 0,
        SectionViews = 1
    }

    internal static class UnplacedViewPurgeModeExtensions
    {
        public static string GetToolTitle(this UnplacedViewPurgeMode mode)
        {
            return mode == UnplacedViewPurgeMode.SectionViews
                ? "Purge Unplaced Sections"
                : "Purge Unplaced 3D Views";
        }

        public static string GetViewKind(this UnplacedViewPurgeMode mode)
        {
            return mode == UnplacedViewPurgeMode.SectionViews
                ? "Section"
                : "3D View";
        }

        public static string GetViewKindPlural(this UnplacedViewPurgeMode mode)
        {
            return mode == UnplacedViewPurgeMode.SectionViews
                ? "Sections"
                : "3D Views";
        }

        public static string GetDescription(this UnplacedViewPurgeMode mode)
        {
            return mode == UnplacedViewPurgeMode.SectionViews
                ? "Review unplaced section views before deleting selected safe candidates from the active project."
                : "Review unplaced 3D views before deleting selected safe candidates from the active project.";
        }

        public static string GetTransactionName(this UnplacedViewPurgeMode mode)
        {
            return mode == UnplacedViewPurgeMode.SectionViews
                ? "Purge Unplaced Sections"
                : "Purge Unplaced 3D Views";
        }
    }
}
