// Tool Name: Filter Pro - Color Palette
// Description: Provides a highly distinct vivid color palette for filter graphics and utilities.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System

using Autodesk.Revit.DB;
using System;
using System.Threading;

namespace AJTools.Models
{
    internal static class ColorPalette
    {
        // Thread-safe Random instance to avoid issues with concurrent access
        private static readonly ThreadLocal<Random> _rand = 
            new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        // Ultra-distinct 20-color neon palette (no similar colors)
        private static readonly Color[] Palette =
        {
            new Color(255,   0,   0), // Pure Red
            new Color(  0, 255,   0), // Pure Green
            new Color(  0,   0, 255), // Pure Blue
            new Color(255, 255,   0), // Yellow
            new Color(255,   0, 255), // Magenta
            new Color(  0, 255, 255), // Cyan

            new Color(255, 128,   0), // Vivid Orange
            new Color(128,   0, 255), // Vivid Purple
            new Color(255,   0, 128), // Hot Pink
            new Color(  0, 128, 255), // Electric Blue

            new Color(  0, 255, 128), // Spring Green
            new Color(128, 255,   0), // Chartreuse
            new Color(128,   0,   0), // Deep Burgundy
            new Color(  0, 128,   0), // Deep Forest Green
            new Color(  0,   0, 128), // Deep Navy

            new Color(255,  64,   0), // Neon Orange-Red
            new Color(255,   0,  64), // Neon Pink-Red
            new Color( 64,   0, 255), // Ultramarine
            new Color(  0,  64, 255), // Royal Blue
            new Color( 64, 255,   0), // Lime Neon
        };

        /// <summary>
        /// Returns a consistent color for an ElementId.
        /// Same ID → same color.
        /// </summary>
        public static Color GetColorFor(ElementId id)
        {
            if (id == null || id.IntegerValue == 0)
                return Palette[0];

            int index = Math.Abs(id.IntegerValue);
            return Palette[index % Palette.Length];
        }

        /// <summary>
        /// Returns a random color from the palette.
        /// </summary>
        public static Color GetRandomColor()
        {
            int idx = _rand.Value.Next(Palette.Length);
            return Palette[idx];
        }
    }
}
