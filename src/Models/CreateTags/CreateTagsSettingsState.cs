// Tool Name: Create Tags - Settings State
// Description: Stores Create Tags user settings per document session.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Models.CreateTags
{
    /// <summary>
    /// Snapshot of Create Tags settings.
    /// </summary>
    public class CreateTagsSettingsState
    {
        /// <summary>
        /// Per-category toggle to include/exclude a selected element from tagging.
        /// </summary>
        public Dictionary<BuiltInCategory, bool> CategoryEnabled { get; set; }

        /// <summary>
        /// Minimum element length, in Revit internal units (feet), below which a selected
        /// element is skipped instead of tagged. Applies to curve-based elements only
        /// (ducts, pipes, cable trays) - point-based elements (equipment, accessories) are
        /// never filtered by length.
        /// </summary>
        public double MinLengthInternal { get; set; }

        /// <summary>
        /// How far the tag sits from its element, in mm on the printed sheet.
        /// </summary>
        /// <remarks>
        /// A real distance in the model, NOT a size on the printed sheet - it is not multiplied by the
        /// view scale. Matches how Smart MEP Tags has always treated its own 300mm offset. Kept in mm
        /// rather than internal feet purely so the settings window reads it back without converting.
        /// </remarks>
        public double TagOffsetMm { get; set; }
    }
}
