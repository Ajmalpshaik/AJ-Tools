#region Metadata
/*
 * Tool Name     : Auto MEP Dimension
 * File Name     : MepRunSelectionFilter.cs
 * Purpose       : Selection filter for picking a service run to dimension - ducts, flex ducts, pipes,
 *                 flex pipes, cable tray and conduit, in the open project.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (ElementIdExtensions)
 *
 * Input         : Elements and references offered by Revit during PickObject.
 * Output        : Whether each may be picked.
 *
 * Notes         :
 * - AllowReference returns TRUE, unlike the duct-only filter it replaces - but that alone does not
 *   make a run inside a link pickable. Pick runs mode picks whole elements (ObjectType.Element), and
 *   for a click on a linked model Revit hands back the RevitLinkInstance itself, never the run inside
 *   it. The command answers that pick with a message saying linked runs cannot be picked there.
 * - AllowElement stays permissive about non-run elements so the command can say "that is not a run"
 *   and let the user try again, rather than making wrong picks silently unclickable.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release, replacing DuctSelectionFilter.
 * v1.0.1 (2026-10-12) - Removed AllowedCategoryIds (nothing read it) and corrected the notes: this
 *                       filter never made runs inside a link pickable.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Services.MepReferenceDimension
{
    /// <summary>
    /// Allows service runs to be picked, in the open project.
    /// </summary>
    internal sealed class MepRunSelectionFilter : ISelectionFilter
    {
        private readonly HashSet<int> _allowedCategoryIds;

        internal MepRunSelectionFilter(IEnumerable<BuiltInCategory> allowedCategories)
        {
            IEnumerable<BuiltInCategory> categories =
                allowedCategories ?? Models.Dimensioning.MepDimensionSettings.SupportedMeasuredCategories;

            _allowedCategoryIds = new HashSet<int>(categories.Select(c => (int)c));
        }

        /// <summary>Permissive: a wrong pick is answered with a message rather than a dead cursor.</summary>
        public bool AllowElement(Element elem)
        {
            return elem != null;
        }

        /// <summary>
        /// Accepts every reference. This does not make a run inside a link pickable on its own - see
        /// the notes at the top of this file.
        /// </summary>
        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }

        /// <summary>True when the element is one of the configured service-run categories.</summary>
        internal bool IsAllowedRun(Element element)
        {
            Category category = element?.Category;
            if (category?.Id == null)
                return false;

            return _allowedCategoryIds.Contains(category.Id.IntValue());
        }
    }
}
