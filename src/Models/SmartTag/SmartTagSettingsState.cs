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

    }
}
