#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsColorValue.cs
 * Purpose       : Represents a graphics color value that supports an explicit "By View" state.
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
 * Input         : Revit color values or RGB values.
 * Output        : Safe Revit color value or By View state.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - By View maps to Color.InvalidColorValue when converted back to a Revit color.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed color value model for the updated Apply Graphics UI.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// UI color value wrapper that supports explicit "By View" state.
    /// </summary>
    internal sealed class GraphicsColorValue
    {
        private GraphicsColorValue(bool isByView, byte red, byte green, byte blue)
        {
            IsByView = isByView;
            Red = red;
            Green = green;
            Blue = blue;
        }

        public bool IsByView { get; }

        public byte Red { get; }

        public byte Green { get; }

        public byte Blue { get; }

        public static GraphicsColorValue ByView()
        {
            return new GraphicsColorValue(true, 0, 0, 0);
        }

        public static GraphicsColorValue FromRgb(byte red, byte green, byte blue)
        {
            return new GraphicsColorValue(false, red, green, blue);
        }

        public static GraphicsColorValue FromRevitColor(Color color)
        {
            if (color == null || !color.IsValid)
            {
                return ByView();
            }

            return FromRgb(color.Red, color.Green, color.Blue);
        }

        public Color ToRevitColorOrInvalid()
        {
            return IsByView
                ? Color.InvalidColorValue
                : new Color(Red, Green, Blue);
        }
    }
}
