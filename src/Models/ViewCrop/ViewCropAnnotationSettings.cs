// Tool Name: View Crop Annotation Settings
// Description: Stores user options for annotation crop offsets.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-11
// Revit Version: 2020

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
