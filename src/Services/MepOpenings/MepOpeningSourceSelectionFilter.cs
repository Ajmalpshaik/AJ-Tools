#region Metadata
/*
 * Tool Name     : Create Openings
 * File Name     : MepOpeningSourceSelectionFilter.cs
 * Purpose       : Restricts source picks to supported MEP elements in the enabled current or linked model scope.
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
 * Input         : User-picked current or linked source element reference.
 * Output        : True only for pipe, duct, cable tray, and conduit in the configured source scope.
 *
 * Notes         :
 * - Linked elements are read only and are only used as source geometry.
 * - If a linked model is selected in settings, picks from other links are rejected.
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
    internal sealed class MepOpeningSourceSelectionFilter : ISelectionFilter
    {
        private readonly Document _hostDoc;
        private readonly bool _allowCurrent;
        private readonly bool _allowLinked;
        private readonly string _sourceLinkUniqueId;
        private readonly MepOpeningSettings _settings;

        public MepOpeningSourceSelectionFilter(Document hostDoc, MepOpeningSettings settings)
        {
            _hostDoc = hostDoc;
            settings = settings ?? MepOpeningSettings.CreateDefault();
            settings.Normalize();

            _settings = settings;
            _allowCurrent = settings.UseCurrentModelSources;
            _allowLinked = settings.UseLinkedModelSources;
            _sourceLinkUniqueId = settings.SourceLinkInstanceUniqueId ?? string.Empty;
        }

        public bool AllowElement(Element elem)
        {
            if (elem == null)
            {
                return false;
            }

            MepOpeningElementKind ignored;
            if (_allowCurrent &&
                MepOpeningSelectionFilter.TryGetElementKind(elem, out ignored) &&
                IsElementKindIncluded(ignored))
            {
                return true;
            }

            var linkInstance = elem as RevitLinkInstance;
            return _allowLinked && IsAllowedLink(linkInstance);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            if (reference == null || _hostDoc == null)
            {
                return false;
            }

            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                if (!_allowLinked)
                {
                    return false;
                }

                RevitLinkInstance linkInstance = _hostDoc.GetElement(reference.ElementId) as RevitLinkInstance;
                if (!IsAllowedLink(linkInstance))
                {
                    return false;
                }

                Document linkDoc = linkInstance.GetLinkDocument();
                Element linkedElement = linkDoc == null ? null : linkDoc.GetElement(reference.LinkedElementId);
                MepOpeningElementKind linkedKind;
                return MepOpeningSelectionFilter.TryGetElementKind(linkedElement, out linkedKind) &&
                       IsElementKindIncluded(linkedKind);
            }

            if (!_allowCurrent)
            {
                return false;
            }

            Element currentElement = _hostDoc.GetElement(reference.ElementId);
            MepOpeningElementKind currentKind;
            return MepOpeningSelectionFilter.TryGetElementKind(currentElement, out currentKind) &&
                   IsElementKindIncluded(currentKind);
        }

        private bool IsAllowedLink(RevitLinkInstance linkInstance)
        {
            if (linkInstance == null || linkInstance.GetLinkDocument() == null)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(_sourceLinkUniqueId) ||
                   string.Equals(linkInstance.UniqueId, _sourceLinkUniqueId, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsElementKindIncluded(MepOpeningElementKind kind)
        {
            MepOpeningElementRule rule = _settings.GetRule(kind);
            return rule == null || rule.IsIncluded;
        }
    }
}
