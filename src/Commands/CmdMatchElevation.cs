#region Metadata
/*
 * Tool Name     : Match MEP Element Elevation
 * File Name     : CmdMatchElevation.cs
 * Purpose       : Matches center, top, or bottom elevation from one picked source MEP element to one or
 *                 more picked target elements, moving each target vertically only (X/Y unchanged).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-03-20
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (MepSelectionFilter, DialogHelper)
 *
 * Input         : Active document - one source MEP element, then target MEP elements picked one-by-one (Esc to finish).
 * Output        : Target elements shifted in Z so their center/top/bottom elevation matches the source (single undo step).
 *
 * Notes         :
 * - Targets Revit 2020 through latest; only the location curve is translated in Z, so curve type
 *   (line, arc, spline) and slope are preserved.
 * - The whole pick session is wrapped in one TransactionGroup and assimilated, so a single Ctrl+Z
 *   reverses every matched target.
 * - Esc during a pick is a normal cancel (handled silently); a target that cannot be moved (e.g. pinned
 *   or owned by another user) is skipped without aborting the session.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.0 (2026-04-11) - Added top/bottom match modes sharing one base command.
 * v1.2.0 (2026-07-01) - Refactor/audit: full metadata block; whole pick session now assimilated into a
 *                       single undo step; per-target skip on failure. Match behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    public enum ElevationMatchMode
    {
        Center,
        Top,
        Bottom
    }

    /// <summary>
    /// Matches center elevation from a source MEP element to selected targets.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchElevation : MatchElevationCommandBase
    {
        protected override ElevationMatchMode MatchMode => ElevationMatchMode.Center;
        protected override string ModeLabel => "Center";
    }

    /// <summary>
    /// Matches top elevation from a source MEP element to selected targets.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchElevationTop : MatchElevationCommandBase
    {
        protected override ElevationMatchMode MatchMode => ElevationMatchMode.Top;
        protected override string ModeLabel => "Top";
    }

    /// <summary>
    /// Matches bottom elevation from a source MEP element to selected targets.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchElevationBottom : MatchElevationCommandBase
    {
        protected override ElevationMatchMode MatchMode => ElevationMatchMode.Bottom;
        protected override string ModeLabel => "Bottom";
    }

    /// <summary>
    /// Shared implementation for elevation matching command variants.
    /// </summary>
    public abstract class MatchElevationCommandBase : IExternalCommand
    {
        private const double ElevationTolerance = 1e-9;

        protected abstract ElevationMatchMode MatchMode { get; }
        protected abstract string ModeLabel { get; }

        /// <summary>
        /// Executes the match elevation workflow.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            var filter = new MepSelectionFilter();
            string title = $"Match Elevation ({ModeLabel})";
            string modeLower = ModeLabel.ToLowerInvariant();

            try
            {
                // 1) Pick SOURCE
                Reference sourceRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    filter,
                    $"Select SOURCE element to copy {modeLower} elevation from");

                Element sourceElem = doc.GetElement(sourceRef);
                double? sourceReferenceElevation = GetReferenceElevation(sourceElem, MatchMode);
                if (sourceReferenceElevation == null)
                {
                    DialogHelper.ShowError(title, "Could not read elevation from the selected element.");
                    return Result.Cancelled;
                }

                // 2) Pick TARGETS in a loop until user cancels. The whole session is grouped so a
                //    single Ctrl+Z reverses every matched target.
                int updatedCount = 0;
                using (TransactionGroup group = new TransactionGroup(doc, title))
                {
                    group.Start();

                    while (true)
                    {
                        Reference targetRef;
                        try
                        {
                            targetRef = uidoc.Selection.PickObject(
                                ObjectType.Element,
                                filter,
                                $"Select TARGET element to match {modeLower} elevation (ESC to finish)");
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            break; // user pressed ESC to finish applying
                        }

                        Element targetElem = doc.GetElement(targetRef);
                        if (targetElem == null)
                        {
                            continue;
                        }

                        using (Transaction t = new Transaction(doc, title))
                        {
                            try
                            {
                                t.Start();

                                bool changed = SetTargetReferenceElevation(
                                    targetElem,
                                    sourceReferenceElevation.Value,
                                    MatchMode);
                                if (changed)
                                {
                                    t.Commit();
                                    updatedCount++;
                                }
                                else
                                {
                                    t.RollBack();
                                }
                            }
                            catch (Exception)
                            {
                                if (t.HasStarted() && !t.HasEnded())
                                {
                                    t.RollBack();
                                }
                                // Skip a target that cannot be moved; keep matching the rest.
                            }
                        }
                    }

                    if (updatedCount > 0)
                    {
                        group.Assimilate();
                    }
                    else
                    {
                        group.RollBack();
                    }
                }

                if (updatedCount > 0)
                {
                    DialogHelper.ShowInfo(title, $"Updated {updatedCount} element(s) to match {modeLower} elevation.");
                    return Result.Succeeded;
                }

                DialogHelper.ShowInfo(title, "No elements were updated.");
                return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static double? GetReferenceElevation(Element elem, ElevationMatchMode mode)
        {
            double? centerElevation = GetCenterElevation(elem);
            if (centerElevation == null)
            {
                return null;
            }

            double halfVerticalSize = GetHalfVerticalSize(elem);
            switch (mode)
            {
                case ElevationMatchMode.Top:
                    return centerElevation.Value + halfVerticalSize;
                case ElevationMatchMode.Bottom:
                    return centerElevation.Value - halfVerticalSize;
                default:
                    return centerElevation.Value;
            }
        }

        /// <summary>
        /// Returns the center elevation (Z at mid-point) of the element's location curve.
        /// </summary>
        private static double? GetCenterElevation(Element elem)
        {
            LocationCurve loc = elem?.Location as LocationCurve;
            if (loc == null)
            {
                return null;
            }

            Curve curve = loc.Curve;
            if (curve == null)
            {
                return null;
            }

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            return (p0.Z + p1.Z) / 2.0;
        }

        /// <summary>
        /// Moves the element vertically so that the requested reference elevation matches targetReferenceElevation.
        /// </summary>
        private static bool SetTargetReferenceElevation(Element elem, double targetReferenceElevation, ElevationMatchMode mode)
        {
            LocationCurve loc = elem?.Location as LocationCurve;
            if (loc == null)
            {
                return false;
            }

            Curve curve = loc.Curve;
            if (curve == null)
            {
                return false;
            }

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            double currentCenter = (p0.Z + p1.Z) / 2.0;

            double targetCenter = targetReferenceElevation;
            double halfVerticalSize = GetHalfVerticalSize(elem);
            switch (mode)
            {
                case ElevationMatchMode.Top:
                    targetCenter = targetReferenceElevation - halfVerticalSize;
                    break;
                case ElevationMatchMode.Bottom:
                    targetCenter = targetReferenceElevation + halfVerticalSize;
                    break;
            }

            double diff = targetCenter - currentCenter;
            if (Math.Abs(diff) < ElevationTolerance)
            {
                return false; // already matched
            }

            // Translate the whole curve in Z direction, preserving curve type (Line, Arc, NURBS, etc.)
            Transform translation = Transform.CreateTranslation(new XYZ(0, 0, diff));
            Curve movedCurve = curve.CreateTransformed(translation);

            loc.Curve = movedCurve;
            return true;
        }

        /// <summary>
        /// Returns half of the element's profile size in internal units (feet) for top/bottom matching.
        /// Falls back to 0 when a profile parameter is unavailable.
        /// </summary>
        private static double GetHalfVerticalSize(Element elem)
        {
            if (TryGetPositiveDouble(elem, BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM, out double cableTrayHeight))
            {
                return cableTrayHeight / 2.0;
            }

            if (TryGetPositiveDouble(elem, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM, out double curveHeight))
            {
                return curveHeight / 2.0;
            }

            if (TryGetPositiveDouble(elem, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM, out double pipeDiameter))
            {
                return pipeDiameter / 2.0;
            }

            if (TryGetPositiveDouble(elem, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, out double curveDiameter))
            {
                return curveDiameter / 2.0;
            }

            return 0.0;
        }

        private static bool TryGetPositiveDouble(Element elem, BuiltInParameter parameterId, out double value)
        {
            value = 0;
            Parameter parameter = elem?.get_Parameter(parameterId);
            if (parameter == null ||
                parameter.StorageType != StorageType.Double ||
                !parameter.HasValue)
            {
                return false;
            }

            double rawValue = parameter.AsDouble();
            if (rawValue <= ElevationTolerance)
            {
                return false;
            }

            value = rawValue;
            return true;
        }
    }
}
