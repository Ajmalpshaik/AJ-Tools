// ==================================================
// Tool Name    : View Crop
// Purpose      : Stores crop margin and element inclusion options for View Crop.
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
