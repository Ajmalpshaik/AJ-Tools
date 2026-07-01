#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropEnums.cs
 * Purpose       : Defines View Crop source and result state enumerations.
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
 * Dependencies  : None
 *
 * Input         : None
 * Output        : Enum types ViewCropExtentSource and ViewCropResultState.
 *
 * Notes         :
 * - Result states: Updated / Skipped / Failed.
 * - Extent sources: ActiveViewElements / AllModelElements.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Metadata refresh and version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
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
