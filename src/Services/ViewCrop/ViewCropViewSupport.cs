#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropViewSupport.cs
 * Purpose       : Validates supported View Crop target view types and provides friendly type labels.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : View instance or ViewType enum.
 * Output        : Boolean support check + display string.
 *
 * Notes         :
 * - Supported: Floor Plan, Ceiling Plan, Engineering Plan, Area Plan.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Metadata refresh and version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
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
