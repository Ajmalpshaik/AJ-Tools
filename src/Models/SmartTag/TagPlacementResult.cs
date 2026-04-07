// Tool Name: Smart MEP Tag - Placement Result Model
// Description: Records the outcome of tagging a single element — success, skip, or failure with reason.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

using Autodesk.Revit.DB;

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// The result of attempting to tag a single MEP element.
    /// Collected across all elements and used to generate the final output report.
    /// </summary>
    internal class TagPlacementResult
    {
        /// <summary>The element that was processed.</summary>
        public ElementId ElementId { get; set; }

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
