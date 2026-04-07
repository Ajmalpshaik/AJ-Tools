// Tool Name: Smart MEP Tag - Pre-Flight Result Model
// Description: Holds all validated view data from Phase 0 checks — passed downstream to every phase.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// Contains all validated environment data from Phase 0 pre-flight checks.
    /// Every subsequent phase receives this object instead of re-querying the view.
    /// </summary>
    internal class PreFlightResult
    {
        /// <summary>Whether all pre-flight checks passed.</summary>
        public bool Passed { get; set; }

        /// <summary>Human-readable error message if a check failed.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>The active view reference.</summary>
        public View ActiveView { get; set; }

        /// <summary>The view type (FloorPlan, CeilingPlan, Section, Elevation).</summary>
        public ViewType ViewType { get; set; }

        /// <summary>The view scale (e.g. 50 for 1:50, 100 for 1:100).</summary>
        public int ViewScale { get; set; }

        /// <summary>
        /// The crop region boundary as a list of XYZ points forming a closed loop.
        /// Used to determine whether elements fall inside the visible area.
        /// </summary>
        public IList<XYZ> CropRegionPoints { get; set; }

        /// <summary>
        /// The crop region as an Outline (min/max bounding box) for fast intersection checks.
        /// </summary>
        public Outline CropOutline { get; set; }

        /// <summary>
        /// The annotation crop boundary outline.
        /// Tags must be placed within this boundary to remain visible on sheets.
        /// </summary>
        public Outline AnnotationCropOutline { get; set; }

        /// <summary>True if a View Template is applied — some tag operations may be restricted.</summary>
        public bool HasViewTemplate { get; set; }

        /// <summary>Warning messages collected during pre-flight (non-fatal).</summary>
        public List<string> Warnings { get; set; }

        public PreFlightResult()
        {
            Warnings = new List<string>();
        }

        /// <summary>
        /// Creates a failed pre-flight result with the given error message.
        /// </summary>
        public static PreFlightResult Fail(string message)
        {
            return new PreFlightResult { Passed = false, ErrorMessage = message };
        }
    }
}
