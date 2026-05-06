// ==================================================
// Tool Name    : View Crop
// Purpose      : Defines View Crop source and result state enumerations.
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
