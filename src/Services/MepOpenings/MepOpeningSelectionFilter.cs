#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningSelectionFilter.cs
 * Purpose       : Identifies supported MEP source elements and supported opening host elements.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-06
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.MepOpenings
 *
 * Input         : Current-model or linked-model element read by the opening tools.
 * Output        : True only for pipe, duct, cable tray, and conduit source elements, plus supported hosts.
 *
 * Notes         :
 * - Cable tray runs and fittings are accepted as cable tray sources.
 * - Other fittings, accessories, and equipment are excluded.
 * - Category checks are version-safe for Revit 2024+ ElementId storage.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 * v1.0.1 (2026-07-06) - Included cable tray runs and fittings in cable tray source detection.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using AJTools.Models.MepOpenings;

namespace AJTools.Services.MepOpenings
{
    internal sealed class MepOpeningSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            MepOpeningElementKind kind;
            return TryGetElementKind(elem, out kind);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }

        public static bool TryGetElementKind(Element element, out MepOpeningElementKind kind)
        {
            kind = MepOpeningElementKind.Pipe;

            if (IsCategory(element, BuiltInCategory.OST_PipeCurves))
            {
                kind = MepOpeningElementKind.Pipe;
                return true;
            }

            if (IsCategory(element, BuiltInCategory.OST_DuctCurves))
            {
                kind = MepOpeningElementKind.Duct;
                return true;
            }

            if (IsCategory(element, BuiltInCategory.OST_CableTray) ||
                IsCategory(element, BuiltInCategory.OST_CableTrayRun) ||
                IsCategory(element, BuiltInCategory.OST_CableTrayFitting))
            {
                kind = MepOpeningElementKind.CableTray;
                return true;
            }

            if (IsCategory(element, BuiltInCategory.OST_Conduit))
            {
                kind = MepOpeningElementKind.Conduit;
                return true;
            }

            return false;
        }

        public static bool IsSupportedHost(Element element, out MepOpeningHostKind hostKind)
        {
            hostKind = MepOpeningHostKind.Wall;

            if (element is Wall || IsCategory(element, BuiltInCategory.OST_Walls))
            {
                hostKind = MepOpeningHostKind.Wall;
                return true;
            }

            if (IsCategory(element, BuiltInCategory.OST_Floors))
            {
                hostKind = MepOpeningHostKind.FloorSlab;
                return true;
            }

            if (IsCategory(element, BuiltInCategory.OST_StructuralFraming))
            {
                hostKind = MepOpeningHostKind.Beam;
                return true;
            }

            return false;
        }

        public static bool IsCategory(Element element, BuiltInCategory category)
        {
            if (element == null || element.Category == null)
            {
                return false;
            }

#if REVIT2024 || REVIT2025 || REVIT2026 || REVIT2027 || REVIT2024_OR_GREATER
            return element.Category.Id.Value == (long)category;
#else
            return element.Category.Id.IntegerValue == (int)category;
#endif
        }
    }
}
