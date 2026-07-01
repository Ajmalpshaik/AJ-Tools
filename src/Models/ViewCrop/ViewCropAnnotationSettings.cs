#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropAnnotationSettings.cs
 * Purpose       : Stores annotation crop offset (mm) used by the annotation crop tool.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-11
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None (uses Constants for unit conversion)
 *
 * Input         : User-set offset value via WPF options window.
 * Output        : ViewCropAnnotationSettings instance, plus OffsetInternal in Revit feet.
 *
 * Notes         :
 * - Default offset: 100 mm.
 * - Clone() returns a fully independent copy.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Metadata refresh and version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using AJTools.Utils;

namespace AJTools.Models.ViewCrop
{
    /// <summary>
    /// Contains configurable options for the Annotation Crop tool.
    /// </summary>
    internal sealed class ViewCropAnnotationSettings
    {
        internal const double DefaultOffsetMm = 100.0;

        internal double OffsetMm { get; set; } = DefaultOffsetMm;

        internal double OffsetInternal => OffsetMm * Constants.MM_TO_FEET;

        internal ViewCropAnnotationSettings Clone()
        {
            return new ViewCropAnnotationSettings
            {
                OffsetMm = OffsetMm
            };
        }
    }
}
