// Tool Name: View Crop Enums
// Description: Enumerations for view crop commands and processing outcomes.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

namespace AJTools.Models.ViewCrop
{
    /// <summary>
    /// Defines the element source used to calculate crop extents.
    /// </summary>
    internal enum ViewCropExtentSource
    {
        ActiveViewElements = 0,
        AllModelElements = 1
    }

    /// <summary>
    /// Defines the result state for a processed view.
    /// </summary>
    internal enum ViewCropResultState
    {
        Updated = 0,
        Skipped = 1,
        Failed = 2
    }
}
