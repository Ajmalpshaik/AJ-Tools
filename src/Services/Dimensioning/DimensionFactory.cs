#region Metadata
/*
 * Tool Name     : Dimensioning (shared)
 * File Name     : DimensionFactory.cs
 * Purpose       : The one place a dimension is created. Resolves the dimension type by name, isolates
 *                 each creation so a single bad reference cannot abort a whole batch, and lists the
 *                 linear dimension types for the settings windows.
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
 * Dependencies  : Autodesk Revit API, AJTools.Utils (Constants)
 *
 * Input         : A view, a dimension line, a ReferenceArray, and an optional dimension type name.
 * Output        : A Dimension, or false plus the reason.
 *
 * Notes         :
 * - Both NewDimension overloads were verified present in the real installed RevitAPI.dll for Revit
 *   2020 and 2024: (View, Line, ReferenceArray) and (View, Line, ReferenceArray, DimensionType). The
 *   typed overload is used whenever a type is resolved, so the style is right at creation rather than
 *   patched afterwards.
 * - Dimension types are filtered to DimensionStyleType.Linear (and LinearFixed): the tools only place
 *   linear dimensions, and offering an angular or radial type would guarantee a failure.
 * - MUST be called inside an already-open transaction. This class deliberately does not open one, so
 *   a whole batch can live in a single undo step.
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

namespace AJTools.Services.Dimensioning
{
    /// <summary>
    /// Creates dimensions and resolves dimension types.
    /// </summary>
    internal static class DimensionFactory
    {
        /// <summary>
        /// Every linear dimension type in the document, ordered by name. These are the only types a
        /// linear dimension can legally use.
        /// </summary>
        internal static IList<DimensionType> GetLinearDimensionTypes(Document doc)
        {
            List<DimensionType> linear = new List<DimensionType>();
            List<DimensionType> all = new List<DimensionType>();

            if (doc == null)
                return linear;

            try
            {
                foreach (DimensionType type in new FilteredElementCollector(doc)
                    .OfClass(typeof(DimensionType))
                    .Cast<DimensionType>())
                {
                    if (type == null || string.IsNullOrWhiteSpace(SafeName(type)))
                        continue;

                    all.Add(type);

                    DimensionStyleType style;
                    try
                    {
                        style = type.StyleType;
                    }
                    catch
                    {
                        continue;
                    }

                    if (style == DimensionStyleType.Linear || style == DimensionStyleType.LinearFixed)
                        linear.Add(type);
                }
            }
            catch
            {
                return linear;
            }

            // If StyleType told us nothing useful, offering an empty list would look like the tool is
            // broken. Fall back to every dimension type and let Revit reject an unusable one.
            List<DimensionType> chosen = linear.Count > 0 ? linear : all;

            return chosen
                .OrderBy(t => SafeName(t), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>The names of every linear dimension type, for a settings dropdown.</summary>
        internal static IList<string> GetLinearDimensionTypeNames(Document doc)
        {
            return GetLinearDimensionTypes(doc)
                .Select(SafeName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Finds a linear dimension type by name. Returns null when the name is blank or no longer
        /// exists in this project, in which case the caller falls back to the document default.
        /// </summary>
        internal static DimensionType ResolveDimensionType(Document doc, string typeName)
        {
            if (doc == null || string.IsNullOrWhiteSpace(typeName))
                return null;

            return GetLinearDimensionTypes(doc)
                .FirstOrDefault(t => string.Equals(SafeName(t), typeName.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// Creates one dimension. Must run inside an open transaction.
        /// Returns false with a reason rather than throwing, so one bad reference cannot abort a batch.
        /// </summary>
        internal static bool TryCreate(
            Document doc,
            View view,
            Line dimensionLine,
            ReferenceArray references,
            DimensionType dimensionType,
            out Dimension dimension,
            out string reason)
        {
            dimension = null;
            reason = string.Empty;

            if (doc == null || view == null)
            {
                reason = "No document or view.";
                return false;
            }

            if (dimensionLine == null)
            {
                reason = "The dimension line could not be built.";
                return false;
            }

            if (references == null || references.Size < 2)
            {
                reason = "Fewer than two references were found.";
                return false;
            }

            try
            {
                dimension = dimensionType == null
                    ? doc.Create.NewDimension(view, dimensionLine, references)
                    : doc.Create.NewDimension(view, dimensionLine, references, dimensionType);
            }
            catch (Exception ex)
            {
                dimension = null;
                reason = Shorten(ex.Message);
                return false;
            }

            if (dimension == null)
            {
                reason = "Revit refused the references for this dimension.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds a bounded dimension line between two points, rejecting anything too short for Revit
        /// to accept.
        /// </summary>
        internal static bool TryCreateLine(XYZ start, XYZ end, out Line line, out string reason)
        {
            line = null;
            reason = string.Empty;

            if (start == null || end == null)
            {
                reason = "The dimension line could not be built.";
                return false;
            }

            if (start.DistanceTo(end) <= Utils.Constants.MIN_DISTANCE_TOLERANCE)
            {
                reason = "The dimension line came out too short.";
                return false;
            }

            try
            {
                line = Line.CreateBound(start, end);
            }
            catch (Exception ex)
            {
                line = null;
                reason = Shorten(ex.Message);
                return false;
            }

            return true;
        }

        private static string SafeName(DimensionType type)
        {
            if (type == null)
                return string.Empty;

            try
            {
                return type.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Shorten(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Revit rejected the dimension.";

            string firstLine = message.Split('\n')[0].Trim();
            return firstLine.Length > 160 ? firstLine.Substring(0, 157) + "..." : firstLine;
        }
    }
}
