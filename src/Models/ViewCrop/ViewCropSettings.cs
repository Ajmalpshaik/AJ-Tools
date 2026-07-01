#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropSettings.cs
 * Purpose       : Stores crop margin and element-inclusion options for View Crop.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None (uses Constants for unit conversion)
 *
 * Input         : User-set values via WPF options window.
 * Output        : ViewCropSettings instance, plus MarginInternal in Revit feet.
 *
 * Notes         :
 * - Default margin: 300 mm.
 * - Default annotation offset: 100 mm.
 * - Clone() returns a fully independent copy.
 *
 * Changelog     :
 * v1.2.0 (2026-06-28) - Added ExtentSource property so the mode (visible/all-model) is stored per document.
 * v1.1.0 (2026-06-27) - Metadata refresh and version coverage notes.
 * v1.0.2 (2026-05-24) - Premium settings memory and coordination models tracking.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
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

        internal bool ApplyAnnotationCrop { get; set; } = false;

        internal double AnnotationOffsetMm { get; set; } = 100.0;

        internal bool ShowDiagnostics { get; set; } = false;

        internal bool IncludeCoordinationModels { get; set; } = false;

        internal ViewCropExtentSource ExtentSource { get; set; } = ViewCropExtentSource.AllModelElements;

        internal double MarginInternal => MarginMm * Constants.MM_TO_FEET;

        internal ViewCropSettings Clone()
        {
            return new ViewCropSettings
            {
                MarginMm = MarginMm,
                IncludeRevitLinks = IncludeRevitLinks,
                IgnoreHiddenCategories = IgnoreHiddenCategories,
                RectangularCropOnly = RectangularCropOnly,
                IncludeDatums = IncludeDatums,
                ApplyAnnotationCrop = ApplyAnnotationCrop,
                AnnotationOffsetMm = AnnotationOffsetMm,
                ShowDiagnostics = ShowDiagnostics,
                IncludeCoordinationModels = IncludeCoordinationModels,
                ExtentSource = ExtentSource
            };
        }
    }
}
