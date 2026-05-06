// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Represents graphics color values with By View support.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit color values or RGB values.
// Output       : Safe Revit color value or By View state.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.1.0 - Cleaned Graphics Tools command flow, shared validation/transaction handling, and metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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
