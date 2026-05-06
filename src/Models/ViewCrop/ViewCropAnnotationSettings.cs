// ==================================================
// Tool Name    : View Crop
// Purpose      : Stores annotation crop offset settings.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-11
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
