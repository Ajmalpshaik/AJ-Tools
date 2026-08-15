#region Metadata
/*
 * Tool Name     : Auto MEP Dimension
 * File Name     : MepDimensionSettings.cs
 * Purpose       : User settings for the Auto MEP Dimension toolset - which service categories get
 *                 measured, which reference targets are used and whether each of those may come from
 *                 the open project, from loaded Revit links, or both, plus run filters and layout.
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
 * Dependencies  : Autodesk Revit API (BuiltInCategory), AJTools.Models.Dimensioning (DimensionEnums)
 *
 * Input         : Deserialized from JSON in the user's AppData folder, or CreateDefault().
 * Output        : Plain settings object consumed by MepReferenceDimensionService.
 *
 * Notes         :
 * - Reference targets carry a DimensionModelScope EACH, not one global "use links" switch. Walls in
 *   the open project and walls in a linked architectural model are separate choices on purpose.
 * - Every length is stored in MILLIMETRES. Paper-millimetre values (padding) are multiplied by the
 *   view scale at the point of use; model-millimetre values (minimum run length, search band) are not.
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
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AJTools.Models.Dimensioning
{
    /// <summary>
    /// One reference target row: what it is, and which model(s) it may be read from.
    /// </summary>
    public class MepDimensionTargetOption
    {
        /// <summary>The kind of element this row configures.</summary>
        public DimensionTargetKind Kind { get; set; }

        /// <summary>Current model, linked models, both, or off.</summary>
        public DimensionModelScope Scope { get; set; }

        public MepDimensionTargetOption() { }

        public MepDimensionTargetOption(DimensionTargetKind kind, DimensionModelScope scope)
        {
            Kind = kind;
            Scope = scope;
        }
    }

    /// <summary>
    /// Settings for the Auto MEP Dimension toolset.
    /// </summary>
    public class MepDimensionSettings
    {
        // ---- What gets measured ------------------------------------------------------------

        /// <summary>Service categories whose straight runs are measured. Stored as the enum's integer value.</summary>
        public List<int> MeasuredCategoryIds { get; set; }

        /// <summary>Skip runs shorter than this, in model millimetres. 0 disables the check.</summary>
        public double MinimumRunLengthMm { get; set; }

        /// <summary>Skip runs that go straight up or down - they read as a point in plan.</summary>
        public bool SkipVerticalRuns { get; set; }

        // ---- What it measures to -----------------------------------------------------------

        /// <summary>Reference targets, each with its own current/linked scope.</summary>
        public List<MepDimensionTargetOption> Targets { get; set; }

        // ---- Layout ------------------------------------------------------------------------

        /// <summary>One dimension carrying the whole chain, or one dimension per gap.</summary>
        public DimensionChainStyle ChainStyle { get; set; }

        /// <summary>Measure to the nearest reference on one side, or produce a chain on each side.</summary>
        public bool DimensionBothSides { get; set; }

        /// <summary>Include the run's own width as a segment in the chain.</summary>
        public bool IncludeRunWidth { get; set; }

        /// <summary>
        /// Paper millimetres between one row and the next when each run gets its own row
        /// (RowPerRun). Multiplied by the view scale, so it looks the same at any scale.
        /// </summary>
        public double RowSpacingMm { get; set; }

        /// <summary>
        /// NO LONGER USED. Kept so an existing settings file still loads without complaint.
        /// It promised an overhang past the last witness line, which cannot be driven from code:
        /// NewDimension reads the supplied line for position and direction only and then draws the
        /// dimension between the references itself. That extension is a Dimension Type property in Revit.
        /// </summary>
        public double PaddingMm { get; set; }

        /// <summary>Dimension type name. Empty means the document default.</summary>
        public string DimensionTypeName { get; set; }

        // ---- Search / matching -------------------------------------------------------------

        /// <summary>
        /// Model millimetres either side of the measuring line within which a face still counts as
        /// crossing it. The pre-1.45.0 hard-coded value was 150 mm.
        /// </summary>
        public double SearchBandMm { get; set; }

        // ---- Safety / behaviour ------------------------------------------------------------

        /// <summary>Skip runs an existing dimension in this view already references.</summary>
        public bool SkipAlreadyDimensioned { get; set; }

        /// <summary>
        /// Show the report when something was skipped or could not be dimensioned. A run that finished
        /// everything asked of it never shows a message - the dimensions are the visible result.
        /// </summary>
        public bool ShowReport { get; set; }

        /// <summary>
        /// The full set of service categories the tool can measure, in display order.
        /// </summary>
        public static IReadOnlyList<BuiltInCategory> SupportedMeasuredCategories { get; } = new[]
        {
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_Conduit
        };

        /// <summary>
        /// The full set of reference targets the tool can measure to, in display order.
        /// </summary>
        public static IReadOnlyList<DimensionTargetKind> SupportedTargetKinds { get; } = new[]
        {
            DimensionTargetKind.Wall,
            DimensionTargetKind.StructuralColumn,
            DimensionTargetKind.StructuralBeam,
            DimensionTargetKind.ArchitecturalColumn,
            DimensionTargetKind.Floor,
            DimensionTargetKind.Grid,
            DimensionTargetKind.Level,
            DimensionTargetKind.MepRun,
            DimensionTargetKind.SameServiceRun
        };

        /// <summary>
        /// Creates the shipped defaults: ducts only measured (the pre-1.45.0 behaviour), measured to
        /// walls, structural columns and beams in the open project, links off until asked for.
        /// </summary>
        public static MepDimensionSettings CreateDefault()
        {
            return new MepDimensionSettings
            {
                MeasuredCategoryIds = new List<int>
                {
                    (int)BuiltInCategory.OST_DuctCurves
                },
                MinimumRunLengthMm = 1000.0,
                SkipVerticalRuns = true,

                Targets = new List<MepDimensionTargetOption>
                {
                    new MepDimensionTargetOption(DimensionTargetKind.Wall, DimensionModelScope.CurrentModel),
                    new MepDimensionTargetOption(DimensionTargetKind.StructuralColumn, DimensionModelScope.CurrentModel),
                    new MepDimensionTargetOption(DimensionTargetKind.StructuralBeam, DimensionModelScope.CurrentModel),
                    new MepDimensionTargetOption(DimensionTargetKind.ArchitecturalColumn, DimensionModelScope.None),
                    new MepDimensionTargetOption(DimensionTargetKind.Floor, DimensionModelScope.None),
                    new MepDimensionTargetOption(DimensionTargetKind.Grid, DimensionModelScope.None),
                    new MepDimensionTargetOption(DimensionTargetKind.Level, DimensionModelScope.None),
                    new MepDimensionTargetOption(DimensionTargetKind.MepRun, DimensionModelScope.CurrentModel),
                    new MepDimensionTargetOption(DimensionTargetKind.SameServiceRun, DimensionModelScope.None)
                },

                ChainStyle = DimensionChainStyle.SingleString,
                DimensionBothSides = false,
                IncludeRunWidth = false,
                RowSpacingMm = 8.0,
                PaddingMm = 6.0,
                DimensionTypeName = string.Empty,

                SearchBandMm = 150.0,

                SkipAlreadyDimensioned = true,
                ShowReport = true
            };
        }

        /// <summary>
        /// Clamps every value into a usable range and guarantees one row per supported target kind.
        /// Safe to call on a partially populated object.
        /// </summary>
        public void Normalize()
        {
            if (MeasuredCategoryIds == null)
                MeasuredCategoryIds = new List<int>();

            MeasuredCategoryIds = MeasuredCategoryIds
                .Where(id => SupportedMeasuredCategories.Any(c => (int)c == id))
                .Distinct()
                .ToList();

            if (MeasuredCategoryIds.Count == 0)
                MeasuredCategoryIds.Add((int)BuiltInCategory.OST_DuctCurves);

            if (Targets == null)
                Targets = new List<MepDimensionTargetOption>();

            // Guarantee exactly one row per supported kind, preserving whatever was saved.
            List<MepDimensionTargetOption> rebuilt = new List<MepDimensionTargetOption>();
            foreach (DimensionTargetKind kind in SupportedTargetKinds)
            {
                MepDimensionTargetOption existing = Targets.FirstOrDefault(t => t != null && t.Kind == kind);
                DimensionModelScope scope = existing == null ? DimensionModelScope.None : existing.Scope;

                if (!Enum.IsDefined(typeof(DimensionModelScope), scope))
                    scope = DimensionModelScope.None;

                rebuilt.Add(new MepDimensionTargetOption(kind, scope));
            }
            Targets = rebuilt;

            // At least one datum/solid target must be usable or nothing can ever be measured to.
            if (Targets.All(t => t.Scope == DimensionModelScope.None ||
                                 t.Kind == DimensionTargetKind.MepRun ||
                                 t.Kind == DimensionTargetKind.SameServiceRun))
            {
                MepDimensionTargetOption wall = Targets.First(t => t.Kind == DimensionTargetKind.Wall);
                wall.Scope = DimensionModelScope.CurrentModel;
            }

            if (!Enum.IsDefined(typeof(DimensionChainStyle), ChainStyle))
                ChainStyle = DimensionChainStyle.SingleString;

            MinimumRunLengthMm = Clamp(MinimumRunLengthMm, 0.0, 100000.0, 1000.0);
            SearchBandMm = Clamp(SearchBandMm, 1.0, 5000.0, 150.0);
            PaddingMm = Clamp(PaddingMm, 0.0, 200.0, 6.0);
            // A settings file written before this option existed has no value at all, which arrives as 0.
            // Clamping 0 to the 1 mm floor would leave the stacked rows 1 mm apart on paper - they would
            // print on top of each other. Absent means "use the default", not "use the minimum".
            RowSpacingMm = RowSpacingMm <= 0.0
                ? 8.0
                : Clamp(RowSpacingMm, 1.0, 200.0, 8.0);

            DimensionTypeName = DimensionTypeName ?? string.Empty;
        }

        /// <summary>True when the given target kind may be read from the open project.</summary>
        public bool UsesCurrentModel(DimensionTargetKind kind)
        {
            DimensionModelScope scope = GetScope(kind);
            return scope == DimensionModelScope.CurrentModel || scope == DimensionModelScope.CurrentAndLinked;
        }

        /// <summary>True when the given target kind may be read from loaded Revit links.</summary>
        public bool UsesLinkedModels(DimensionTargetKind kind)
        {
            DimensionModelScope scope = GetScope(kind);
            return scope == DimensionModelScope.LinkedModels || scope == DimensionModelScope.CurrentAndLinked;
        }

        /// <summary>True when any target at all wants linked geometry - lets the collector skip link work entirely.</summary>
        public bool AnyLinkedTargets()
        {
            return Targets != null && Targets.Any(t =>
                t != null &&
                (t.Scope == DimensionModelScope.LinkedModels || t.Scope == DimensionModelScope.CurrentAndLinked));
        }

        /// <summary>The configured scope for a target kind, or None when it is not configured.</summary>
        public DimensionModelScope GetScope(DimensionTargetKind kind)
        {
            MepDimensionTargetOption option = Targets?.FirstOrDefault(t => t != null && t.Kind == kind);
            return option == null ? DimensionModelScope.None : option.Scope;
        }

        /// <summary>The measured service categories as Revit categories.</summary>
        public IList<BuiltInCategory> GetMeasuredCategories()
        {
            List<BuiltInCategory> result = new List<BuiltInCategory>();
            if (MeasuredCategoryIds == null)
                return result;

            foreach (BuiltInCategory category in SupportedMeasuredCategories)
            {
                if (MeasuredCategoryIds.Contains((int)category))
                    result.Add(category);
            }

            return result;
        }

        /// <summary>Returns an independent copy.</summary>
        public MepDimensionSettings Clone()
        {
            return new MepDimensionSettings
            {
                MeasuredCategoryIds = MeasuredCategoryIds == null
                    ? new List<int>()
                    : new List<int>(MeasuredCategoryIds),
                MinimumRunLengthMm = MinimumRunLengthMm,
                SkipVerticalRuns = SkipVerticalRuns,
                Targets = Targets == null
                    ? new List<MepDimensionTargetOption>()
                    : Targets.Select(t => new MepDimensionTargetOption(t.Kind, t.Scope)).ToList(),
                ChainStyle = ChainStyle,
                DimensionBothSides = DimensionBothSides,
                IncludeRunWidth = IncludeRunWidth,
                RowSpacingMm = RowSpacingMm,
                PaddingMm = PaddingMm,
                DimensionTypeName = DimensionTypeName,
                SearchBandMm = SearchBandMm,
                SkipAlreadyDimensioned = SkipAlreadyDimensioned,
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
