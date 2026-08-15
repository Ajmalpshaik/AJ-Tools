#region Metadata
/*
 * Tool Name     : Automatic Dimension (Grids / Levels)
 * File Name     : AutoDatumDimensionSettings.cs
 * Purpose       : User settings for the Automatic Dimension toolset - row layout, offsets, which side
 *                 of the view rows sit on, dimension type per row, datum filtering, and whether grids
 *                 and levels may be read from loaded Revit links.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : AJTools.Models.Dimensioning (DimensionEnums)
 *
 * Input         : Deserialized from JSON in the user's AppData folder, or CreateDefault().
 * Output        : Plain settings object consumed by AutoDimensionService.
 *
 * Notes         :
 * - Every length is stored in MILLIMETRES and converted to Revit internal feet at the point of use.
 *   Offsets are per-paper-millimetre: the service multiplies them by the view scale.
 * - Normalize() clamps every value into a usable range so a hand-edited or partial JSON file can
 *   never produce an unusable configuration.
 * - Targets Revit 2020 through latest.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;

namespace AJTools.Models.Dimensioning
{
    /// <summary>
    /// Settings for the Automatic Dimension (grids / levels) toolset.
    /// </summary>
    public class AutoDatumDimensionSettings
    {
        internal const double MinOffsetMm = 0.0;
        internal const double MaxOffsetMm = 200.0;

        // ---- Rows -------------------------------------------------------------------------

        /// <summary>Create the chain that steps through every datum in turn.</summary>
        public bool CreateIndividualRow { get; set; }

        /// <summary>Create the single first-to-last overall dimension.</summary>
        public bool CreateOverallRow { get; set; }

        /// <summary>
        /// Suppress the overall row when only two datums exist - otherwise it duplicates the
        /// individual chain exactly, offset by one row.
        /// </summary>
        public bool SkipOverallWhenTwoDatums { get; set; }

        // ---- Placement --------------------------------------------------------------------

        /// <summary>Which side(s) of the drawing the rows are placed on.</summary>
        public DimensionPlacementSide Side { get; set; }

        /// <summary>Paper millimetres from the drawing to the first dimension row (multiplied by view scale).</summary>
        public double FirstRowOffsetMm { get; set; }

        /// <summary>Paper millimetres between one dimension row and the next (multiplied by view scale).</summary>
        public double RowSpacingMm { get; set; }

        // ---- Dimension types --------------------------------------------------------------

        /// <summary>Dimension type name for the individual chain. Empty means the document default.</summary>
        public string IndividualDimensionTypeName { get; set; }

        /// <summary>Dimension type name for the overall row. Empty means the document default.</summary>
        public string OverallDimensionTypeName { get; set; }

        // ---- What to dimension ------------------------------------------------------------

        /// <summary>Read grids from the open project, from loaded links, from both, or not at all.</summary>
        public DimensionModelScope GridScope { get; set; }

        /// <summary>Read levels from the open project, from loaded links, from both, or not at all.</summary>
        public DimensionModelScope LevelScope { get; set; }

        /// <summary>Only dimension levels whose type is a building story.</summary>
        public bool StoryLevelsOnly { get; set; }

        /// <summary>
        /// Semicolon-separated name fragments; any grid or level whose name contains one of them is skipped.
        /// Empty means nothing is excluded.
        /// </summary>
        public string ExcludeNameFragments { get; set; }

        // ---- Safety / behaviour -----------------------------------------------------------

        /// <summary>Skip datums that an existing dimension in this view already references.</summary>
        public bool SkipAlreadyDimensioned { get; set; }

        /// <summary>
        /// Require the view's crop box to be on. When false the tool falls back to the extents of the
        /// datums themselves, so it still runs in an uncropped view.
        /// </summary>
        public bool RequireCropBox { get; set; }

        /// <summary>
        /// Show the report when something was skipped or could not be dimensioned. A run that finished
        /// everything asked of it never shows a message - the dimensions are the visible result.
        /// </summary>
        public bool ShowReport { get; set; }

        /// <summary>
        /// Creates the shipped defaults. These reproduce the pre-1.45.0 layout (8 mm to the first row,
        /// 6 mm between rows, one chain plus one overall, one side only) with the new safety options on.
        /// </summary>
        public static AutoDatumDimensionSettings CreateDefault()
        {
            return new AutoDatumDimensionSettings
            {
                CreateIndividualRow = true,
                CreateOverallRow = true,
                SkipOverallWhenTwoDatums = true,

                Side = DimensionPlacementSide.Primary,
                FirstRowOffsetMm = 8.0,
                RowSpacingMm = 6.0,

                IndividualDimensionTypeName = string.Empty,
                OverallDimensionTypeName = string.Empty,

                GridScope = DimensionModelScope.CurrentModel,
                LevelScope = DimensionModelScope.CurrentModel,
                StoryLevelsOnly = false,
                ExcludeNameFragments = string.Empty,

                SkipAlreadyDimensioned = true,
                RequireCropBox = false,
                ShowReport = true
            };
        }

        /// <summary>
        /// Clamps every value into a usable range. Safe to call on a partially populated object.
        /// </summary>
        public void Normalize()
        {
            if (!CreateIndividualRow && !CreateOverallRow)
                CreateIndividualRow = true;

            if (!Enum.IsDefined(typeof(DimensionPlacementSide), Side))
                Side = DimensionPlacementSide.Primary;

            if (!Enum.IsDefined(typeof(DimensionModelScope), GridScope))
                GridScope = DimensionModelScope.CurrentModel;

            if (!Enum.IsDefined(typeof(DimensionModelScope), LevelScope))
                LevelScope = DimensionModelScope.CurrentModel;

            if (GridScope == DimensionModelScope.None && LevelScope == DimensionModelScope.None)
                GridScope = DimensionModelScope.CurrentModel;

            FirstRowOffsetMm = Clamp(FirstRowOffsetMm, MinOffsetMm, MaxOffsetMm, 8.0);
            RowSpacingMm = Clamp(RowSpacingMm, MinOffsetMm, MaxOffsetMm, 6.0);

            IndividualDimensionTypeName = IndividualDimensionTypeName ?? string.Empty;
            OverallDimensionTypeName = OverallDimensionTypeName ?? string.Empty;
            ExcludeNameFragments = ExcludeNameFragments ?? string.Empty;
        }

        /// <summary>Returns an independent copy.</summary>
        public AutoDatumDimensionSettings Clone()
        {
            return new AutoDatumDimensionSettings
            {
                CreateIndividualRow = CreateIndividualRow,
                CreateOverallRow = CreateOverallRow,
                SkipOverallWhenTwoDatums = SkipOverallWhenTwoDatums,
                Side = Side,
                FirstRowOffsetMm = FirstRowOffsetMm,
                RowSpacingMm = RowSpacingMm,
                IndividualDimensionTypeName = IndividualDimensionTypeName,
                OverallDimensionTypeName = OverallDimensionTypeName,
                GridScope = GridScope,
                LevelScope = LevelScope,
                StoryLevelsOnly = StoryLevelsOnly,
                ExcludeNameFragments = ExcludeNameFragments,
                SkipAlreadyDimensioned = SkipAlreadyDimensioned,
                RequireCropBox = RequireCropBox,
                ShowReport = ShowReport
            };
        }

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return fallback;
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
