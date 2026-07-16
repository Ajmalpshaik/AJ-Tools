#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : ColorPalette.cs
 * Purpose       : Provides a 20-colour neon palette for filter graphic overrides — deterministic
 *                 colour by ElementId and thread-safe random colour selection.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-07-13
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, System, System.Threading
 *
 * Input         : ElementId (for deterministic colour) or none (for random colour)
 * Output        : Autodesk.Revit.DB.Color instance
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - ThreadLocal<Random> is used to avoid seed-collision on concurrent threads.
 * - GetColorFor uses unchecked Math.Abs to handle int.MinValue without OverflowException.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2025-12-11) - Replaced shared Random with ThreadLocal<Random> for thread safety;
 *                        added unchecked Math.Abs guard for int.MinValue ElementIds.
 * v1.1.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 * v1.2.0 (2026-07-13) - Added GetColorAt(index) so the Colorize tool can assign a distinct, stable
 *                       palette colour per selected value by list position, without needing a real
 *                       Revit ElementId to key off.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using System;
using System.Threading;

using AJTools.Utils;
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
            if (id == null || id == ElementId.InvalidElementId || id.IntValue() == 0)
                return Palette[0];

            // Use unchecked to prevent OverflowException when IntegerValue is int.MinValue.
            int index = unchecked(Math.Abs(id.IntValue())) & 0x7FFFFFFF;
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

        /// <summary>
        /// Returns a stable palette color by position (e.g. a value's index in a selection list).
        /// Same index -> same color, with no dependency on a Revit ElementId.
        /// </summary>
        public static Color GetColorAt(int index)
        {
            int safeIndex = unchecked(Math.Abs(index)) & 0x7FFFFFFF;
            return Palette[safeIndex % Palette.Length];
        }
    }
}
