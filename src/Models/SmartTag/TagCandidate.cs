// Tool Name: Smart MEP Tag - Tag Candidate Model
// Description: Represents an MEP element that passed filtering and is ready for tag placement scoring.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

using Autodesk.Revit.DB;

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// An MEP element that has passed all Phase 1 filters and is queued for tagging.
    /// Carries all the data the scoring engine needs to decide where to place the tag.
    /// </summary>
    internal class TagCandidate
    {
        /// <summary>The Revit element ID.</summary>
        public ElementId ElementId { get; set; }

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
