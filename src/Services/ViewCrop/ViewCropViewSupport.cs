// Tool Name: View Crop View Support
// Description: Validation and labeling helpers for supported view types.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

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
                || viewType == ViewType.AreaPlan
                || viewType == ViewType.Section
                || viewType == ViewType.Elevation
                || viewType == ViewType.Detail;
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
