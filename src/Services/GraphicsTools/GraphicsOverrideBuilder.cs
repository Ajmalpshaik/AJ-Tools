// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Builds Revit OverrideGraphicSettings from Apply Graphics input values.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-03-30
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Graphics override input model.
// Output       : Revit OverrideGraphicSettings object.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.4 - Reviewed override building for persisted settings, split apply actions, and Revit 2020 compatibility.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;

namespace AJTools.Services.GraphicsTools
{
    /// <summary>
    /// Creates complete OverrideGraphicSettings objects from UI input.
    /// </summary>
    internal static class GraphicsOverrideBuilder
    {
        public static OverrideGraphicSettings Build(GraphicsOverrideInput input)
        {
            var safeInput = input ?? new GraphicsOverrideInput();
            var settings = new OverrideGraphicSettings();

            GraphicsColorValue cutLineColor = safeInput.UseProjectionSurfaceSettingsForCut
                ? safeInput.ProjectionLineColor
                : safeInput.CutLineColor;
            ElementId cutLinePatternId = safeInput.UseProjectionSurfaceSettingsForCut
                ? safeInput.ProjectionLinePatternId
                : safeInput.CutLinePatternId;
            int cutLineWeight = safeInput.UseProjectionSurfaceSettingsForCut
                ? safeInput.ProjectionLineWeight
                : safeInput.CutLineWeight;
            ElementId cutForegroundPatternId = safeInput.UseProjectionSurfaceSettingsForCut
                ? safeInput.SurfaceForegroundPatternId
                : safeInput.CutForegroundPatternId;
            GraphicsColorValue cutForegroundColor = safeInput.UseProjectionSurfaceSettingsForCut
                ? safeInput.SurfaceForegroundPatternColor
                : safeInput.CutForegroundPatternColor;
            ElementId cutBackgroundPatternId = safeInput.UseProjectionSurfaceSettingsForCut
                ? safeInput.SurfaceBackgroundPatternId
                : safeInput.CutBackgroundPatternId;
            GraphicsColorValue cutBackgroundColor = safeInput.UseProjectionSurfaceSettingsForCut
                ? safeInput.SurfaceBackgroundPatternColor
                : safeInput.CutBackgroundPatternColor;

            settings.SetProjectionLineColor(ToColorOrInvalid(safeInput.ProjectionLineColor));
            settings.SetProjectionLinePatternId(ToIdOrInvalid(safeInput.ProjectionLinePatternId));
            settings.SetProjectionLineWeight(NormalizeLineWeight(safeInput.ProjectionLineWeight));

            ElementId surfaceForegroundPatternId = ToIdOrInvalid(safeInput.SurfaceForegroundPatternId);
            settings.SetSurfaceForegroundPatternId(surfaceForegroundPatternId);
            settings.SetSurfaceForegroundPatternVisible(IsValidId(surfaceForegroundPatternId));
            settings.SetSurfaceForegroundPatternColor(ToColorOrInvalid(safeInput.SurfaceForegroundPatternColor));

            ElementId surfaceBackgroundPatternId = ToIdOrInvalid(safeInput.SurfaceBackgroundPatternId);
            settings.SetSurfaceBackgroundPatternId(surfaceBackgroundPatternId);
            settings.SetSurfaceBackgroundPatternVisible(IsValidId(surfaceBackgroundPatternId));
            settings.SetSurfaceBackgroundPatternColor(ToColorOrInvalid(safeInput.SurfaceBackgroundPatternColor));

            settings.SetSurfaceTransparency(ClampTransparency(safeInput.Transparency));

            settings.SetCutLineColor(ToColorOrInvalid(cutLineColor));
            settings.SetCutLinePatternId(ToIdOrInvalid(cutLinePatternId));
            settings.SetCutLineWeight(NormalizeLineWeight(cutLineWeight));

            ElementId resolvedCutForegroundPatternId = ToIdOrInvalid(cutForegroundPatternId);
            settings.SetCutForegroundPatternId(resolvedCutForegroundPatternId);
            settings.SetCutForegroundPatternVisible(IsValidId(resolvedCutForegroundPatternId));
            settings.SetCutForegroundPatternColor(ToColorOrInvalid(cutForegroundColor));

            ElementId resolvedCutBackgroundPatternId = ToIdOrInvalid(cutBackgroundPatternId);
            settings.SetCutBackgroundPatternId(resolvedCutBackgroundPatternId);
            settings.SetCutBackgroundPatternVisible(IsValidId(resolvedCutBackgroundPatternId));
            settings.SetCutBackgroundPatternColor(ToColorOrInvalid(cutBackgroundColor));

            settings.SetHalftone(safeInput.Halftone);

            return settings;
        }

        public static OverrideGraphicSettings Clone(OverrideGraphicSettings source)
        {
            return source == null
                ? new OverrideGraphicSettings()
                : new OverrideGraphicSettings(source);
        }

        private static Color ToColorOrInvalid(GraphicsColorValue colorValue)
        {
            return colorValue == null
                ? Color.InvalidColorValue
                : colorValue.ToRevitColorOrInvalid();
        }

        private static ElementId ToIdOrInvalid(ElementId id)
        {
            return id ?? ElementId.InvalidElementId;
        }

        private static bool IsValidId(ElementId id)
        {
            return id != null && id != ElementId.InvalidElementId;
        }

        private static int NormalizeLineWeight(int lineWeight)
        {
            if (lineWeight == OverrideGraphicSettings.InvalidPenNumber)
            {
                return OverrideGraphicSettings.InvalidPenNumber;
            }

            if (lineWeight < 1)
            {
                return OverrideGraphicSettings.InvalidPenNumber;
            }

            if (lineWeight > 16)
            {
                return 16;
            }

            return lineWeight;
        }

        private static int ClampTransparency(int transparency)
        {
            if (transparency < 0)
            {
                return 0;
            }

            if (transparency > 100)
            {
                return 100;
            }

            return transparency;
        }
    }
}
