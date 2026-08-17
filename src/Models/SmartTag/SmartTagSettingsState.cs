// Tool Name: Smart MEP Tag - Settings State
// Description: Stores Smart MEP Tag user settings per document session.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// Snapshot of Smart MEP Tag settings.
    /// </summary>
    public class SmartTagSettingsState
    {
        /// <summary>
        /// Fixed offset from host element to nearest tag text edge in Revit internal units (feet).
        /// </summary>
        public double OffsetInternal { get; set; }

        /// <summary>
        /// Per-category toggle to include/exclude tagging.
        /// </summary>
        public Dictionary<BuiltInCategory, bool> CategoryEnabled { get; set; }

        /// <summary>
        /// Per-category host-to-tag offset in Revit internal units (feet).
        /// </summary>
        public Dictionary<BuiltInCategory, double> CategoryOffsetInternal { get; set; }

        /// <summary>
        /// Per-category tagging priority used to determine processing order.
        /// </summary>
        public Dictionary<BuiltInCategory, TagPriority> CategoryPriority { get; set; }

        /// <summary>
        /// Per-category "give this tag a leader". False places the tag beside the element instead.
        /// </summary>
        /// <remarks>
        /// Added v1.49.7 for accessories, where a leader is noise - the tag reads better sitting next
        /// to the fitting. True for every category by default, which is how the tool behaved before
        /// this existed.
        /// </remarks>
        public Dictionary<BuiltInCategory, bool> CategoryUseLeader { get; set; }

        /// <summary>
        /// Shortest run worth tagging, in mm. Applies to duct/pipe/cable tray only.
        /// </summary>
        /// <remarks>
        /// Was the hardcoded <c>MinCurveLength</c> (1000mm) until v1.49.7.
        /// </remarks>
        public double MinLengthMm { get; set; }

        /// <summary>
        /// Whether the width/height minimums below are applied at all. Off means length is the only
        /// size test, so every duct is tagged whatever its cross-section.
        /// </summary>
        public bool FilterBySize { get; set; }

        /// <summary>
        /// Minimum width in mm. 0 means no width minimum. A round section reports its diameter here.
        /// </summary>
        public double MinWidthMm { get; set; }

        /// <summary>
        /// Minimum height in mm. 0 means no height minimum. A round section reports its diameter here.
        /// </summary>
        /// <remarks>
        /// Defaults to 0, not 100, deliberately: the old hardcoded filter only ever checked width, so
        /// a default height minimum would silently stop shallow ducts being tagged.
        /// </remarks>
        public double MinHeightMm { get; set; }
    }
}
