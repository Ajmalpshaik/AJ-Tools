// Tool Name: Smart MEP Tag - Tag Candidate Model
// Description: Represents an MEP element that passed filtering and is ready for tag placement scoring.
// Author: Ajmal P.S.
// Version: 1.1.0
// Revit Version: 2020
// Changelog: 1.1.0 (2026-10-14) - Carries the link instance for an element inside a linked model.

using Autodesk.Revit.DB;

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// An MEP element that has passed all Phase 1 filters and is queued for tagging.
    /// Carries all the data the scoring engine needs to decide where to place the tag.
    /// </summary>
    internal class TagCandidate
    {
        /// <summary>
        /// The Revit element ID. For a linked element this is its id INSIDE the link - ids repeat
        /// between models, so it only means something together with <see cref="LinkInstanceId"/>.
        /// </summary>
        public ElementId ElementId { get; set; }

        /// <summary>
        /// The link instance the element is seen through; null for an element of this model.
        /// </summary>
        public ElementId LinkInstanceId { get; set; }

        /// <summary>Link name for the report; empty for an element of this model.</summary>
        public string LinkName { get; set; }

        /// <summary>True when the element lives in a linked model.</summary>
        public bool IsLinked => LinkInstanceId != null && LinkInstanceId != ElementId.InvalidElementId;

        /// <summary>The built-in category (OST_DuctCurves, OST_PipeCurves, etc.).</summary>
        public BuiltInCategory Category { get; set; }

        /// <summary>Tagging priority — controls processing order.</summary>
        public TagPriority Priority { get; set; }

        /// <summary>Element bounding box in view coordinates.</summary>
        public BoundingBoxXYZ BoundingBox { get; set; }

        /// <summary>
        /// Midpoint of the element curve (for ducts/pipes) or bounding box centre (for equipment).
        /// This is the anchor point around which candidate tag positions are generated.
        /// </summary>
        public XYZ Midpoint { get; set; }

        /// <summary>True if more than 5 elements exist within 500mm — tag may be harder to place.</summary>
        public bool IsDenseZone { get; set; }

        /// <summary>Orientation in the active view — drives preferred tag offset direction.</summary>
        public ElementOrientation Orientation { get; set; }

        /// <summary>
        /// The resolved tag FamilySymbol ID to use for this element.
        /// Set during Phase 2 (Tag Family Selection). Null if no tag family found.
        /// </summary>
        public ElementId TagTypeId { get; set; }

        /// <summary>
        /// The geometry curve for linear elements (Ducts, Pipes) used to slide candidate positions.
        /// Will be null for point-based elements (Equipment, Accessories).
        /// </summary>
        public Curve ElementCurve { get; set; }

        /// <summary>
        /// Width of the host element, used to push tags outside its boundaries.
        /// </summary>
        public double HostWidth { get; set; }

        /// <summary>
        /// True if the tag requires a leader due to clashes, False otherwise.
        /// </summary>
        public bool NeedsLeader { get; set; }
    }
}
