#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 2, Selected Elements Only
 * File Name     : SmartTagSelectionFilter.cs
 * Purpose       : When "Selected elements only" is chosen in Smart MEP Tagging Settings, says which
 *                 elements the user selected before pressing the button, so only those are tagged.
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
 * Dependencies  : Autodesk Revit API (UI selection), AJTools.Utils (ElementIdExtensions)
 *
 * Input         : The current Revit selection.
 * Output        : Yes/no per element - is it part of what the user selected?
 *
 * Notes         :
 * - This model's elements: tagged when the element itself is selected.
 * - Linked elements: tagged when their LINK is selected (select the link to tag inside it), or - on
 *   Revit 2023 and newer - when the linked element itself was picked (Tab-select inside a link).
 *   Selection.GetReferences, which reports linked elements, exists from Revit 2023 only (checked in
 *   the Revit API files: not in 2020 or 2022); Revit 2020-2022 report only the link as selected.
 * - Linked identity is (link instance id, element id), never a bare id - ids repeat between models.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// The user's selection, for "Selected elements only".
    /// </summary>
    internal sealed class SmartTagSelectionFilter
    {
        private readonly HashSet<ElementId> _selectedIds;
        private readonly HashSet<(long LinkId, long ElementId)> _selectedLinkedKeys;

        private SmartTagSelectionFilter(
            HashSet<ElementId> selectedIds,
            HashSet<(long LinkId, long ElementId)> selectedLinkedKeys)
        {
            _selectedIds = selectedIds;
            _selectedLinkedKeys = selectedLinkedKeys;
        }

        /// <summary>How many things are selected (elements, links, and linked elements picked one by one).</summary>
        public int Count => _selectedIds.Count + _selectedLinkedKeys.Count;

        /// <summary>True when nothing at all is selected.</summary>
        public bool IsEmpty => Count == 0;

        /// <summary>Reads what is selected right now. Never throws - an unreadable selection reads as empty.</summary>
        public static SmartTagSelectionFilter FromCurrentSelection(UIDocument uidoc)
        {
            var ids = new HashSet<ElementId>();
            var linkedKeys = new HashSet<(long LinkId, long ElementId)>();

            try
            {
                ICollection<ElementId> selected = uidoc?.Selection?.GetElementIds();
                if (selected != null)
                {
                    foreach (ElementId id in selected)
                    {
                        if (id != null && id != ElementId.InvalidElementId)
                            ids.Add(id);
                    }
                }
            }
            catch (Exception)
            {
                // Nothing readable is the same as nothing selected; the caller says so.
            }

#if REVIT2023_OR_GREATER
            try
            {
                // Linked elements picked one by one (Tab inside a link) only show up here.
                IList<Reference> references = uidoc?.Selection?.GetReferences();
                if (references != null)
                {
                    foreach (Reference reference in references)
                    {
                        if (reference == null)
                            continue;

                        ElementId linkedId = reference.LinkedElementId;
                        if (linkedId != null && linkedId != ElementId.InvalidElementId)
                            linkedKeys.Add(SmartTagLinkedElementCollector.MakeKey(reference.ElementId, linkedId));
                    }
                }
            }
            catch (Exception)
            {
                // The element-id selection above still applies.
            }
#endif

            return new SmartTagSelectionFilter(ids, linkedKeys);
        }

        /// <summary>True when this model's element is selected.</summary>
        public bool IncludesHostElement(ElementId elementId)
        {
            return elementId != null && _selectedIds.Contains(elementId);
        }

        /// <summary>
        /// True when the linked element is selected - its whole link selected, or the element itself
        /// picked inside the link (Revit 2023+).
        /// </summary>
        public bool IncludesLinkedElement(ElementId linkInstanceId, ElementId elementId)
        {
            if (linkInstanceId == null || elementId == null)
                return false;

            return _selectedIds.Contains(linkInstanceId)
                || _selectedLinkedKeys.Contains(SmartTagLinkedElementCollector.MakeKey(linkInstanceId, elementId));
        }

        /// <summary>
        /// True when anything inside this link could be selected - so links with nothing selected in
        /// them are not read at all.
        /// </summary>
        public bool MayIncludeLink(ElementId linkInstanceId)
        {
            if (linkInstanceId == null)
                return false;

            if (_selectedIds.Contains(linkInstanceId))
                return true;

            long linkId = linkInstanceId.LongValue();
            return _selectedLinkedKeys.Any(key => key.LinkId == linkId);
        }
    }
}
