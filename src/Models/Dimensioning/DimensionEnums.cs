#region Metadata
/*
 * Tool Name     : Dimensioning (shared)
 * File Name     : DimensionEnums.cs
 * Purpose       : Shared public enums for the Automatic Dimension (grids/levels) and Auto MEP Dimension
 *                 toolsets - which side of the view a dimension row sits on, which model a reference may
 *                 come from, and what kind of thing a reference points at.
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
 * Dependencies  : none
 *
 * Input         : n/a - type declarations only.
 * Output        : n/a.
 *
 * Notes         :
 * - These enums are deliberately public: a public WPF settings window constructor cannot take an
 *   internal enum parameter (CS0051), and both settings windows expose them.
 * - Targets Revit 2020 through latest.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.Dimensioning
{
    /// <summary>
    /// Which side of the drawing a dimension row is placed on.
    /// "Both" places a matching row on each opposite side.
    /// </summary>
    public enum DimensionPlacementSide
    {
        /// <summary>Above (grids in plan/section) or left (levels).</summary>
        Primary,

        /// <summary>Below (grids in plan/section) or right (levels).</summary>
        Secondary,

        /// <summary>One row on each side.</summary>
        Both
    }

    /// <summary>
    /// Where a reference element may be read from. The two model sources are deliberately
    /// independent so the current model and the linked models can be enabled separately.
    /// </summary>
    public enum DimensionModelScope
    {
        /// <summary>Do not use this target at all.</summary>
        None,

        /// <summary>Only elements that live in the open project.</summary>
        CurrentModel,

        /// <summary>Only elements that live inside a loaded Revit link.</summary>
        LinkedModels,

        /// <summary>Both the open project and every loaded Revit link.</summary>
        CurrentAndLinked
    }

    /// <summary>
    /// The kind of element a dimension reference points at. Drives face-vs-datum reference
    /// collection and the ordering priority when two references share a coordinate.
    /// </summary>
    public enum DimensionTargetKind
    {
        /// <summary>
        /// A run of a DIFFERENT service to the one being measured - a pipe or cable tray picked up while
        /// dimensioning a duct. Value 0 is fixed: saved settings files store these as integers.
        /// </summary>
        MepRun = 0,

        Wall = 1,
        StructuralColumn = 2,
        StructuralBeam = 3,
        ArchitecturalColumn = 4,
        Floor = 5,

        /// <summary>A grid line - referenced as a datum, not as a face.</summary>
        Grid = 6,

        /// <summary>A level - referenced as a datum, not as a face.</summary>
        Level = 7,

        /// <summary>
        /// A run of the SAME service as the one being measured - duct to duct, pipe to pipe. Kept
        /// separate from MepRun because wanting a duct measured against nearby pipework is a different
        /// decision from wanting it measured against the next duct along.
        /// </summary>
        SameServiceRun = 8
    }

    /// <summary>
    /// How the chain of references between the reference face and the measured run is written out.
    /// </summary>
    public enum DimensionChainStyle
    {
        /// <summary>One dimension element carrying every reference, so the string reads as one run with segments.</summary>
        SingleString,

        /// <summary>A separate two-reference dimension element per gap (the pre-1.45.0 behaviour).</summary>
        SeparateSegments

        // A third style, RowPerRun (value 2), briefly stacked one row per run. Ajmal did not want it:
        // rather than piling a new row on top, the tool should REPLACE the dimension it made earlier so
        // the result stays a single clean string. It was removed in 1.48.0. The value is deliberately
        // NOT reused - a settings file still holding 2 fails the Enum.IsDefined guard in Normalize()
        // and falls back to SingleString, which is exactly the intended migration.
    }
}
