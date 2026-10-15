#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 4, View Plane Maths
 * File Name     : SmartTagViewPlane.cs
 * Purpose       : Step 4: turns 3D model points and boxes into flat positions on the view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-15
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Utils, the other Smart MEP Tags files
 *
 * Input         : Model points and boxes, view directions.
 * Output        : Flat view-plane points and boxes.
 *
 * Notes         :
 * - Part of the Smart MEP Tags restructure (suite 1.58.0): the code that used to sit in
 *   SmartMepTagService.cs and SmartTagPlacementEngine.cs is split one job per file, in step
 *   folders. Every routine was moved as-is - tagging results are unchanged.
 * - The numbers that tune the algorithm live in SmartTagTuning.cs.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Split out of the old Smart MEP Tag service / placement engine.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using AJTools.Models.SmartTag;
using AJTools.Services.CreateTags;
using AJTools.Services.LeaderLogic;
using AJTools.Services.TagClash;
using AJTools.Services.ViewCrop;
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagCandidatePositions;
using static AJTools.Services.SmartTag.SmartTagCreator;
using static AJTools.Services.SmartTag.SmartTagElementCollector;
using static AJTools.Services.SmartTag.SmartTagElementFilters;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;
using static AJTools.Services.SmartTag.SmartTagPlacementEngine;
using static AJTools.Services.SmartTag.SmartTagPositionFinder;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagTypeSelector;
using static AJTools.Services.SmartTag.SmartTagViewCheck;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Turns 3D model points and boxes into flat positions on the view.
    /// </summary>
    internal static class SmartTagViewPlane
    {
        internal static XYZ GetViewRightDirection(View view)
        {
            try
            {
                if (view != null && view.RightDirection != null && view.RightDirection.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
                    return view.RightDirection.Normalize();
            }
            catch (Exception) { }

            return XYZ.BasisX;
        }

        internal static XYZ GetViewUpDirection(View view)
        {
            try
            {
                if (view != null && view.UpDirection != null && view.UpDirection.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
                    return view.UpDirection.Normalize();
            }
            catch (Exception) { }

            return XYZ.BasisY;
        }

        // Thin wrapper kept so the eight call sites below read unchanged; the maths itself now lives
        // once, in TagViewGeometry.
        internal static UV ProjectToViewPlane(XYZ point, XYZ viewRight, XYZ viewUp)
        {
            return TagViewGeometry.ProjectToView(point, viewRight, viewUp);
        }

        internal static AnnotationBox CreateCandidateBoxInViewPlane(
            XYZ candidatePos,
            double estimatedTagWidth,
            double estimatedTagHeight,
            XYZ viewRight,
            XYZ viewUp)
        {
            UV uv = ProjectToViewPlane(candidatePos, viewRight, viewUp);
            double halfW = estimatedTagWidth * 0.5;
            double halfH = estimatedTagHeight * 0.5;
            return new AnnotationBox(
                uv.U - halfW, uv.V - halfH,
                uv.U + halfW, uv.V + halfH);
        }

        // Thin wrapper kept so the eight call sites below read unchanged; the eight-corner walk (one of
        // six copies of it that were in this project) now lives once, in TagViewGeometry.
        internal static AnnotationBox ConvertBoundingBoxToViewPlane(BoundingBoxXYZ bb, XYZ viewRight, XYZ viewUp)
        {
            return TagViewGeometry.ToViewBox(bb, viewRight, viewUp);
        }

#if DEBUG
        internal static bool TryRunGeometryRegressionChecks(out string error)
        {
            error = null;
            const double tol = 1e-6;

            try
            {
                var rotated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 1, 0),
                    Transform = Transform.CreateRotationAtPoint(XYZ.BasisZ, 0.5 * Math.PI, XYZ.Zero)
                };

                AnnotationBox rotatedBox = ConvertBoundingBoxToViewPlane(rotated, XYZ.BasisX, XYZ.BasisY);
                if (rotatedBox == null)
                {
                    error = "ConvertBoundingBoxToViewPlane returned null for rotated bbox.";
                    return false;
                }

                if (!NearlyEqual(rotatedBox.MinX, -1, tol)
                    || !NearlyEqual(rotatedBox.MaxX, 0, tol)
                    || !NearlyEqual(rotatedBox.MinY, 0, tol)
                    || !NearlyEqual(rotatedBox.MaxY, 2, tol))
                {
                    error = "Rotated bbox projection mismatch.";
                    return false;
                }

                var translated = new BoundingBoxXYZ
                {
                    Min = new XYZ(0, 0, 0),
                    Max = new XYZ(2, 1, 0),
                    Transform = Transform.CreateTranslation(new XYZ(3, -2, 0))
                };

                AnnotationBox translatedBox = ConvertBoundingBoxToViewPlane(translated, XYZ.BasisX, XYZ.BasisY);
                if (translatedBox == null)
                {
                    error = "ConvertBoundingBoxToViewPlane returned null for translated bbox.";
                    return false;
                }

                if (!NearlyEqual(translatedBox.MinX, 3, tol)
                    || !NearlyEqual(translatedBox.MaxX, 5, tol)
                    || !NearlyEqual(translatedBox.MinY, -2, tol)
                    || !NearlyEqual(translatedBox.MaxY, -1, tol))
                {
                    error = "Translated bbox projection mismatch.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool NearlyEqual(double a, double b, double tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }
#endif
    }
}
