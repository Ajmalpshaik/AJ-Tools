#region Metadata
/*
 * Tool Name     : Apply Graphics
 * File Name     : GraphicsOverrideInput.cs
 * Purpose       : Holds Apply Graphics settings input before building Revit OverrideGraphicSettings.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : WPF graphics settings selections.
 * Output        : Structured graphics override input model.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Plain data container; no Revit API calls beyond ElementId/enum references.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed override input model for split apply actions and persisted UI settings.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    internal enum GraphicsApplyMode
    {
        SelectedElements = 0,
        Categories = 1
    }

    /// <summary>
    /// Input model used to build a complete OverrideGraphicSettings package.
    /// </summary>
    internal sealed class GraphicsOverrideInput
    {
        public GraphicsOverrideInput()
        {
            ProjectionLineColor = GraphicsColorValue.ByView();
            ProjectionLinePatternId = ElementId.InvalidElementId;
            ProjectionLineWeight = OverrideGraphicSettings.InvalidPenNumber;

            SurfaceForegroundPatternId = ElementId.InvalidElementId;
            SurfaceForegroundPatternColor = GraphicsColorValue.ByView();
            SurfaceBackgroundPatternId = ElementId.InvalidElementId;
            SurfaceBackgroundPatternColor = GraphicsColorValue.ByView();

            Transparency = 0;

            CutLineColor = GraphicsColorValue.ByView();
            CutLinePatternId = ElementId.InvalidElementId;
            CutLineWeight = OverrideGraphicSettings.InvalidPenNumber;

            CutForegroundPatternId = ElementId.InvalidElementId;
            CutForegroundPatternColor = GraphicsColorValue.ByView();
            CutBackgroundPatternId = ElementId.InvalidElementId;
            CutBackgroundPatternColor = GraphicsColorValue.ByView();

            Halftone = false;
            ApplyMode = GraphicsApplyMode.SelectedElements;
            UseProjectionSurfaceSettingsForCut = false;
        }

        public GraphicsColorValue ProjectionLineColor { get; set; }

        public ElementId ProjectionLinePatternId { get; set; }

        public int ProjectionLineWeight { get; set; }

        public ElementId SurfaceForegroundPatternId { get; set; }

        public GraphicsColorValue SurfaceForegroundPatternColor { get; set; }

        public ElementId SurfaceBackgroundPatternId { get; set; }

        public GraphicsColorValue SurfaceBackgroundPatternColor { get; set; }

        public int Transparency { get; set; }

        public GraphicsColorValue CutLineColor { get; set; }

        public ElementId CutLinePatternId { get; set; }

        public int CutLineWeight { get; set; }

        public ElementId CutForegroundPatternId { get; set; }

        public GraphicsColorValue CutForegroundPatternColor { get; set; }

        public ElementId CutBackgroundPatternId { get; set; }

        public GraphicsColorValue CutBackgroundPatternColor { get; set; }

        public bool Halftone { get; set; }

        public GraphicsApplyMode ApplyMode { get; set; }

        public bool UseProjectionSurfaceSettingsForCut { get; set; }
    }
}
