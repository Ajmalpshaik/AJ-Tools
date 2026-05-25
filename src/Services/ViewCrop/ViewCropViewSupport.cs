// ==================================================
// Tool Name    : View Crop
// Purpose      : Validates supported View Crop target view types and labels.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-08
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.1 - Standardized metadata after production cleanup.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using Autodesk.Revit.DB;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Centralizes support checks for views targeted by View Crop tools.
    /// </summary>
    internal static class ViewCropViewSupport
    {
        internal static bool IsSupportedViewType(ViewType viewType)
        {
            return viewType == ViewType.FloorPlan
                || viewType == ViewType.CeilingPlan
                || viewType == ViewType.EngineeringPlan
                || viewType == ViewType.AreaPlan;
        }

        internal static bool TryValidateType(View view, out string reason)
        {
            if (view == null)
            {
                reason = "View not found.";
                return false;
            }

            if (view.IsTemplate)
            {
                reason = "View templates are not supported.";
                return false;
            }

            if (!IsSupportedViewType(view.ViewType))
            {
                reason = $"Unsupported view type: {view.ViewType}.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal static string ToFriendlyTypeName(View view)
        {
            if (view == null)
                return string.Empty;

            return ToFriendlyTypeName(view.ViewType);
        }

        internal static string ToFriendlyTypeName(ViewType viewType)
        {
            switch (viewType)
            {
                case ViewType.FloorPlan:
                    return "Floor Plan";
                case ViewType.CeilingPlan:
                    return "Ceiling Plan";
                case ViewType.EngineeringPlan:
                    return "Engineering Plan";
                case ViewType.AreaPlan:
                    return "Area Plan";
                case ViewType.Section:
                    return "Section";
                case ViewType.Elevation:
                    return "Elevation";
                case ViewType.Detail:
                    return "Detail/Callout";
                case ViewType.ThreeD:
                    return "3D View";
                case ViewType.DrawingSheet:
                    return "Sheet";
                case ViewType.Legend:
                    return "Legend";
                case ViewType.Schedule:
                    return "Schedule";
                case ViewType.DraftingView:
                    return "Drafting View";
                default:
                    return viewType.ToString();
            }
        }
    }
}
