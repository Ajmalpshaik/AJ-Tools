// Tool Name: Smart MEP Tag - Placement Result Model
// Description: Records the outcome of tagging a single element — success, skip, or failure with reason.
// Author: Ajmal P.S.
// Version: 1.1.0
// Revit Version: 2020
// Changelog: 1.1.0 (2026-10-14) - Carries the link for an element inside a linked model.

using Autodesk.Revit.DB;

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// The result of attempting to tag a single MEP element.
    /// Collected across all elements and used to generate the final output report.
    /// </summary>
    internal class TagPlacementResult
    {
        /// <summary>The element that was processed (its id inside the link, for a linked element).</summary>
        public ElementId ElementId { get; set; }

        /// <summary>The link instance the element is seen through; null for an element of this model.</summary>
        public ElementId LinkInstanceId { get; set; }

        /// <summary>Link name for the report; empty for an element of this model.</summary>
        public string LinkName { get; set; }

        /// <summary>True when the element lives in a linked model.</summary>
        public bool IsLinked => LinkInstanceId != null && LinkInstanceId != ElementId.InvalidElementId;

        /// <summary>The built-in category of the element.</summary>
        public BuiltInCategory Category { get; set; }

        /// <summary>True if the tag was placed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>If not successful, the reason the element was skipped or failed.</summary>
        public TagSkipReason SkipReason { get; set; }

        /// <summary>Optional human-readable detail for the report (e.g. fallback tag name used).</summary>
        public string Note { get; set; }
    }
}
