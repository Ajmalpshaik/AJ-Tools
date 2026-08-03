// ==================================================
// Tool Name    : Purge Unplaced Views
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-05-11
// Last Updated : 2026-07-28
// Target       : Revit 2020 - latest
// Framework    : .NET Framework 4.7.2 (2020) / .NET 8 (2025-2026) / .NET 10 (2027+)
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
//                v1.1.0 (2026-07-28) - Added Schedules/Legends/DraftingViews modes (same "not placed on any
//                sheet" shape as the original ThreeDViews/SectionViews, extended to 3 more view kinds).
//                ThreeDViews/SectionViews behaviour unchanged.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

namespace AJTools.Models.Purge
{
    public enum UnplacedViewPurgeMode
    {
        ThreeDViews = 0,
        SectionViews = 1,
        Schedules = 2,
        Legends = 3,
        DraftingViews = 4
    }

    internal static class UnplacedViewPurgeModeExtensions
    {
        public static string GetToolTitle(this UnplacedViewPurgeMode mode)
        {
            switch (mode)
            {
                case UnplacedViewPurgeMode.SectionViews:
                    return "Purge Unplaced Sections";
                case UnplacedViewPurgeMode.Schedules:
                    return "Purge Unplaced Schedules";
                case UnplacedViewPurgeMode.Legends:
                    return "Purge Unplaced Legends";
                case UnplacedViewPurgeMode.DraftingViews:
                    return "Purge Unplaced Drafting Views";
                default:
                    return "Purge Unplaced 3D Views";
            }
        }

        public static string GetViewKind(this UnplacedViewPurgeMode mode)
        {
            switch (mode)
            {
                case UnplacedViewPurgeMode.SectionViews:
                    return "Section";
                case UnplacedViewPurgeMode.Schedules:
                    return "Schedule";
                case UnplacedViewPurgeMode.Legends:
                    return "Legend";
                case UnplacedViewPurgeMode.DraftingViews:
                    return "Drafting View";
                default:
                    return "3D View";
            }
        }

        public static string GetViewKindPlural(this UnplacedViewPurgeMode mode)
        {
            switch (mode)
            {
                case UnplacedViewPurgeMode.SectionViews:
                    return "Sections";
                case UnplacedViewPurgeMode.Schedules:
                    return "Schedules";
                case UnplacedViewPurgeMode.Legends:
                    return "Legends";
                case UnplacedViewPurgeMode.DraftingViews:
                    return "Drafting Views";
                default:
                    return "3D Views";
            }
        }

        public static string GetDescription(this UnplacedViewPurgeMode mode)
        {
            switch (mode)
            {
                case UnplacedViewPurgeMode.SectionViews:
                    return "Review unplaced section views before deleting selected safe candidates from the active project.";
                case UnplacedViewPurgeMode.Schedules:
                    return "Review schedules that are not placed on any sheet before deleting selected safe candidates from the active project.";
                case UnplacedViewPurgeMode.Legends:
                    return "Review legends that are not placed on any sheet before deleting selected safe candidates from the active project.";
                case UnplacedViewPurgeMode.DraftingViews:
                    return "Review drafting views that are not placed on any sheet before deleting selected safe candidates from the active project.";
                default:
                    return "Review unplaced 3D views before deleting selected safe candidates from the active project.";
            }
        }

        public static string GetTransactionName(this UnplacedViewPurgeMode mode)
        {
            return mode.GetToolTitle();
        }
    }
}
