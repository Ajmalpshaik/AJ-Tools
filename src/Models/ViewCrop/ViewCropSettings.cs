// Tool Name: View Crop Settings
// Description: Stores user options for crop calculation and application.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

using AJTools.Utils;

namespace AJTools.Models.ViewCrop
{
    /// <summary>
    /// Contains configurable options for the View Crop tools.
    /// </summary>
    internal sealed class ViewCropSettings
    {
        internal const double DefaultMarginMm = 300.0;

        internal double MarginMm { get; set; } = DefaultMarginMm;

        internal bool IncludeRevitLinks { get; set; } = true;

        internal bool IgnoreHiddenCategories { get; set; } = true;

        internal bool RectangularCropOnly { get; set; } = true;

        internal bool IncludeDatums { get; set; } = false;

        internal double MarginInternal => MarginMm * Constants.MM_TO_FEET;

        internal ViewCropSettings Clone()
        {
            return new ViewCropSettings
            {
                MarginMm = MarginMm,
                IncludeRevitLinks = IncludeRevitLinks,
                IgnoreHiddenCategories = IgnoreHiddenCategories,
                RectangularCropOnly = RectangularCropOnly,
                IncludeDatums = IncludeDatums
            };
        }
    }
}
