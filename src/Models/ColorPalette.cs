// Tool Name: Filter Pro - Color Palette
// Description: Provides a consistent vivid color palette for filter graphics and utilities.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System
using Autodesk.Revit.DB;
using System;

namespace AJTools.Models
{
    internal static class ColorPalette
    {
        private static readonly Random _rand = new Random();

        private static readonly Color[] Palette =
        {
            // Highly distinct / vivid palette (warm, jewel, neon mix)
            new Color(255, 0, 54),    // Neon Red
            new Color(0, 255, 102),   // Neon Green
            new Color(0, 191, 255),   // Deep Sky Blue
            new Color(255, 215, 0),   // Gold
            new Color(186, 85, 211),  // Medium Orchid
            new Color(255, 69, 0),    // Orange Red
            new Color(0, 255, 255),   // Aqua
            new Color(255, 20, 147),  // Deep Pink
            new Color(50, 205, 50),   // Lime Green
            new Color(138, 43, 226),  // Blue Violet
            new Color(255, 140, 0),   // Dark Orange
            new Color(64, 224, 208),  // Turquoise
            new Color(255, 99, 71),   // Tomato
            new Color(72, 61, 139),   // Dark Slate Blue
            new Color(0, 206, 209),   // Dark Turquoise
            new Color(199, 21, 133),  // Medium Violet Red
        };

        public static Color GetColorFor(ElementId id)
        {
            if (id == null || id.IntegerValue == 0)
                return Palette[0];
            int index = Math.Abs(id.IntegerValue);
            return Palette[index % Palette.Length];
        }

        public static Color GetRandomColor()
        {
            int idx = _rand.Next(Palette.Length);
            return Palette[idx];
        }
    }
}
