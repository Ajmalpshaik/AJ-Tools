#region Metadata
/*
 * Tool Name     : Create Openings
 * File Name     : MepOpeningHostSelectionFilter.cs
 * Purpose       : Restricts host-selection workflow picks to supported opening hosts.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-04
 * Last Updated  : 2026-07-04
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.MepOpenings
 *
 * Input         : User-picked host element.
 * Output        : True only for walls, floors/slabs, and beams in the active model.
 *
 * Notes         :
 * - Direct openings can only be created in editable current-model hosts.
 * - Linked host support is reserved for the family opening workflow.
 *
 * Changelog     :
 * v1.0.0 (2026-07-04) - Initial release.
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
    internal sealed class MepOpeningHostSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            MepOpeningHostKind ignored;
            return MepOpeningSelectionFilter.IsSupportedHost(elem, out ignored);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
